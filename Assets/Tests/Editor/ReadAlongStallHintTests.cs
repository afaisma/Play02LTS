using NUnit.Framework;
using UnityEngine;

// EditMode tests for the read-along stall hint's pure logic:
//   - ClampCenterX      — keeping the caption pill fully on canvas (the arrow it points at sits
//                         near the screen edge, so a pill centred on it used to hang off-screen)
//   - CaptionPending /  — the once-ever guard: the wordless arrow pulse repeats forever, the
//     MarkCaptionShown    written caption is for a first-time grown-up and retires after one show
// Lives in an Editor folder so it compiles into Assembly-CSharp-Editor and can see the predefined
// Assembly-CSharp game code without an asmdef (same approach as the other tests here).
namespace ReadingBuddy.Tests
{
    public class ReadAlongStallHintTests
    {
        // ---- ClampCenterX ----

        [Test]
        public void ClampCenterX_ComfortablyInside_IsUnchanged()
        {
            Assert.AreEqual(540f, ReadAlongStallHint.ClampCenterX(540f, 300f, 1080f, 12f));
        }

        [Test]
        public void ClampCenterX_NearRightEdge_ClampsToRightLimit()
        {
            // A pill centred on an arrow at x=1040 would run 190px past the canvas.
            float x = ReadAlongStallHint.ClampCenterX(1040f, 300f, 1080f, 12f);
            Assert.AreEqual(1080f - 12f - 150f, x);
            Assert.LessOrEqual(x + 150f, 1080f - 12f); // fully inside, margin respected
        }

        [Test]
        public void ClampCenterX_NearLeftEdge_ClampsSymmetrically()
        {
            float x = ReadAlongStallHint.ClampCenterX(40f, 300f, 1080f, 12f);
            Assert.AreEqual(12f + 150f, x);
            Assert.GreaterOrEqual(x - 150f, 12f);
        }

        [Test]
        public void ClampCenterX_ExactlyOnTheLimit_IsUnchanged()
        {
            Assert.AreEqual(162f, ReadAlongStallHint.ClampCenterX(162f, 300f, 1080f, 12f));
            Assert.AreEqual(918f, ReadAlongStallHint.ClampCenterX(918f, 300f, 1080f, 12f));
        }

        [Test]
        public void ClampCenterX_PillWiderThanCanvas_CentresWithoutNaN()
        {
            // min > max here; clamping between crossed bounds must not produce NaN or an edge pin.
            float x = ReadAlongStallHint.ClampCenterX(900f, 1400f, 1080f, 12f);
            Assert.IsFalse(float.IsNaN(x));
            Assert.AreEqual(540f, x);
        }

        [Test]
        public void ClampCenterX_ZeroWidthCanvas_DoesNotNaN()
        {
            float x = ReadAlongStallHint.ClampCenterX(0f, 300f, 0f, 12f);
            Assert.IsFalse(float.IsNaN(x));
            Assert.AreEqual(0f, x);
        }

        // ---- once-ever caption guard ----

        // The guard is a real PlayerPrefs key, so clear it either side of every test rather than
        // leaving the editor's prefs (and the next test) in whatever state the last one left.
        [SetUp]
        public void ClearBefore() => PlayerPrefs.DeleteKey(ReadAlongStallHint.CaptionShownKey);

        [TearDown]
        public void ClearAfter() => PlayerPrefs.DeleteKey(ReadAlongStallHint.CaptionShownKey);

        [Test]
        public void CaptionPending_TrueOnAFreshDevice()
        {
            Assert.IsTrue(ReadAlongStallHint.CaptionPending());
        }

        [Test]
        public void CaptionPending_FalseOnceMarkedShown()
        {
            ReadAlongStallHint.MarkCaptionShown();
            Assert.IsFalse(ReadAlongStallHint.CaptionPending());
        }

        [Test]
        public void MarkCaptionShown_IsIdempotent()
        {
            ReadAlongStallHint.MarkCaptionShown();
            ReadAlongStallHint.MarkCaptionShown();
            Assert.IsFalse(ReadAlongStallHint.CaptionPending());
            Assert.AreEqual(1, PlayerPrefs.GetInt(ReadAlongStallHint.CaptionShownKey, 0));
        }
    }
}
