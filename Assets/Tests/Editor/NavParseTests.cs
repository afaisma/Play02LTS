using System.Collections.Generic;
using NUnit.Framework;

// EditMode tests for Nav.Parse and Nav.ResolveBook (Nav.cs) — pure address
// parsing + catalog resolution. These exercise the testable helpers only; they
// never call Nav.Go or any scene load (not valid in EditMode).
namespace ReadingBuddy.Tests
{
    public class NavParseTests
    {
        // ---- Parse ----

        [Test]
        public void Parse_StoryWithBookAndPage()
        {
            var (scene, args) = Nav.Parse("story?book=The Big Pig&page=3");
            Assert.AreEqual("story", scene);
            Assert.AreEqual("The Big Pig", args["book"]);
            Assert.AreEqual("3", args["page"]);
        }

        [Test]
        public void Parse_LibraryWithLevelFilter()
        {
            var (scene, args) = Nav.Parse("library?filter=level1");
            Assert.AreEqual("library", scene);
            Assert.AreEqual("level1", args["filter"]);
        }

        [Test]
        public void Parse_SceneOnly_NoArgs()
        {
            var (scene, args) = Nav.Parse("map");
            Assert.AreEqual("map", scene);
            Assert.AreEqual(0, args.Count);
        }

        [Test]
        public void Parse_Empty_EmptySceneAndArgs()
        {
            var (scene, args) = Nav.Parse("");
            Assert.AreEqual("", scene);
            Assert.AreEqual(0, args.Count);
        }

        [Test]
        public void Parse_SceneIsCaseInsensitiveAndTrimmed()
        {
            var (scene, _) = Nav.Parse("  LIBRARY  ");
            Assert.AreEqual("library", scene);
        }

        [Test]
        public void Parse_DecodesPlusAndPercent20ToSpace()
        {
            var (_, args) = Nav.Parse("story?book=The+Big%20Pig");
            Assert.AreEqual("The Big Pig", args["book"]);
        }

        // ---- ResolveBook ----

        private List<PRBook> savedCatalog;

        [SetUp]
        public void SeedCatalog()
        {
            savedCatalog = Globals.g_listPRBooks;
            Globals.g_listPRBooks = new List<PRBook>
            {
                new PRBook { id = "b1", bookName = "The Big Pig", bookUrl = "pig.txt" },
                new PRBook { id = "b2", bookName = "Tiny Cat",    bookUrl = "cat.txt" },
            };
        }

        [TearDown]
        public void RestoreCatalog()
        {
            Globals.g_listPRBooks = savedCatalog;
        }

        [Test]
        public void ResolveBook_ById()
        {
            var book = Nav.ResolveBook(new Dictionary<string, string> { ["id"] = "b2" });
            Assert.IsNotNull(book);
            Assert.AreEqual("Tiny Cat", book.bookName);
        }

        [Test]
        public void ResolveBook_ByName_IsCaseInsensitive()
        {
            var book = Nav.ResolveBook(new Dictionary<string, string> { ["book"] = "the big pig" });
            Assert.IsNotNull(book);
            Assert.AreEqual("b1", book.id);
        }

        [Test]
        public void ResolveBook_ByUrl()
        {
            var book = Nav.ResolveBook(new Dictionary<string, string> { ["url"] = "cat.txt" });
            Assert.IsNotNull(book);
            Assert.AreEqual("b2", book.id);
        }

        [Test]
        public void ResolveBook_Miss_ReturnsNull()
        {
            var book = Nav.ResolveBook(new Dictionary<string, string> { ["book"] = "Nonexistent" });
            Assert.IsNull(book);
        }

        [Test]
        public void ResolveBook_NoKeys_ReturnsNull()
        {
            var book = Nav.ResolveBook(new Dictionary<string, string>());
            Assert.IsNull(book);
        }

        [Test]
        public void ResolveBook_NullCatalog_ReturnsNull()
        {
            Globals.g_listPRBooks = null;
            var book = Nav.ResolveBook(new Dictionary<string, string> { ["id"] = "b1" });
            Assert.IsNull(book);
        }
    }
}
