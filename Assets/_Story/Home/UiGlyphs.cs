using UnityEngine;
using UnityEngine.UI;

// ============================================================================================
// Code-drawn glyph kit, shared by the reading-mode picker's tiles and the reader toolbar's icon
// buttons (via ToolbarButtonStyle).
//
// Drawn in code from primitive shapes (the same technique as the Home rail's check chip): plain
// rects, ellipses, triangles and masked rings, tinted with UiTheme colours. Deliberately NOT
// emoji or font glyphs — the project's UI font ships a static atlas, so any character it wasn't
// built with renders as tofu; and NOT image assets, so nothing new has to ship.
//
// Every glyph is authored in a centred DesignBox x DesignBox coordinate system and then scaled to
// whatever size the caller asks for, so the SAME drawing serves a 96px picker tile and a ~34px
// toolbar button.
// ============================================================================================
public static class UiGlyphs
{
    // The box every glyph is authored in. Callers pass their own size; parts scale uniformly.
    public const float DesignBox = 96f;

    // Speaker: driver rect + cone triangle, then either sound arcs (audio on) or a slash (silent).
    // Used by the picker's "App Reads" / "App Is Silent" tiles and by the toolbar's reading-mode
    // button, so the reader's icon and the row it opens are literally the same drawing.
    public static void BuildSpeaker(Transform parent, Color ink, bool waves, bool muted,
                                    float size = DesignBox)
    {
        var box = NewBox(parent, "Speaker", size);
        AddShape(box, "Driver",  null,             -30f, 0f, 20f, 30f, 0f, ink);
        AddShape(box, "Cone",    TriangleSprite(),  -8f, 0f, 28f, 56f, 0f, ink);
        if (waves)
        {
            // Two concentric rings whose LEFT halves are clipped away, leaving open arcs.
            var clip = AddClip(box, "Waves", 26f, 0f, 34f, 76f);
            AddShape(clip, "Arc1", RingSprite(), -17f, 0f, 44f, 44f, 0f, ink);
            AddShape(clip, "Arc2", RingSprite(), -31f, 0f, 72f, 72f, 0f, ink);
        }
        if (muted)
            AddShape(box, "Slash", null, 8f, 0f, 78f, 8f, -45f, ink);
    }

    // A DesignBox-sized drawing surface, uniformly scaled so the parts inside can keep their
    // authored coordinates whatever the requested size is.
    public static Transform NewBox(Transform parent, string name, float size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(DesignBox, DesignBox);
        rt.localScale = Vector3.one * (size / DesignBox);
        return go.transform;
    }

    // One primitive part. A null sprite draws a plain filled rectangle.
    public static Transform AddShape(Transform parent, string name, Sprite sprite,
                                     float x, float y, float w, float h, float angle, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(w, h);
        rt.anchoredPosition = new Vector2(x, y);
        if (angle != 0f) rt.localRotation = Quaternion.Euler(0f, 0f, angle);
        var img = go.GetComponent<Image>();
        if (sprite != null) img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;   // the ROW / BUTTON owns the tap
        return go.transform;
    }

    // A clipping window: children are shown only where they overlap this rect. Used to cut whole
    // rings down to the arcs the speaker and microphone glyphs need.
    public static Transform AddClip(Transform parent, string name, float x, float y, float w, float h)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(RectMask2D));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(w, h);
        rt.anchoredPosition = new Vector2(x, y);
        return go.transform;
    }

    // Triangle pointing LEFT (apex at the left edge, base along the right edge) — the speaker cone.
    private static Sprite _triangleSprite;
    public static Sprite TriangleSprite()
    {
        if (_triangleSprite != null) return _triangleSprite;
        const int d = 64;
        var tex = new Texture2D(d, d, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[d * d];
        for (int y = 0; y < d; y++)
            for (int x = 0; x < d; x++)
            {
                // Half-height of the cone grows linearly from 0 at the apex to d/2 at the base.
                float half = (x + 0.5f) * 0.5f;
                bool inside = Mathf.Abs(y + 0.5f - d * 0.5f) <= half;
                px[y * d + x] = inside ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
            }
        tex.SetPixels32(px);
        tex.Apply();
        _triangleSprite = Sprite.Create(tex, new Rect(0, 0, d, d), new Vector2(0.5f, 0.5f));
        return _triangleSprite;
    }

    // Annulus (open ring). Clipped by AddClip into the arc a glyph needs.
    private static Sprite _ringSprite;
    public static Sprite RingSprite()
    {
        if (_ringSprite != null) return _ringSprite;
        const int d = 128;
        const float outer = d * 0.5f;
        const float inner = outer * 0.74f;
        var tex = new Texture2D(d, d, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color[d * d];
        for (int y = 0; y < d; y++)
            for (int x = 0; x < d; x++)
            {
                float dx = x + 0.5f - outer, dy = y + 0.5f - outer;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                // Anti-aliased on both edges of the band, same coverage trick as CircleSprite.
                float a = Mathf.Min(Mathf.Clamp01(outer - r + 0.5f), Mathf.Clamp01(r - inner + 0.5f));
                px[y * d + x] = new Color(1f, 1f, 1f, a);
            }
        tex.SetPixels(px);
        tex.Apply();
        _ringSprite = Sprite.Create(tex, new Rect(0, 0, d, d), new Vector2(0.5f, 0.5f));
        return _ringSprite;
    }

    // A solid white circle sprite, generated once and cached. Tinted via Image.color. Avoids any
    // built-in/Resources asset dependency that may be absent across Unity versions.
    private static Sprite _circleSprite;
    public static Sprite CircleSprite()
    {
        if (_circleSprite != null) return _circleSprite;
        const int d = 64;
        var tex = new Texture2D(d, d, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        float r = d * 0.5f;
        var px = new Color32[d * d];
        for (int y = 0; y < d; y++)
            for (int x = 0; x < d; x++)
            {
                float dx = x + 0.5f - r, dy = y + 0.5f - r;
                bool inside = dx * dx + dy * dy <= (r - 0.5f) * (r - 0.5f);
                px[y * d + x] = inside ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
            }
        tex.SetPixels32(px);
        tex.Apply();
        _circleSprite = Sprite.Create(tex, new Rect(0, 0, d, d), new Vector2(0.5f, 0.5f));
        return _circleSprite;
    }
}
