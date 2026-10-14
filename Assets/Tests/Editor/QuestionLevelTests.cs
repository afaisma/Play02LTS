using System;
using System.Collections.Generic;
using NUnit.Framework;

// EditMode tests for the question levels: catalog tags "questions-a".."questions-d" decide which
// copy of a book the lists show (QuestionLevel), and the line format of the local results file
// (DialogueResults).
namespace ReadingBuddy.Tests
{
    public class QuestionLevelTests
    {
        private static PRBook Book(string id, params string[] tags) =>
            new PRBook { id = id, bookName = "B" + id, tags = new List<string>(tags) };

        [Test]
        public void BookWithoutLevelTag_IsShownAtEveryLevel()
        {
            foreach (string level in QuestionLevel.Letters)
            {
                Assert.IsTrue(QuestionLevel.Shows(Book("1"), level));
                Assert.IsTrue(QuestionLevel.Shows(Book("2", "interactive"), level));
            }
        }

        [Test]
        public void BookWithLevelTags_IsShownOnlyAtItsLevels()
        {
            PRBook b = Book("1", "interactive", "questions-b");
            Assert.IsFalse(QuestionLevel.Shows(b, "a"));
            Assert.IsTrue(QuestionLevel.Shows(b, "b"));
            Assert.IsFalse(QuestionLevel.Shows(b, "c"));

            PRBook cd = Book("2", "questions-c", "questions-d");
            Assert.IsFalse(QuestionLevel.Shows(cd, "b"));
            Assert.IsTrue(QuestionLevel.Shows(cd, "c"));
            Assert.IsTrue(QuestionLevel.Shows(cd, "d"));
        }

        [Test]
        public void Of_IsTheLevelOfTheBook_OrEmpty()
        {
            Assert.AreEqual("c", QuestionLevel.Of(Book("1", "interactive", "questions-c")));
            Assert.AreEqual("", QuestionLevel.Of(Book("2", "interactive")));
            Assert.AreEqual("", QuestionLevel.Of(null));
        }

        [Test]
        public void Visible_KeepsCatalogOrder_AndOneCopyPerLevel()
        {
            var all = new List<PRBook>
            {
                Book("cowA", "questions-a"), Book("cowB", "questions-b"), Book("plain"),
                Book("jackA", "questions-a"), Book("jackB", "questions-b"),
            };
            List<PRBook> shown = QuestionLevel.Visible(all, "b");
            Assert.AreEqual(new[] { "cowB", "plain", "jackB" }, shown.ConvertAll(x => x.id).ToArray());
            Assert.IsNull(QuestionLevel.Visible(null, "a"));
        }

        [Test]
        public void Visible_OnlyLevelBooks_LeavesOutBooksWithoutALevel()
        {
            var all = new List<PRBook> { Book("cowA", "questions-a"), Book("plain"), Book("jackB", "questions-b") };
            Assert.AreEqual(new[] { "cowA" }, QuestionLevel.Visible(all, "a", true).ConvertAll(x => x.id).ToArray());
            Assert.AreEqual(new[] { "cowA", "plain" }, QuestionLevel.Visible(all, "a").ConvertAll(x => x.id).ToArray());
            Assert.IsEmpty(QuestionLevel.Visible(all, "c", true));
        }

        [Test]
        public void AnsweredCount_CountsDistinctAnsweredPages()
        {
            var t = new System.DateTime(2026, 10, 11, 23, 5, 9, System.DateTimeKind.Utc);
            string file = System.IO.Path.Combine(UnityEngine.Application.persistentDataPath, DialogueResults.FileName);
            string backup = System.IO.File.Exists(file) ? System.IO.File.ReadAllText(file) : null;
            try
            {
                System.IO.File.WriteAllText(file, DialogueResults.Header + "\n"
                    + DialogueResults.Line(t, "136", "Cow, with \"questions\"", "a", 3, "cow", true, "touch", 1) + "\n"
                    + DialogueResults.Line(t, "136", "Cow", "a", 3, "painter", false, "touch", 1) + "\n"
                    + DialogueResults.Line(t, "136", "Cow", "a", 6, "", false, "skip", 0) + "\n"
                    + DialogueResults.Line(t, "136", "Cow", "a", 8, "", false, "none", 1) + "\n"
                    + DialogueResults.Line(t, "136", "Cow", "a", 10, "painter", true, "voice", 2) + "\n"
                    + DialogueResults.Line(t, "140", "Jack", "a", 3, "cow", true, "touch", 1) + "\n"
                    + DialogueResults.Line(t, "140", "Jack", "a", 5, "a, b", true, "voice", 1) + "\n");
                Assert.AreEqual(2, DialogueResults.AnsweredCount("136"), "pages 3 and 10; skip and no answer do not count");
                Assert.AreEqual(2, DialogueResults.AnsweredCount("140"), "an answer with a comma is one cell");
                Assert.AreEqual(new[] { "x", "a, \"b\"", "", "1" }, DialogueResults.Cells("x,\"a, \"\"b\"\"\",,1").ToArray());
                Assert.AreEqual(0, DialogueResults.AnsweredCount("999"));
                Assert.AreEqual(0, DialogueResults.AnsweredCount(""));
            }
            finally
            {
                if (backup != null) System.IO.File.WriteAllText(file, backup); else System.IO.File.Delete(file);
            }
        }

        [Test]
        public void CatalogHasLevels_OnlyWithLevelTaggedBooks()
        {
            Assert.IsFalse(QuestionLevel.CatalogHasLevels(null));
            Assert.IsFalse(QuestionLevel.CatalogHasLevels(new List<PRBook> { Book("1"), Book("2", "interactive") }));
            Assert.IsTrue(QuestionLevel.CatalogHasLevels(new List<PRBook> { Book("1"), Book("2", "questions-d") }));
        }

        [Test]
        public void ParseJSON_ReadsTags_LowerCase_AndMissingMeansNone()
        {
            string json = @"{ ""schema_version"": 1, ""books"": [
              { ""name"": ""A"", ""script"": ""a/s.txt"", ""id"": ""1"", ""tags"": [ ""Interactive"", ""questions-A"" ] },
              { ""name"": ""B"", ""script"": ""b/s.txt"", ""id"": ""2"" } ] }";
            List<PRBook> books = Globals.ParseJSON(json);
            Assert.AreEqual(2, books.Count);
            Assert.AreEqual(new[] { "interactive", "questions-a" }, books[0].tags.ToArray());
            Assert.IsEmpty(books[1].tags);
            Assert.IsTrue(QuestionLevel.Shows(books[0], "a"));
            Assert.IsFalse(QuestionLevel.Shows(books[0], "b"));
        }

        [Test]
        public void TextAbout_HasOneLinePerLevel()
        {
            Assert.AreEqual(QuestionLevel.Letters.Length, QuestionLevel.TextAbout.Length);
        }

        [Test]
        public void ResultLine_HasTheHeaderColumns_AndQuotesCommas()
        {
            var t = new DateTime(2026, 10, 11, 23, 5, 9, DateTimeKind.Utc);
            string line = DialogueResults.Line(t, "135", "Jack, and the \"Beanstalk\"", "b", 3, "yes", true, "voice", 2);
            Assert.AreEqual("2026-10-11T23:05:09Z,135,\"Jack, and the \"\"Beanstalk\"\"\",b,3,yes,1,voice,2", line);

            string none = DialogueResults.Line(t, "", null, "a", 0, "", false, "none", 1);
            Assert.AreEqual("2026-10-11T23:05:09Z,,,a,0,,0,none,1", none);
            Assert.AreEqual(DialogueResults.Header.Split(',').Length, none.Split(',').Length);
        }
    }
}
