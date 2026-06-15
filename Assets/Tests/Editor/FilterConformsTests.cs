using NUnit.Framework;

// EditMode tests for Filter.Conforms (BooksScrollView.cs) — the library genre/age matching logic.
namespace ReadingBuddy.Tests
{
    public class FilterConformsTests
    {
        private static PRBook Book(string genre, int ageFrom = 0, int ageTo = 0)
        {
            return new PRBook { genre = genre, ageFrom = ageFrom, ageTo = ageTo };
        }

        [Test]
        public void Everything_MatchesAnyBook()
        {
            var filter = new Filter { genre = "everything" };
            Assert.IsTrue(filter.Conforms(Book("math")));
            Assert.IsTrue(filter.Conforms(Book("")));
        }

        [Test]
        public void Genre_SubstringMatch_IsCaseInsensitive()
        {
            var filter = new Filter { genre = "family" };
            Assert.IsTrue(filter.Conforms(Book("Family")));
        }

        [Test]
        public void Genre_MatchesWithinMultiGenreString()
        {
            var filter = new Filter { genre = "family" };
            Assert.IsTrue(filter.Conforms(Book("rhymebooks : family : special education")));
        }

        [Test]
        public void Genre_NoMatch_ReturnsFalse()
        {
            var filter = new Filter { genre = "math" };
            Assert.IsFalse(filter.Conforms(Book("rhymebooks : family")));
        }

        [Test]
        public void AgeRange_BookInside_ReturnsTrue()
        {
            // Age branch is only reached when genre is empty.
            var filter = new Filter();
            filter.SetFilter(3, 6, "");
            Assert.IsTrue(filter.Conforms(Book("", ageFrom: 3, ageTo: 6)));
        }

        [Test]
        public void AgeRange_BookOutside_ReturnsFalse()
        {
            var filter = new Filter();
            filter.SetFilter(3, 6, "");
            Assert.IsFalse(filter.Conforms(Book("", ageFrom: 2, ageTo: 8)));
        }

        [Test]
        public void EmptyFilter_MatchesEverything()
        {
            // genre == "" and ageFrom/ageTo == 0 -> falls through to the final return true.
            var filter = new Filter();
            Assert.IsTrue(filter.Conforms(Book("anything", ageFrom: 4, ageTo: 9)));
        }

        // ---- "levelN" addressable filter ----

        private static PRBook LevelBook(int level)
        {
            return new PRBook { genre = "", level = level };
        }

        [Test]
        public void LevelFilter_MatchesBookOfSameLevel()
        {
            var filter = new Filter();
            filter.SetFilter(0, 0, "level1");
            Assert.IsTrue(filter.Conforms(LevelBook(1)));
        }

        [Test]
        public void LevelFilter_DoesNotMatchDifferentLevel()
        {
            var filter = new Filter();
            filter.SetFilter(0, 0, "level1");
            Assert.IsFalse(filter.Conforms(LevelBook(2)));
        }

        [Test]
        public void LevelFilter_IsCaseInsensitive()
        {
            var filter = new Filter();
            filter.SetFilter(0, 0, "LEVEL3");
            Assert.IsTrue(filter.Conforms(LevelBook(3)));
            Assert.IsFalse(filter.Conforms(LevelBook(1)));
        }

        [Test]
        public void GenreFilter_StillConforms_NoRegressionFromLevel()
        {
            // A real genre token is not a level token: level stays 0, genre logic unchanged.
            var filter = new Filter();
            filter.SetFilter(0, 0, "family");
            Assert.AreEqual(0, filter.level);
            Assert.IsTrue(filter.Conforms(Book("rhymebooks : family")));
            Assert.IsFalse(filter.Conforms(Book("math")));
        }

        // ---- Navigation tiles (action set) show only on the home "All Books" view ----

        private static PRBook NavTile(string action)
        {
            // A nav tile is a catalog entry with an action and (typically) no genre.
            return new PRBook { genre = "", action = action };
        }

        [Test]
        public void NavTile_ConformsOnHomeView_EmptyGenreAndLevelZero()
        {
            var filter = new Filter(); // genre "", level 0 — the home view
            Assert.IsTrue(filter.Conforms(NavTile("library?filter=level1")));
        }

        [Test]
        public void NavTile_ConformsOnEverythingView()
        {
            var filter = new Filter();
            filter.SetFilter(0, 0, "everything");
            Assert.IsTrue(filter.Conforms(NavTile("library?filter=level1")));
        }

        [Test]
        public void NavTile_DoesNotConformUnderLevelFilter()
        {
            var filter = new Filter();
            filter.SetFilter(0, 0, "level1");
            Assert.IsFalse(filter.Conforms(NavTile("library?filter=level1")));
        }

        [Test]
        public void NavTile_DoesNotConformUnderGenreFilter()
        {
            var filter = new Filter();
            filter.SetFilter(0, 0, "science");
            Assert.IsFalse(filter.Conforms(NavTile("library?filter=level1")));
        }

        [Test]
        public void NormalBook_Unaffected_ByNavTileRule()
        {
            // A normal book (action "") still matches the home view as before — no regression.
            var filter = new Filter();
            Assert.IsTrue(filter.Conforms(Book("math")));
        }
    }
}
