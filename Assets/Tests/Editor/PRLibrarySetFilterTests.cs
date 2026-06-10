using NUnit.Framework;

// EditMode test documenting the data condition behind the 2026-06-09 SetFilter guard fix
// (BUG_LIBRARY_FILTER_POISONS_CATEGORY_INDEX). The full SetFilter needs scene objects, so only
// the pure precondition — FindIndex returning -1 for an unknown filter — is asserted here.
namespace ReadingBuddy.Tests
{
    public class PRLibrarySetFilterTests
    {
        [Test]
        public void BookCategories_FindIndex_UnknownAgeFilter_ReturnsMinus1()
        {
            // "2-3 years" is a FilterContainer age filter that is NOT in bookCategories. Before the
            // guard, this -1 was written to the static currentCategory and crashed the next Library
            // entry on bookCategories[-1]. This test documents why the guard exists.
            int idx = PRLibrary.bookCategories.FindIndex(c => c.Settings == "2-3 years");
            Assert.AreEqual(-1, idx);
        }

        [Test]
        public void BookCategories_FindIndex_KnownFilter_IsNonNegative()
        {
            int idx = PRLibrary.bookCategories.FindIndex(c => c.Settings == "everything");
            Assert.GreaterOrEqual(idx, 0);
        }
    }
}
