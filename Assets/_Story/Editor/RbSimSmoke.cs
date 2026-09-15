// ReadingBuddy — tier-2 "iOS Simulator smoke" (ported 2026-09-15 from EveryWord's
// EveryWordSimSmoke.cs; design: a REAL IL2CPP iOS build in Apple's Simulator, driven by a
// scripted plan, screenshots taken by the host, Apple tools only).
//
//   1. ExportSimulator(): a SECOND Xcode export with Target SDK = Simulator (arm64) and the
//      READINGBUDDY_DEV define, into Build/iOS-sim. The define travels only through
//      BuildPlayerOptions.extraScriptingDefines — never into ProjectSettings — so the in-app
//      driver cannot exist in a device/store build. SDK + architecture are restored in a
//      finally block and the project is saved so ProjectSettings.asset on disk stays as committed.
//   2. Recognissimo: its iOS libRecognissimo.a has x86_64 (Intel Simulator) + arm64 (DEVICE)
//      slices; an arm64 Simulator link fails on it. For Simulator exports only, the plugin is
//      excluded and RbSimStubs (IPostprocessBuild) links a generated C stub for the 19
//      __Internal entry points. Read-along is therefore UNAVAILABLE in the Simulator (the app's
//      "recognizer unavailable -> narration" fallback handles it); plans must not pick IRead.
//   3. Shell(): starts tools/sim_smoke.sh detached on the Mac with its output in a log file,
//      so the bridge can trigger it and follow the log through the connected folder.
//   4. BuildStamp does NOT bump the build counter for Simulator exports (see BuildStamp.cs).
//
// Bridge pattern (the MCP bridge re-sends a call whose response timed out): call
// RbSimSmoke.ExportSimulatorAsync(thenSmoke) (fast, returns at once), then RbSimSmoke.RunPending()
// (times out while the export runs; re-sends are no-ops because the pending key is cleared
// first, and a request within DuplicateWindowSeconds of the last export start is ignored).
// Progress: Build/_smoke/trace.txt, Build/iOS-sim/_export_status.json, Build/_smoke/smoke.log.
using System;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Debug = UnityEngine.Debug;

public static class RbSimSmoke
{
    public const string Define = "READINGBUDDY_DEV";
    public const string SimFolder = "Build/iOS-sim";                    // relative to the project root
    public const string RecognissimoPlugin = "Assets/Recognissimo/Runtime/Plugins/iOS/libRecognissimo.a";
    public const int DuplicateWindowSeconds = 120;

    public const string PendingKey   = "RB.SimExportPending";
    public const string ThenSmokeKey = "RB.SimSmokeAfterExport";
    private const string LastStartKey = "RB.SimExportLastStart";

    public static string ProjectRoot => Path.GetDirectoryName(Application.dataPath);
    public static string SmokeDir => Path.Combine(ProjectRoot, "Build", "_smoke");
    public static string StatusPath => Path.Combine(ProjectRoot, SimFolder, "_export_status.json");

    [Serializable]
    public class Status { public string stage; public bool ok; public string message; public string when; public float seconds; }

    private static void WriteStatus(string stage, bool ok, string message, float seconds)
    {
        var s = new Status { stage = stage, ok = ok, message = message, when = DateTime.UtcNow.ToString("o"), seconds = seconds };
        Directory.CreateDirectory(Path.GetDirectoryName(StatusPath));
        File.WriteAllText(StatusPath, JsonUtility.ToJson(s, true));
        Trace(stage + " " + (ok ? "ok" : "not-ok") + " " + message);
    }

    /// Append-only trace OUTSIDE the export folder (a full re-export wipes Build/iOS-sim).
    public static void Trace(string line)
    {
        Directory.CreateDirectory(SmokeDir);
        File.AppendAllText(Path.Combine(SmokeDir, "trace.txt"),
            DateTime.UtcNow.ToString("HH:mm:ss.fff") + " " + line + "\n");
    }

    // ------------------------------------------------------------------ menu items

    [MenuItem("Tools/ReadingBuddy/Simulator smoke (export + build + 4 devices)", false, 200)]
    private static void MenuSmoke() => Debug.Log("[RB smoke] " + ExportSimulatorAsync(true));

    [MenuItem("Tools/ReadingBuddy/Simulator export only", false, 201)]
    private static void MenuExportOnly() => Debug.Log("[RB smoke] " + ExportSimulatorAsync(false));

    // ------------------------------------------------------------------ scheduling

    /// Schedules the export on the next editor tick so a bridge call returns at once. The
    /// request is also kept in EditorPrefs: a domain reload between the call and the tick drops
    /// delayCall delegates, and RbSimSmokeResume picks the request up again after the reload.
    public static string ExportSimulatorAsync(bool thenSmoke = false)
    {
        if (RecentlyExported())
            return "ignored: an export ran less than " + DuplicateWindowSeconds + " s ago (bridge re-send?)";
        WriteStatus("export", false, "scheduled", 0);
        EditorPrefs.SetString(PendingKey, "sim");
        EditorPrefs.SetBool(ThenSmokeKey, thenSmoke);
        EditorApplication.delayCall += RunPending;
        return "scheduled: " + SimFolder + " (watch " + StatusPath + "; call RbSimSmoke.RunPending() if idle)";
    }

    private static bool RecentlyExported()
    {
        string last = EditorPrefs.GetString(LastStartKey, "");
        long ticks;
        if (!long.TryParse(last, out ticks)) return false;
        double age = (DateTime.UtcNow - new DateTime(ticks, DateTimeKind.Utc)).TotalSeconds;
        if (age < DuplicateWindowSeconds) { Trace("export request ignored: last export started " + (int)age + " s ago"); return true; }
        return false;
    }

    /// Runs a pending export. Clears the pending key FIRST so a re-sent call is a no-op.
    public static void RunPending()
    {
        if (string.IsNullOrEmpty(EditorPrefs.GetString(PendingKey, ""))) return;
        EditorPrefs.DeleteKey(PendingKey);
        EditorPrefs.SetString(LastStartKey, DateTime.UtcNow.Ticks.ToString());
        ExportSimulator();
    }

    // ------------------------------------------------------------------ export

    /// Unity wipes the export folder itself before a full export, and that wipe fails with
    /// "IOException: Directory not empty" whenever Finder drops a .DS_Store into a folder being
    /// deleted. So: rename the old export aside (atomic), then delete the renamed copy with retries.
    private static void ClearExportFolder(string absFolder)
    {
        if (!Directory.Exists(absFolder)) return;
        string trash = Path.Combine(SmokeDir, Path.GetFileName(absFolder) + "-old-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss"));
        Directory.CreateDirectory(SmokeDir);
        try { Directory.Move(absFolder, trash); }
        catch (Exception e) { Trace("could not move the old export aside: " + e.Message); return; }
        for (int attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                foreach (string ds in Directory.GetFiles(trash, ".DS_Store", SearchOption.AllDirectories)) File.Delete(ds);
                Directory.Delete(trash, true);
                Trace("old export removed (attempt " + attempt + ")");
                return;
            }
            catch (Exception e)
            {
                Trace("old export delete attempt " + attempt + ": " + e.Message);
                System.Threading.Thread.Sleep(500);
            }
        }
        Trace("old export left for the user: " + trash);
    }

    private static string[] EnabledScenes()
    {
        var list = new System.Collections.Generic.List<string>();
        foreach (var s in EditorBuildSettings.scenes) if (s.enabled) list.Add(s.path);
        return list.ToArray();
    }

    private static PluginImporter RecognissimoImporter() => AssetImporter.GetAtPath(RecognissimoPlugin) as PluginImporter;

    public static void ExportSimulator()
    {
        var sw = Stopwatch.StartNew();
        if (EditorApplication.isPlaying) { WriteStatus("export", false, "exit Play mode first", 0); return; }

        var prevSdk  = PlayerSettings.iOS.sdkVersion;
        var prevArch = PlayerSettings.iOS.simulatorSdkArchitecture;
        var plugin   = RecognissimoImporter();
        bool prevPluginIos = plugin != null && plugin.GetCompatibleWithPlatform(BuildTarget.iOS);

        string absFolder = Path.Combine(ProjectRoot, SimFolder);
        // xcodebuild's DerivedData must not live inside the export (the wipe would fail on it).
        string leftover = Path.Combine(absFolder, "DerivedData");
        if (Directory.Exists(leftover))
        {
            string aside = Path.Combine(SmokeDir, "DerivedData-old-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss"));
            Directory.CreateDirectory(SmokeDir);
            Directory.Move(leftover, aside);
            Trace("moved leftover DerivedData aside: " + aside);
        }
        ClearExportFolder(absFolder);
        try
        {
            PlayerSettings.iOS.sdkVersion = iOSSdkVersion.SimulatorSDK;
            // Apple-silicon Mac: the Simulator runs arm64 apps; Unity's default x86_64 slice
            // installs with "Needs to Be Updated / no matching arch" on Xcode 26.
            PlayerSettings.iOS.simulatorSdkArchitecture = AppleMobileArchitectureSimulator.ARM64;
            if (plugin != null && prevPluginIos)
            {   // device-only arm64 slice: keep it out of the Simulator link (RbSimStubs supplies the symbols)
                plugin.SetCompatibleWithPlatform(BuildTarget.iOS, false);
                plugin.SaveAndReimport();
                Trace("Recognissimo iOS plugin excluded for the Simulator export");
            }
            var opts = new BuildPlayerOptions
            {
                scenes = EnabledScenes(),
                locationPathName = SimFolder,
                target = BuildTarget.iOS,
                options = BuildOptions.None,
                extraScriptingDefines = new[] { Define },
            };
            Trace("before BuildPlayer (" + opts.scenes.Length + " scenes)");
            var report = BuildPipeline.BuildPlayer(opts);
            Trace("after BuildPlayer: " + report.summary.result);
            bool ok = report.summary.result == BuildResult.Succeeded;
            WriteStatus("export", ok,
                ok ? "exported " + SimFolder + " (" + report.summary.totalSize / (1024 * 1024) + " MB)"
                   : "build result " + report.summary.result + ", errors " + report.summary.totalErrors,
                (float)sw.Elapsed.TotalSeconds);
            Debug.Log("[RB smoke] simulator export " + (ok ? "OK" : "FAILED") + " -> " + SimFolder);
            if (ok && EditorPrefs.GetBool(ThenSmokeKey, false))
            {
                EditorPrefs.SetBool(ThenSmokeKey, false);
                int pid = Shell("smoke", "", "", Path.Combine(SmokeDir, "smoke.log"));
                Trace("smoke started pid " + pid);
                Debug.Log("[RB smoke] simulator smoke started (pid " + pid + ") — log Build/_smoke/smoke.log");
            }
        }
        catch (Exception e)
        {
            WriteStatus("export", false, "exception: " + e.Message, (float)sw.Elapsed.TotalSeconds);
            Debug.LogError("[RB smoke] simulator export failed: " + e);
        }
        finally
        {
            PlayerSettings.iOS.sdkVersion = prevSdk;                     // Device again, always
            PlayerSettings.iOS.simulatorSdkArchitecture = prevArch;
            var p = RecognissimoImporter();
            if (p != null && prevPluginIos && !p.GetCompatibleWithPlatform(BuildTarget.iOS))
            {
                p.SetCompatibleWithPlatform(BuildTarget.iOS, true);
                p.SaveAndReimport();
            }
            Trace("settings restored (sdk=" + prevSdk + ", plugin iOS=" + prevPluginIos + ")");
            AssetDatabase.SaveAssets();
            EditorApplication.ExecuteMenuItem("File/Save Project");
            Trace("project saved");
        }
    }

    // ------------------------------------------------------------------ shell

    /// Runs `tools/sim_smoke.sh <stage> <extra> <extra2>` detached on the Mac; stdout+stderr -> log.
    public static int Shell(string stage, string extra, string extra2, string logPath)
    {
        string script = Path.Combine(ProjectRoot, "tools", "sim_smoke.sh");
        if (!File.Exists(script)) throw new FileNotFoundException(script);
        Directory.CreateDirectory(Path.GetDirectoryName(logPath));
        string cmd = $"/bin/zsh '{script}' '{stage}' '{extra}' '{extra2}' > '{logPath}' 2>&1";
        var psi = new ProcessStartInfo("/bin/zsh", "-lc \"" + cmd.Replace("\"", "\\\"") + "\"")
        {
            UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = ProjectRoot,
        };
        return Process.Start(psi).Id;
    }
}

/// After any domain reload: run a simulator export that was scheduled but not started.
[InitializeOnLoad]
public static class RbSimSmokeResume
{
    static RbSimSmokeResume()
    {
        if (!string.IsNullOrEmpty(EditorPrefs.GetString(RbSimSmoke.PendingKey, "")))
            EditorApplication.delayCall += RbSimSmoke.RunPending;
    }
}

/// Guard: a device export with the Simulator SDK, or a "-sim" export with the device SDK, is
/// refused. The dev define only travels through RbSimSmoke.ExportSimulator.
public class RbSimGuard : IPreprocessBuildWithReport
{
    public int callbackOrder => -999;   // before BuildStamp (-100)

    public void OnPreprocessBuild(BuildReport report)
    {
        if (report.summary.platform != BuildTarget.iOS) return;
        string leaf = Path.GetFileName(Path.GetFullPath(report.summary.outputPath).TrimEnd('/', '\\'));
        bool simFolder = leaf.EndsWith("-sim");
        bool simSdk = PlayerSettings.iOS.sdkVersion == iOSSdkVersion.SimulatorSDK;
        if (simSdk && !simFolder)
            throw new BuildFailedException("ReadingBuddy: export REFUSED — iOS SDK is Simulator but '" + leaf + "' is a device/store export folder");
        if (simFolder && !simSdk)
            throw new BuildFailedException("ReadingBuddy: export REFUSED — '" + leaf + "' is the Simulator export folder but the iOS SDK is Device");
    }
}

#if UNITY_IOS
/// Simulator exports only: link a C stub for Recognissimo's 19 __Internal entry points (the
/// real .a is excluded above). Every stub returns 0; read-along is unavailable in the Simulator.
public class RbSimStubs : IPostprocessBuildWithReport
{
    public int callbackOrder => 100;

    private static readonly string[] EntryPoints =
    {
        // exact EntryPoint names from Assets/Recognissimo/Runtime/Scripts (grep 'EntryPoint = "')
        "Recognissimo_Algorithm_Create", "Recognissimo_Algorithm_Free",
        "Recognissimo_Context_Abort", "Recognissimo_Context_Create", "Recognissimo_Context_EnqueueFloat32",
        "Recognissimo_Context_EnqueuePCM16", "Recognissimo_Context_Free", "Recognissimo_Context_LastError",
        "Recognissimo_Context_NextEvent", "Recognissimo_Context_Start", "Recognissimo_Context_Stop",
        "Recognissimo_LanguageModel_Create", "Recognissimo_LanguageModel_Free",
        "Recognissimo_SpeechRecognizer_Result", "Recognissimo_SpeechRecognizer_Setup",
        "Recognissimo_VoiceActivityDetector_Result", "Recognissimo_VoiceActivityDetector_Setup",
        "Recognissimo_VoiceControl_Result", "Recognissimo_VoiceControl_Setup",
    };

    public void OnPostprocessBuild(BuildReport report)
    {
        if (report.summary.platform != BuildTarget.iOS) return;
        if (PlayerSettings.iOS.sdkVersion != iOSSdkVersion.SimulatorSDK) return;
        string root = report.summary.outputPath;
        string rel = "Libraries/RecognissimoSimStubs.c";
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("// Generated by RbSimStubs for iOS SIMULATOR exports only. The real libRecognissimo.a");
        sb.AppendLine("// carries a device-only arm64 slice; these stubs let the app link. Read-along is");
        sb.AppendLine("// unavailable in the Simulator: every call returns 0 / does nothing.");
        sb.AppendLine("#include <stdint.h>");
        foreach (var e in EntryPoints) sb.AppendLine("int64_t " + e + "(void) { return 0; }");
        File.WriteAllText(Path.Combine(root, rel), sb.ToString());

        string pbx = UnityEditor.iOS.Xcode.PBXProject.GetPBXProjectPath(root);
        var proj = new UnityEditor.iOS.Xcode.PBXProject();
        proj.ReadFromFile(pbx);
        string target = proj.GetUnityFrameworkTargetGuid();
        string guid = proj.AddFile(rel, rel);
        proj.AddFileToBuild(target, guid);
        proj.WriteToFile(pbx);
        RbSimSmoke.Trace("RbSimStubs: linked " + rel + " into UnityFramework");
    }
}
#endif
