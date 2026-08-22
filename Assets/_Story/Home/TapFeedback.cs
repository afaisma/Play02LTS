using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// ============================================================================================
// Shared tap feedback for the code-built hub scenes (_Home, _LearnToRead).
//
// The problem it solves: a card tap used to do nothing visible until the destination scene
// finished loading — ~0.5s in the editor, ~2s on an Android tablet — so the tap read as dead
// and children re-tapped. The cost is the destination scene itself, so the fix is not to make
// the load cheaper but to ACKNOWLEDGE the tap before it starts:
//
//   pointer down -> the card scales to 0.96 immediately (PressScale, no coroutine, same frame)
//   click        -> TapThenGo: hold the press, wait one beat so it actually renders, fade a
//                   full-screen cover in over ~0.1s, THEN navigate.
//
// Everything after the click runs on a hidden DontDestroyOnLoad runner rather than on the
// calling controller, so a card (or its whole controller) being destroyed mid-transition can
// never strand the navigation.
//
// Once a navigation is armed, every card in the scene stops responding: the cover blocks
// raycasts and the _armed latch rejects further TapThenGo calls until the next scene loads.
// ============================================================================================
public static class TapFeedback
{
    public const float PressScale = 0.96f;   // pressed-state shrink
    private const float Beat = 0.06f;        // realtime hold so the press paints before the load
    private const float FadeSeconds = 0.10f; // cover fade-in
    private const float DisarmAfter = 6f;    // watchdog: a nav that no-ops must not wedge the scene

    private static bool _armed;
    private static bool _hooked;
    private static int _generation; // identifies the live transition, so a stale watchdog can't disarm a new one

    /// <summary>True once a tap has committed to a navigation; all further taps are ignored.</summary>
    public static bool Armed => _armed;

    /// <summary>
    /// Give a tappable card instant pressed-state feedback. Safe to call on any card built in
    /// code; the pressed scale is applied on pointer-down and released on pointer-up, both in
    /// the same frame as the event.
    /// </summary>
    public static void AddPressFeedback(GameObject card)
    {
        if (card != null && card.GetComponent<TapPressScale>() == null)
            card.AddComponent<TapPressScale>();
    }

    /// <summary>
    /// One-shot "press -> beat -> cover -> navigate". Returns immediately; the navigation runs
    /// ~0.16s later, by which time the press and the cover have rendered. The first call arms
    /// the scene — later calls (a second tap during the beat, a different card) are dropped.
    /// </summary>
    public static void TapThenGo(Transform card, System.Action nav)
    {
        if (nav == null || _armed) return;
        _armed = true;
        _generation++;
        HookSceneReset();
        Navigation.MarkTap();
        TapRunner.Run(Sequence(card, nav, _generation));
    }

    private static IEnumerator Sequence(Transform card, System.Action nav, int generation)
    {
        // Hold the pressed state through the transition. The pointer-up handler already
        // restored the scale, but no frame is presented between it and the click, so the
        // card simply stays pressed.
        if (card != null) card.localScale = Vector3.one * PressScale;

        float t = 0f;
        while (t < Beat) { t += Time.unscaledDeltaTime; yield return null; }

        CanvasGroup cover = BuildCover();
        t = 0f;
        while (t < FadeSeconds)
        {
            t += Time.unscaledDeltaTime;
            if (cover != null) cover.alpha = Mathf.Clamp01(t / FadeSeconds);
            yield return null;
        }
        if (cover != null) cover.alpha = 1f;

        nav();

        // The scene switch normally clears _armed (and discards the cover) within a frame or
        // two. This only matters if the navigation fail-softed into a no-op — without it the
        // hub would stay permanently untappable. The generation check keeps this stale
        // watchdog from disarming a transition that started after ours.
        yield return new WaitForSecondsRealtime(DisarmAfter);
        if (generation == _generation) _armed = false;
    }

    // Full-screen page-coloured cover on its own overlay canvas, above everything the hub
    // draws (HomeController's canvas is sortingOrder 100). A root object in the active scene,
    // so the scene switch destroys it for us.
    private static CanvasGroup BuildCover()
    {
        var go = new GameObject("NavCover", typeof(Canvas), typeof(GraphicRaycaster), typeof(CanvasGroup));
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32760;

        var group = go.GetComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = true; // swallows re-taps for the rest of the transition

        var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fill.transform.SetParent(go.transform, false);
        var rt = fill.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        fill.GetComponent<Image>().color = UiTheme.Bg;
        return group;
    }

    // A fresh scene means fresh cards: re-arm. Subscribed once for the app's lifetime.
    private static void HookSceneReset()
    {
        if (_hooked) return;
        _hooked = true;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += (scene, mode) => _armed = false;
    }

    // Hidden, persistent coroutine host. Persistent on purpose: the transition must survive the
    // destruction of the card, the controller, and the outgoing scene.
    private class TapRunner : MonoBehaviour
    {
        private static TapRunner _instance;

        public static void Run(IEnumerator routine)
        {
            if (_instance == null)
            {
                var go = new GameObject("~TapFeedback") { hideFlags = HideFlags.HideAndDontSave };
                Object.DontDestroyOnLoad(go);
                _instance = go.AddComponent<TapRunner>();
            }
            _instance.StartCoroutine(routine);
        }
    }
}

/// <summary>
/// Instant pressed-state scale, added by TapFeedback.AddPressFeedback. Kept as a tiny
/// pointer handler rather than an EventTrigger so it costs one component and no allocation.
/// </summary>
public class TapPressScale : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    private Vector3 _rest = Vector3.one;

    private void Awake() => _rest = transform.localScale;

    public void OnPointerDown(PointerEventData eventData)
    {
        if (TapFeedback.Armed) return;
        transform.localScale = _rest * TapFeedback.PressScale;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (TapFeedback.Armed) return;
        transform.localScale = _rest;
    }
}
