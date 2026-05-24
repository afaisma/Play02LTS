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
    public const string Map        = "_Map";
    public const string Message    = "_Message";

    public static void GoToStart()     => SceneManager.LoadScene(StartScene);
    public static void GoToLibrary()   => SceneManager.LoadScene(Library);
    public static void GoToStory()     => SceneManager.LoadScene(Story);
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
}
