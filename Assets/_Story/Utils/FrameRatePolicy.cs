using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

// ============================================================================================
// App-wide frame-rate policy.
//
// Nothing in the project used to set Application.targetFrameRate, so both Android and iOS ran
// at Unity's mobile default of 30 fps (vSyncCount is ignored on mobile). Page turns, scroll
// inertia and the tap press/fade all looked chopped at 30 for no reason other than the default.
//
// The policy is two halves:
//
//   target  — Application.targetFrameRate = 60. Set once at startup and re-asserted on every
//             scene load, because a few Unity paths (quality changes, some plugins) reset it.
//
//   throttle — UnityEngine.Rendering.OnDemandRendering.renderFrameInterval. Update() still runs
//             at 60 (so input latency never changes), but only every Nth frame is RENDERED.
//             interval 1 = full 60 fps while anything is moving; interval 2 = an effective
//             30 fps render cost while the app just sits on a page. We never go above 2, so a
//             signal we failed to notice costs at worst the 30 fps we already shipped.
//
// Everything here is additive: no scene, prefab, or ProjectSettings change, and no other script
// needs to call into it. Rollback = delete this file.
// ============================================================================================
public static class FrameRatePolicy
{
    public const int TargetFrameRate = 60;
    public const int IntervalActive = 1;   // render every frame -> 60 fps
    public const int IntervalIdle = 2;     // render every 2nd frame -> effective 30 fps

    // TODO(ProMotion): 120 Hz iPads/iPhones can run at 120, but that needs
    // CADisableMinimumFrameDuration = true in Info.plist, which is a ProjectSettings change and
    // is deliberately out of scope for this batch. When we do it, the device's real ceiling is
    //   (int)Screen.currentResolution.refreshRateRatio.value
    // and the target becomes Mathf.Min(120, that). Until the plist key ships, asking for >60 on
    // iOS is silently clamped to 60 anyway.

    private const float IdleAfterSeconds = 1.0f;   // input keeps us at 60 for this long after the last touch
    private const float SceneSettleSeconds = 1.5f; // nav fades + layout settling after a scene load
    private const float RescanSeconds = 1.0f;      // how often the component caches are refreshed
    private const float ScrollVelocitySqr = 1f;    // |velocity| > 1 px/s counts as still moving

    private static bool _hooked;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (_hooked) return;
        _hooked = true;

        ApplyTargetFrameRate();

        var go = new GameObject("~FrameRatePolicy") { hideFlags = HideFlags.HideAndDontSave };
        UnityEngine.Object.DontDestroyOnLoad(go);
        go.AddComponent<Runner>();
    }

    private static void ApplyTargetFrameRate()
    {
        if (Application.targetFrameRate != TargetFrameRate)
            Application.targetFrameRate = TargetFrameRate;
    }

    /// <summary>
    /// The whole policy, as a pure function of the six signals — no Unity state, so it is
    /// directly unit-testable. Any signal being true means something on screen is moving and we
    /// must render every frame; all-false means the page is static and half the frames can be
    /// skipped. Never returns anything but IntervalActive or IntervalIdle.
    /// </summary>
    public static int DecideInterval(bool inputRecent, bool audioPlaying, bool tweensPlaying,
                                     bool scrollMoving, bool sceneSettling, bool videoPlaying)
    {
        bool active = inputRecent || audioPlaying || tweensPlaying
                      || scrollMoving || sceneSettling || videoPlaying;
        return active ? IntervalActive : IntervalIdle;
    }

    // ----------------------------------------------------------------------------------------
    // The runner: gathers the six signals each Update and applies DecideInterval's answer.
    // It owns no logic of its own beyond "how do I observe this signal".
    // ----------------------------------------------------------------------------------------
    private sealed class Runner : MonoBehaviour
    {
        private float _lastInputTime = float.NegativeInfinity;
        private float _lastSceneLoadTime;
        private float _nextRescanTime;
        private int _appliedInterval = -1;

        // Scene component caches. Refreshed on scene load and once a second afterwards — the
        // hub scenes build their UI in code during Start(), which runs AFTER sceneLoaded, so a
        // load-time scan alone would miss every ScrollRect on _Home and _Library.
        private AudioAndTextPlayer[] _players = Array.Empty<AudioAndTextPlayer>();
        private ScrollRect[] _scrolls = Array.Empty<ScrollRect>();
        private VideoPlayer[] _videos = Array.Empty<VideoPlayer>();

        // Latches: if an optional API ever throws we stop calling it and fall back to "active",
        // i.e. plain 60 fps. A broken dependency must never cost us an exception per frame.
        private bool _tweenApiFailed;
        private bool _renderApiFailed;

        private void Awake()
        {
            _lastSceneLoadTime = Time.unscaledTime;
            Rescan();
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDestroy() => SceneManager.sceneLoaded -= OnSceneLoaded;

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            _lastSceneLoadTime = Time.unscaledTime;
            ApplyTargetFrameRate();
            Rescan();
        }

        private void Update()
        {
            if (Input.touchCount > 0 || Input.GetMouseButton(0) || Input.anyKey)
                _lastInputTime = Time.unscaledTime;

            if (Time.unscaledTime >= _nextRescanTime) Rescan();

            int interval = DecideInterval(
                inputRecent:   Time.unscaledTime - _lastInputTime < IdleAfterSeconds,
                audioPlaying:  AnyNarrationPlaying(),
                tweensPlaying: AnyTweenPlaying(),
                scrollMoving:  AnyScrollMoving(),
                sceneSettling: SceneSettling(),
                videoPlaying:  AnyVideoPlaying());

            ApplyInterval(interval);
            FpsProbe.Sample(interval);
        }

        // ---- signals ----

        private bool AnyNarrationPlaying()
        {
            for (int i = 0; i < _players.Length; i++)
            {
                var p = _players[i];
                if (p != null && p.IsPlaying) return true;
            }
            return false;
        }

        private bool AnyTweenPlaying()
        {
            if (_tweenApiFailed) return true; // degrade to 60 fps, never to an exception
            try
            {
                return DG.Tweening.DOTween.TotalPlayingTweens() > 0;
            }
            catch (Exception e)
            {
                _tweenApiFailed = true;
                Debug.LogWarning($"[FPS] DOTween tween count unavailable, pinning 60 fps: {e.Message}");
                return true;
            }
        }

        private bool AnyScrollMoving()
        {
            // Velocity, not "is dragging": this is what keeps the frame rate up through the
            // inertia flick after the finger has already left the screen.
            for (int i = 0; i < _scrolls.Length; i++)
            {
                var sr = _scrolls[i];
                if (sr != null && sr.velocity.sqrMagnitude > ScrollVelocitySqr) return true;
            }
            return false;
        }

        private bool SceneSettling()
        {
            // TapFeedback.Armed spans "the child tapped a card" through "the next scene loaded",
            // which is exactly the window a scene load occupies; the timer then covers the fades
            // and layout settling on the far side of it.
            return TapFeedback.Armed
                   || Time.unscaledTime - _lastSceneLoadTime < SceneSettleSeconds;
        }

        private bool AnyVideoPlaying()
        {
            for (int i = 0; i < _videos.Length; i++)
            {
                var v = _videos[i];
                if (v != null && v.isPlaying) return true;
            }
            return false;
        }

        // ---- plumbing ----

        private void Rescan()
        {
            _nextRescanTime = Time.unscaledTime + RescanSeconds;
            _players = FindObjectsByType<AudioAndTextPlayer>(FindObjectsSortMode.None);
            _scrolls = FindObjectsByType<ScrollRect>(FindObjectsSortMode.None);
            _videos = FindObjectsByType<VideoPlayer>(FindObjectsSortMode.None);
        }

        private void ApplyInterval(int interval)
        {
            if (_renderApiFailed || interval == _appliedInterval) return;
            try
            {
                OnDemandRendering.renderFrameInterval = interval;
            }
            catch (Exception e)
            {
                _renderApiFailed = true;
                Debug.LogWarning($"[FPS] OnDemandRendering unavailable, staying at 60 fps: {e.Message}");
                return;
            }
            _appliedInterval = interval;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[FPS] policy: target={TargetFrameRate} interval={interval}");
#endif
        }
    }

    // ----------------------------------------------------------------------------------------
    // Dev-build FPS probe. Gated exactly like the [NAV] timing line in Navigation.cs, so it
    // compiles away entirely in a release build — Sample() becomes an empty static call that the
    // IL stripper removes.
    // ----------------------------------------------------------------------------------------
    private static class FpsProbe
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private const float ReportSeconds = 5f;
        private const int Capacity = 1024; // 5s at 60 fps is ~300; the cap only guards a runaway

        private static readonly float[] _samples = new float[Capacity];
        private static int _count;
        private static int _updates;
        private static int _rendered;
        private static float _windowStart = -1f;
#endif

        public static void Sample(int interval)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_windowStart < 0f) _windowStart = Time.unscaledTime;

            _updates++;
            if (WillRender()) _rendered++;
            if (_count < Capacity) _samples[_count++] = Time.unscaledDeltaTime;

            if (Time.unscaledTime - _windowStart < ReportSeconds) return;

            var sorted = new float[_count];
            Array.Copy(_samples, sorted, _count);
            Array.Sort(sorted);

            float total = 0f;
            for (int i = 0; i < _count; i++) total += sorted[i];
            float avgMs = _count > 0 ? (total / _count) * 1000f : 0f;
            float p95Ms = _count > 0 ? sorted[Mathf.Clamp(Mathf.CeilToInt(_count * 0.95f) - 1, 0, _count - 1)] * 1000f : 0f;

            Debug.Log($"[FPS] {SceneManager.GetActiveScene().name} avg={avgMs:F1} p95={p95Ms:F1} " +
                      $"rendered={_rendered}/{_updates} interval={interval}");

            _count = 0;
            _updates = 0;
            _rendered = 0;
            _windowStart = Time.unscaledTime;
#endif
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static bool WillRender()
        {
            try { return OnDemandRendering.willCurrentFrameRender; }
            catch { return true; }
        }
#endif
    }
}
