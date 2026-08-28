using NUnit.Framework;

// EditMode tests for the blinking next-page hint's pure gate:
//   ShouldBlink — the hint coaches a "tap next" tap, so it must stay quiet while autopage is
//   turning the pages by itself (the default), while still behaving exactly as before in scenes
//   that have no AudioAndTextPlayer at all (_Map, _Message) and once the 5 showings are used up.
// Lives in an Editor folder so it compiles into Assembly-CSharp-Editor and can see the predefined
// Assembly-CSharp game code without an asmdef (same approach as the other tests here).
namespace ReadingBuddy.Tests
{
    public class BlinkingOutlineHintTests
    {
        [Test]
        public void StoryScene_AutopageOff_Blinks()
        {
            Assert.IsTrue(BlinkingOutlineHint.ShouldBlink(true, false, 0, 5));
        }

        [Test]
        public void StoryScene_AutopageOn_DoesNotBlink()
        {
            Assert.IsFalse(BlinkingOutlineHint.ShouldBlink(true, true, 0, 5));
        }

        [Test]
        public void NoPlayerInScene_BlinksAsBefore()
        {
            // _Map / _Message have no AudioAndTextPlayer — behaviour must be unchanged there.
            Assert.IsTrue(BlinkingOutlineHint.ShouldBlink(false, false, 0, 5));
            Assert.IsTrue(BlinkingOutlineHint.ShouldBlink(false, true, 0, 5)); // stale flag is ignored
        }

        [Test]
        public void AllShowingsUsedUp_DoesNotBlink()
        {
            Assert.IsFalse(BlinkingOutlineHint.ShouldBlink(true, false, 5, 5));
            Assert.IsFalse(BlinkingOutlineHint.ShouldBlink(false, false, 6, 5));
        }

        [Test]
        public void LastRemainingShowing_StillBlinks()
        {
            Assert.IsTrue(BlinkingOutlineHint.ShouldBlink(true, false, 4, 5));
        }
    }
}
