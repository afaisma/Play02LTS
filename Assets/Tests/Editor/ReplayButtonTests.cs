using System.Reflection;
using NUnit.Framework;
using UnityEngine;

// EditMode tests for pause on the reading page (round 7, Part 5): the face of the toolbar's Repeat
// button, and the narration player's pause flags (a pause only while the page is read aloud; Play
// clears it; a stop - the reading-mode picker opening, a page turn while paused - clears both).
namespace ReadingBuddy.Tests
{
    public class ReplayButtonTests
    {
        private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;

        [Test]
        public void ReadingAloud_ShowsPause()
        {
            Assert.AreEqual(PRScript.ReplayButtonState.Pause, PRScript.ReplayButtonStateFor(readingAloud: true, paused: false));
        }

        [Test]
        public void Paused_ShowsPlay()
        {
            Assert.AreEqual(PRScript.ReplayButtonState.Play, PRScript.ReplayButtonStateFor(readingAloud: true, paused: true));
        }

        [Test]
        public void FinishedOrNoNarration_ShowsRepeat()
        {
            // The page has ended, or pictures only / "I read": the player is not reading aloud.
            Assert.AreEqual(PRScript.ReplayButtonState.Repeat, PRScript.ReplayButtonStateFor(readingAloud: false, paused: false));
        }

        private static AudioAndTextPlayer MakePlayer(out GameObject go)
        {
            go = new GameObject("NarrationPlayerTest");
            var source = go.AddComponent<AudioSource>();
            var player = go.AddComponent<AudioAndTextPlayer>();
            typeof(AudioAndTextPlayer).GetField("audioSource", Private).SetValue(player, source);
            return player;
        }

        // What LoadAudioAndTimings sets when a page starts to narrate with sound.
        private static void StartReadingAloud(AudioAndTextPlayer player) =>
            typeof(AudioAndTextPlayer).GetField("_readingAloud", Private).SetValue(player, true);

        private static PRScript.ReplayButtonState Face(AudioAndTextPlayer p) =>
            PRScript.ReplayButtonStateFor(p.IsReadingAloud, p.IsPaused);

        [Test]
        public void Player_NotReadingAloud_CannotBePaused()
        {
            var player = MakePlayer(out var go);
            try
            {
                player.PauseReading();
                Assert.IsFalse(player.IsPaused);
                Assert.AreEqual(PRScript.ReplayButtonState.Repeat, Face(player));
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void Player_PauseThenPlay_ShowsPlayThenPause()
        {
            var player = MakePlayer(out var go);
            try
            {
                StartReadingAloud(player);
                Assert.AreEqual(PRScript.ReplayButtonState.Pause, Face(player));
                player.PauseReading();
                Assert.IsTrue(player.IsPaused);
                Assert.AreEqual(PRScript.ReplayButtonState.Play, Face(player));
                player.ResumeReading();
                Assert.IsFalse(player.IsPaused);
                Assert.AreEqual(PRScript.ReplayButtonState.Pause, Face(player));
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void Player_PausedBetweenTwoWords_MarksTheWordJustRead()
        {
            var player = MakePlayer(out var go);
            try
            {
                var fg = new GameObject("Foreground").AddComponent<TMPro.TextMeshProUGUI>();
                var bg = new GameObject("Background").AddComponent<TMPro.TextMeshProUGUI>();
                fg.transform.SetParent(go.transform); bg.transform.SetParent(go.transform);
                var t = typeof(AudioAndTextPlayer);
                t.GetField("uiForeground", Private).SetValue(player, fg);
                t.GetField("uiBackground", Private).SetValue(player, bg);
                // Words, spaces and punctuation are separate tokens; the mark is on the space after the comma.
                t.GetField("currentWordTimings", Private).SetValue(player, new System.Collections.Generic.List<WordTiming>
                {
                    new WordTiming { Word = "Jack", Time = 0f }, new WordTiming { Word = " ", Time = 300f },
                    new WordTiming { Word = "mother", Time = 400f }, new WordTiming { Word = ",", Time = 800f },
                    new WordTiming { Word = " ", Time = 850f }, new WordTiming { Word = "went", Time = 900f },
                });
                t.GetField("currentWordIndex", Private).SetValue(player, 4);
                StartReadingAloud(player);
                player.PauseReading();
                Assert.AreEqual(2, (int)t.GetField("currentWordIndex", Private).GetValue(player));
                StringAssert.Contains(">mother</mark>", bg.text);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void Player_StopWhilePaused_ClearsThePause()
        {
            var player = MakePlayer(out var go);
            try
            {
                StartReadingAloud(player);
                player.PauseReading();
                player.StopAudio(); // the reading-mode picker opens, or the reader turns the page
                Assert.IsFalse(player.IsPaused);
                Assert.IsFalse(player.IsReadingAloud);
                Assert.AreEqual(PRScript.ReplayButtonState.Repeat, Face(player));
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
