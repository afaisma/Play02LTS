using NUnit.Framework;

// EditMode tests for the 2026-08-24 shelf-label fix: "Level N" belongs to the Learn-to-Read ladder
// only (a tester saw "Level 4" on Peter Rabbit in Classic), and a ladder row must not say the level
// twice (the catalog author is "ReadingBuddy Level N", which stacked above the "Level N" chip).
namespace ReadingBuddy.Tests
{
    public class BookViewItemShelfLabelTests
    {
        private string _savedFilter;

        [SetUp]
        public void SaveFilter() => _savedFilter = Globals.g_libraryFilter;

        [TearDown]
        public void RestoreFilter() => Globals.g_libraryFilter = _savedFilter;

        [Test]
        public void IsLearnToReadShelf_LadderTokens_AreTheLadder()
        {
            Globals.g_libraryFilter = "learn to read";
            Assert.IsTrue(BookViewItem.IsLearnToReadShelf());

            Globals.g_libraryFilter = "level1";
            Assert.IsTrue(BookViewItem.IsLearnToReadShelf());

            Globals.g_libraryFilter = "Level4";   // Nav addresses are case-insensitive
            Assert.IsTrue(BookViewItem.IsLearnToReadShelf());
        }

        [Test]
        public void IsLearnToReadShelf_OtherShelves_AreNot()
        {
            Globals.g_libraryFilter = "classic";
            Assert.IsFalse(BookViewItem.IsLearnToReadShelf());

            Globals.g_libraryFilter = "everything";
            Assert.IsFalse(BookViewItem.IsLearnToReadShelf());

            Globals.g_libraryFilter = "level5";   // not a rung
            Assert.IsFalse(BookViewItem.IsLearnToReadShelf());

            Globals.g_libraryFilter = "";
            Assert.IsFalse(BookViewItem.IsLearnToReadShelf());
        }

        [Test]
        public void AuthorLine_OnLadder_DropsTheRedundantLevelSuffix()
        {
            var book = new PRBook { bookAuthor = "ReadingBuddy Level 2", level = 2 };
            Assert.AreEqual("ReadingBuddy", BookViewItem.AuthorLine(book, true));
        }

        [Test]
        public void AuthorLine_WhenNoLevelChipIsShown_IsVerbatim()
        {
            var book = new PRBook { bookAuthor = "ReadingBuddy Level 2", level = 2 };
            Assert.AreEqual("ReadingBuddy Level 2", BookViewItem.AuthorLine(book, false));
        }

        [Test]
        public void AuthorLine_AuthorWithoutTheSuffix_IsUntouched()
        {
            var book = new PRBook { bookAuthor = "Beatrix Potter", level = 4 };
            Assert.AreEqual("Beatrix Potter", BookViewItem.AuthorLine(book, true));

            // A different level in the author string is NOT this book's level: leave it alone.
            var mismatch = new PRBook { bookAuthor = "ReadingBuddy Level 3", level = 2 };
            Assert.AreEqual("ReadingBuddy Level 3", BookViewItem.AuthorLine(mismatch, true));
        }

        [Test]
        public void AuthorLine_MissingAuthor_IsEmptyNotNull()
        {
            Assert.AreEqual("", BookViewItem.AuthorLine(new PRBook { bookAuthor = null, level = 1 }, true));
        }
    }
}
