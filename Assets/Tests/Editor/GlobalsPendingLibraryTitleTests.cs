using NUnit.Framework;

// EditMode tests for the pending-shelf-title carry (2026-08-24: a Home door's own label travels with
// the navigation, so "Stories" no longer opens a shelf headed "All Books"). Only the carry itself is
// asserted — Globals.GotoLibrary loads a scene, so the tests drive the field + consumer directly, in
// the same order PRLibrary.SetFilter does.
namespace ReadingBuddy.Tests
{
    public class GlobalsPendingLibraryTitleTests
    {
        [TearDown]
        public void ClearCarry() => Globals.g_pendingLibraryTitle = null;

        [Test]
        public void Consume_ReturnsTheTitleAndClearsIt()
        {
            Globals.g_pendingLibraryTitle = "Songs & Sounds";
            Assert.AreEqual("Songs & Sounds", Globals.ConsumePendingLibraryTitle());
            Assert.IsNull(Globals.g_pendingLibraryTitle);
        }

        [Test]
        public void Consume_IsOnceOnly()
        {
            // This is what keeps the Library's own filter chips on their derived titles: only the
            // entry SetFilter sees the door's label; every later SetFilter gets nothing.
            Globals.g_pendingLibraryTitle = "Stories";
            Globals.ConsumePendingLibraryTitle();
            Assert.IsTrue(string.IsNullOrEmpty(Globals.ConsumePendingLibraryTitle()));
        }

        [Test]
        public void Consume_WithNothingPending_IsEmpty()
        {
            Assert.IsTrue(string.IsNullOrEmpty(Globals.ConsumePendingLibraryTitle()));
        }
    }
}
