#if UNITY_EDITOR
using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Builds an Android APK of another edition: the same code as ReadingBuddy, but with the identifier
/// and name of that edition (Resources/Editions/*.json). The running app picks its edition from
/// its identifier, so the APK reads that edition's catalog and installs next to the store app. It
/// cannot be mistaken for a store build, and a store build can never show test books.
///
/// An edition with its own icon files in Assets/_Story/Editions/Icons/ (&lt;id&gt;_background,
/// _foreground, _round and _legacy .png) gets them as the app icon; without them the APK keeps the
/// ReadingBuddy icon.
///
/// Menu: Tools / ReadingBuddy / Build ... (Android APK). The project settings are put back
/// afterwards, also when the build fails. The APK goes to Build_Android/.
///
/// iOS: ExportIOS does the same for an Xcode project (device SDK) in Build/iOS-&lt;fileStem&gt;/.
/// The iOS icon is the edition's single 1024 px file &lt;id&gt;.png (no alpha) on every iOS slot.
/// After putting the settings back it checks them (and ProjectSettings.asset on disk); a
/// difference makes the result FAILED. Only Alex archives, signs and uploads the Xcode project.
/// </summary>
public static class RbTestBuild
{
    private const string TestEditionId = "readingbuddy-test";
    private const string TalkEditionId = "story-comprehension";
    private const string IconsFolder = "Assets/_Story/Editions/Icons/";

    private static readonly PlatformIconKind[] IconKinds =
        { AndroidPlatformIconKind.Adaptive, AndroidPlatformIconKind.Round, AndroidPlatformIconKind.Legacy };

    [MenuItem("Tools/ReadingBuddy/Build TEST app (Android APK)")]
    public static void BuildTestMenu() => BuildMenu(TestEditionId, "ReadingBuddyTest");

    [MenuItem("Tools/ReadingBuddy/Build Story Comprehension (Android APK)")]
    public static void BuildTalkMenu() => BuildMenu(TalkEditionId, "StoryComprehension");

    [MenuItem("Tools/ReadingBuddy/Export Story Comprehension (iOS Xcode project)")]
    public static void ExportTalkIOSMenu()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("Export StoryComprehension", "Leave Play mode first.", "OK");
            return;
        }
        string note = EditorUserBuildSettings.activeBuildTarget == BuildTarget.iOS ? ""
            : "\n\nUnity will switch the project to iOS first. That can take several minutes, " +
              "and the project stays on iOS afterwards.";
        if (!EditorUtility.DisplayDialog("Export StoryComprehension",
                "Export the iOS Xcode project of the edition " + TalkEditionId + "?" + note, "Export", "Cancel"))
            return;
        string result = ExportIOS(TalkEditionId, "StoryComprehension");
        EditorUtility.DisplayDialog("Export StoryComprehension", result, "OK");
    }

    /// <summary>
    /// Batch mode (Editor closed):
    /// Unity -batchmode -quit -buildTarget iOS -projectPath ... -executeMethod RbTestBuild.BatchExportIOSStoryComprehension
    /// Exit code 0 = ALL DONE.
    /// </summary>
    public static void BatchExportIOSStoryComprehension()
    {
        string result = ExportIOS(TalkEditionId, "StoryComprehension");
        Debug.Log("[RB iOS export] " + result);
        EditorApplication.Exit(result.StartsWith("ALL DONE") ? 0 : 1);
    }

    private static void BuildMenu(string editionId, string fileStem)
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("Build " + fileStem, "Leave Play mode first.", "OK");
            return;
        }
        string note = EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android ? ""
            : "\n\nUnity will switch the project to Android first. That can take several minutes, " +
              "and the project stays on Android afterwards.";
        if (!EditorUtility.DisplayDialog("Build " + fileStem,
                "Build the Android APK of the edition " + editionId + "?" + note, "Build", "Cancel"))
            return;
        string result = BuildAndroid(editionId, fileStem);
        EditorUtility.DisplayDialog("Build " + fileStem, result, "OK");
    }

    /// <summary>Builds the test edition's APK. Returns one line that starts with "ALL DONE" or "FAILED".</summary>
    public static string BuildAndroid() => BuildAndroid(TestEditionId, "ReadingBuddyTest");

    private const string PendingKey = "RbTestBuild.pending";   // "<editionId>|<fileStem>[|ios]"
    private const string StatusFile = "Build/_smoke/apk_status.txt";
    private const string IosStatusFile = "Build/_smoke/ios_status.txt";

    /// <summary>
    /// For the editor bridge: remembers a build request, and RunPending builds it once. A bridge
    /// call that waits for a build times out and is sent again; a second RunPending finds nothing.
    /// kind "android" = APK (result in apk_status.txt), "ios" = Xcode project (ios_status.txt).
    /// </summary>
    public static string Schedule(string editionId, string fileStem, string kind = "android")
    {
        bool ios = kind == "ios";
        EditorPrefs.SetString(PendingKey, editionId + "|" + fileStem + (ios ? "|ios" : ""));
        return "scheduled " + fileStem + (ios ? " (iOS)" : "") + ": call RbTestBuild.RunPending(); the result goes to " +
               (ios ? IosStatusFile : StatusFile);
    }

    public static string RunPending()
    {
        string pending = EditorPrefs.GetString(PendingKey, "");
        if (pending.Length == 0) return "nothing pending";
        EditorPrefs.DeleteKey(PendingKey);
        string[] parts = pending.Split('|');
        if (parts.Length > 2 && parts[2] == "ios") return ExportIOS(parts[0], parts[1]); // writes ios_status.txt itself
        string status = Path.Combine(Path.GetDirectoryName(Application.dataPath), StatusFile);
        File.WriteAllText(status, "building " + parts[1] + "\n");
        string result = BuildAndroid(parts[0], parts[1]);
        File.AppendAllText(status, result + "\n");
        return result;
    }

    /// <summary>Builds an edition's APK. Returns one line that starts with "ALL DONE" or "FAILED".</summary>
    public static string BuildAndroid(string editionId, string fileStem)
    {
        Edition test = FindEdition(editionId);
        if (test == null || test.appIdentifiers.Length == 0)
            return "FAILED: no edition file " + editionId + " in Resources/" + Edition.ResourcesFolder;

        string prevId = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);
        string prevName = PlayerSettings.productName;
        bool prevBundle = EditorUserBuildSettings.buildAppBundle;
        string root = Path.GetDirectoryName(Application.dataPath);
        string buildDir = Path.Combine(root, "Build_Android");
        string built = Path.Combine(buildDir, fileStem + ".apk");
        List<Texture2D[]> prevIcons = null;
        try
        {
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, test.appIdentifiers[0]);
            PlayerSettings.productName = test.appName;
            EditorUserBuildSettings.buildAppBundle = false; // an APK to send, not a store bundle
            prevIcons = SetEditionIcons(NamedBuildTarget.Android, editionId);
            Directory.CreateDirectory(buildDir);

            var scenes = new System.Collections.Generic.List<string>();
            foreach (var s in EditorBuildSettings.scenes) if (s.enabled) scenes.Add(s.path);
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes.ToArray(),
                locationPathName = built,
                target = BuildTarget.Android,
                options = BuildOptions.None,
            });
            if (report.summary.result != BuildResult.Succeeded)
                return "FAILED: build result " + report.summary.result + ", " + report.summary.totalErrors + " error(s). See the Console.";
            // Named after the build: every build raises the build number (BuildStamp), and the
            // file name must show the number that is inside the APK.
            string apk = Path.Combine(buildDir, fileStem + "_" + PlayerSettings.bundleVersion + "_" +
                                                PlayerSettings.Android.bundleVersionCode + ".apk");
            if (File.Exists(apk)) File.Delete(apk);
            File.Move(built, apk);
            long mb = new FileInfo(apk).Length / (1024 * 1024);
            return "ALL DONE: " + apk + " (" + mb + " MB), identifier " +
                   test.appIdentifiers[0] + ", catalog " + test.catalogUrl +
                   (prevIcons != null ? ", own icon" : ", ReadingBuddy icon");
        }
        catch (Exception e)
        {
            Debug.LogError("[RB test build] " + e);
            return "FAILED: " + e.Message;
        }
        finally
        {
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, prevId);
            PlayerSettings.productName = prevName;
            EditorUserBuildSettings.buildAppBundle = prevBundle;
            if (prevIcons != null) RestoreIcons(NamedBuildTarget.Android, prevIcons);
            AssetDatabase.SaveAssets();
            Debug.Log("[RB test build] settings put back: " + prevId + " / " + prevName);
        }
    }

    // ------------------------------------------------------------------ iOS

    /// <summary>
    /// Exports the edition's iOS Xcode project (device SDK) to Build/iOS-&lt;fileStem&gt;/. Returns one
    /// line that starts with "ALL DONE" or "FAILED"; the same line goes to Build/_smoke/ios_status.txt.
    /// A real export: BuildStamp raises the build number.
    /// </summary>
    public static string ExportIOS(string editionId, string fileStem)
    {
        string root = Path.GetDirectoryName(Application.dataPath);
        string status = Path.Combine(root, IosStatusFile);
        Directory.CreateDirectory(Path.GetDirectoryName(status));
        File.WriteAllText(status, "exporting " + fileStem + " (iOS)\n");
        string result = ExportIOSChecked(editionId, fileStem, root);
        if (result.StartsWith("FAILED")) Debug.LogError("[RB iOS export] " + result);
        File.AppendAllText(status, result + "\n");
        return result;
    }

    private static string ExportIOSChecked(string editionId, string fileStem, string root)
    {
        Edition edition = FindEdition(editionId);
        if (edition == null || edition.appIdentifiers.Length == 0)
            return "FAILED: no edition file " + editionId + " in Resources/" + Edition.ResourcesFolder;
        if (EditorApplication.isPlaying) return "FAILED: leave Play mode first";
        if (PlayerSettings.iOS.sdkVersion == iOSSdkVersion.SimulatorSDK)
            return "FAILED: the iOS SDK is set to Simulator (a Simulator export did not restore it?)";
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.iOS &&
            !EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.iOS, BuildTarget.iOS))
            return "FAILED: could not switch the active build target to iOS";

        string folder = Path.Combine(root, "Build", "iOS-" + fileStem);
        if (Directory.Exists(folder))
        {   // never delete: the old export goes to Build/_to_delete/
            string aside = Path.Combine(root, "Build", "_to_delete", "iOS-" + fileStem + "-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"));
            Directory.CreateDirectory(Path.GetDirectoryName(aside));
            Directory.Move(folder, aside);
        }

        Identity prev = null;
        string result;
        try
        {
            prev = ApplyEdition(NamedBuildTarget.iOS, edition);
            var scenes = new List<string>();
            foreach (var s in EditorBuildSettings.scenes) if (s.enabled) scenes.Add(s.path);
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes.ToArray(),
                locationPathName = folder,
                target = BuildTarget.iOS,
                options = BuildOptions.None,
            });
            if (report.summary.result != BuildResult.Succeeded)
                result = "FAILED: export result " + report.summary.result + ", " + report.summary.totalErrors + " error(s). See the log.";
            else
                result = "ALL DONE: " + folder + ", " + ExportedBundleIds(folder) + ", name " + edition.appName +
                         ", version " + PlayerSettings.bundleVersion + " (" + PlayerSettings.iOS.buildNumber + ")" +
                         ", catalog " + edition.catalogUrl + (prev.icons != null ? ", own icon" : ", ReadingBuddy icon");
        }
        catch (Exception e)
        {
            Debug.LogError("[RB iOS export] " + e);
            result = "FAILED: " + e.Message;
        }
        finally
        {
            if (prev != null) Restore(prev);
            AssetDatabase.SaveAssets();
            EditorApplication.ExecuteMenuItem("File/Save Project");
        }
        if (prev == null) return result;
        string bad = VerifyRestored(prev, edition.appIdentifiers[0]);
        if (bad.Length > 0) return "FAILED: settings NOT put back (" + bad + "); export: " + result;
        Debug.Log("[RB iOS export] settings put back and checked: " + prev.id + " / " + prev.name);
        return result;
    }

    /// <summary>Unity writes ${PRODUCT_BUNDLE_IDENTIFIER} into Info.plist; the value is in the Xcode project.</summary>
    private static string ExportedBundleIds(string folder)
    {
        string plist = Run("/usr/libexec/PlistBuddy", "-c \"Print CFBundleIdentifier\" \"" + Path.Combine(folder, "Info.plist") + "\"");
        var ids = new List<string>();
        string pbx = Path.Combine(folder, "Unity-iPhone.xcodeproj", "project.pbxproj");
        if (File.Exists(pbx))
            foreach (System.Text.RegularExpressions.Match m in
                     System.Text.RegularExpressions.Regex.Matches(File.ReadAllText(pbx), @"PRODUCT_BUNDLE_IDENTIFIER = ([^;]+);"))
            {
                string v = m.Groups[1].Value.Trim('"');
                if (!v.StartsWith("com.unity3d.") && !ids.Contains(v)) ids.Add(v);
            }
        return "Info.plist CFBundleIdentifier " + plist + ", Xcode PRODUCT_BUNDLE_IDENTIFIER " + string.Join("+", ids);
    }

    private static string Run(string exe, string args)
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo(exe, args)
            { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
            using (var p = System.Diagnostics.Process.Start(psi))
            {
                string o = p.StandardOutput.ReadToEnd().Trim();
                p.WaitForExit(5000);
                return o.Length > 0 ? o : "(empty)";
            }
        }
        catch (Exception e) { return "(" + exe + " failed: " + e.Message + ")"; }
    }

    // ------------------------------------------------------------------ identity (iOS, also used by RbSimSmoke)

    /// <summary>What ApplyEdition changed, so Restore can put it back and VerifyRestored can check it.</summary>
    public class Identity
    {
        public NamedBuildTarget target;
        public string id;
        public string name;
        public List<Texture2D[]> icons;   // null = the icons were not changed
    }

    public static Edition FindEditionById(string id) => FindEdition(id);

    /// <summary>Sets identifier, product name and icons of the target from the edition.</summary>
    public static Identity ApplyEdition(NamedBuildTarget target, Edition edition)
    {
        var prev = new Identity
        {
            target = target,
            id = PlayerSettings.GetApplicationIdentifier(target),
            name = PlayerSettings.productName,
        };
        PlayerSettings.SetApplicationIdentifier(target, edition.appIdentifiers[0]);
        PlayerSettings.productName = edition.appName;
        prev.icons = SetEditionIcons(target, edition.id);
        return prev;
    }

    public static void Restore(Identity prev)
    {
        PlayerSettings.SetApplicationIdentifier(prev.target, prev.id);
        PlayerSettings.productName = prev.name;
        if (prev.icons != null) RestoreIcons(prev.target, prev.icons);
    }

    /// <summary>
    /// "" when identifier, product name and icons equal the remembered values and the saved
    /// ProjectSettings.asset no longer contains the edition's identifier; otherwise what differs.
    /// Call after the project was saved.
    /// </summary>
    public static string VerifyRestored(Identity prev, string editionIdentifier)
    {
        var bad = new List<string>();
        string id = PlayerSettings.GetApplicationIdentifier(prev.target);
        if (id != prev.id) bad.Add("identifier " + id + " instead of " + prev.id);
        if (PlayerSettings.productName != prev.name) bad.Add("product name " + PlayerSettings.productName + " instead of " + prev.name);
        if (prev.icons != null)
        {
            List<Texture2D[]> now = GetIconTextures(prev.target);
            bool same = now.Count == prev.icons.Count;
            for (int i = 0; same && i < now.Count; i++)
            {
                same = now[i].Length == prev.icons[i].Length;
                for (int j = 0; same && j < now[i].Length; j++) same = now[i][j] == prev.icons[i][j];
            }
            if (!same) bad.Add("icons differ");
        }
        string asset = Path.Combine(Path.GetDirectoryName(Application.dataPath), "ProjectSettings", "ProjectSettings.asset");
        string text = File.ReadAllText(asset);
        if (text.Contains(editionIdentifier)) bad.Add("ProjectSettings.asset on disk still contains " + editionIdentifier);
        if (!text.Contains("productName: " + prev.name + "\n")) bad.Add("ProjectSettings.asset on disk has another productName");
        return string.Join("; ", bad);
    }

    // ------------------------------------------------------------------ icons

    private static PlatformIconKind[] KindsFor(NamedBuildTarget target) =>
        target == NamedBuildTarget.Android ? IconKinds : PlayerSettings.GetSupportedIconKinds(target);

    /// <summary>
    /// Puts the edition's icon files on every icon slot of the target. Android: the four files
    /// &lt;id&gt;_background/_foreground/_round/_legacy; iOS: the one 1024 px file &lt;id&gt;.png on every
    /// slot (Unity scales it). Returns the textures that were there (one entry per slot, in
    /// KindsFor order), or null when the edition has no icon files.
    /// </summary>
    private static List<Texture2D[]> SetEditionIcons(NamedBuildTarget target, string editionId)
    {
        PlatformIconKind[] kinds = KindsFor(target);
        var wanted = new Texture2D[kinds.Length][];
        if (target == NamedBuildTarget.Android)
        {
            Texture2D background = LoadIcon(editionId + "_background");
            Texture2D foreground = LoadIcon(editionId + "_foreground");
            Texture2D round = LoadIcon(editionId + "_round");
            Texture2D legacy = LoadIcon(editionId + "_legacy");
            if (background == null || foreground == null || round == null || legacy == null) return null;
            wanted[0] = new[] { background, foreground }; wanted[1] = new[] { round }; wanted[2] = new[] { legacy };
        }
        else
        {
            Texture2D icon = LoadIcon(editionId);
            if (icon == null) return null;
            for (int k = 0; k < kinds.Length; k++) wanted[k] = new[] { icon };
        }
        List<Texture2D[]> prev = GetIconTextures(target);
        for (int k = 0; k < kinds.Length; k++)
        {
            PlatformIcon[] icons = PlayerSettings.GetPlatformIcons(target, kinds[k]);
            foreach (PlatformIcon icon in icons) icon.SetTextures(wanted[k]);
            PlayerSettings.SetPlatformIcons(target, kinds[k], icons);
        }
        return prev;
    }

    private static List<Texture2D[]> GetIconTextures(NamedBuildTarget target)
    {
        var list = new List<Texture2D[]>();
        foreach (PlatformIconKind kind in KindsFor(target))
            foreach (PlatformIcon icon in PlayerSettings.GetPlatformIcons(target, kind))
                list.Add(icon.GetTextures());
        return list;
    }

    private static void RestoreIcons(NamedBuildTarget target, List<Texture2D[]> prev)
    {
        int n = 0;
        foreach (PlatformIconKind kind in KindsFor(target))
        {
            PlatformIcon[] icons = PlayerSettings.GetPlatformIcons(target, kind);
            foreach (PlatformIcon icon in icons) icon.SetTextures(prev[n++]);
            PlayerSettings.SetPlatformIcons(target, kind, icons);
        }
    }

    private static Texture2D LoadIcon(string name) =>
        AssetDatabase.LoadAssetAtPath<Texture2D>(IconsFolder + name + ".png");

    private static Edition FindEdition(string id)
    {
        foreach (TextAsset file in Resources.LoadAll<TextAsset>(Edition.ResourcesFolder))
        {
            Edition e = Edition.Parse(file.text);
            if (e != null && e.id == id) return e;
        }
        return null;
    }
}
#endif
