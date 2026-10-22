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

    private const string PendingKey = "RbTestBuild.pending";   // "<editionId>|<fileStem>"
    private const string StatusFile = "Build/_smoke/apk_status.txt";

    /// <summary>
    /// For the editor bridge: remembers a build request, and RunPending builds it once. A bridge
    /// call that waits for a build times out and is sent again; a second RunPending finds nothing.
    /// </summary>
    public static string Schedule(string editionId, string fileStem)
    {
        EditorPrefs.SetString(PendingKey, editionId + "|" + fileStem);
        return "scheduled " + fileStem + ": call RbTestBuild.RunPending(); the result goes to " + StatusFile;
    }

    public static string RunPending()
    {
        string pending = EditorPrefs.GetString(PendingKey, "");
        if (pending.Length == 0) return "nothing pending";
        EditorPrefs.DeleteKey(PendingKey);
        string[] parts = pending.Split('|');
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
            prevIcons = SetEditionIcons(editionId);
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
            if (prevIcons != null) RestoreIcons(prevIcons);
            AssetDatabase.SaveAssets();
            Debug.Log("[RB test build] settings put back: " + prevId + " / " + prevName);
        }
    }

    /// <summary>
    /// Puts the edition's icon files on every Android icon slot. Returns the textures that were
    /// there (one entry per slot, kinds in IconKinds order), or null when the edition has no icon files.
    /// </summary>
    private static List<Texture2D[]> SetEditionIcons(string editionId)
    {
        Texture2D background = LoadIcon(editionId + "_background");
        Texture2D foreground = LoadIcon(editionId + "_foreground");
        Texture2D round = LoadIcon(editionId + "_round");
        Texture2D legacy = LoadIcon(editionId + "_legacy");
        if (background == null || foreground == null || round == null || legacy == null) return null;
        Texture2D[][] wanted = { new[] { background, foreground }, new[] { round }, new[] { legacy } };
        var prev = new List<Texture2D[]>();
        for (int k = 0; k < IconKinds.Length; k++)
        {
            PlatformIcon[] icons = PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android, IconKinds[k]);
            foreach (PlatformIcon icon in icons)
            {
                prev.Add(icon.GetTextures());
                icon.SetTextures(wanted[k]);
            }
            PlayerSettings.SetPlatformIcons(NamedBuildTarget.Android, IconKinds[k], icons);
        }
        return prev;
    }

    private static void RestoreIcons(List<Texture2D[]> prev)
    {
        int n = 0;
        foreach (PlatformIconKind kind in IconKinds)
        {
            PlatformIcon[] icons = PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android, kind);
            foreach (PlatformIcon icon in icons) icon.SetTextures(prev[n++]);
            PlayerSettings.SetPlatformIcons(NamedBuildTarget.Android, kind, icons);
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
