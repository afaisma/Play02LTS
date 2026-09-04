using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

// ============================================================================================
// Build versioning (the "which APK is this?" fix):
//   1. Every player build auto-increments Android bundleVersionCode BEFORE building — Android
//      silently keeps the installed app when offered an older-or-equal code, which is exactly
//      how a tester ends up reporting "nothing changed". Codes must never repeat.
//   2. The version/build/date/git quadruple is written to Assets/Resources/build_info.json,
//      which the runtime BuildInfo class shows in the For-grown-ups footer and the [BUILD] log.
// Tools menu: "Write Build Stamp (no bump)" refreshes the json without consuming a code —
// for editor testing; a real build always re-stamps on top of it.
// ============================================================================================
public class BuildStamp : IPreprocessBuildWithReport
{
    public int callbackOrder { get { return -100; } }

    [Serializable]
    private class Payload
    {
        public string version;
        public int build;
        public string builtAt;
        public string git;
    }

    public void OnPreprocessBuild(BuildReport report)
    {
        PlayerSettings.Android.bundleVersionCode += 1;
        // iOS CFBundleVersion must also ascend (TestFlight/App Store reject reused numbers).
        // Mirror the one counter instead of keeping a second one, so an Android build and an
        // iOS build never share a number and the For-grown-ups footer means the same thing
        // on both platforms.
        PlayerSettings.iOS.buildNumber = PlayerSettings.Android.bundleVersionCode.ToString();
        AssetDatabase.SaveAssets(); // persist the bump alongside the stamp
        WriteStamp();
        Debug.Log("[BUILD-STAMP] bundleVersionCode/iOS buildNumber -> "
                  + PlayerSettings.Android.bundleVersionCode + "; build_info.json written");
    }

    [MenuItem("Tools/ReadingBuddy/Write Build Stamp (no bump)")]
    public static void WriteStampMenu() { WriteStamp(); }

    public static void WriteStamp()
    {
        var p = new Payload
        {
            version = PlayerSettings.bundleVersion,
            build = PlayerSettings.Android.bundleVersionCode,
            builtAt = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm",
                        System.Globalization.CultureInfo.InvariantCulture),
            git = GitDescribe()
        };
        string dir = Path.Combine(Application.dataPath, "Resources");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "build_info.json"), JsonUtility.ToJson(p, true));
        AssetDatabase.ImportAsset("Assets/Resources/build_info.json");
    }

    // Short commit hash, with "*" appended when the working tree is dirty. Any git failure
    // (no git, no repo, timeout) degrades to "" — the stamp still carries version/build/date.
    private static string GitDescribe()
    {
        string hash = RunGit("rev-parse --short HEAD");
        if (string.IsNullOrEmpty(hash)) return "";
        string dirty = RunGit("status --porcelain");
        return dirty == null ? hash : (dirty.Length > 0 ? hash + "*" : hash);
    }

    private static string RunGit(string args)
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo("git", args)
            {
                WorkingDirectory = Path.GetDirectoryName(Application.dataPath),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using (var proc = System.Diagnostics.Process.Start(psi))
            {
                string outp = proc.StandardOutput.ReadToEnd();
                if (!proc.WaitForExit(4000)) { try { proc.Kill(); } catch { } return null; }
                return proc.ExitCode == 0 ? outp.Trim() : null;
            }
        }
        catch { return null; }
    }
}
