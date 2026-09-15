using NUnit.Framework;
using UnityEngine;

// EditMode tests for StoryLayoutTuning.Compute — the pure art/text split of the reader page.
// The four fixtures are the smoke devices, with the screen size and Screen.safeArea each one
// reports in Recordings/simcaps/sim/*_log.txt, and the toolbar's authored 7% anchor span.
namespace ReadingBuddy.Tests
{
    public class StoryLayoutTuningTests
    {
        private const float ToolbarHeightY = 0.07f; // _Story's Toolbar anchorMax.y - anchorMin.y

        private static StoryLayoutTuning.Split Phone17ProMax() =>
            StoryLayoutTuning.Compute(1320f, 2868f, new Rect(0f, 102f, 1320f, 2580f), ToolbarHeightY);

        private static StoryLayoutTuning.Split PhoneSE() =>
            StoryLayoutTuning.Compute(750f, 1334f, new Rect(0f, 0f, 750f, 1334f), ToolbarHeightY);

        private static StoryLayoutTuning.Split IPadMini() =>
            StoryLayoutTuning.Compute(1488f, 2266f, new Rect(0f, 50f, 1488f, 2216f), ToolbarHeightY);

        private static StoryLayoutTuning.Split IPadPro13() =>
            StoryLayoutTuning.Compute(2064f, 2752f, new Rect(0f, 50f, 2064f, 2702f), ToolbarHeightY);

        // The untouched rule, for asserting that a narrow screen is left exactly as it was.
        private static float SquareArtBottomY(float screenH, Rect safeArea) =>
            (safeArea.yMax - safeArea.width) / screenH;

        [Test]
        public void Toolbar_SitsOnTopOfTheHomeIndicatorInset()
        {
            var s = Phone17ProMax();
            Assert.AreEqual(102f / 2868f, s.ToolbarBottomY, 1e-5f);
            Assert.AreEqual(102f / 2868f + ToolbarHeightY, s.ToolbarTopY, 1e-5f);
            Assert.AreEqual(s.ToolbarTopY, s.TextBottomY, 1e-6f, "the text block starts where the toolbar ends");
        }

        [Test]
        public void Art_StopsAtTheTopOfTheSafeArea()
        {
            Assert.AreEqual(2682f / 2868f, Phone17ProMax().ArtTopY, 1e-5f, "below the Dynamic Island");
            Assert.AreEqual(1f, IPadPro13().ArtTopY, 1e-5f, "an iPad has no top inset");
        }

        [Test]
        public void NarrowScreens_KeepTheSquareArt_Unchanged()
        {
            Assert.Less(1320f / 2868f, StoryLayoutTuning.WideAspect);
            Assert.AreEqual(SquareArtBottomY(2868f, new Rect(0f, 102f, 1320f, 2580f)),
                            Phone17ProMax().ArtBottomY, 1e-5f);

            Assert.Less(750f / 1334f, StoryLayoutTuning.WideAspect);
            Assert.AreEqual(SquareArtBottomY(1334f, new Rect(0f, 0f, 750f, 1334f)),
                            PhoneSE().ArtBottomY, 1e-5f);
        }

        [Test]
        public void IPadMini_IsBelowTheThreshold_AndIsNotRetuned()
        {
            Assert.Less(1488f / 2266f, StoryLayoutTuning.WideAspect, "0.657 must stay a no-op");
            Assert.AreEqual(SquareArtBottomY(2266f, new Rect(0f, 50f, 1488f, 2216f)),
                            IPadMini().ArtBottomY, 1e-5f);
            // It reads fine at ~28% even though that is under the wide-screen floor.
            Assert.AreEqual(0.277f, IPadMini().TextShare, 0.005f);
        }

        [Test]
        public void IPadPro13_IsAboveTheThreshold_AndGetsTheMinimumTextShare()
        {
            Assert.GreaterOrEqual(2064f / 2752f, StoryLayoutTuning.WideAspect, "0.75 must be retuned");

            // Before: the square art leaves the text ~18% of the usable height.
            float before = new StoryLayoutTuning.Split(
                50f / 2752f, 50f / 2752f + ToolbarHeightY,
                SquareArtBottomY(2752f, new Rect(0f, 50f, 2064f, 2702f)), 1f).TextShare;
            Assert.AreEqual(0.178f, before, 0.005f);

            Assert.AreEqual(StoryLayoutTuning.MinTextShare, IPadPro13().TextShare, 1e-4f);
        }

        [Test]
        public void RetuningOnlyEverShrinksTheArt()
        {
            var s = IPadPro13();
            float square = SquareArtBottomY(2752f, new Rect(0f, 50f, 2064f, 2702f));
            Assert.Greater(s.ArtBottomY, square, "the art's bottom edge moves UP, i.e. the art gets shorter");
            Assert.Less(s.ArtTopY - s.ArtBottomY, s.ArtTopY - square);
        }

        [Test]
        public void TheThreshold_IsTheOnlyThingThatDecides()
        {
            // Two inset-free screens either side of WideAspect. The narrower one keeps the square
            // art even though its text share is well under the floor — that is the no-op guarantee
            // the phones and the iPad mini rely on.
            var narrow = StoryLayoutTuning.Compute(699f, 1000f, new Rect(0f, 0f, 699f, 1000f), ToolbarHeightY);
            Assert.AreEqual(SquareArtBottomY(1000f, new Rect(0f, 0f, 699f, 1000f)), narrow.ArtBottomY, 1e-5f);
            Assert.Less(narrow.TextShare, StoryLayoutTuning.MinTextShare);

            var wide = StoryLayoutTuning.Compute(701f, 1000f, new Rect(0f, 0f, 701f, 1000f), ToolbarHeightY);
            Assert.AreEqual(StoryLayoutTuning.MinTextShare, wide.TextShare, 1e-4f);
        }

        [Test]
        public void EveryDevice_KeepsTheBandsInOrder()
        {
            foreach (var s in new[] { Phone17ProMax(), PhoneSE(), IPadMini(), IPadPro13() })
            {
                Assert.LessOrEqual(s.ToolbarBottomY, s.ToolbarTopY);
                Assert.LessOrEqual(s.ToolbarTopY, s.ArtBottomY);
                Assert.LessOrEqual(s.ArtBottomY, s.ArtTopY);
                Assert.LessOrEqual(s.ArtTopY, 1f);
                Assert.GreaterOrEqual(s.ToolbarBottomY, 0f);
            }
        }

        [Test]
        public void AnExtremeLandscapeScreen_StillProducesAValidSplit()
        {
            // Art wider than it is tall: the square rule would put the art's bottom below the
            // toolbar, so the clamp has to take over rather than invert the bands.
            var s = StoryLayoutTuning.Compute(2732f, 2048f, new Rect(0f, 0f, 2732f, 2048f), ToolbarHeightY);
            Assert.LessOrEqual(s.ToolbarTopY, s.ArtBottomY);
            Assert.LessOrEqual(s.ArtBottomY, s.ArtTopY);
            Assert.GreaterOrEqual(s.TextShare, StoryLayoutTuning.MinTextShare - 1e-4f);
        }

        [Test]
        public void ADegenerateScreen_DoesNotThrow()
        {
            var s = StoryLayoutTuning.Compute(0f, 0f, new Rect(0f, 0f, 0f, 0f), ToolbarHeightY);
            Assert.AreEqual(0f, s.ToolbarBottomY);
            Assert.AreEqual(1f, s.ArtTopY);
        }
    }
}
