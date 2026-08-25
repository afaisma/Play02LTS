using TMPro;
using UnityEngine;
using UnityEngine.UI;

// ============================================================================================
// "Turn pages automatically" — the autopage preference's new home.
//
// It used to be a row inside the "How shall we read?" modal, which put a grown-up's setting in
// front of the child on every book start. The modal now asks only HOW the book is read; the
// preference lives here, defaults to ON, and is still stored under the SAME key
// (UnifiedReadingModePicker.AutopageKey) so an existing explicit choice carries over untouched.
//
// Built in CODE against the live scene (the same approach BackButtonToHome takes) rather than by
// editing _Settings.unity: it slots into the empty band left by the two deactivated start-scene
// controls, so nothing already on the screen moves. Rollback = drop the call in SettingsScene.
// ============================================================================================
public static class AutopageSettingRow
{
    // The free band between ButtonRateThisApp (tops out at 0.20) and txtReadingSpeedDescr
    // (starts at 0.48) — occupied only by DropdownStartScene / txtTitleSraerScene, both inactive.
    private static readonly Vector2 AnchorMin = new Vector2(0.10f, 0.36f);
    private static readonly Vector2 AnchorMax = new Vector2(0.90f, 0.44f);

    private const string RowName = "AutopageRow"; // also the idempotency guard

    /// <summary>
    /// Build the row on the Settings canvas. Safe to call more than once (a second call is a
    /// no-op) and a no-op when the scene has no Canvas.
    /// </summary>
    public static void Attach()
    {
        Transform canvas = FindSettingsCanvas();
        if (canvas == null)
        {
            Debug.LogWarning("AutopageSettingRow: Settings canvas not found; row not built.");
            return;
        }
        if (canvas.Find(RowName) != null) return;

        var row = new GameObject(RowName, typeof(RectTransform), typeof(Image), typeof(Toggle));
        row.transform.SetParent(canvas, false);
        var rt = row.GetComponent<RectTransform>();
        rt.anchorMin = AnchorMin; rt.anchorMax = AnchorMax;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        var card = row.GetComponent<Image>();
        card.sprite = RoundedSprite(); card.type = Image.Type.Sliced;
        card.color = UiTheme.Surface;

        // Label, left. Filling the row leaves the whole card tappable through the toggle below.
        var label = MakeText(row.transform, "Label", "Turn pages automatically", 34,
                             TextAlignmentOptions.Left);
        var lrt = label.rectTransform;
        lrt.anchorMin = new Vector2(0f, 0f); lrt.anchorMax = new Vector2(0.78f, 1f);
        lrt.offsetMin = new Vector2(28f, 0f); lrt.offsetMax = Vector2.zero;

        // Checkbox, right — the same box + check-fill the modal's toggle used.
        var box = new GameObject("Box", typeof(RectTransform), typeof(Image));
        box.transform.SetParent(row.transform, false);
        var brt = box.GetComponent<RectTransform>();
        brt.anchorMin = brt.anchorMax = new Vector2(1f, 0.5f);
        brt.pivot = new Vector2(1f, 0.5f);
        brt.sizeDelta = new Vector2(64f, 64f);
        brt.anchoredPosition = new Vector2(-28f, 0f);
        var boxImg = box.GetComponent<Image>();
        boxImg.sprite = RoundedSprite(); boxImg.type = Image.Type.Sliced;
        boxImg.color = UiTheme.Track;

        var check = new GameObject("Check", typeof(RectTransform), typeof(Image));
        check.transform.SetParent(box.transform, false);
        var crt = check.GetComponent<RectTransform>();
        crt.anchorMin = new Vector2(0.15f, 0.15f); crt.anchorMax = new Vector2(0.85f, 0.85f);
        crt.offsetMin = Vector2.zero; crt.offsetMax = Vector2.zero;
        var checkImg = check.GetComponent<Image>();
        checkImg.color = UiTheme.Primary;
        checkImg.raycastTarget = false;

        var toggle = row.GetComponent<Toggle>();
        toggle.targetGraphic = card;                 // the whole card is the hit target
        toggle.graphic = checkImg;
        toggle.isOn = UnifiedReadingModePicker.AutopageEnabled();
        toggle.onValueChanged.AddListener(UnifiedReadingModePicker.SetAutopageEnabled);
    }

    /// <summary>
    /// The canvas the anchors above are expressed against. Resolved through a control we know sits
    /// on it rather than the first Canvas in the scene — persistent objects (the debug console) can
    /// carry their own canvas, and landing on one of those would put the row in the wrong space.
    /// </summary>
    private static Transform FindSettingsCanvas()
    {
        foreach (string sibling in new[] { "ButtonRateThisApp", "txtVersion", "sliderReadingRate" })
        {
            var go = GameObject.Find(sibling);
            if (go != null && go.transform.parent != null) return go.transform.parent;
        }
        var named = GameObject.Find("Canvas");
        return named != null ? named.transform : null;
    }

    private static TMP_Text MakeText(Transform parent, string name, string text, float size,
                                     TextAlignmentOptions align)
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
        tmp.color = UiTheme.TextPrimary;
        tmp.raycastTarget = false;
        return tmp;
    }

    // Procedural rounded-rect (9-sliced), matching the card corners on Home and in the picker.
    private static Sprite _roundedSprite;
    private static Sprite RoundedSprite()
    {
        if (_roundedSprite != null) return _roundedSprite;
        const int r = 24; int size = r * 2 + 4;
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
        _roundedSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f),
            100f, 0, SpriteMeshType.FullRect, new Vector4(r, r, r, r));
        return _roundedSprite;
    }
}
