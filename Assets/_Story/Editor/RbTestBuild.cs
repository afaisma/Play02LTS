#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Builds the TEST app for Android: the same code as ReadingBuddy, but with the identifier and
/// name of the test edition (Resources/Editions/readingbuddy-test.json). The running app picks
/// its edition from its identifier, so this APK reads the test catalog (stories.test.json) and
/// installs next to the store app. It cannot be mistaken for a store build, and a store build
/// can never show test books.
///
/// Menu: Tools / ReadingBuddy / Build TEST app (Android APK). The project settings are put back
/// afterwards, also when the build fails. The APK goes to Build_Android/.
/// </summary>
public static class RbTestBuild
{
    private const string TestEditionId = "readingbuddy-test";

    [MenuItem("Tools/ReadingBuddy/Build TEST app (Android APK)")]
    public static void BuildAndroidMenu()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("Build TEST app", "Leave Play mode first.", "OK");
            return;
        }
        string note = EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android ? ""
            : "\n\nUnity will switch the project to Android first. That can take several minutes, " +
              "and the project stays on Android afterwards.";
        if (!EditorUtility.DisplayDialog("Build TEST app",
                "Build the Android APK of the test edition (ReadingBuddy Test)?" + note, "Build", "Cancel"))
            return;
        string result = BuildAndroid();
        EditorUtility.DisplayDialog("Build TEST app", result, "OK");
    }

    /// <summary>Builds the APK. Returns one line that starts with "ALL DONE" or "FAILED".</summary>
    public static string BuildAndroid()
    {
        Edition test = FindEdition(TestEditionId);
        if (test == null || test.appIdentifiers.Length == 0)
            return "FAILED: no edition file " + TestEditionId + " in Resources/" + Edition.ResourcesFolder;

        string prevId = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);
        string prevName = PlayerSettings.productName;
        bool prevBundle = EditorUserBuildSettings.buildAppBundle;
        string root = Path.GetDirectoryName(Application.dataPath);
        string apk = Path.Combine("Build_Android",
            "ReadingBuddyTest_" + PlayerSettings.bundleVersion + "_" + PlayerSettings.Android.bundleVersionCode + ".apk");
        try
        {
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, test.appIdentifiers[0]);
            PlayerSettings.productName = test.appName;
            EditorUserBuildSettings.buildAppBundle = false; // an APK to send, not a store bundle
            Directory.CreateDirectory(Path.Combine(root, "Build_Android"));

            var scenes = new System.Collections.Generic.List<string>();
            foreach (var s in EditorBuildSettings.scenes) if (s.enabled) scenes.Add(s.path);
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes.ToArray(),
                locationPathName = apk,
                target = BuildTarget.Android,
                options = BuildOptions.None,
            });
            if (report.summary.result != BuildResult.Succeeded)
                return "FAILED: build result " + report.summary.result + ", " + report.summary.totalErrors + " error(s). See the Console.";
            long mb = new FileInfo(Path.Combine(root, apk)).Length / (1024 * 1024);
            return "ALL DONE: " + Path.Combine(root, apk) + " (" + mb + " MB), identifier " +
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
