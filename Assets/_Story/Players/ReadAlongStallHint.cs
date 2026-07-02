using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

// Reader-side responder for ReadAlongService's stall hint. Attached to the _Story Next arrow (btnNext):
// when read-along reports the page has stalled (~5s with no advance, not complete), it gently pulses
// this arrow and shows a quiet "Tap to turn the page" caption just above it. Both clear on the next
// advance / page change (StallHintCleared). Calm by design — slow yoyo scale, UiTheme.Surface pill,
// no alarm. Finds the persistent ReadAlongService at runtime (mirrors how the picker wires up).
public class ReadAlongStallHint : MonoBehaviour
{
    private const string CaptionText = "Tap to turn the page";

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

        _pulse = _arrow.DOScale(_baseScale * 1.12f, 0.7f)
            .SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetUpdate(true);
        ShowCaption(true);
    }

    private void OnStallCleared() => StopHint();

    private void StopHint()
    {
        if (_pulse != null) { _pulse.Kill(); _pulse = null; _arrow.localScale = _baseScale; }
        ShowCaption(false);
    }

    private void ShowCaption(bool show)
    {
        if (show && _caption == null) BuildCaption();
        if (_caption == null) return;
        if (show) PositionCaption();
        _caption.SetActive(show);
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
    // own anchoring / layout), in the shared overlay canvas's pixel space.
    private void PositionCaption()
    {
        if (_captionRt == null) return;
        var corners = new Vector3[4];
        _arrow.GetWorldCorners(corners);
        Vector3 topCenter = (corners[1] + corners[2]) * 0.5f; // mid of the arrow's top edge
        _captionRt.position = topCenter + new Vector3(0f, 16f, 0f);
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
