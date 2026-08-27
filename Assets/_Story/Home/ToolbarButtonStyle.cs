using System;
using UnityEngine;
using UnityEngine.UI;

// The shared look for the small round icon buttons that make up scene chrome — today the reader
// toolbar's home / reading-mode / replay buttons. One rounded UiTheme.Surface container with a
// code-drawn glyph centred inside it, plus the standard press feedback.
//
// Extracted from HomeButton so the CONTAINER is drawn in exactly one place: HomeButton passes the
// house glyph, the reading-mode entry passes the speaker glyph, and anything else passes its own
// builder. Nothing in here knows what the glyph is.
public static class ToolbarButtonStyle
{
    // Fraction of the button's short side taken up by the glyph (the rest is the Surface ring).
    public const float GlyphScale = 0.56f;

    // Name of the glyph slot child. One fixed name for every glyph so Apply stays idempotent
    // whichever builder drew the contents.
    public const string GlyphSlotName = "ToolbarGlyph";

    /// <summary>
    /// Restyle an already-wired Button (e.g. the reader toolbar's scene-built buttons) to the
    /// shared look without disturbing its position or its existing onClick. Idempotent: the glyph
    /// slot is built once and reused on repeat calls.
    ///
    /// Only the PRESS half of the tap treatment is added here — callers whose onClick navigates
    /// already run their own TapFeedback.TapThenGo, and wrapping it a second time would deadlock
    /// on the latch.
    /// </summary>
    /// <param name="glyphBuilder">Draws the glyph into the supplied slot transform, given the
    /// slot's square size in px. Invoked once, only when the slot is first created.</param>
    public static void Apply(Button button, Action<Transform, float> glyphBuilder)
    {
        if (button == null) return;
        TapFeedback.AddPressFeedback(button.gameObject);
        var bg = button.GetComponent<Image>();
        if (bg != null) StyleBackground(bg);

        if (button.transform.Find(GlyphSlotName) != null) return; // already styled

        // Scene-built buttons are not all square (the Library pill is 190x96, the grown-up
        // toolbars' are 204x114), so size the glyph off the SHORT side — off the width it would
        // overflow the button vertically.
        var rect = ((RectTransform)button.transform).rect;
        float size = Mathf.Min(rect.width, rect.height) * GlyphScale;
        glyphBuilder?.Invoke(NewGlyphSlot(button.transform, size), size);
    }

    /// <summary>Paint the rounded Surface container onto a button's background Image.</summary>
    public static void StyleBackground(Image bg)
    {
        if (bg == null) return;
        bg.sprite = BackgroundSprite();
        bg.type = Image.Type.Sliced;
        bg.color = UiTheme.Surface;
    }

    /// <summary>Centred square slot, `size` px on a side, for a glyph builder to draw into.</summary>
    public static Transform NewGlyphSlot(Transform parent, float size)
    {
        var slot = new GameObject(GlyphSlotName, typeof(RectTransform));
        slot.transform.SetParent(parent, false);
        var rt = slot.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(size, size);
        return slot.transform;
    }

    // Procedural rounded-rect (9-sliced) sprite for the Surface backing, mirroring the
    // controllers' own RoundedSprite so the button matches the code-built scenes and works in
    // player builds.
    private static Sprite _bg;
    public static Sprite BackgroundSprite()
    {
        if (_bg != null) return _bg;
        const int r = 24;
        int size = r * 2 + 4;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float fx = x + 0.5f, fy = y + 0.5f;
                float cx = Mathf.Clamp(fx, r, size - r);
                float cy = Mathf.Clamp(fy, r, size - r);
                float dx = fx - cx, dy = fy - cy;
                px[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(r - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f));
            }
        tex.SetPixels(px);
        tex.Apply();
        _bg = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f),
            100f, 0, SpriteMeshType.FullRect, new Vector4(r, r, r, r));
        return _bg;
    }
}
