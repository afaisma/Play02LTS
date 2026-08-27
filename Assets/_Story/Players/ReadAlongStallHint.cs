using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

// Reader-side responder for ReadAlongService's stall hint. Attached to the _Story Next arrow (btnNext):
// when read-along reports the page has stalled (~5s with no advance, not complete), it gently pulses
// this arrow and — the FIRST TIME EVER on this device — shows a quiet "Turn page" caption just above
// it. Both clear on the next advance / page change (StallHintCleared). Calm by design — slow yoyo
// scale, UiTheme.Surface pill, no alarm. Finds the persistent ReadAlongService at runtime (mirrors
// how the picker wires up).
public class ReadAlongStallHint : MonoBehaviour
{
    private const string CaptionText = "Turn page";

    // Once-ever flag for the CAPTION only (see OnStallHint for why the pulse is not gated).
    public const string CaptionShownKey = "read_along_caption_shown";

    private const float CaptionGap = 16f;    // px above the arrow's top edge
    private const float CaptionMargin = 12f; // px of canvas kept clear either side of the pill

    private RectTransform _arrow;
    private Vector3 _baseScale;
    private ReadAlongService _service;
    private Tween _pulse;
    private GameObject _caption;
    private RectTransform _captionRt;

    private void Awake()
    {
        _arrow = GetComponent<RectTransform>();
        _baseScale = _arrow.localScale;
    }

    private void Update()
    {
        if (_service != null) return; // already wired
        _service = FindObjectOfType<ReadAlongService>();
        if (_service != null)
        {
            _service.StallHint += OnStallHint;
            _service.StallHintCleared += OnStallCleared;
        }
    }

    private void OnDisable() => StopHint(); // scene change / arrow hidden — never leave it pulsing

    private void OnDestroy()
    {
        if (_service != null)
        {
            _service.StallHint -= OnStallHint;
            _service.StallHintCleared -= OnStallCleared;
        }
        StopHint();
    }

    private void OnStallHint()
    {
        if (!isActiveAndEnabled) return; // arrow hidden (e.g. last page) — nothing to point at
        if (_pulse != null) return;      // already hinting

        // The PULSE is not gated: a wordless "look here" is the aid a pre-reader can actually use,
        // and it costs nothing to repeat, so it fires on every stall for the life of the app.
        _pulse = _arrow.DOScale(_baseScale * 1.12f, 0.7f)
            .SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetUpdate(true);

        // The CAPTION is once-ever: it is written for a first-time grown-up, who reads it once and
        // then knows. Re-showing it every page is what testers saw as nagging.
        if (CaptionPending() && ShowCaption(true)) MarkCaptionShown();
    }

    /// <summary>True until the caption has been shown once on this device.</summary>
    public static bool CaptionPending() => PlayerPrefs.GetInt(CaptionShownKey, 0) == 0;

    /// <summary>Record that the caption was actually displayed, retiring it for good.</summary>
    public static void MarkCaptionShown()
    {
        PlayerPrefs.SetInt(CaptionShownKey, 1);
        PlayerPrefs.Save();
    }

    private void OnStallCleared() => StopHint();

    private void StopHint()
    {
        if (_pulse != null) { _pulse.Kill(); _pulse = null; _arrow.localScale = _baseScale; }
        ShowCaption(false);
    }

    // Returns true only when the caption is actually on screen afterwards — the caller uses that to
    // decide whether the once-ever flag has been earned (a missing canvas must not burn it).
    private bool ShowCaption(bool show)
    {
        if (show && _caption == null) BuildCaption();
        if (_caption == null) return false;
        _caption.SetActive(show);
        if (!show) return false;
        // Activate BEFORE measuring: the pill's ContentSizeFitter only resolves a width once the
        // object is active, and PositionCaption needs that width to clamp against the canvas edge.
        Canvas.ForceUpdateCanvases();
        PositionCaption();
        return true;
    }

    // A quiet rounded UiTheme.Surface pill with secondary-text caption. Non-blocking (no raycasts).
    private void BuildCaption()
    {
        var canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return;

        _caption = new GameObject("ReadAlongStallCaption",
            typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
        _caption.transform.SetParent(canvas.transform, false);
        _captionRt = _caption.GetComponent<RectTransform>();
        _captionRt.pivot = new Vector2(0.5f, 0f);

        var img = _caption.GetComponent<Image>();
        img.sprite = RoundedSprite(); img.type = Image.Type.Sliced;
        img.color = UiTheme.Surface;
        img.raycastTarget = false;

        var hlg = _caption.GetComponent<HorizontalLayoutGroup>();
        hlg.padding = new RectOffset(22, 22, 12, 12);
        hlg.childControlWidth = true; hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;
        var fit = _caption.GetComponent<ContentSizeFitter>();
        fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var labelGO = new GameObject("Label", typeof(RectTransform));
        labelGO.transform.SetParent(_caption.transform, false);
        var tmp = labelGO.AddComponent<TextMeshProUGUI>();
        tmp.font = UiTheme.Font();
        tmp.text = CaptionText;
        tmp.fontSize = 30;
        tmp.color = UiTheme.TextSecondary;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
    }

    // Place the caption just above the arrow, exact relative to it (works regardless of the toolbar's
    // own anchoring / layout), in the shared overlay canvas's pixel space — then pull it back inside
    // the canvas. The arrow sits near the screen edge, so a pill centred on it hangs off-screen.
    private void PositionCaption()
    {
        if (_captionRt == null) return;
        var corners = new Vector3[4];
        _arrow.GetWorldCorners(corners);
        Vector3 topCenter = (corners[1] + corners[2]) * 0.5f; // mid of the arrow's top edge
        Vector3 target = topCenter + new Vector3(0f, CaptionGap, 0f);

        // Clamp in the canvas's own local space, where x runs -w/2..+w/2 about its centre; the
        // helper works in 0..w from the left edge, so shift in and back out.
        var canvasRt = _captionRt.parent as RectTransform;
        if (canvasRt != null)
        {
            float canvasWidth = canvasRt.rect.width;
            Vector3 local = canvasRt.InverseTransformPoint(target);
            float centerX = ClampCenterX(local.x + canvasWidth * 0.5f,
                                         _captionRt.rect.width, canvasWidth, CaptionMargin);
            local.x = centerX - canvasWidth * 0.5f;
            target = canvasRt.TransformPoint(local);
        }
        _captionRt.position = target;
    }

    /// <summary>
    /// Keep a pill of `pillWidth` fully inside a `canvasWidth`-wide canvas with `margin` px to
    /// spare, given its desired centre. A pill too wide to fit is centred rather than pinned to
    /// one edge (and never returns NaN).
    /// </summary>
    public static float ClampCenterX(float centerX, float pillWidth, float canvasWidth, float margin)
    {
        float half = pillWidth * 0.5f;
        float min = margin + half;
        float max = canvasWidth - margin - half;
        if (min > max) return canvasWidth * 0.5f;
        return Mathf.Clamp(centerX, min, max);
    }

    // Procedural rounded-rect (9-sliced) sprite for the pill, matching the project's other code-built UI.
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
