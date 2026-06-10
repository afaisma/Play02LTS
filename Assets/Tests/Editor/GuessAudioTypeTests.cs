using NUnit.Framework;
using UnityEngine;

// EditMode tests for AudioPlayer.GuessAudioTypeFromUrl (made public for reuse by SoundBar).
namespace ReadingBuddy.Tests
{
    public class GuessAudioTypeTests
    {
        [Test]
        public void Wav() => Assert.AreEqual(AudioType.WAV, AudioPlayer.GuessAudioTypeFromUrl("http://x/a.wav"));

        [Test]
        public void Ogg() => Assert.AreEqual(AudioType.OGGVORBIS, AudioPlayer.GuessAudioTypeFromUrl("http://x/a.ogg"));

        [Test]
        public void Mp3() => Assert.AreEqual(AudioType.MPEG, AudioPlayer.GuessAudioTypeFromUrl("http://x/a.mp3"));

        [Test]
        public void Aiff() => Assert.AreEqual(AudioType.AIFF, AudioPlayer.GuessAudioTypeFromUrl("http://x/a.aiff"));

        [Test]
        public void Aif() => Assert.AreEqual(AudioType.AIFF, AudioPlayer.GuessAudioTypeFromUrl("http://x/a.aif"));

        [Test]
        public void NoExtension_DefaultsToMpeg() => Assert.AreEqual(AudioType.MPEG, AudioPlayer.GuessAudioTypeFromUrl("http://x/a"));

        [Test]
        public void QueryStringStrippedBeforeExtensionLookup()
        {
            Assert.AreEqual(AudioType.WAV, AudioPlayer.GuessAudioTypeFromUrl("http://x/a.wav?token=123"));
        }

        [Test]
        public void Empty_DefaultsToMpeg() => Assert.AreEqual(AudioType.MPEG, AudioPlayer.GuessAudioTypeFromUrl(""));

        [Test]
        public void Null_DefaultsToMpeg() => Assert.AreEqual(AudioType.MPEG, AudioPlayer.GuessAudioTypeFromUrl(null));
    }
}
