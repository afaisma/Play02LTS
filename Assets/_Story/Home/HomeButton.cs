using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

// One reusable "home" control, shared by every surface that needs to return to the _Home scene:
// the Learn-to-Read ladder header and the end-of-book Read-next sheet call Create(), and the
// scene-built reader toolbar restyles its existing button through Apply(). (Home's own title row
// no longer carries one — it navigated to the screen the child was already on.) Keeping the look in
// one place means they all render the identical round button — a house glyph tinted UiTheme.Primary (sage) on a
// rounded UiTheme.Surface background. On-palette by construction: no colours beyond UiTheme.
public static class HomeButton
{
    // Fraction of the button's size taken up by the house glyph (the rest is the Surface ring).
    private const float GlyphScale = 0.56f;

    // Build a fresh round home button under `parent`, sized `size` px, invoking `onClick` on tap.
    // The outer slot carries the LayoutElement so it sits in the header/title row; the inner button
    // keeps a fixed square size so it stays round even when the row stretches it vertically.
    //
    // `onClick` must be the RAW navigation: the tap is run through TapFeedback (instant pressed
    // state, a beat, a fade, then the nav) because _Home is an async, non-trivial load and an
    // unacknowledged tap reads as dead. Callers must NOT wrap it themselves — a nested TapThenGo
    // hits the already-set latch, gets dropped, and the navigation never runs.
    public static GameObject Create(Transform parent, float size, UnityAction onClick)
    {
        var slot = new GameObject("HomeButton", typeof(RectTransform), typeof(LayoutElement));
        slot.transform.SetParent(parent, false);
        var le = slot.GetComponent<LayoutElement>();
        le.preferredWidth = size; le.minWidth = size;

        var btnGO = new GameObject("Button", typeof(RectTransform), typeof(Image), typeof(Button));
        btnGO.transform.SetParent(slot.transform, false);
        var rt = btnGO.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(size, size);

        StyleBackground(btnGO.GetComponent<Image>());
        AddGlyph(btnGO.transform, size * GlyphScale);

        TapFeedback.AddPressFeedback(btnGO);
        if (onClick != null)
            btnGO.GetComponent<Button>().onClick.AddListener(
                () => TapFeedback.TapThenGo(btnGO.transform, () => onClick()));
        return slot;
    }

    // Restyle an already-wired Button (e.g. the reader toolbar's serialized btnHome) to the shared
    // look without disturbing its position or its existing onClick. Idempotent: the glyph child is
    // reused on repeat calls. Only the PRESS half of the tap treatment is added here — the serialized
    // onClick already points at a handler (PRScript.Home) that does its own TapThenGo, and wrapping
    // it a second time would deadlock on the latch.
    public static void Apply(Button button)
    {
        if (button == null) return;
        TapFeedback.AddPressFeedback(button.gameObject);
        var bg = button.GetComponent<Image>();
        if (bg != null) StyleBackground(bg);

        var existing = button.transform.Find("HouseGlyph");
        // Scene-built buttons are not all square (the Library pill is 190x96, the grown-up
        // toolbars' are 204x114), so size the glyph off the SHORT side — off the width it would
        // overflow the button vertically.
        var rect = ((RectTransform)button.transform).rect;
        float size = Mathf.Min(rect.width, rect.height);
        if (existing == null)
            AddGlyph(button.transform, size * GlyphScale);
    }

    private static void StyleBackground(Image bg)
    {
        bg.sprite = BackgroundSprite();
        bg.type = Image.Type.Sliced;
        bg.color = UiTheme.Surface;
    }

    private static void AddGlyph(Transform parent, float glyphSize)
    {
        var glyphGO = new GameObject("HouseGlyph", typeof(RectTransform), typeof(Image));
        glyphGO.transform.SetParent(parent, false);
        var rt = glyphGO.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(glyphSize, glyphSize);
        var glyph = glyphGO.GetComponent<Image>();
        glyph.sprite = HouseSprite();
        glyph.color = UiTheme.Primary;
        glyph.preserveAspect = true;
        glyph.raycastTarget = false;
    }

    // Single-colour rounded house, loaded once from Resources and tinted at use.
    private static Sprite _house;
    private static Sprite HouseSprite()
    {
        if (_house == null) _house = Resources.Load<Sprite>("Icons/home");
        return _house;
    }

    // Procedural rounded-rect (9-sliced) sprite for the Surface backing, mirroring the controllers'
    // own RoundedSprite so the button matches the code-built scenes and works in player builds.
    private static Sprite _bg;
    private static Sprite BackgroundSprite()
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
