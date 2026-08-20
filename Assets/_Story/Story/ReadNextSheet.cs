using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// ============================================================================================
// End-of-book "Read next" sheet — variant A (INLINE END CARD) of ENDBOOK_MOCK_2026-08-20.
//
// A code-built bottom sheet (no scene edits, same shape as the kid-safe script-error panel and the
// reading-mode picker): it covers the reader's TEXT area only, so the final page's art — the picture
// children want to sit with — stays fully visible above it. Contents: a praise line, one big
// next-book card (cover + name + go pill, the whole card is the tap target), and a quiet row with
// Home and "Read again".
//
// Owned by PRScript: created ~1s after the last page finishes (PRScript.OnLastStepFinished) and
// destroyed on any real step change, so a Prev off the last page takes it with it.
//
// Nothing here navigates by itself. Every exit is a tap:
//   next card   -> Globals.GotoPrBook(next)         (fresh _Story scene load)
//   Home        -> PRScript.Home()
//   Read again  -> Globals.GotoPrBook(current book) (already flagged done, so it resumes at page 1)
// ============================================================================================
public class ReadNextSheet : MonoBehaviour
{
    // ---- metrics, in the _Story CanvasMain reference space (1020x1980) ----
    private const float Pad          = 26f;
    private const float PraiseH      = 78f;
    private const float QuietRowH    = 96f;
    private const float CardPad      = 16f;
    private const float CardAccentH  = 13f;  // the mock's 5 CSS px, scaled from its 390pt phone (x2.6)
    private const float CoverFraction = 0.44f;
    private const float HomeBtnSize  = 84f;
    private const float SlideSec     = 0.32f;
    private const float ReshowSec    = 5f;   // dismissed by an art tap -> comes back on its own

    private PRScript _prScript;
    private RectTransform _sheet;
    private GameObject _artCatcher;   // tap the page art  -> hide the sheet
    private GameObject _textCatcher;  // tap the text area -> bring it back
    private float _sheetHeight;
    private bool _shown = true;
    private Coroutine _reshowCo;

    /// <summary>
    /// Build and slide in the sheet under <paramref name="canvas"/>. <paramref name="next"/> may be
    /// null — that renders the "read them all" state instead of the next-book card. Returns null when
    /// there is no canvas to build on (sheet is optional chrome; the book still ends fine without it).
    /// </summary>
    public static ReadNextSheet Create(PRScript prScript, Canvas canvas, PRBook next)
    {
        if (prScript == null || canvas == null) return null;
        var canvasRT = canvas.transform as RectTransform;
        if (canvasRT == null) return null;

        var rootGO = new GameObject("ReadNextSheet", typeof(RectTransform));
        rootGO.transform.SetParent(canvas.transform, false);
        Stretch((RectTransform)rootGO.transform);
        rootGO.transform.SetAsLastSibling(); // above the page, below nothing

        var sheet = rootGO.AddComponent<ReadNextSheet>();
        sheet.Build(prScript, canvasRT, next);
        return sheet;
    }

    private void Build(PRScript prScript, RectTransform canvasRT, PRBook next)
    {
        _prScript = prScript;

        // Cover the text area only. Found by name so no scene wiring is needed; if the reader's
        // layout ever renames it, fall back to the bottom 42% — the mock's proportion.
        RectTransform textArea = FindTextArea(canvasRT);
        Rect area = textArea != null ? NormalizedRect(textArea, canvasRT) : Rect.MinMaxRect(0f, 0f, 1f, 0.42f);
        _sheetHeight = Mathf.Max(1f, canvasRT.rect.height * area.height);

        // Tap the art to look at the picture; tap the text area (or just wait) to bring the sheet back.
        _artCatcher  = BuildCatcher("ArtCatcher", new Vector2(0f, area.yMax), Vector2.one, Hide);
        _textCatcher = BuildCatcher("TextCatcher", area.min, area.max, Show);
        _textCatcher.SetActive(false);

        var sheetGO = new GameObject("Sheet", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
        sheetGO.transform.SetParent(transform, false);
        _sheet = (RectTransform)sheetGO.transform;
        _sheet.anchorMin = area.min; _sheet.anchorMax = area.max;
        _sheet.offsetMin = Vector2.zero; _sheet.offsetMax = Vector2.zero;
        var bg = sheetGO.GetComponent<Image>();
        bg.color = UiTheme.Bg;            // the reader's page tint, as in the mock
        var vlg = sheetGO.GetComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset((int)Pad, (int)Pad, (int)Pad, (int)Pad);
        vlg.spacing = 18f;
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childControlWidth = true; vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;

        var praise = MakeText(sheetGO.transform, "Praise", "Great reading!", 52, TextAlignmentOptions.Center);
        praise.fontStyle = FontStyles.Bold;
        praise.color = UiTheme.TextPrimary;
        praise.gameObject.AddComponent<LayoutElement>().preferredHeight = PraiseH;

        float cardWidth = canvasRT.rect.width * area.width - 2f * Pad;
        if (next != null) BuildNextCard(sheetGO.transform, next, cardWidth);
        else BuildAllReadState(sheetGO.transform);

        BuildQuietRow(sheetGO.transform, next != null);

        // Slide up from just below its own height — a short beat, unscaled like the picker's tweens.
        _sheet.anchoredPosition = new Vector2(0f, -_sheetHeight);
        _sheet.DOAnchorPosY(0f, SlideSec).SetEase(Ease.OutCubic).SetUpdate(true);
    }

    // ---------------------------------------------------------------- the next-book card

    // One big picture-button, same visual grammar as the Home doors: Surface card, rounded cover on
    // the left, meta on the right, Primary accent bar along the bottom edge, whole card tappable.
    private void BuildNextCard(Transform parent, PRBook next, float cardWidth)
    {
        var cardGO = new GameObject("NextCard",
            typeof(RectTransform), typeof(Image), typeof(Button), typeof(VerticalLayoutGroup), typeof(LayoutElement));
        cardGO.transform.SetParent(parent, false);
        var cardImg = cardGO.GetComponent<Image>();
        cardImg.sprite = RoundedSprite(); cardImg.type = Image.Type.Sliced;
        cardImg.color = UiTheme.Surface;
        cardGO.GetComponent<LayoutElement>().flexibleHeight = 1f; // takes the sheet's spare height (>= 150pt)
        var vlg = cardGO.GetComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset((int)CardPad, (int)CardPad, (int)CardPad, (int)CardPad);
        vlg.spacing = 10f;
        vlg.childControlWidth = true; vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = true;

        var row = new GameObject("Row", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        row.transform.SetParent(cardGO.transform, false);
        var hlg = row.GetComponent<HorizontalLayoutGroup>();
        hlg.spacing = 22f;
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childControlWidth = true; hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = true;

        float coverW = Mathf.Max(80f, (cardWidth - 2f * CardPad) * CoverFraction);
        BuildCover(row.transform, next, coverW);

        var meta = new GameObject("Meta", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
        meta.transform.SetParent(row.transform, false);
        meta.GetComponent<LayoutElement>().flexibleWidth = 1f;
        var mvlg = meta.GetComponent<VerticalLayoutGroup>();
        mvlg.spacing = 6f;
        mvlg.childAlignment = TextAnchor.MiddleLeft;
        mvlg.childControlWidth = true; mvlg.childControlHeight = true;
        mvlg.childForceExpandWidth = true; mvlg.childForceExpandHeight = false;

        var kicker = MakeText(meta.transform, "Kicker", "READ NEXT", 26, TextAlignmentOptions.Left);
        kicker.color = UiTheme.TextSecondary;
        kicker.fontStyle = FontStyles.Bold;
        kicker.gameObject.AddComponent<LayoutElement>().preferredHeight = 34f;

        var title = MakeText(meta.transform, "Title", next.bookName, 44, TextAlignmentOptions.Left);
        title.fontStyle = FontStyles.Bold;
        title.color = UiTheme.TextPrimary;
        title.enableWordWrapping = true;
        title.gameObject.AddComponent<LayoutElement>().preferredHeight = 100f;

        // The go pill is decoration on a card that is itself the button (raycastTarget off, so the
        // tap always lands on the card).
        var pill = new GameObject("GoPill", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        pill.transform.SetParent(meta.transform, false);
        var ple = pill.GetComponent<LayoutElement>();
        ple.preferredHeight = 72f; ple.preferredWidth = 260f; ple.flexibleWidth = 0f;
        var pimg = pill.GetComponent<Image>();
        pimg.sprite = RoundedSprite(); pimg.type = Image.Type.Sliced;
        pimg.color = UiTheme.Primary;
        pimg.raycastTarget = false;
        var pillLabel = MakeText(pill.transform, "Label", "Let's go!", 36, TextAlignmentOptions.Center);
        pillLabel.fontStyle = FontStyles.Bold;
        pillLabel.color = UiTheme.OnPrimary;
        Stretch(pillLabel.rectTransform);

        var accent = new GameObject("Accent", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        accent.transform.SetParent(cardGO.transform, false);
        var ale = accent.GetComponent<LayoutElement>();
        ale.preferredHeight = CardAccentH; ale.flexibleHeight = 0f;
        var aimg = accent.GetComponent<Image>();
        aimg.sprite = RoundedSprite(); aimg.type = Image.Type.Sliced;
        aimg.color = UiTheme.Primary;
        aimg.raycastTarget = false;

        var captured = next;
        cardGO.GetComponent<Button>().onClick.AddListener(() => Globals.GotoPrBook(captured));
    }

    // Cover block: rounded 3:2 art, loaded exactly as the Library and Home cards load covers.
    private void BuildCover(Transform parent, PRBook next, float width)
    {
        var slot = new GameObject("Cover", typeof(RectTransform), typeof(LayoutElement));
        slot.transform.SetParent(parent, false);
        var le = slot.GetComponent<LayoutElement>();
        le.preferredWidth = width; le.minWidth = width; le.flexibleWidth = 0f;

        var frame = new GameObject("Frame", typeof(RectTransform), typeof(Image), typeof(Mask));
        frame.transform.SetParent(slot.transform, false);
        Stretch((RectTransform)frame.transform);
        var frameImg = frame.GetComponent<Image>();
        frameImg.sprite = RoundedSprite(); frameImg.type = Image.Type.Sliced;
        frameImg.color = UiTheme.Track;                 // placeholder tint until the cover lands
        frameImg.raycastTarget = false;
        frame.GetComponent<Mask>().showMaskGraphic = true;

        var artGO = new GameObject("Art", typeof(RectTransform), typeof(Image));
        artGO.transform.SetParent(frame.transform, false);
        Stretch((RectTransform)artGO.transform);
        var art = artGO.GetComponent<Image>();
        art.color = Color.white;
        art.preserveAspect = true;
        art.raycastTarget = false;

        StartCoroutine(LoadCover(next, art, slot));
    }

    // Degraded, never broken: if the cover can't be fetched (PRUtils plants the shared "NoImage"
    // placeholder on failure), drop the picture block and show the book's name large instead.
    private IEnumerator LoadCover(PRBook next, Image art, GameObject slot)
    {
        string url = Globals.WithContentRev(Globals.baseURL + next.bookImageUrl, next.contentRev);
        yield return PRUtils.DownloadImage(url, art, true, true);
        if (art == null || slot == null) yield break;
        if (art.sprite != null && art.sprite != Resources.Load<Sprite>("NoImage")) yield break;

        Debug.LogWarning("ReadNextSheet: cover unavailable (" + url + "); showing the title instead.");
        for (int i = slot.transform.childCount - 1; i >= 0; i--) Destroy(slot.transform.GetChild(i).gameObject);
        var fallback = MakeText(slot.transform, "TitleFallback", next.bookName, 44, TextAlignmentOptions.Center);
        fallback.fontStyle = FontStyles.Bold;
        fallback.color = UiTheme.TextPrimary;
        fallback.enableWordWrapping = true;
        Stretch(fallback.rectTransform);
    }

    // No unread book left on this shelf — celebrate instead of offering nothing (mock's notes).
    private void BuildAllReadState(Transform parent)
    {
        var box = new GameObject("AllRead",
            typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(LayoutElement));
        box.transform.SetParent(parent, false);
        var img = box.GetComponent<Image>();
        img.sprite = RoundedSprite(); img.type = Image.Type.Sliced;
        img.color = UiTheme.Surface;
        box.GetComponent<LayoutElement>().flexibleHeight = 1f;
        var vlg = box.GetComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(24, 24, 24, 24);
        vlg.spacing = 20f;
        vlg.childAlignment = TextAnchor.MiddleCenter;
        vlg.childControlWidth = true; vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;

        var label = MakeText(box.transform, "Label", "You read them all!", 52, TextAlignmentOptions.Center);
        label.fontStyle = FontStyles.Bold;
        label.color = UiTheme.TextPrimary;
        label.gameObject.AddComponent<LayoutElement>().preferredHeight = 90f;

        // A large Home button is the whole action here.
        var row = new GameObject("HomeRow", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        row.transform.SetParent(box.transform, false);
        row.GetComponent<LayoutElement>().preferredHeight = 150f;
        var hlg = row.GetComponent<HorizontalLayoutGroup>();
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childControlWidth = true; hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;
        HomeButton.Create(row.transform, 150f, GoHome);
    }

    // Quiet escapes: Home on the left, "Read again" on the right (centred in the all-read state,
    // where Home already sits in the card).
    private void BuildQuietRow(Transform parent, bool showHome)
    {
        var row = new GameObject("QuietRow", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        row.transform.SetParent(parent, false);
        row.GetComponent<LayoutElement>().preferredHeight = QuietRowH;
        var hlg = row.GetComponent<HorizontalLayoutGroup>();
        hlg.spacing = 16f;
        hlg.childAlignment = showHome ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter;
        hlg.childControlWidth = true; hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;

        if (showHome)
        {
            HomeButton.Create(row.transform, HomeBtnSize, GoHome);
            var spacer = new GameObject("Spacer", typeof(RectTransform), typeof(LayoutElement));
            spacer.transform.SetParent(row.transform, false);
            spacer.GetComponent<LayoutElement>().flexibleWidth = 1f;
        }

        BuildPill(row.transform, "Read again", ReadAgain);
    }

    private void BuildPill(Transform parent, string text, UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject("Pill_" + text,
            typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        var le = go.GetComponent<LayoutElement>();
        le.preferredHeight = HomeBtnSize; le.preferredWidth = 300f; le.flexibleWidth = 0f;
        var img = go.GetComponent<Image>();
        img.sprite = RoundedSprite(); img.type = Image.Type.Sliced;
        img.color = UiTheme.Surface;
        var label = MakeText(go.transform, "Label", text, 32, TextAlignmentOptions.Center);
        label.color = UiTheme.TextSecondary;
        Stretch(label.rectTransform);
        go.GetComponent<Button>().onClick.AddListener(onClick);
    }

    // ---------------------------------------------------------------- actions

    private void GoHome()
    {
        if (_prScript != null) _prScript.Home();
    }

    // The book is already flagged done by SetCurrentStep on the last page, and the resume guard sends
    // done books back to page 1 — so re-opening it IS "read again"; no extra state to reset.
    private void ReadAgain()
    {
        if (Globals.g_prbook != null) Globals.GotoPrBook(Globals.g_prbook);
    }

    // ---------------------------------------------------------------- show / hide

    private void Hide()
    {
        if (!_shown || _sheet == null) return;
        _shown = false;
        _artCatcher.SetActive(false);
        _textCatcher.SetActive(true);
        _sheet.DOKill();
        _sheet.DOAnchorPosY(-_sheetHeight, SlideSec).SetEase(Ease.InCubic).SetUpdate(true);
        if (_reshowCo != null) StopCoroutine(_reshowCo);
        _reshowCo = StartCoroutine(ReshowAfterDelay());
    }

    private void Show()
    {
        if (_shown || _sheet == null) return;
        _shown = true;
        _artCatcher.SetActive(true);
        _textCatcher.SetActive(false);
        if (_reshowCo != null) { StopCoroutine(_reshowCo); _reshowCo = null; }
        _sheet.DOKill();
        _sheet.DOAnchorPosY(0f, SlideSec).SetEase(Ease.OutCubic).SetUpdate(true);
    }

    private IEnumerator ReshowAfterDelay()
    {
        yield return new WaitForSecondsRealtime(ReshowSec);
        _reshowCo = null;
        Show();
    }

    private void OnDestroy()
    {
        if (_sheet != null) _sheet.DOKill();
    }

    // ---------------------------------------------------------------- helpers

    // A transparent full-rect tap target. Alpha 0 still receives raycasts, so it reads taps on the
    // art (dismiss) / on the covered text area (bring back) without drawing anything.
    private GameObject BuildCatcher(string name, Vector2 anchorMin, Vector2 anchorMax,
                                    UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(transform, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        go.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
        go.GetComponent<Button>().onClick.AddListener(onClick);
        return go;
    }

    // The reader's text area, by name (the same object the swipe handlers address as "textforeground").
    private static RectTransform FindTextArea(RectTransform canvasRT)
    {
        foreach (var rt in canvasRT.GetComponentsInChildren<RectTransform>(true))
            if (rt.name.ToLowerInvariant() == "textforeground") return rt;
        return null;
    }

    // target's rect expressed as 0..1 anchors inside the canvas.
    private static Rect NormalizedRect(RectTransform target, RectTransform canvasRT)
    {
        var tc = new Vector3[4]; target.GetWorldCorners(tc);
        var cc = new Vector3[4]; canvasRT.GetWorldCorners(cc);
        float w = cc[2].x - cc[0].x, h = cc[2].y - cc[0].y;
        if (w <= 0f || h <= 0f) return Rect.MinMaxRect(0f, 0f, 1f, 0.42f);
        return Rect.MinMaxRect(
            Mathf.Clamp01((tc[0].x - cc[0].x) / w), Mathf.Clamp01((tc[0].y - cc[0].y) / h),
            Mathf.Clamp01((tc[2].x - cc[0].x) / w), Mathf.Clamp01((tc[2].y - cc[0].y) / h));
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    // Mirrors HomeController.MakeText / UnifiedReadingModePicker.MakeText: TMP label in the shared
    // rounded kid font, auto-sizing down so long book names still fit their box.
    private static TMP_Text MakeText(Transform parent, string name, string text, float size, TextAlignmentOptions align)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.font = UiTheme.Font();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = size * 0.55f;
        tmp.fontSizeMax = size;
        tmp.alignment = align;
        tmp.color = UiTheme.TextPrimary;
        tmp.raycastTarget = false;
        return tmp;
    }

    // Procedural rounded-rect (9-sliced), same construction as the other code-built surfaces.
    private static Sprite _rounded;
    private static Sprite RoundedSprite()
    {
        if (_rounded != null) return _rounded;
        const int r = 30;
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
        _rounded = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f),
            100f, 0, SpriteMeshType.FullRect, new Vector4(r, r, r, r));
        return _rounded;
    }
}
