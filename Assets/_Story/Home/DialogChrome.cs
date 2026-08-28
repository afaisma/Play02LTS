using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// ============================================================================================
// Shared chrome for the app's dialog cards — the rounded UiTheme.Surface card, its Fredoka text,
// and the small X in its top-right corner.
//
// It exists because two pre-theme, scene-built dialogs (the "Rate app" panel and the "No
// internet" notice) are restyled at runtime rather than rebuilt in the scene assets, and both
// must end up with the SAME card and the SAME close button as the code-built screens
// (WelcomeController, UnifiedReadingModePicker). Each decorator (RateAppPanelStyle,
// NetworkStatusDialogStyle) owns its own layout; everything they share lives here.
//
// The X is drawn with UiGlyphs primitives, not a font character or an image asset, for the same
// reason the picker's tiles are: the UI font ships a static atlas, so an arbitrary glyph would
// render as tofu.
// ============================================================================================
public static class DialogChrome
{
    /// <summary>Fixed name of the close button, so callers stay idempotent.</summary>
    public const string CloseButtonName = "DialogClose";

    private const float CloseSize  = 76f; // diameter of the tap target
    private const float CloseInset = 22f; // gap from the card's top-right corner

    /// <summary>
    /// Paint a rounded UiTheme.Surface card onto an existing Image (the card's own background).
    /// </summary>
    public static void StyleCard(Image background)
    {
        if (background == null) return;
        background.sprite = RoundedSprite();
        background.type = Image.Type.Sliced;
        background.color = UiTheme.Surface;
        background.raycastTarget = true; // the card eats taps meant for whatever is behind it
    }

    /// <summary>
    /// Add the small X to a card's top-right corner. Idempotent: a card that already carries one
    /// keeps it (and its existing handler) untouched.
    /// </summary>
    public static Button AddCloseButton(RectTransform card, Action onClose)
    {
        if (card == null) return null;
        var existing = card.Find(CloseButtonName);
        if (existing != null) return existing.GetComponent<Button>();

        var go = new GameObject(CloseButtonName, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(card, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 1f);
        rt.sizeDelta = new Vector2(CloseSize, CloseSize);
        rt.anchoredPosition = new Vector2(-CloseInset, -CloseInset);

        var img = go.GetComponent<Image>();
        img.sprite = UiGlyphs.CircleSprite();
        img.color = UiTheme.Track; // quiet disc, so the X reads as chrome rather than an action

        var button = go.GetComponent<Button>();
        button.transition = Selectable.Transition.None; // TapFeedback owns the press feedback
        if (onClose != null) button.onClick.AddListener(() => onClose());
        TapFeedback.AddPressFeedback(go);

        UiGlyphs.BuildClose(go.transform, UiTheme.TextPrimary, CloseSize * 0.44f);

        // A layout group on the card must not stretch or reflow the corner X.
        var ignore = go.AddComponent<LayoutElement>();
        ignore.ignoreLayout = true;

        go.transform.SetAsLastSibling(); // always on top of the card's content
        return button;
    }

    /// <summary>A Fredoka TMP label, matching the code-built screens' MakeText.</summary>
    public static TMP_Text MakeText(Transform parent, string name, string text, float size,
                                    TextAlignmentOptions align, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.font = UiTheme.Font();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = size * 0.6f;
        tmp.fontSizeMax = size;
        tmp.alignment = align;
        tmp.color = color;
        tmp.raycastTarget = false;
        return tmp;
    }

    /// <summary>Stretch a RectTransform to fill its parent, with an optional uniform inset.</summary>
    public static void Stretch(RectTransform rt, float inset = 0f)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(inset, inset);
        rt.offsetMax = new Vector2(-inset, -inset);
    }

    // Procedural rounded-rect (9-sliced) sprite, mirroring the one the code-built scenes generate
    // so a restyled legacy card has exactly the same corner radius as a native one.
    private static Sprite _rounded;
    public static Sprite RoundedSprite()
    {
        if (_rounded != null) return _rounded;
        const int r = 32;
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
        tex.SetPixels(px); tex.Apply();
        _rounded = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f),
            100f, 0, SpriteMeshType.FullRect, new Vector4(r, r, r, r));
        return _rounded;
    }
}
