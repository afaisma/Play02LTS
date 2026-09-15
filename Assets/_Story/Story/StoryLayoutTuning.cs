using UnityEngine;

// ============================================================================================
// How the reader page splits its height between the ART (gallery) and the TEXT block.
//
// The long-standing rule — art is a SQUARE the width of the safe area, pinned to the top of the
// safe area; text fills what is left above the toolbar — is fine on a phone but starves the text
// on a 4:3 tablet: on the iPad Pro 13" (2064x2752) the square takes 75% of the screen and the
// text block is left with ~18% of the usable height, so auto-size shrinks the narration to a
// SMALLER font than on a phone (three cramped lines vs. five roomy ones).
//
// So on wide screens only (aspect w/h >= WideAspect) the art region is SHRUNK until the text
// block owns at least MinTextShare of the usable height. The art keeps its aspect — PRUtils.
// DownloadImage sets preserveAspect, so a square picture simply letterboxes inside the shorter
// region and is never stretched. Below the threshold nothing moves at all: the iPad mini
// (0.657), iPhone SE (0.562) and iPhone 17 Pro Max (0.460) all take the untouched split, which
// is what the smoke captures regress against.
//
// Compute() is pure and unit-tested; the Runner below is the whole runtime half — installed by
// PositionUIInSafeArea on scene load (it owns the three serialized rects), and re-applying on
// its own when the screen size or safe area changes.
// ============================================================================================
public static class StoryLayoutTuning
{
    /// <summary>Screen aspect (w/h) at or above which the split is re-tuned. Tablet 4:3 is 0.75.</summary>
    public const float WideAspect = 0.70f;

    /// <summary>Minimum share of the usable height (toolbar top .. safe-area top) for the text.</summary>
    public const float MinTextShare = 0.38f;

    /// <summary>
    /// The reader's vertical bands, as fractions of the screen height measured from the BOTTOM
    /// (the same space RectTransform anchors live in, since the story canvas covers the screen).
    /// </summary>
    public readonly struct Split
    {
        public readonly float ToolbarBottomY, ToolbarTopY, ArtBottomY, ArtTopY;
        public Split(float toolbarBottomY, float toolbarTopY, float artBottomY, float artTopY)
        { ToolbarBottomY = toolbarBottomY; ToolbarTopY = toolbarTopY; ArtBottomY = artBottomY; ArtTopY = artTopY; }

        /// <summary>The text block sits between the toolbar and the art.</summary>
        public float TextBottomY => ToolbarTopY;
        public float TextTopY => ArtBottomY;

        /// <summary>Share of the usable height (toolbar top .. safe-area top) the text block owns.</summary>
        public float TextShare
        {
            get
            {
                float usable = ArtTopY - ToolbarTopY;
                return usable <= 0f ? 0f : (ArtBottomY - ToolbarTopY) / usable;
            }
        }
    }

    /// <summary>
    /// Work out the split for a screen. <paramref name="toolbarHeightY"/> is the toolbar's height
    /// as a fraction of the screen (its anchor span, which the bottom-inset lift does not change).
    /// The toolbar sits on top of the home-indicator inset, the art is pinned to the top of the
    /// safe area, and on a wide screen the art's bottom edge is pushed DOWN (never up) until the
    /// text block reaches MinTextShare.
    /// </summary>
    public static Split Compute(float screenW, float screenH, Rect safeArea, float toolbarHeightY)
    {
        if (screenW <= 0f || screenH <= 0f) return new Split(0f, 0f, 0f, 1f);

        var insets = SafeAreaInsets.Pixels(safeArea, screenW, screenH);
        float toolbarBottomY = insets.Bottom / screenH;
        float toolbarTopY = Mathf.Clamp01(toolbarBottomY + Mathf.Max(0f, toolbarHeightY));
        float artTopY = Mathf.Clamp01(1f - insets.Top / screenH);

        // The historical rule: a square as wide as the safe area, hanging off the safe-area top.
        float safeWidth = Mathf.Min(safeArea.width, screenW);
        float artBottomY = artTopY - safeWidth / screenH;

        if (screenW / screenH >= WideAspect)
        {
            float floor = toolbarTopY + MinTextShare * (artTopY - toolbarTopY);
            artBottomY = Mathf.Max(artBottomY, floor); // raising the art's BOTTOM shrinks the art
        }

        artBottomY = Mathf.Clamp(artBottomY, toolbarTopY, artTopY);
        return new Split(toolbarBottomY, toolbarTopY, artBottomY, artTopY);
    }

    // ---------------------------------------------------------------- runtime

    /// <summary>
    /// Install the runtime half on <paramref name="gallery"/>'s scene and apply it once. Safe to
    /// call repeatedly — a second call just re-applies. Called from PositionUIInSafeArea.Start.
    /// </summary>
    public static void Install(RectTransform gallery, RectTransform text, RectTransform toolbar)
    {
        if (gallery == null || text == null || toolbar == null) return;
        var runner = gallery.GetComponent<Runner>();
        if (runner == null) runner = gallery.gameObject.AddComponent<Runner>();
        runner.Bind(gallery, text, toolbar);
        runner.ApplyIfChanged();
    }

    // Applies the split and re-applies it when the screen size or safe area changes (rotation,
    // iPad split view) — one cheap comparison per frame, the shape InputTuning's Enforcer uses.
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    private sealed class Runner : MonoBehaviour
    {
        private RectTransform _gallery, _text, _toolbar;
        private float _toolbarHeightY;

        private bool _applied;
        private int _screenW, _screenH;
        private Rect _safeArea;

        internal void Bind(RectTransform gallery, RectTransform text, RectTransform toolbar)
        {
            _gallery = gallery; _text = text; _toolbar = toolbar;
            // Read the toolbar's height ONCE, from its anchor span: SafeAreaInsets.ApplyBottom
            // lifts it by moving offsets, so the span stays the scene's authored 7%.
            _toolbarHeightY = Mathf.Max(0f, toolbar.anchorMax.y - toolbar.anchorMin.y);
        }

        private void LateUpdate() => ApplyIfChanged();

        internal void ApplyIfChanged()
        {
            if (_gallery == null || _text == null || _toolbar == null) return;
            Rect safe = Screen.safeArea;
            if (_applied && Screen.width == _screenW && Screen.height == _screenH && safe == _safeArea) return;
            _screenW = Screen.width; _screenH = Screen.height; _safeArea = safe; _applied = true;

            var split = Compute(Screen.width, Screen.height, safe, _toolbarHeightY);
            float xMin = Screen.width <= 0 ? 0f : safe.xMin / Screen.width;
            float xMax = Screen.width <= 0 ? 1f : safe.xMax / Screen.width;

            _gallery.anchorMin = new Vector2(xMin, split.ArtBottomY);
            _gallery.anchorMax = new Vector2(xMax, split.ArtTopY);
            _gallery.offsetMin = Vector2.zero; _gallery.offsetMax = Vector2.zero;

            _text.anchorMin = new Vector2(xMin, split.TextBottomY);
            _text.anchorMax = new Vector2(xMax, split.TextTopY);
            _text.offsetMin = Vector2.zero; _text.offsetMax = Vector2.zero;
        }
    }
}
