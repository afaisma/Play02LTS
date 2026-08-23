using UnityEngine;

// ============================================================================================
// Runtime face of the build stamp written by Editor/BuildStamp.cs (Assets/Resources/
// build_info.json). Answers "which build is this?" in one line — shown in the For-grown-ups
// footer and logged once at startup — so a tester never again has to guess whether an APK
// actually changed. In the editor, or in a player built before stamping existed, every field
// falls back to something honest instead of throwing.
// ============================================================================================
public static class BuildInfo
{
    [System.Serializable]
    private class Payload
    {
        public string version;  // PlayerSettings.bundleVersion at build time, e.g. "1.3.0"
        public int build;       // Android bundleVersionCode the build shipped with
        public string builtAt;  // "yyyy-MM-dd HH:mm" (UTC)
        public string git;      // short commit hash, "*" suffix = uncommitted changes; "" unknown
    }

    private static Payload _p;
    private static bool _loaded;
    private static bool _logged;

    private static Payload Load()
    {
        if (_loaded) return _p;
        _loaded = true;
        var ta = Resources.Load<TextAsset>("build_info");
        if (ta != null && !string.IsNullOrEmpty(ta.text))
        {
            try { _p = JsonUtility.FromJson<Payload>(ta.text); }
            catch { _p = null; } // malformed stamp -> fall back, never crash the Home screen
        }
        return _p;
    }

    /// <summary>One line for humans: "v1.3.0 · build 3 · 2026-08-23 14:02 UTC · abc1234".</summary>
    public static string Line()
    {
        var p = Load();
        if (p == null || string.IsNullOrEmpty(p.version))
            return "v" + Application.version + (Application.isEditor ? " · editor" : " · unstamped build");
        string s = "v" + p.version + " · build " + p.build + " · " + p.builtAt + " UTC";
        if (!string.IsNullOrEmpty(p.git)) s += " · " + p.git;
        return s;
    }

    /// <summary>Log the stamp once per session, greppable as [BUILD].</summary>
    public static void LogOnce()
    {
        if (_logged) return;
        _logged = true;
        Debug.Log("[BUILD] " + Line());
    }
}
