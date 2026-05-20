using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

/// <summary>
/// File-backed cache that survives across app launches. Acts as the
/// persistent tier below the in-memory LRUs in PRUtils.cacheImages
/// (sprites) and AudioAndTextPlayer.CacheAudioAndTimingsStructs
/// (audio + timings).
///
/// Layout: <c>Application.persistentDataPath/cache/&lt;subdir&gt;/&lt;md5&gt;&lt;ext&gt;</c>
///
/// Eviction: each subdir is capped by file count
/// (<see cref="MaxImages"/>, <see cref="MaxAudios"/>, <see cref="MaxTimings"/>);
/// when over the cap, files with the oldest last-access time are
/// deleted. Hit paths touch last-access so frequently-used items
/// survive.
///
/// All operations are best-effort. Disk failures (full disk, permissions,
/// corrupt files) log a warning and fall through; callers must handle the
/// "cache miss" case anyway.
/// </summary>
public static class DiskCache
{
    public const int MaxImages  = 200;   // ~10 MB at ~50 KB avg
    public const int MaxAudios  = 100;   // ~10 MB at ~100 KB avg
    public const int MaxTimings = 100;   // ~1 MB at ~10 KB avg (JSON)

    private static string Root => Path.Combine(Application.persistentDataPath, "cache");

    /// <summary>Compute the on-disk path for a URL. Creates the parent
    /// directory if needed. Does not check existence.</summary>
    public static string PathFor(string url, string subdir, string ext)
    {
        string dir = Path.Combine(Root, subdir);
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        return Path.Combine(dir, Hash(url) + ext);
    }

    /// <summary>Read cached bytes for a URL, or null on miss/error.
    /// On hit, touches last-access time to keep this file in the LRU.</summary>
    public static byte[] TryReadBytes(string url, string subdir, string ext)
    {
        try
        {
            string path = PathFor(url, subdir, ext);
            if (!File.Exists(path)) return null;
            File.SetLastAccessTimeUtc(path, DateTime.UtcNow);
            return File.ReadAllBytes(path);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"DiskCache.TryReadBytes failed for {url}: {e.Message}");
            return null;
        }
    }

    /// <summary>Read cached text for a URL, or null on miss/error.</summary>
    public static string TryReadText(string url, string subdir, string ext)
    {
        try
        {
            string path = PathFor(url, subdir, ext);
            if (!File.Exists(path)) return null;
            File.SetLastAccessTimeUtc(path, DateTime.UtcNow);
            return File.ReadAllText(path);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"DiskCache.TryReadText failed for {url}: {e.Message}");
            return null;
        }
    }

    public static void WriteBytes(string url, string subdir, string ext, byte[] data, int maxFiles)
    {
        if (data == null || data.Length == 0) return;
        try
        {
            string path = PathFor(url, subdir, ext);
            File.WriteAllBytes(path, data);
            TrimSubdir(subdir, maxFiles);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"DiskCache.WriteBytes failed for {url}: {e.Message}");
        }
    }

    public static void WriteText(string url, string subdir, string ext, string text, int maxFiles)
    {
        if (string.IsNullOrEmpty(text)) return;
        try
        {
            string path = PathFor(url, subdir, ext);
            File.WriteAllText(path, text);
            TrimSubdir(subdir, maxFiles);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"DiskCache.WriteText failed for {url}: {e.Message}");
        }
    }

    private static void TrimSubdir(string subdir, int maxFiles)
    {
        try
        {
            string dir = Path.Combine(Root, subdir);
            if (!Directory.Exists(dir)) return;
            var files = new DirectoryInfo(dir).GetFiles();
            if (files.Length <= maxFiles) return;
            // Oldest last-access time first → those are the LRU victims.
            Array.Sort(files, (a, b) => a.LastAccessTimeUtc.CompareTo(b.LastAccessTimeUtc));
            int toDelete = files.Length - maxFiles;
            for (int i = 0; i < toDelete; i++)
            {
                try { files[i].Delete(); }
                catch { /* best-effort eviction; ignore lock/perm errors */ }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"DiskCache.TrimSubdir failed for {subdir}: {e.Message}");
        }
    }

    private static string Hash(string s)
    {
        using (var md5 = MD5.Create())
        {
            var bytes = md5.ComputeHash(Encoding.UTF8.GetBytes(s));
            var sb = new StringBuilder(bytes.Length * 2);
            foreach (var b in bytes) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }
    }
}
