using UnityEngine;

// ============================================================================================
// Desktop window policy (Windows / macOS / Linux players only — compiled out elsewhere).
//
// ReadingBuddy's UI is authored portrait (canvases reference 1080x1920, match 0.5). On a
// desktop the player would otherwise open as a landscape fullscreen window and smear a phone
// layout across a monitor. This keeps the window PORTRAIT at 9:16:
//   • on start: a comfortable window that fits the monitor (height ≈ 90% of the display,
//     capped at 1280), centred by the OS;
//   • on user resize: after the drag settles (Debounce), snap back to 9:16, driving from
//     whichever dimension the user changed most — so dragging the bottom edge scales the
//     window, dragging the side edge scales it too, and it never ends up landscape.
// The math lives in FitPortrait() (pure, unit-tested); the MonoBehaviour only observes
// Screen.width/height and applies the answer via Screen.SetResolution in windowed mode.
// ============================================================================================
public static class WindowAspectLock
{
    public const float Aspect = 9f / 16f;      // width / height
    public const int MinHeight = 640;
    public const int MaxDefaultHeight = 1280;
    public const float DisplayFraction = 0.90f; // start-up window height vs. display height
    public const float Tolerance = 0.015f;      // aspect drift that triggers a snap (1.5%)
    public const float Debounce = 0.25f;        // seconds of no size change before snapping

    /// <summary>Is (w,h) already portrait 9:16 within tolerance?</summary>
    public static bool IsPortraitFit(int w, int h)
    {
        if (w <= 0 || h <= 0) return false;
        return Mathf.Abs((float)w / h - Aspect) <= Tolerance * Aspect;
    }

    /// <summary>
    /// Given the window's current size and the size it had before the user touched it, return a
    /// 9:16 size. Drives from the dimension that changed most (so both edge drags feel natural);
    /// clamps height to [MinHeight, maxHeight]. Always returns a valid portrait size.
    /// </summary>
    public static Vector2Int FitPortrait(int w, int h, int lastW, int lastH, int maxHeight)
    {
        int dw = Mathf.Abs(w - lastW), dh = Mathf.Abs(h - lastH);
        int height = dw > dh ? Mathf.RoundToInt(w / Aspect) : h;
        int cap = Mathf.Max(MinHeight, maxHeight);
        height = Mathf.Clamp(height, MinHeight, cap);
        int width = Mathf.RoundToInt(height * Aspect);
        return new Vector2Int(width, height);
    }

    /// <summary>Start-up size: DisplayFraction of the display height, capped, and 9:16.</summary>
    public static Vector2Int InitialSize(int displayHeight)
    {
        int height = Mathf.RoundToInt(displayHeight * DisplayFraction);
        height = Mathf.Clamp(height, MinHeight, MaxDefaultHeight);
        return new Vector2Int(Mathf.RoundToInt(height * Aspect), height);
    }

#if UNITY_STANDALONE && !UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (Object.FindFirstObjectByType<Runner>() != null) return;
        var go = new GameObject("WindowAspectLock");
        Object.DontDestroyOnLoad(go);
        go.AddComponent<Runner>();
    }

    private sealed class Runner : MonoBehaviour
    {
        private int _lastW, _lastH;
        private float _stableSince;
        private bool _dirty;

        private void Start()
        {
            var size = InitialSize(Screen.currentResolution.height);
            Screen.SetResolution(size.x, size.y, FullScreenMode.Windowed);
            _lastW = size.x; _lastH = size.y;
        }

        private void Update()
        {
            int w = Screen.width, h = Screen.height;
            if (w != _lastW || h != _lastH)
            {
                // Size is changing (a drag in progress): remember the PRE-drag size once, then
                // track the live size and wait for it to settle.
                if (!_dirty) { _pendingFromW = _lastW; _pendingFromH = _lastH; }
                _dirty = true;
                _stableSince = Time.unscaledTime;
                _lastW = w; _lastH = h;
                return;
            }
            if (!_dirty || Time.unscaledTime - _stableSince < Debounce) return;
            _dirty = false;
            if (IsPortraitFit(w, h)) return;

            int maxH = Mathf.RoundToInt(Screen.currentResolution.height * 0.97f);
            var fit = FitPortrait(w, h, _pendingFromW, _pendingFromH, maxH);
            if (fit.x == w && fit.y == h) return;
            Screen.SetResolution(fit.x, fit.y, FullScreenMode.Windowed);
            _lastW = fit.x; _lastH = fit.y;
        }

        // The size the window had BEFORE the current drag began (so "which dimension changed
        // most" is measured against the pre-drag size, not the previous frame).
        private int _pendingFromW, _pendingFromH;
    }
#endif
}
