using NUnit.Framework;

// EditMode tests for the shelf-title derivation behind the 2026-08-24 "shelf titles must match door
// labels" fix. The full SetFilter needs scene objects, so only the pure title helper is asserted
// here — the door-label carry itself is covered by GlobalsPendingLibraryTitleTests.
namespace ReadingBuddy.Tests
{
    public class PRLibraryTitleCaseTests
    {
        [Test]
        public void TitleCase_CapitalizesEveryWord()
        {
            // The reported case: PRUtils.CapitalizeFirstLetter only touched index 0, so this filter
            // rendered as "Sound & speech".
            Assert.AreEqual("Sound & Speech", PRLibrary.TitleCase("sound & speech"));
            Assert.AreEqual("Special Education", PRLibrary.TitleCase("special education"));
        }

        [Test]
        public void TitleCase_SingleWord_MatchesOldBehaviour()
        {
            Assert.AreEqual("Rhymebooks", PRLibrary.TitleCase("rhymebooks"));
            Assert.AreEqual("Math", PRLibrary.TitleCase("math"));
        }

        [Test]
        public void TitleCase_EmptyOrNull_IsEmpty()
        {
            Assert.AreEqual(string.Empty, PRLibrary.TitleCase(null));
            Assert.AreEqual(string.Empty, PRLibrary.TitleCase(""));
        }

        [Test]
        public void TitleCase_LeavesAlreadyCapitalizedTextAlone()
        {
            Assert.AreEqual("Learn To Read", PRLibrary.TitleCase("Learn To Read"));
        }
    }
}
