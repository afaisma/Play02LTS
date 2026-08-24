using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

// ============================================================================================
// App-wide input tuning. One knob today: EventSystem.pixelDragThreshold.
//
// Unity's default is 10 RAW screen pixels. On a tablet that is a fraction of a millimetre of
// finger travel, so a tap that slides even slightly is reclassified as a scroll drag and the
// Button underneath NEVER receives its click — silently, with no visual clue. Everything a
// child taps inside a ScrollRect is affected: Home's doors, the "For grown-ups" footer, the
// Library's covers. It is the second half of the "does not always open the modal" report.
//
// Raising the threshold to about a quarter-inch of real finger travel keeps scrolling
// responsive (a deliberate scroll moves far further than that) while letting slightly-moving
// taps land. Never lowers an existing threshold, so a scene that deliberately set a larger one
// keeps it.
//
// EventSystem is a per-scene object, so this re-applies on every scene load rather than living
// in any one controller.
// ============================================================================================
public static class InputTuning
{
    private const int DefaultDragThreshold = 10;  // Unity's own default, in raw pixels
    private const float DragInches = 0.25f;       // ~0.6 cm of finger travel
    private const int DragFallbackPx = 30;        // when the platform doesn't report Screen.dpi

    private static bool _hooked;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (_hooked) return;
        _hooked = true;
        // A sceneLoaded callback is NOT enough here: with async scene loads the incoming
        // scene's EventSystem may not yet be EventSystem.current when sceneLoaded fires, so
        // the raise landed on nothing (observed: _Home kept the default 10 while _Welcome got
        // the raised value). A persistent one-component enforcer checks each frame instead —
        // one int compare per frame, immune to every activation-order race.
        var go = new GameObject("~InputTuning") { hideFlags = HideFlags.HideAndDontSave };
        Object.DontDestroyOnLoad(go);
        go.AddComponent<Enforcer>();
        Apply(); // the scene that was already loaded when this ran
    }

    private sealed class Enforcer : MonoBehaviour
    {
        private void Update() => Apply();
    }

    /// <summary>
    /// Raise the current EventSystem's drag threshold. Safe to call repeatedly; a no-op when there
    /// is no EventSystem yet (the next scene load re-applies).
    /// </summary>
    public static void Apply()
    {
        var es = EventSystem.current;
        if (es == null) return;
        int threshold = DragThreshold();
        if (es.pixelDragThreshold < threshold) es.pixelDragThreshold = threshold;
    }

    /// <summary>
    /// How far, in raw pixels, a finger may drift before the tap becomes a drag. DPI-scaled so the
    /// distance is the same physical travel on a dense tablet and a cheap phone.
    /// </summary>
    public static int DragThreshold()
    {
        int dpiBased = Screen.dpi > 0f ? (int)(DragInches * Screen.dpi) : DragFallbackPx;
        return Mathf.Max(DefaultDragThreshold, dpiBased);
    }
}
