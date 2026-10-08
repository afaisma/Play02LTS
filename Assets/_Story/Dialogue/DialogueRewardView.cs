using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The drawn parts of the rewards, built in code like the rest of the dialogue sheet: the row of
/// stars, the confetti and the end-of-book summary. Calm by design: soft colours, slow movement,
/// nothing flashes.
/// </summary>
public static class DialogueRewardView
{
    private static readonly Color Gold = Hex(0xE2B04A), GoldSoft = new Color(0.886f, 0.690f, 0.290f, 0.35f);
    private static readonly Color CardBg = Hex(0xFFFDF8);
    private const float CaptionWidth = 270f;
    private static readonly Color[] ConfettiColors =
        { Hex(0x8FA67E), Hex(0xE2B04A), Hex(0xA9BEC5), Hex(0xE8B4A0), Hex(0xC9B6D9) };

    /// <summary>
    /// A rounded card with one star per question: gold = answered, pale = not (yet). The star at
    /// newIndex pops in; with glow it also gets a soft halo (an answer given by voice or sound).
    /// A caption ("I heard you!") stands after the stars, inside the card, so it moves nothing.
    /// </summary>
    public static RectTransform BuildStarPill(Transform parent, IList<bool> stars, int newIndex, bool glow,
                                              string caption, float maxWidth)
    {
        bool withCaption = !string.IsNullOrEmpty(caption);
        if (withCaption) maxWidth -= CaptionWidth + 18f;
        var go = new GameObject("Stars", typeof(RectTransform), typeof(Image), typeof(Outline),
            typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
        go.transform.SetParent(parent, false);
        var bg = go.GetComponent<Image>();
        bg.sprite = DialogChrome.RoundedSprite(); bg.type = Image.Type.Sliced; bg.color = CardBg; bg.raycastTarget = false;
        var edge = go.GetComponent<Outline>();
        edge.effectColor = UiTheme.Track; edge.effectDistance = new Vector2(3f, 3f);
        FillStarRow(go, stars, newIndex, glow, maxWidth, new RectOffset(28, 28, 14, 14));
        if (withCaption)
        {
            go.GetComponent<HorizontalLayoutGroup>().padding.right = 36;
            TMP_Text label = DialogChrome.MakeText(go.transform, "Caption", caption, 44f, TextAlignmentOptions.Center, Hex(0x5F5A4E));
            label.enableWordWrapping = false;
            var le = label.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = CaptionWidth; le.minWidth = CaptionWidth; le.preferredHeight = 56f;
        }
        return (RectTransform)go.transform;
    }

    private static void FillStarRow(GameObject row, IList<bool> stars, int newIndex, bool glow, float maxWidth, RectOffset padding)
    {
        int n = Mathf.Max(1, stars.Count);
        // Smaller stars for a book with many questions, so the row always fits the screen.
        float gap = n > 12 ? 6f : 10f;
        float size = Mathf.Clamp((maxWidth - padding.left - padding.right - gap * (n - 1)) / n, 12f, 64f);
        var hlg = row.GetComponent<HorizontalLayoutGroup>();
        hlg.padding = padding;
        hlg.spacing = gap;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childControlWidth = true; hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;
        var fit = row.GetComponent<ContentSizeFitter>();
        fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        for (int i = 0; i < stars.Count; i++)
        {
            var slot = new GameObject("Star_" + i, typeof(RectTransform), typeof(LayoutElement));
            slot.transform.SetParent(row.transform, false);
            var le = slot.GetComponent<LayoutElement>();
            le.preferredWidth = le.minWidth = size; le.preferredHeight = le.minHeight = size;

            bool isNew = i == newIndex && stars[i];
            if (isNew && glow)
            {
                RectTransform halo = Shape("Halo", slot.transform, UiGlyphs.CircleSprite(), GoldSoft, size * 1.9f);
                halo.localScale = Vector3.one * 0.5f;
                halo.DOScale(1f, 0.9f).SetEase(Ease.OutSine).SetDelay(0.35f).SetUpdate(true);
                halo.GetComponent<Image>().DOFade(0f, 0.9f).SetEase(Ease.InSine).SetDelay(0.9f).SetUpdate(true);
            }
            RectTransform star = Shape("Shape", slot.transform, StarSprite(), stars[i] ? Gold : UiTheme.Track, size);
            if (isNew)
            {
                star.localScale = Vector3.zero;
                star.DOScale(1f, 0.45f).SetEase(Ease.OutBack).SetDelay(0.25f).SetUpdate(true);
            }
        }
    }

    /// <summary>The end of the book: "You answered 7 questions!" over the row of stars.</summary>
    public static RectTransform BuildSummary(Transform parent, string text, IList<bool> stars, float maxWidth)
    {
        var go = new GameObject("Summary", typeof(RectTransform), typeof(Image), typeof(Outline),
            typeof(VerticalLayoutGroup), typeof(ContentSizeFitter), typeof(CanvasGroup));
        go.transform.SetParent(parent, false);
        var bg = go.GetComponent<Image>();
        bg.sprite = DialogChrome.RoundedSprite(); bg.type = Image.Type.Sliced; bg.color = CardBg; bg.raycastTarget = false;
        var edge = go.GetComponent<Outline>();
        edge.effectColor = UiTheme.Track; edge.effectDistance = new Vector2(3f, 3f);
        var vlg = go.GetComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(44, 44, 26, 22);
        vlg.spacing = 6f;
        vlg.childAlignment = TextAnchor.MiddleCenter;
        vlg.childControlWidth = true; vlg.childControlHeight = true;
        vlg.childForceExpandWidth = false; vlg.childForceExpandHeight = false;
        var fit = go.GetComponent<ContentSizeFitter>();
        fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var group = go.GetComponent<CanvasGroup>();
        group.blocksRaycasts = false; group.interactable = false;

        TMP_Text label = DialogChrome.MakeText(go.transform, "Text", text, 58f, TextAlignmentOptions.Center, UiTheme.TextPrimary);
        label.fontStyle = FontStyles.Bold;
        label.enableAutoSizing = false;
        label.enableWordWrapping = false;

        var row = new GameObject("StarRow", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
        row.transform.SetParent(go.transform, false);
        FillStarRow(row, stars, -1, false, maxWidth - 88f, new RectOffset(0, 0, 8, 8));
        return (RectTransform)go.transform;
    }

    /// <summary>Small soft pieces fall slowly from the top of the layer and fade. About two seconds.</summary>
    public static void Confetti(RectTransform layer, int count)
    {
        float w = layer.rect.width, h = layer.rect.height;
        for (int i = 0; i < count; i++)
        {
            bool round = i % 3 == 0;
            var go = new GameObject("Confetti", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(layer, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.sizeDelta = round ? Vector2.one * Random.Range(18f, 28f) : new Vector2(Random.Range(16f, 26f), Random.Range(28f, 40f));
            float x = Random.Range(-0.46f, 0.46f) * w;
            rt.anchoredPosition = new Vector2(x, Random.Range(20f, 160f));
            rt.localEulerAngles = new Vector3(0f, 0f, Random.Range(0f, 360f));
            var img = go.GetComponent<Image>();
            img.sprite = round ? UiGlyphs.CircleSprite() : DialogChrome.RoundedSprite();
            if (!round) img.type = Image.Type.Sliced;
            img.color = ConfettiColors[i % ConfettiColors.Length];
            img.raycastTarget = false;

            float seconds = Random.Range(1.8f, 2.6f), delay = Random.Range(0f, 0.5f);
            rt.DOAnchorPos(new Vector2(x + Random.Range(-70f, 70f), -h * Random.Range(0.45f, 0.7f)), seconds)
                .SetEase(Ease.InSine).SetDelay(delay).SetUpdate(true);
            rt.DOLocalRotate(new Vector3(0f, 0f, Random.Range(-240f, 240f)), seconds, RotateMode.LocalAxisAdd)
                .SetEase(Ease.Linear).SetDelay(delay).SetUpdate(true);
            img.DOFade(0f, 0.6f).SetDelay(delay + seconds - 0.6f).SetUpdate(true)
                .OnComplete(() => { if (go != null) Object.Destroy(go); });
        }
    }

    /// <summary>Stops the confetti and removes what is left of it.</summary>
    public static void ClearConfetti(RectTransform layer)
    {
        for (int i = layer.childCount - 1; i >= 0; i--)
        {
            Transform child = layer.GetChild(i);
            if (child.name != "Confetti") continue;
            child.DOKill();
            var img = child.GetComponent<Image>();
            if (img != null) img.DOKill();
            Object.Destroy(child.gameObject);
        }
    }

    private static RectTransform Shape(string name, Transform parent, Sprite sprite, Color color, float size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(size, size);
        var img = go.GetComponent<Image>();
        img.sprite = sprite; img.color = color; img.raycastTarget = false;
        return rt;
    }

    // A five-pointed star with slightly full arms, drawn once into a small texture.
    private static Sprite _star;
    public static Sprite StarSprite()
    {
        if (_star != null) return _star;
        const int size = 128, ss = 3;
        var points = new Vector2[10];
        for (int i = 0; i < 10; i++)
        {
            float radius = i % 2 == 0 ? 61f : 29f;
            float angle = Mathf.PI / 2f + i * Mathf.PI / 5f;
            points[i] = new Vector2(size / 2f + radius * Mathf.Cos(angle), size / 2f - 4f + radius * Mathf.Sin(angle));
        }
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                int inside = 0;
                for (int sy = 0; sy < ss; sy++)
                    for (int sx = 0; sx < ss; sx++)
                        if (Inside(points, x + (sx + 0.5f) / ss, y + (sy + 0.5f) / ss)) inside++;
                px[y * size + x] = new Color(1f, 1f, 1f, inside / (float)(ss * ss));
            }
        tex.SetPixels(px); tex.Apply();
        _star = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        return _star;
    }

    private static bool Inside(Vector2[] polygon, float x, float y)
    {
        bool inside = false;
        for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
            if ((polygon[i].y > y) != (polygon[j].y > y) &&
                x < (polygon[j].x - polygon[i].x) * (y - polygon[i].y) / (polygon[j].y - polygon[i].y) + polygon[i].x)
                inside = !inside;
        return inside;
    }

    private static Color Hex(uint rgb) =>
        new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);
}
