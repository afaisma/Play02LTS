using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

// EditMode tests for the word-tap feature's pure logic on AudioAndTextPlayer:
//   - BuildTtsUrls   — the shared {chunk}_{rate}{post}.mp3 / ..._timings{post}.json naming + ?v=rev
//                      (still used by Play() for the page-narration URL)
//   - NormalizeWord  — token → word-bank key normalization (must match SceneForge)
//   - TryPlayWord    — bank-only decision: play (and consume the tap) only when the bank is loaded
//                      and contains the normalized word; otherwise no-op
// Lives in an Editor folder so it compiles into Assembly-CSharp-Editor and can see the predefined
// Assembly-CSharp game code without an asmdef (same approach as the other tests here).
namespace ReadingBuddy.Tests
{
    public class WordTapPlayerTests
    {
        // ---- BuildTtsUrls ----

        [Test]
        public void BuildTtsUrls_NoRev_NoPostfix()
        {
            var u = AudioAndTextPlayer.BuildTtsUrls("page1", "0", "", "");
            Assert.AreEqual("page1_0.mp3", u.audio);
            Assert.AreEqual("page1_0_timings.json", u.timings);
        }

        [Test]
        public void BuildTtsUrls_WithPostfix()
        {
            var u = AudioAndTextPlayer.BuildTtsUrls("page1", "-20", "_amy", "");
            Assert.AreEqual("page1_-20_amy.mp3", u.audio);
            Assert.AreEqual("page1_-20_timings_amy.json", u.timings);
        }

        [Test]
        public void BuildTtsUrls_WithRev_AppendsVersionQueryToBoth()
        {
            var u = AudioAndTextPlayer.BuildTtsUrls("page1", "0", "_amy", "abc123");
            Assert.AreEqual("page1_0_amy.mp3?v=abc123", u.audio);
            Assert.AreEqual("page1_0_timings_amy.json?v=abc123", u.timings);
        }

        // ---- NormalizeWord (must match SceneForge) ----

        [Test]
        public void NormalizeWord_TrimsTrailingPunctuation()
        {
            Assert.AreEqual("me", AudioAndTextPlayer.NormalizeWord("Me,"));
        }

        [Test]
        public void NormalizeWord_KeepsInternalApostrophe()
        {
            Assert.AreEqual("don't", AudioAndTextPlayer.NormalizeWord("Don't"));
        }

        [Test]
        public void NormalizeWord_TrimsSurroundingWhitespaceAndPunctuation()
        {
            Assert.AreEqual("pigeon", AudioAndTextPlayer.NormalizeWord("  …Pigeon. "));
        }

        [Test]
        public void NormalizeWord_PunctuationOrEmpty_ReturnsEmpty()
        {
            Assert.AreEqual("", AudioAndTextPlayer.NormalizeWord("."));
            Assert.AreEqual("", AudioAndTextPlayer.NormalizeWord("…"));
            Assert.AreEqual("", AudioAndTextPlayer.NormalizeWord("   "));
            Assert.AreEqual("", AudioAndTextPlayer.NormalizeWord(""));
            Assert.AreEqual("", AudioAndTextPlayer.NormalizeWord(null));
        }

        // ---- TryPlayWord: bank-only decision ----
        // TryPlayWord is an instance method gated on private bank state; the test sets that state via
        // reflection and asserts only the bool decision. Playback itself is a no-op here because the
        // dedicated _wordTapSource is left null (PlaySlice early-returns on a null source).

        private static AudioAndTextPlayer NewPlayer(out GameObject go)
        {
            go = new GameObject("wordtap-test");
            return go.AddComponent<AudioAndTextPlayer>();
        }

        private static void SetBank(AudioAndTextPlayer p, bool ready, AudioClip clip,
            Dictionary<string, (float start, float end)> map)
        {
            var t = typeof(AudioAndTextPlayer);
            const BindingFlags F = BindingFlags.NonPublic | BindingFlags.Instance;
            t.GetField("_wordBankReady", F).SetValue(p, ready);
            t.GetField("_wordBankClip", F).SetValue(p, clip);
            t.GetField("_wordBankMap", F).SetValue(p, map);
        }

        [Test]
        public void TryPlayWord_BankReadyAndWordPresent_ReturnsTrue()
        {
            var player = NewPlayer(out var go);
            try
            {
                var clip = AudioClip.Create("wb", 44100, 1, 44100, false);
                var map = new Dictionary<string, (float start, float end)> { { "me", (100f, 400f) } };
                SetBank(player, true, clip, map);

                Assert.IsTrue(player.TryPlayWord("Me,")); // "Me," normalizes to "me", which is in the bank
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void TryPlayWord_WordMissingFromBank_ReturnsFalse()
        {
            var player = NewPlayer(out var go);
            try
            {
                var clip = AudioClip.Create("wb", 44100, 1, 44100, false);
                var map = new Dictionary<string, (float start, float end)> { { "me", (100f, 400f) } };
                SetBank(player, true, clip, map);

                Assert.IsFalse(player.TryPlayWord("dog")); // not in the bank → no-op, tap falls through
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void TryPlayWord_BankNotReady_ReturnsFalse()
        {
            var player = NewPlayer(out var go);
            try
            {
                // No bank loaded at all (defaults): ready=false, clip=null, map=null.
                Assert.IsFalse(player.TryPlayWord("me"));
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void TryPlayWord_Disabled_ReturnsFalse()
        {
            var player = NewPlayer(out var go);
            try
            {
                var clip = AudioClip.Create("wb", 44100, 1, 44100, false);
                var map = new Dictionary<string, (float start, float end)> { { "me", (100f, 400f) } };
                SetBank(player, true, clip, map);
                player.wordTapEnabled = false;

                Assert.IsFalse(player.TryPlayWord("me")); // gate off → never plays
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
