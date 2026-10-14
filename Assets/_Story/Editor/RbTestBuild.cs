#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Builds an Android APK of another edition: the same code as ReadingBuddy, but with the identifier
/// and name of that edition (Resources/Editions/*.json). The running app picks its edition from
/// its identifier, so the APK reads that edition's catalog and installs next to the store app. It
/// cannot be mistaken for a store build, and a store build can never show test books.
///
/// Menu: Tools / ReadingBuddy / Build ... (Android APK). The project settings are put back
/// afterwards, also when the build fails. The APK goes to Build_Android/.
/// </summary>
public static class RbTestBuild
{
    private const string TestEditionId = "readingbuddy-test";
    private const string TalkEditionId = "readingbuddy-talk";

    [MenuItem("Tools/ReadingBuddy/Build TEST app (Android APK)")]
    public static void BuildTestMenu() => BuildMenu(TestEditionId, "ReadingBuddyTest");

    [MenuItem("Tools/ReadingBuddy/Build ReadingBuddy Talk (Android APK)")]
    public static void BuildTalkMenu() => BuildMenu(TalkEditionId, "ReadingBuddyTalk");

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
        try
        {
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, test.appIdentifiers[0]);
            PlayerSettings.productName = test.appName;
            EditorUserBuildSettings.buildAppBundle = false; // an APK to send, not a store bundle
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
                   test.appIdentifiers[0] + ", catalog " + test.catalogUrl;
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
            AssetDatabase.SaveAssets();
            Debug.Log("[RB test build] settings put back: " + prevId + " / " + prevName);
        }
    }

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
