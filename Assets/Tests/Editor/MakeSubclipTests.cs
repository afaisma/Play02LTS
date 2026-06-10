using NUnit.Framework;
using UnityEngine;

// EditMode tests for AudioClipUtilities.MakeSubclip — the multi-channel-correct subclip extractor
// that AudioPlayer.PlayAudio actually uses. These cover the mono AND stereo paths, which is the
// test class that would have caught the mono-only "channels: 1" bug still present in the dead
// PRUtils.MakeSubclip.
//
// NOTE — two negative cases from the plan (invalid range -> null, null clip -> null) are NOT
// asserted here. Both emit Debug.LogError, which the Unity Test Runner treats as a test failure
// unless suppressed with LogAssert.Expect. LogAssert lives in UnityEngine.TestTools, which is NOT
// auto-referenced into Assembly-CSharp-Editor (the Editor-folder approach chosen to avoid putting
// an asmdef on game code). Those cases need either a test asmdef referencing UnityEngine.TestRunner
// (which then cannot see the predefined game assembly) or the M-R3 negative paths to log warnings
// instead of errors. Reported as a known gap.
namespace ReadingBuddy.Tests
{
    public class MakeSubclipTests
    {
        private const int Freq = 44100;

        // Build a 1-second clip whose flat sample buffer is a ramp data[i] = i, so we can verify
        // exactly which source samples ended up in the subclip.
        private static AudioClip RampClip(string name, int channels)
        {
            var clip = AudioClip.Create(name, Freq, channels, Freq, false);
            var data = new float[Freq * channels];
            for (int i = 0; i < data.Length; i++) data[i] = i;
            clip.SetData(data, 0);
            return clip;
        }

        [Test]
        public void Mono_LengthChannelsFrequency()
        {
            AudioClip src = RampClip("mono", 1);
            AudioClip sub = AudioClipUtilities.MakeSubclip(src, 0.25f, 0.75f);

            Assert.IsNotNull(sub);
            Assert.AreEqual(1, sub.channels);
            Assert.AreEqual(Freq, sub.frequency);
            // 0.75s - 0.25s = 0.5s = 22050 samples per channel.
            Assert.AreEqual(22050, sub.samples);

            Object.DestroyImmediate(src);
            Object.DestroyImmediate(sub);
        }

        [Test]
        public void Mono_SampleContentStartsAtRequestedOffset()
        {
            AudioClip src = RampClip("mono2", 1);
            AudioClip sub = AudioClipUtilities.MakeSubclip(src, 0.25f, 0.75f);

            var got = new float[sub.samples];
            sub.GetData(got, 0);
            // First subclip sample should be the source sample at floor(0.25 * 44100) = 11025.
            Assert.That(got[0], Is.EqualTo(11025f).Within(0.5f));

            Object.DestroyImmediate(src);
            Object.DestroyImmediate(sub);
        }

        [Test]
        public void Stereo_LengthChannelsFrequency()
        {
            AudioClip src = RampClip("stereo", 2);
            AudioClip sub = AudioClipUtilities.MakeSubclip(src, 0.25f, 0.75f);

            Assert.IsNotNull(sub);
            Assert.AreEqual(2, sub.channels);
            Assert.AreEqual(Freq, sub.frequency);
            Assert.AreEqual(22050, sub.samples);

            Object.DestroyImmediate(src);
            Object.DestroyImmediate(sub);
        }

        [Test]
        public void Stereo_SampleContentPreservesInterleavedData()
        {
            AudioClip src = RampClip("stereo2", 2);
            AudioClip sub = AudioClipUtilities.MakeSubclip(src, 0.25f, 0.75f);

            var got = new float[sub.samples * sub.channels];
            sub.GetData(got, 0);
            // Interleaved: first value is source frame 11025, channel 0 -> flat index 11025*2 = 22050.
            Assert.That(got[0], Is.EqualTo(22050f).Within(0.5f));

            Object.DestroyImmediate(src);
            Object.DestroyImmediate(sub);
        }

        [Test]
        public void WholeClip_StopEqualsLength_IsAllowed_M_R3_2()
        {
            AudioClip src = RampClip("whole", 1);
            // M-R3-2: stop == clip.length is a legitimate whole-clip request (previously rejected).
            AudioClip sub = AudioClipUtilities.MakeSubclip(src, 0f, src.length);

            Assert.IsNotNull(sub);
            Assert.AreEqual(Freq, sub.samples);

            Object.DestroyImmediate(src);
            Object.DestroyImmediate(sub);
        }
    }
}
