using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

// EditMode tests for FX pure logic: id parsing, library lookup, lab-book
// resolution, and target-rect math. No scene / runtime is created.
namespace FXTests
{
    public class FXLogicTests
    {
        // ---- id parsing ----

        [Test]
        public void NormalizeId_TrimsAndLowercases()
        {
            Assert.AreEqual("stars", FX.NormalizeId("  Stars "));
            Assert.AreEqual("book_done", FX.NormalizeId("BOOK_DONE"));
        }

        [Test]
        public void NormalizeId_NullOrBlank_IsEmpty()
        {
            Assert.AreEqual("", FX.NormalizeId(null));
            Assert.AreEqual("", FX.NormalizeId("   "));
        }

        // ---- library lookup ----

        [Test]
        public void Find_DefaultLibrary_ResolvesKnownIds_CaseInsensitive()
        {
            var lib = FXLibrary.CreateDefault();
            Assert.IsNotNull(lib.Find("stars"));
            Assert.IsNotNull(lib.Find("STARS"));
            Assert.IsNotNull(lib.Find("book_done"));
            Assert.IsNotNull(lib.Find("chime"));
        }

        [Test]
        public void Find_UnknownId_ReturnsNull()
        {
            var lib = FXLibrary.CreateDefault();
            Assert.IsNull(lib.Find("does_not_exist"));
            Assert.IsNull(lib.Find(""));
        }

        // ---- lab-book resolution (against the catalog) ----

        [Test]
        public void ResolveBook_ById_AndFirstToken_AndName()
        {
            var books = new List<PRBook>
            {
                new PRBook { id = "a1", bookName = "The Big Pig" },
                new PRBook { id = "b2", bookName = "Tiny Cat" },
            };

            Assert.AreEqual("Tiny Cat", FXLibrary.ResolveBook(books, "b2", null).bookName);
            Assert.AreEqual("a1", FXLibrary.ResolveBook(books, "*first*", null).id);
            Assert.AreEqual("b2", FXLibrary.ResolveBook(books, null, "tiny cat").id); // case-insensitive
        }

        [Test]
        public void ResolveBook_MissOrEmpty_ReturnsNull()
        {
            var books = new List<PRBook> { new PRBook { id = "a1", bookName = "Solo" } };
            Assert.IsNull(FXLibrary.ResolveBook(books, "zzz", "nope"));
            Assert.IsNull(FXLibrary.ResolveBook(null, "*first*", null));
            Assert.IsNull(FXLibrary.ResolveBook(new List<PRBook>(), "*first*", null));
        }

        // ---- target-rect math ----

        [Test]
        public void CenterOfCorners_AveragesFourCorners()
        {
            // Unity corner order: BL, TL, TR, BR.
            var corners = new[]
            {
                new Vector3(0, 0, 0),
                new Vector3(0, 10, 0),
                new Vector3(20, 10, 0),
                new Vector3(20, 0, 0),
            };
            Assert.AreEqual(new Vector3(10, 5, 0), FXMath.CenterOfCorners(corners));
        }

        [Test]
        public void CenterOfCorners_Degenerate_ReturnsZero()
        {
            Assert.AreEqual(Vector3.zero, FXMath.CenterOfCorners(null));
            Assert.AreEqual(Vector3.zero, FXMath.CenterOfCorners(new Vector3[2]));
        }

        [Test]
        public void PlacementOffset_CornersAndCenter()
        {
            var size = new Vector2(100, 200);
            float inset = 10f;

            Assert.AreEqual(new Vector2(40, 90), FXMath.PlacementOffset(FXLibrary.FXPlacement.TopRight, size, inset));
            Assert.AreEqual(new Vector2(-40, -90), FXMath.PlacementOffset(FXLibrary.FXPlacement.BottomLeft, size, inset));
            Assert.AreEqual(Vector2.zero, FXMath.PlacementOffset(FXLibrary.FXPlacement.Center, size, inset));
        }
    }
}
