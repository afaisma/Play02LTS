using NUnit.Framework;

// EditMode tests for the shelf display-name map behind the 2026-08-24 title-unification fix: the
// Home doors carry their own labels, but the in-library category arrows derive a title from the
// filter token, so the same room read "Fairytales" one way in and "Fairy Tales" the other.
namespace ReadingBuddy.Tests
{
    public class PRLibraryDisplayNameTests
    {
        [Test]
        public void DisplayName_MappedTokens_MatchTheDoorLabels()
        {
            Assert.AreEqual("Fairy Tales", PRLibrary.DisplayName("fairytales"));
            Assert.AreEqual("Stories", PRLibrary.DisplayName("everything"));
            Assert.AreEqual("Good Habits", PRLibrary.DisplayName("manners"));
            Assert.AreEqual("New Books", PRLibrary.DisplayName("new"));
            Assert.AreEqual("Learn to Read", PRLibrary.DisplayName("learn to read"));
        }

        [Test]
        public void DisplayName_UnmappedToken_FallsBackToTitleCase()
        {
            Assert.AreEqual("Sound & Speech", PRLibrary.DisplayName("sound & speech"));
            Assert.AreEqual("Special Education", PRLibrary.DisplayName("special education"));
            Assert.AreEqual("Adventure", PRLibrary.DisplayName("adventure"));
        }

        [Test]
        public void DisplayName_EveryCategoryToken_HasANonEmptyName()
        {
            foreach (var category in PRLibrary.bookCategories)
                Assert.IsNotEmpty(PRLibrary.DisplayName(category.Settings), category.Settings);
        }

        [Test]
        public void DisplayName_EmptyOrNull_IsEmpty()
        {
            Assert.AreEqual(string.Empty, PRLibrary.DisplayName(null));
            Assert.AreEqual(string.Empty, PRLibrary.DisplayName(""));
        }
    }
}
