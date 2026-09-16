using System.Collections.Generic;
using NUnit.Framework;

// EditMode tests for the two pure helpers behind the Learn-to-Read shelf's level dividers:
// the per-level skill hint (ReadingLevels.Skill) and the "Start here" marker
// (BooksScrollView.StartHereLevel).
namespace ReadingBuddy.Tests
{
    public class ReadingLadderShelfTests
    {
        // ---- ReadingLevels.Skill ----

        [Test]
        public void Skill_NamesWhatEachLevelPractises()
        {
            Assert.AreEqual("short vowels and first words", ReadingLevels.Skill(1));
            Assert.AreEqual("blends and sight words", ReadingLevels.Skill(2));
            Assert.AreEqual("long vowels, longer sentences", ReadingLevels.Skill(3));
            Assert.AreEqual("chapter-length stories", ReadingLevels.Skill(4));
        }

        [Test]
        public void Skill_OutOfRange_IsEmpty()
        {
            // A catalog level the app doesn't know about renders no hint line rather than "Level 9".
            Assert.AreEqual("", ReadingLevels.Skill(0));
            Assert.AreEqual("", ReadingLevels.Skill(5));
            Assert.AreEqual("", ReadingLevels.Skill(-1));
        }

        // ---- BooksScrollView.StartHereLevel ----

        private static Dictionary<int, (int total, int done)> Counts(
            params (int level, int total, int done)[] rows)
        {
            var counts = new Dictionary<int, (int total, int done)>();
            foreach (var r in rows) counts[r.level] = (r.total, r.done);
            return counts;
        }

        [Test]
        public void StartHereLevel_IsTheFirstIncompleteLevel()
        {
            var counts = Counts((1, 4, 4), (2, 6, 2), (3, 5, 0));
            Assert.AreEqual(2, BooksScrollView.StartHereLevel(counts));
        }

        [Test]
        public void StartHereLevel_IsLevelOrder_NotInsertionOrder()
        {
            // Dictionary enumeration order is not level order, so the helper must take the minimum.
            var counts = Counts((4, 3, 0), (2, 6, 1), (3, 5, 5), (1, 4, 4));
            Assert.AreEqual(2, BooksScrollView.StartHereLevel(counts));
        }

        [Test]
        public void StartHereLevel_FirstLevelIncomplete_MarksLevelOne()
        {
            var counts = Counts((1, 4, 0), (2, 6, 0));
            Assert.AreEqual(1, BooksScrollView.StartHereLevel(counts));
        }

        [Test]
        public void StartHereLevel_AllComplete_IsMinusOne()
        {
            var counts = Counts((1, 4, 4), (2, 6, 6), (3, 5, 5));
            Assert.AreEqual(-1, BooksScrollView.StartHereLevel(counts));
        }

        [Test]
        public void StartHereLevel_Empty_IsMinusOne()
        {
            Assert.AreEqual(-1, BooksScrollView.StartHereLevel(Counts()));
            Assert.AreEqual(-1, BooksScrollView.StartHereLevel(null));
        }
    }
}
