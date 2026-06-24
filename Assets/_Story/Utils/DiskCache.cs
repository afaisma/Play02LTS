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
    // Per-tier on-disk budgets (bytes). Sized so a child can keep a few dozen
    // opened books fully offline (~300 MB total). Eviction is size-based LRU.
    public const long ImageBudgetBytes   = 120L * 1024 * 1024; // page art
    public const long AudioBudgetBytes   = 150L * 1024 * 1024; // narration + word audio
    public const long TimingsBudgetBytes =   8L * 1024 * 1024; // word-timing JSON
    public const long ScriptBudgetBytes  =   4L * 1024 * 1024; // per-book chunk scripts
    public const long CatalogBudgetBytes =   2L * 1024 * 1024; // library catalog (latest)

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

    public static void WriteBytes(string url, string subdir, string ext, byte[] data, long maxBytes)
    {
        if (data == null || data.Length == 0) return;
        try
        {
            string path = PathFor(url, subdir, ext);
            File.WriteAllBytes(path, data);
            TrimSubdirToBudget(subdir, maxBytes);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"DiskCache.WriteBytes failed for {url}: {e.Message}");
        }
    }

    public static void WriteText(string url, string subdir, string ext, string text, long maxBytes)
    {
        if (string.IsNullOrEmpty(text)) return;
        try
        {
            string path = PathFor(url, subdir, ext);
            File.WriteAllText(path, text);
            TrimSubdirToBudget(subdir, maxBytes);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"DiskCache.WriteText failed for {url}: {e.Message}");
        }
    }

    private static void TrimSubdirToBudget(string subdir, long maxBytes)
    {
        try
        {
            string dir = Path.Combine(Root, subdir);
            if (!Directory.Exists(dir)) return;
            var files = new DirectoryInfo(dir).GetFiles();
            long total = 0;
            foreach (var f in files) total += f.Length;
            if (total <= maxBytes) return;
            // Oldest last-access time first → those are the LRU victims.
            Array.Sort(files, (a, b) => a.LastAccessTimeUtc.CompareTo(b.LastAccessTimeUtc));
            for (int i = 0; i < files.Length && total > maxBytes; i++)
            {
                long len = files[i].Length;
                try { files[i].Delete(); total -= len; }
                catch { /* best-effort eviction; ignore lock/perm errors */ }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"DiskCache.TrimSubdirToBudget failed for {subdir}: {e.Message}");
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
