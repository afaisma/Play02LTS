using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Attach to an overlay GameObject (picture, video, or sprites) to make it
/// draggable by touch / mouse. Translates the parent RectTransform's
/// anchored position by the pointer delta on each OnDrag tick, then clamps
/// the overlay rect to stay inside the parent's world rect (the picture
/// area, since overlays are parented under Gallery).
///
/// Unity's EventSystem suppresses <c>Button.onClick</c> automatically when a
/// drag exceeds <see cref="EventSystem.pixelDragThreshold"/>, so small taps
/// still trigger the overlay's existing pause/resume tap behaviour and only
/// real drags move the overlay. No extra logic is needed for tap-vs-drag
/// disambiguation.
///
/// Add via <c>gameObject.AddComponent&lt;OverlayDragHandler&gt;()</c>;
/// remove via <c>Destroy(GetComponent&lt;OverlayDragHandler&gt;())</c>.
/// Gallery.SetOverlayProperty("draggable", 0/1) does both.
/// </summary>
public class OverlayDragHandler : MonoBehaviour, IDragHandler
{
    /// <summary>Keep the overlay inside the parent rect. Default true.</summary>
    public bool clampToParent = true;

    private RectTransform _rt;
    private RectTransform _parentRt;
    private Canvas _canvas;

    void Awake()
    {
        _rt       = GetComponent<RectTransform>();
        _parentRt = _rt != null ? _rt.parent as RectTransform : null;
        _canvas   = GetComponentInParent<Canvas>();
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (_rt == null || _parentRt == null) return;

        // eventData.delta is in screen pixels; anchoredPosition lives in the
        // canvas's local units. The Canvas Scaler's scaleFactor is the ratio.
        // Screen Space Overlay canvases set scaleFactor based on reference
        // resolution; Camera-space canvases also expose scaleFactor.
        float scale = (_canvas != null && _canvas.scaleFactor > 0f)
            ? _canvas.scaleFactor
            : 1f;
        _rt.anchoredPosition += eventData.delta / scale;

        if (clampToParent) ClampToParent();
    }

    private void ClampToParent()
    {
        // Using world corners makes the clamp anchor-config-agnostic: it
        // works for stretched anchors (our overlay setup), point anchors,
        // or any combination thereof.
        var rtCorners = new Vector3[4];
        var parentCorners = new Vector3[4];
        _rt.GetWorldCorners(rtCorners);
        _parentRt.GetWorldCorners(parentCorners);

        // GetWorldCorners returns: 0 = BL, 1 = TL, 2 = TR, 3 = BR.
        Vector3 rtBL = rtCorners[0], rtTR = rtCorners[2];
        Vector3 pBL  = parentCorners[0], pTR = parentCorners[2];

        float dx = 0f, dy = 0f;
        if (rtBL.x < pBL.x) dx = pBL.x - rtBL.x;
        else if (rtTR.x > pTR.x) dx = pTR.x - rtTR.x;
        if (rtBL.y < pBL.y) dy = pBL.y - rtBL.y;
        else if (rtTR.y > pTR.y) dy = pTR.y - rtTR.y;

        if (dx != 0f || dy != 0f)
            _rt.position += new Vector3(dx, dy, 0f);
    }
}
