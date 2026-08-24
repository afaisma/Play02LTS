using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Single source of truth for cross-scene navigation. Replaces the
/// duplicate Settings() / Map() / Bookstore() / Library() / Parents()
/// methods that previously lived independently in PRLibrary,
/// PRBookstore, and MapManager. Also centralizes scene names as
/// constants so future renames (or deprecations like the eventual
/// _Map removal) are a one-line edit here rather than a scattered
/// grep across the codebase.
///
/// External callers (PRLibrary.Settings(), PRBookstore.Map(), etc.)
/// keep their public method shells so scene-side button onClick
/// wirings don't break — those wrapper methods now just delegate
/// here.
///
/// The four EXPENSIVE destinations (_Library, _LearnToRead, _Story, _Home) load
/// asynchronously — a synchronous LoadScene froze the frame for ~0.5s in the
/// editor and ~2s on an Android tablet, which read as a dead tap. Every caller
/// of these already sets its state (Globals.g_libraryFilter, g_scriptName,
/// g_prbook, ...) BEFORE navigating, so deferred activation is safe.
///
/// _Home was initially left synchronous on the theory that "back to home must
/// feel instant" — but Home is not a cheap scene (catalog scan, door config,
/// cover downloads), so the sync load simply froze the tap for the same ~2s on
/// device. It now loads async like the rest, and every home-return control pairs
/// it with TapFeedback so the tap is acknowledged before the load starts. The
/// remaining destinations (Bookstore, Settings, Parents, Map, Message) are thin
/// and stay synchronous.
/// </summary>
public static class Navigation
{
    // Production scene names. Single point of edit if a scene is ever renamed.
    public const string StartScene = "_StartScene";
    public const string Library    = "_Library";
    public const string Story      = "_Story";
    public const string Bookstore  = "_Bookstore";
    public const string Settings   = "_Settings";
    public const string Parents    = "_Parents";
    public const string Map         = "_Map";
    public const string Message     = "_Message";
    public const string Home        = "_Home";
    public const string LearnToRead = "_LearnToRead";

    public static void GoToStart()       => SceneManager.LoadScene(StartScene);
    public static void GoToHome()        => GoToSceneAsync(Home);
    public static void GoToLearnToRead() => GoToSceneAsync(LearnToRead);
    public static void GoToLibrary()   => GoToSceneAsync(Library);
    public static void GoToStory()     => GoToSceneAsync(Story);
    public static void GoToBookstore() => SceneManager.LoadScene(Bookstore);
    public static void GoToSettings()  => SceneManager.LoadScene(Settings);
    public static void GoToParents()   => SceneManager.LoadScene(Parents);
    public static void GoToMap()       => SceneManager.LoadScene(Map);
    public static void GoToMessage()   => SceneManager.LoadScene(Message);

    /// <summary>
    /// Generic variant for scripts that compute scene names at runtime
    /// (StartScene.startSceneName, StartSceneCombo's PlayerPrefs-driven
    /// override, etc.). Prefer the named methods above when the
    /// destination is known statically — they catch typos at compile
    /// time.
    /// </summary>
    public static void GoToScene(string sceneName) => SceneManager.LoadScene(sceneName);

    /// <summary>
    /// Async scene load. allowSceneActivation is left true, so this behaves exactly
    /// like LoadScene from the caller's point of view (fire and forget) except the
    /// work is spread across frames instead of stalling one. LoadSceneAsync does NOT
    /// need a coroutine to drive it — the returned AsyncOperation is returned only for
    /// callers that want to watch progress.
    /// </summary>
    public static AsyncOperation GoToSceneAsync(string sceneName)
    {
        BeginNavTiming(sceneName);
        return SceneManager.LoadSceneAsync(sceneName);
    }

    /// <summary>
    /// Stamps "the child touched the screen" for the QA timing line below. Called by
    /// TapFeedback before it plays the press beat, so the reported number covers the
    /// whole perceived wait (press + fade + load), not just the load. No-op outside
    /// the editor and development builds.
    /// </summary>
    public static void MarkTap()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        _tapRealtime = Time.realtimeSinceStartup;
        _tapPending = true;
#endif
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private static float _tapRealtime;
    private static bool _tapPending;
    private static string _pendingScene;

    private static void BeginNavTiming(string sceneName)
    {
        // Navigation that wasn't preceded by a tap (auto-advance, alert retry, ...)
        // still gets timed — from the moment the load was requested.
        if (!_tapPending) _tapRealtime = Time.realtimeSinceStartup;
        _tapPending = false;
        _pendingScene = sceneName;
        SceneManager.sceneLoaded -= OnNavSceneLoaded;
        SceneManager.sceneLoaded += OnNavSceneLoaded;
    }

    private static void OnNavSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        SceneManager.sceneLoaded -= OnNavSceneLoaded;
        if (_pendingScene == null) return;

        string sceneName = _pendingScene;
        float tapAt = _tapRealtime;
        _pendingScene = null;
        // sceneLoaded fires DURING activation; one more frame lands us on the first
        // frame the new scene actually presents, which is when the tap stops feeling dead.
        NavTimingRunner.RunNextFrame(() =>
        {
            int ms = Mathf.RoundToInt((Time.realtimeSinceStartup - tapAt) * 1000f);
            Debug.Log($"[NAV] {sceneName} interactive in {ms} ms");
        });
    }

    // Throwaway host for the single-frame wait. Lives in the freshly loaded scene and
    // deletes itself after logging.
    private class NavTimingRunner : MonoBehaviour
    {
        public static void RunNextFrame(System.Action action)
        {
            var go = new GameObject("~NavTiming") { hideFlags = HideFlags.HideAndDontSave };
            go.AddComponent<NavTimingRunner>().StartCoroutine(WaitOneFrame(go, action));
        }

        private static IEnumerator WaitOneFrame(GameObject go, System.Action action)
        {
            yield return null;
            action();
            Destroy(go);
        }
    }
#else
    private static void BeginNavTiming(string sceneName) { }
#endif
}
