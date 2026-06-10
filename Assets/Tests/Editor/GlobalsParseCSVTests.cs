using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

// EditMode tests for Globals.ParseCSV (Phase 1 of READINGBUDDY_AUTOMATED_TESTS_PLAN.md).
// Lives in an Editor folder so it compiles into Assembly-CSharp-Editor and can see the
// predefined Assembly-CSharp game code without adding an asmdef to runtime code.
namespace ReadingBuddy.Tests
{
    public class GlobalsParseCSVTests
    {
        private const string Header = "name,author,image,url,ageFrom,ageTo,genre,notes,id,printed,kindle";
        private string _origBaseURL;

        [SetUp]
        public void SetUp()
        {
            // Reset the static Globals.baseURL that ParseCSV reads when resolving relative bookUrls.
            _origBaseURL = Globals.baseURL;
            Globals.baseURL = "http://cdn/";
            // ParseCSV reads PlayerPrefs for each row's bookUrl (page/done). Clear the keys the
            // test rows use so reads are deterministic (targeted, so we don't wipe the dev's prefs).
            ClearTestPrefs();
        }

        [TearDown]
        public void TearDown()
        {
            Globals.baseURL = _origBaseURL;
            ClearTestPrefs();
        }

        private void ClearTestPrefs()
        {
            foreach (string url in new[] { "stories/mybook/", "stories/a/", "stories/b/", "http://other/x/" })
            {
                PlayerPrefs.DeleteKey(Globals.Prefs_BookUrl_To_Page_Key(url));
                PlayerPrefs.DeleteKey(Globals.Prefs_BookUrl_To_BookDone_Key(url));
            }
        }

        [Test]
        public void WellFormed9ColumnRow_ParsesAllFields()
        {
            string csv = Header + "\nMy Book,Jane Doe,img.jpg,stories/mybook/,3,6,adventure,Fun notes,id123";
            List<PRBook> books = Globals.ParseCSV(csv);

            Assert.AreEqual(1, books.Count);
            PRBook b = books[0];
            Assert.AreEqual("My Book", b.bookName);
            Assert.AreEqual("Jane Doe", b.bookAuthor);
            Assert.AreEqual("img.jpg", b.bookImageUrl);
            Assert.AreEqual("stories/mybook/", b.bookUrl);
            Assert.AreEqual(3, b.ageFrom);
            Assert.AreEqual(6, b.ageTo);
            Assert.AreEqual("adventure", b.genre);
            Assert.AreEqual("Fun notes", b.notesForParents);
            Assert.AreEqual("id123", b.id);
            // 9-column rows leave the two optional bookstore URLs empty.
            Assert.AreEqual("", b.bookStoreUrlPrinted);
            Assert.AreEqual("", b.bookStoreUrlKindle);
            Assert.AreEqual(0, b.number);
        }

        [Test]
        public void WellFormed11ColumnRow_ParsesBookstoreUrls()
        {
            string csv = Header +
                "\nMy Book,Jane Doe,img.jpg,stories/mybook/,3,6,adventure,Fun notes,id123,http://amazon/print,http://amazon/kindle";
            List<PRBook> books = Globals.ParseCSV(csv);

            Assert.AreEqual(1, books.Count);
            Assert.AreEqual("http://amazon/print", books[0].bookStoreUrlPrinted);
            Assert.AreEqual("http://amazon/kindle", books[0].bookStoreUrlKindle);
        }

        [Test]
        public void MalformedRow_IsSkipped_RemainingRowsStillParse()
        {
            // Middle row has a non-numeric ageFrom -> int.Parse throws -> row skipped (C2 behavior).
            string csv = Header +
                "\nGood A,Auth,img,stories/a/,3,6,genre,notes,idA" +
                "\nBad,Auth,img,stories/bad/,NOTANUMBER,6,genre,notes,idBad" +
                "\nGood B,Auth,img,stories/b/,4,8,genre,notes,idB";
            List<PRBook> books = Globals.ParseCSV(csv);

            Assert.AreEqual(2, books.Count);
            Assert.AreEqual("Good A", books[0].bookName);
            Assert.AreEqual("Good B", books[1].bookName);
        }

        [Test]
        public void MalformedRow_NumberStaysContiguous()
        {
            // book.number is assigned counter++ only on a successful parse, so the surviving
            // rows are numbered 0,1 with no gap where the bad row was.
            string csv = Header +
                "\nGood A,Auth,img,stories/a/,3,6,genre,notes,idA" +
                "\nBad,Auth,img,stories/bad/,NOTANUMBER,6,genre,notes,idBad" +
                "\nGood B,Auth,img,stories/b/,4,8,genre,notes,idB";
            List<PRBook> books = Globals.ParseCSV(csv);

            Assert.AreEqual(0, books[0].number);
            Assert.AreEqual(1, books[1].number);
        }

        [Test]
        public void EmptyLines_AreSkipped()
        {
            string csv = Header +
                "\nGood A,Auth,img,stories/a/,3,6,genre,notes,idA" +
                "\n   " +
                "\n" +
                "\nGood B,Auth,img,stories/b/,4,8,genre,notes,idB";
            List<PRBook> books = Globals.ParseCSV(csv);

            Assert.AreEqual(2, books.Count);
        }

        [Test]
        public void HeaderRow_IsSkipped()
        {
            // The first line is always discarded as the header, even though it looks parseable.
            string csv = Header + "\nMy Book,Jane Doe,img.jpg,stories/mybook/,3,6,adventure,Fun notes,id123";
            List<PRBook> books = Globals.ParseCSV(csv);

            Assert.AreEqual(1, books.Count);
            Assert.AreEqual("My Book", books[0].bookName);
        }

        [Test]
        public void RelativeBookUrl_GetsBaseUrlPrefix()
        {
            string csv = Header + "\nMy Book,Auth,img,stories/mybook/,3,6,genre,notes,id";
            List<PRBook> books = Globals.ParseCSV(csv);

            // baseURL is "http://cdn/" (set in SetUp); relative urls get it prepended.
            Assert.AreEqual("http://cdn/stories/mybook/", books[0].bookFullUrl);
        }

        [Test]
        public void AbsoluteBookUrl_IsNotPrefixed()
        {
            string csv = Header + "\nMy Book,Auth,img,http://other/x/,3,6,genre,notes,id";
            List<PRBook> books = Globals.ParseCSV(csv);

            Assert.AreEqual("http://other/x/", books[0].bookFullUrl);
        }

        [Test]
        public void CommaInsideField_SilentlyShiftsColumns_DocumentedLimitation()
        {
            // DOCUMENTED LIMITATION, not an endorsement: ParseCSV does a naive Split(',') with no
            // quote handling, so an unescaped comma inside a field shifts every later column by one.
            // Here notesForParents contains "Hello, world", which pushes id to the next token.
            // If CSV parsing is ever hardened, FLIP these assertions.
            string csv = Header + "\nBook,Auth,img,stories/a/,3,6,genre,Hello, world,idShouldBeHere,print,kindle";
            List<PRBook> books = Globals.ParseCSV(csv);

            Assert.AreEqual(1, books.Count);
            // notes captured only the text before the stray comma...
            Assert.AreEqual("Hello", books[0].notesForParents);
            // ...and id is the shifted token (" world" trimmed), NOT "idShouldBeHere".
            Assert.AreEqual("world", books[0].id);
            Assert.AreEqual("idShouldBeHere", books[0].bookStoreUrlPrinted);
        }
    }
}
