using UnityEngine;
using UnityEngine.UI;

// ============================================================================================
// One place that turns Screen.safeArea into CANVAS units and keeps a RectTransform clear of the
// notch / Dynamic Island / home indicator.
//
// Unity reports Screen.safeArea in SCREEN pixels with the origin at the BOTTOM-LEFT, so
//     bottom inset = safeArea.yMin          top inset = Screen.height - safeArea.yMax
// The smoke header (Recordings/simcaps/sim/*_log.txt) prints the raw rect: "safe 0,102,1320,2580"
// on the 1320x2868 iPhone 17 Pro Max is a 102 px home-indicator inset and a 186 px island inset,
// while the iPads print "safe 0,50,W,H-50" — a 50 px home indicator and NO top inset, because the
// app hides the status bar. Reading that y as a TOP inset is the easy mistake this class exists
// to stop repeating.
//
// Every canvas in the app is ScaleWithScreenSize 1080x1920 match 0.5, so a pixel inset has to be
// divided by the scaler's factor before it can be added to an anchoredPosition, an offset, or a
// layout padding. ScaleFactor() is Unity's own formula, kept pure so it can be unit-tested.
//
// Applying is idempotent: every Apply* installs a Binding on the target that remembers the
// UNTOUCHED baseline and re-applies only when the screen size or the safe area actually changes
// (rotation, iPad split view). Calling it twice — or from two different wiring points — can
// therefore never stack two insets.
// ============================================================================================
public static class SafeAreaInsets
{
    /// <summary>
    /// Extra breathing room added BELOW the top inset, so a title never sits exactly on the
    /// island's edge. Deliberately only added when there IS a top inset: a device without one
    /// (iPhone SE, both iPads) must render pixel-identically to before.
    /// </summary>
    public const float TopBreathingMargin = 16f;

    /// <summary>The canvas reference resolution every scene in the app uses.</summary>
    public static readonly Vector2 Reference = new Vector2(1080f, 1920f);

    /// <summary>Insets on the four edges, in whatever unit the producer used.</summary>
    public readonly struct Edges
    {
        public readonly float Left, Right, Bottom, Top;
        public Edges(float left, float right, float bottom, float top)
        { Left = left; Right = right; Bottom = bottom; Top = top; }

        public static readonly Edges Zero = new Edges(0f, 0f, 0f, 0f);
        public bool IsZero => Left == 0f && Right == 0f && Bottom == 0f && Top == 0f;

        /// <summary>The same insets divided by a canvas scale factor (pixels -> canvas units).</summary>
        public Edges Divided(float scaleFactor)
        {
            if (scaleFactor <= 0f) return Zero;
            return new Edges(Left / scaleFactor, Right / scaleFactor, Bottom / scaleFactor, Top / scaleFactor);
        }

        /// <summary>Top inset plus the breathing margin — zero when there is no top inset.</summary>
        public float TopWithMargin => Top > 0f ? Top + TopBreathingMargin : 0f;
    }

    // ---------------------------------------------------------------- pure math

    /// <summary>
    /// Pixel insets from a safe-area rect on a screenW x screenH screen. Unity's origin is
    /// bottom-left, so safeArea.yMin is the BOTTOM inset. Negative results (a safe area larger
    /// than the screen, which some editor simulators report) clamp to zero, and a degenerate
    /// screen yields no insets at all.
    /// </summary>
    public static Edges Pixels(Rect safeArea, float screenW, float screenH)
    {
        if (screenW <= 0f || screenH <= 0f) return Edges.Zero;
        if (safeArea.width <= 0f || safeArea.height <= 0f) return Edges.Zero;
        return new Edges(
            Mathf.Max(0f, safeArea.xMin),
            Mathf.Max(0f, screenW - safeArea.xMax),
            Mathf.Max(0f, safeArea.yMin),
            Mathf.Max(0f, screenH - safeArea.yMax));
    }

    /// <summary>
    /// CanvasScaler.ScaleMode.ScaleWithScreenSize's scale factor: the geometric blend of the two
    /// axis ratios weighted by matchWidthOrHeight (0 = width, 1 = height). Canvas units are
    /// screen pixels divided by this.
    /// </summary>
    public static float ScaleFactor(float screenW, float screenH, Vector2 reference, float match)
    {
        if (screenW <= 0f || screenH <= 0f || reference.x <= 0f || reference.y <= 0f) return 1f;
        float logWidth = Mathf.Log(screenW / reference.x, 2f);
        float logHeight = Mathf.Log(screenH / reference.y, 2f);
        return Mathf.Pow(2f, Mathf.Lerp(logWidth, logHeight, Mathf.Clamp01(match)));
    }

    // ---------------------------------------------------------------- runtime queries

    /// <summary>The live safe-area insets in the canvas units of <paramref name="canvas"/>.</summary>
    public static Edges ForCanvas(Canvas canvas)
    {
        return Pixels(Screen.safeArea, Screen.width, Screen.height).Divided(CanvasScale(canvas));
    }

    /// <summary>The live safe-area insets in the canvas units of the canvas this rect lives on.</summary>
    public static Edges ForRect(RectTransform rect)
    {
        return ForCanvas(rect == null ? null : rect.GetComponentInParent<Canvas>());
    }

    // Canvas.scaleFactor is the authority once the scaler has run; before that (a canvas built
    // this same frame) it can still read 1, so fall back to recomputing from the scaler itself.
    private static float CanvasScale(Canvas canvas)
    {
        if (canvas == null) return 1f;
        var root = canvas.rootCanvas != null ? canvas.rootCanvas : canvas;
        var scaler = root.GetComponent<CanvasScaler>();
        if (scaler != null && scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize)
            return ScaleFactor(Screen.width, Screen.height, scaler.referenceResolution, scaler.matchWidthOrHeight);
        return root.scaleFactor > 0f ? root.scaleFactor : 1f;
    }

    // ---------------------------------------------------------------- apply

    /// <summary>
    /// Lift a bottom-anchored rect (a toolbar, a bottom bar) clear of the home-indicator inset.
    /// Shifting BOTH offsets keeps the rect's height, and works for a stretched rect and a
    /// fixed-size one alike (for the latter it is exactly an anchoredPosition shift).
    /// </summary>
    public static void ApplyBottom(RectTransform rect) => Bind(rect, Mode.Bottom, null);

    /// <summary>Push a top-anchored rect (a title) down below the notch, plus the breathing margin.</summary>
    public static void ApplyTop(RectTransform rect) => Bind(rect, Mode.Top, null);

    /// <summary>
    /// Move only a rect's TOP edge down by the same amount ApplyTop moves a title, leaving the
    /// bottom edge where it is. For the panel that sits under a title moved with ApplyTop: it
    /// gives back exactly the room the title took, so the two keep their authored relationship
    /// instead of the title landing on top of the panel's content.
    /// </summary>
    public static void ApplyTopEdge(RectTransform rect) => Bind(rect, Mode.TopEdge, null);

    /// <summary>Inset all four edges of a full-stretch rect (a content root, a backdrop).</summary>
    public static void ApplyPadding(RectTransform rect) => Bind(rect, Mode.Padding, null);

    /// <summary>
    /// Add the top and bottom insets to a layout group's padding — the natural place for the
    /// code-built scenes, whose whole page is one VerticalLayoutGroup.
    /// </summary>
    public static void ApplyLayoutPadding(LayoutGroup group)
    {
        if (group == null) return;
        Bind(group.transform as RectTransform, Mode.LayoutPadding, group);
    }

    private enum Mode { Bottom, Top, TopEdge, Padding, LayoutPadding }

    private static void Bind(RectTransform rect, Mode mode, LayoutGroup group)
    {
        if (rect == null) return;
        var binding = rect.GetComponent<Binding>();
        if (binding == null)
        {
            binding = rect.gameObject.AddComponent<Binding>();
            binding.Capture(rect, mode, group);
        }
        binding.ApplyIfChanged();
    }

    // Remembers the pre-inset baseline and re-applies from it, so the inset is set — never
    // accumulated. One cheap comparison per frame (the same shape as InputTuning's Enforcer)
    // catches rotation and iPad split-view resizes, which no scene callback reports.
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    private sealed class Binding : MonoBehaviour
    {
        private RectTransform _rect;
        private LayoutGroup _group;
        private Mode _mode;
        private Vector2 _baseOffsetMin, _baseOffsetMax;
        private RectOffset _basePadding;

        private bool _applied;
        private int _screenW, _screenH;
        private Rect _safeArea;

        internal void Capture(RectTransform rect, Mode mode, LayoutGroup group)
        {
            _rect = rect; _mode = mode; _group = group;
            _baseOffsetMin = rect.offsetMin; _baseOffsetMax = rect.offsetMax;
            if (group != null)
            {
                var p = group.padding;
                _basePadding = new RectOffset(p.left, p.right, p.top, p.bottom);
            }
        }

        private void LateUpdate() => ApplyIfChanged();

        internal void ApplyIfChanged()
        {
            if (_rect == null) return;
            Rect safe = Screen.safeArea;
            if (_applied && Screen.width == _screenW && Screen.height == _screenH && safe == _safeArea) return;
            _screenW = Screen.width; _screenH = Screen.height; _safeArea = safe; _applied = true;
            Apply(ForRect(_rect));
        }

        private void Apply(Edges e)
        {
            switch (_mode)
            {
                case Mode.Bottom:
                    _rect.offsetMin = _baseOffsetMin + new Vector2(0f, e.Bottom);
                    _rect.offsetMax = _baseOffsetMax + new Vector2(0f, e.Bottom);
                    break;
                case Mode.Top:
                    _rect.offsetMin = _baseOffsetMin - new Vector2(0f, e.TopWithMargin);
                    _rect.offsetMax = _baseOffsetMax - new Vector2(0f, e.TopWithMargin);
                    break;
                case Mode.TopEdge:
                    _rect.offsetMax = _baseOffsetMax - new Vector2(0f, e.TopWithMargin);
                    break;
                case Mode.Padding:
                    _rect.offsetMin = _baseOffsetMin + new Vector2(e.Left, e.Bottom);
                    _rect.offsetMax = _baseOffsetMax - new Vector2(e.Right, e.TopWithMargin);
                    break;
                case Mode.LayoutPadding:
                    if (_group == null || _basePadding == null) break;
                    _group.padding = new RectOffset(
                        _basePadding.left, _basePadding.right,
                        _basePadding.top + Mathf.RoundToInt(e.TopWithMargin),
                        _basePadding.bottom + Mathf.RoundToInt(e.Bottom));
                    LayoutRebuilder.MarkLayoutForRebuild(_rect);
                    break;
            }
        }
    }
}
