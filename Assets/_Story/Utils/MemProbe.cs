// Memory probe: one line summarising what is resident, for the device log and for the editor
// sweeps. Added 2026-09-17 while chasing the iOS memory kill (EXC_RESOURCE, 2 GB high
// watermark). Cheap enough to log on every page turn: it walks the loaded Texture2D and
// AudioClip objects once (a few thousand at most).
//
//   [MEM] <tag> total=812MB mono=131MB tex2d=196MB/41 audio=9MB/19 gfx=245MB foot=934MB
//         topTex=TF3.png:2700x2700, ... topAudio=chunk_4:2MB, ...
//
// total = Profiler.GetTotalAllocatedMemoryLong (native + managed, what iOS counts)
// mono  = managed C# heap in use; tex2d = sum of width*height*4 over Texture2D objects
// (RGBA upper bound), audio = samples*channels*4 over AudioClips, gfx = graphics driver.
// foot  = task_vm_info.phys_footprint — the ONE number jetsam's ~2 GB limit is applied to.
// On-device iOS only (Plugins/iOS/MemFootprint.mm); omitted in the editor and elsewhere.
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Profiling;

public static class MemProbe
{
#if UNITY_IOS && !UNITY_EDITOR
    [System.Runtime.InteropServices.DllImport("__Internal")]
    private static extern long rb_phys_footprint();
#endif

    private static bool _lowMemoryHooked;

    /// <summary>Subscribe once per session to Application.lowMemory, so the last thing in the
    /// log before a jetsam kill is a full picture of what was resident.</summary>
    public static void InstallLowMemoryHandler()
    {
        if (_lowMemoryHooked) return;
        _lowMemoryHooked = true;
        Application.lowMemory += () => Log("lowMemory");
    }

    public static string Line(string tag, int top = 4)
    {
        long texB = 0; int texN = 0;
        var texTop = new List<KeyValuePair<long, string>>();
        foreach (var t in Resources.FindObjectsOfTypeAll<Texture2D>())
        {
            long b = (long)t.width * t.height * 4; texB += b; texN++;
            texTop.Add(new KeyValuePair<long, string>(b, (string.IsNullOrEmpty(t.name) ? "?" : t.name) + ":" + t.width + "x" + t.height));
        }
        long audB = 0; int audN = 0;
        var audTop = new List<KeyValuePair<long, string>>();
        foreach (var c in Resources.FindObjectsOfTypeAll<AudioClip>())
        {
            long b = (long)c.samples * c.channels * 4; audB += b; audN++;
            audTop.Add(new KeyValuePair<long, string>(b, (string.IsNullOrEmpty(c.name) ? "?" : c.name) + ":" + (b / 1048576) + "MB"));
        }
        texTop.Sort((a, b) => b.Key.CompareTo(a.Key));
        audTop.Sort((a, b) => b.Key.CompareTo(a.Key));

        var sb = new StringBuilder();
        sb.Append("[MEM] ").Append(tag)
          .Append(" total=").Append(Profiler.GetTotalAllocatedMemoryLong() / 1048576).Append("MB")
          .Append(" mono=").Append(Profiler.GetMonoUsedSizeLong() / 1048576).Append("MB")
          .Append(" tex2d=").Append(texB / 1048576).Append("MB/").Append(texN)
          .Append(" audio=").Append(audB / 1048576).Append("MB/").Append(audN)
          .Append(" gfx=").Append(Profiler.GetAllocatedMemoryForGraphicsDriver() / 1048576).Append("MB");
#if UNITY_IOS && !UNITY_EDITOR
        long foot = -1;
        try { foot = rb_phys_footprint(); } catch { } // missing symbol -> just omit the field
        if (foot >= 0) sb.Append(" foot=").Append(foot / 1048576).Append("MB");
#endif
        sb.Append(" topTex=");
        for (int i = 0; i < Mathf.Min(top, texTop.Count); i++) sb.Append(i > 0 ? "," : "").Append(texTop[i].Value);
        sb.Append(" topAudio=");
        for (int i = 0; i < Mathf.Min(top, audTop.Count); i++) sb.Append(i > 0 ? "," : "").Append(audTop[i].Value);
        return sb.ToString();
    }

    /// <summary>Console line plus an append to Documents/memlog.txt, so a detached device run
    /// (no debugger, no Console.app) still leaves a record: pull the app container from Xcode's
    /// Devices window. The file is capped at ~256 KB (oldest half dropped) so it can't grow.</summary>
    public static void Log(string tag)
    {
        string line = Line(tag);
        Debug.Log(line);
        try
        {
            string path = System.IO.Path.Combine(Application.persistentDataPath, "memlog.txt");
            var fi = new System.IO.FileInfo(path);
            if (fi.Exists && fi.Length > 256 * 1024)
            {
                string[] all = System.IO.File.ReadAllLines(path);
                var keep = new string[all.Length - all.Length / 2];
                System.Array.Copy(all, all.Length / 2, keep, 0, keep.Length);
                System.IO.File.WriteAllLines(path, keep);
            }
            System.IO.File.AppendAllText(path, System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss ") + line + "\n");
        }
        catch { /* logging must never throw */ }
    }
}

/// <summary>Editor/sweep helper: appends a MemProbe line to Documents/sweep/_mem.txt every
/// <see cref="interval"/> seconds, tagged with the sweep's latest log marker.</summary>
public class MemProbeSampler : MonoBehaviour
{
    public float interval = 2f;
    private string _prev = "";

    private void Start() { StartCoroutine(Run()); }

    private System.Collections.IEnumerator Run()
    {
        string dir = System.IO.Path.Combine(Application.persistentDataPath, "sweep");
        System.IO.Directory.CreateDirectory(dir);
        string outp = System.IO.Path.Combine(dir, "_mem.txt");
        string logp = System.IO.Path.Combine(dir, "_log.txt");
        System.IO.File.WriteAllText(outp, "");
        while (true)
        {
            string marker = "";
            try
            {
                var l = System.IO.File.ReadAllLines(logp);
                for (int i = l.Length - 1; i >= 0; i--)
                    if (!l[i].StartsWith("ok ") && !l[i].StartsWith("#") && !l[i].StartsWith("ERR")) { marker = l[i]; break; }
            }
            catch { }
            string line = MemProbe.Line(marker + "/" + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name, 3);
            if (line != _prev) { System.IO.File.AppendAllText(outp, line + "\n"); _prev = line; }
            yield return new WaitForSecondsRealtime(interval);
        }
    }
}
