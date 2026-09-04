using NUnit.Framework;

// EditMode tests for FrameRatePolicy.DecideInterval — the pure half of the frame-rate policy.
// The runner that gathers the signals needs a live player loop and is not covered here.
namespace ReadingBuddy.Tests
{
    public class FrameRatePolicyTests
    {
        // Named helper so each test reads as "only this one signal is on".
        private static int Decide(bool input = false, bool audio = false, bool tweens = false,
                                  bool scroll = false, bool scene = false, bool video = false)
        {
            return FrameRatePolicy.DecideInterval(input, audio, tweens, scroll, scene, video);
        }

        [Test]
        public void AllQuiet_ThrottlesToHalfRate()
        {
            Assert.AreEqual(FrameRatePolicy.IntervalIdle, Decide());
        }

        [Test]
        public void InputAlone_RendersEveryFrame()
        {
            Assert.AreEqual(FrameRatePolicy.IntervalActive, Decide(input: true));
        }

        [Test]
        public void NarrationAlone_RendersEveryFrame()
        {
            Assert.AreEqual(FrameRatePolicy.IntervalActive, Decide(audio: true));
        }

        [Test]
        public void TweensAlone_RendersEveryFrame()
        {
            Assert.AreEqual(FrameRatePolicy.IntervalActive, Decide(tweens: true));
        }

        [Test]
        public void ScrollInertiaAlone_RendersEveryFrame()
        {
            Assert.AreEqual(FrameRatePolicy.IntervalActive, Decide(scroll: true));
        }

        [Test]
        public void SceneSettlingAlone_RendersEveryFrame()
        {
            Assert.AreEqual(FrameRatePolicy.IntervalActive, Decide(scene: true));
        }

        [Test]
        public void VideoAlone_RendersEveryFrame()
        {
            Assert.AreEqual(FrameRatePolicy.IntervalActive, Decide(video: true));
        }

        [Test]
        public void EverySignalOn_RendersEveryFrame()
        {
            Assert.AreEqual(FrameRatePolicy.IntervalActive,
                Decide(true, true, true, true, true, true));
        }

        [Test]
        public void NeverThrottlesBelowHalfRate()
        {
            // The idle interval is the worst case the policy may ever apply: a signal we failed
            // to notice must cost at most the 30 fps the app shipped with before this policy.
            Assert.AreEqual(2, FrameRatePolicy.IntervalIdle);
            Assert.AreEqual(1, FrameRatePolicy.IntervalActive);
            Assert.AreEqual(60, FrameRatePolicy.TargetFrameRate);
        }
    }
}
