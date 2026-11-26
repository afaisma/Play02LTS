using System.Collections.Generic;
using NUnit.Framework;

// EditMode tests for BookPrefetch.ListKey — the key that decides whether Story Comprehension's book
// download runs again. It carries each book's reading speed: after a speed change in Settings the
// narration of the new speed must be downloaded (bug of 2026-11-26: the key had no speed, so Home
// said "All 20 books are on this phone" while the new narration was missing).
namespace ReadingBuddy.Tests
{
    public class BookPrefetchListKeyTests
    {
        private static List<PRBook> Books(string rev = "r1") => new List<PRBook>
        {
            new PRBook { bookUrl = "TheBigPig_A/script.txt", contentRev = rev },
            new PRBook { bookUrl = "Pigeon_A/script.txt", contentRev = rev },
        };

        [Test]
        public void SameBooksSameSpeed_SameKey()
        {
            Assert.AreEqual(BookPrefetch.ListKey(Books(), b => "-30"), BookPrefetch.ListKey(Books(), b => "-30"));
        }

        [Test]
        public void AnotherSpeed_AnotherKey()
        {
            Assert.AreNotEqual(BookPrefetch.ListKey(Books(), b => "-30"), BookPrefetch.ListKey(Books(), b => "0"));
        }

        [Test]
        public void AnotherSpeedForOneBook_AnotherKey()
        {
            Assert.AreNotEqual(BookPrefetch.ListKey(Books(), b => "-30"),
                BookPrefetch.ListKey(Books(), b => b.bookUrl.StartsWith("Pigeon") ? "10" : "-30"));
        }

        [Test]
        public void AnotherContentRevision_AnotherKey()
        {
            Assert.AreNotEqual(BookPrefetch.ListKey(Books("r1"), b => "-30"), BookPrefetch.ListKey(Books("r2"), b => "-30"));
        }

        [Test]
        public void NavigationTilesAndNulls_AreNotInTheKey()
        {
            var withTiles = Books();
            withTiles.Add(null);
            withTiles.Add(new PRBook { bookUrl = "tile", action = "library:everything" });
            Assert.AreEqual(BookPrefetch.ListKey(Books(), b => "-30"), BookPrefetch.ListKey(withTiles, b => "-30"));
        }
    }
}
