using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEngine;

// EditMode tests for DiskCache round-trips and LRU trimming. These hit the real filesystem under
// Application.persistentDataPath/cache/<subdir>; each test uses a dedicated subdir that is deleted
// in [SetUp] and [TearDown] so nothing leaks between runs or into real cache subdirs.
namespace ReadingBuddy.Tests
{
    public class DiskCacheTests
    {
        private const string Sub = "test_diskcache";
        private const string TrimSub = "test_diskcache_trim";

        private static string SubDirPath(string sub)
        {
            return Path.Combine(Application.persistentDataPath, "cache", sub);
        }

        private static void NukeSubdir(string sub)
        {
            string dir = SubDirPath(sub);
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }

        [SetUp]
        public void SetUp()
        {
            NukeSubdir(Sub);
            NukeSubdir(TrimSub);
        }

        [TearDown]
        public void TearDown()
        {
            NukeSubdir(Sub);
            NukeSubdir(TrimSub);
        }

        [Test]
        public void WriteBytes_TryReadBytes_RoundTrips()
        {
            string url = "http://t/audio.mp3";
            byte[] data = Encoding.UTF8.GetBytes("hello-bytes");
            DiskCache.WriteBytes(url, Sub, ".bin", data, DiskCache.AudioBudgetBytes);

            byte[] read = DiskCache.TryReadBytes(url, Sub, ".bin");
            Assert.AreEqual(data, read);
        }

        [Test]
        public void WriteText_TryReadText_RoundTrips()
        {
            string url = "http://t/timings.json";
            string text = "[{\"word\":\"hi\",\"time\":0.0}]";
            DiskCache.WriteText(url, Sub, ".json", text, DiskCache.TimingsBudgetBytes);

            string read = DiskCache.TryReadText(url, Sub, ".json");
            Assert.AreEqual(text, read);
        }

        [Test]
        public void TryReadBytes_Miss_ReturnsNull()
        {
            Assert.IsNull(DiskCache.TryReadBytes("http://t/never-written", Sub, ".bin"));
        }

        [Test]
        public void TryReadText_Miss_ReturnsNull()
        {
            Assert.IsNull(DiskCache.TryReadText("http://t/never-written", Sub, ".json"));
        }

        [Test]
        public void TrimSubdir_EvictsOldestAccessedBeyondCap()
        {
            byte[] data = Encoding.UTF8.GetBytes("x");
            string u1 = "http://t/1", u2 = "http://t/2", u3 = "http://t/3", u4 = "http://t/4";

            // Seed three files without triggering a trim (cap well above count).
            DiskCache.WriteBytes(u1, TrimSub, ".bin", data, 100);
            DiskCache.WriteBytes(u2, TrimSub, ".bin", data, 100);
            DiskCache.WriteBytes(u3, TrimSub, ".bin", data, 100);

            // Make last-access order explicit: u1 oldest, u3 newest.
            File.SetLastAccessTimeUtc(DiskCache.PathFor(u1, TrimSub, ".bin"), System.DateTime.UtcNow.AddMinutes(-30));
            File.SetLastAccessTimeUtc(DiskCache.PathFor(u2, TrimSub, ".bin"), System.DateTime.UtcNow.AddMinutes(-20));
            File.SetLastAccessTimeUtc(DiskCache.PathFor(u3, TrimSub, ".bin"), System.DateTime.UtcNow.AddMinutes(-10));

            // Writing a 4th with cap=3 forces a trim of exactly one file: the oldest-accessed (u1).
            DiskCache.WriteBytes(u4, TrimSub, ".bin", data, 3);

            Assert.IsFalse(File.Exists(DiskCache.PathFor(u1, TrimSub, ".bin")), "oldest (u1) should be evicted");
            Assert.IsTrue(File.Exists(DiskCache.PathFor(u2, TrimSub, ".bin")));
            Assert.IsTrue(File.Exists(DiskCache.PathFor(u3, TrimSub, ".bin")));
            Assert.IsTrue(File.Exists(DiskCache.PathFor(u4, TrimSub, ".bin")));
        }
    }
}
