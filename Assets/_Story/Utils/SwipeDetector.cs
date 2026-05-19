using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;

public class SwipeDetector : MonoBehaviour
{
    private Vector2 startPos;
    private bool    _startedOnDraggableOverlay;
    private const float minSwipeDist = 50.0f;
    public PRScript prScript;

    public void Start()
    {
        prScript  = gameObject.GetComponent<PRScript>();
    }
    void Update()
    {
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);

            switch (touch.phase)
            {
                case TouchPhase.Began:
                    startPos = touch.position;
                    // If the touch landed on a draggable overlay, the
                    // overlay's IDragHandler owns this gesture — don't
                    // treat its motion as a page-turn swipe. We check at
                    // start (not end) because a successful drag carries
                    // the overlay along with the pointer, so the end-
                    // position raycast would still land on the overlay
                    // and we'd want to suppress the swipe; but we also
                    // want to allow swipes that *originate* on the page
                    // picture and happen to pass through an overlay rect.
                    _startedOnDraggableOverlay = IsOverDraggableOverlay(touch.position);
                    break;

                case TouchPhase.Ended:
                    if (_startedOnDraggableOverlay)
                    {
                        _startedOnDraggableOverlay = false;
                        break;
                    }
                    float swipeDist = (touch.position - startPos).magnitude;

                    if (swipeDist > minSwipeDist)
                    {
                        Vector2 swipeDirection = touch.position - startPos;

                        if (Mathf.Abs(swipeDirection.x) > Mathf.Abs(swipeDirection.y))
                        {
                            // Get the UI objects under the end of the swipe
                            List<RaycastResult> results = new List<RaycastResult>();
                            // L-R2-4: pass the actual EventSystem to avoid the
                            // "null EventSystem" warning on modern Unity.
                            PointerEventData ped = new PointerEventData(EventSystem.current);
                            ped.position = touch.position;
                            EventSystem.current.RaycastAll(ped, results);

                            // H-R2-1: only honor the topmost SwipeableObject under
                            // the touch. RaycastAll returns the full stack, and
                            // without this break a swipe over overlapping
                            // swipeable areas (gallery + textforeground) would
                            // advance the page twice.
                            foreach (RaycastResult result in results)
                            {
                                SwipeableObject swipeable = result.gameObject.GetComponent<SwipeableObject>();
                                if (swipeable == null)
                                    continue;

                                if (swipeDirection.x > 0)
                                    prScript.RightSwipe(swipeable);
                                else
                                    prScript.LeftSwipe(swipeable);

                                break;
                            }
                        }
                    }
                    break;
            }
        }
    }

    /// <summary>True if a draggable overlay (OverlayDragHandler) sits under
    /// the given screen position. Used to suppress page-turn swipes that
    /// originate on a draggable element.</summary>
    private bool IsOverDraggableOverlay(Vector2 screenPos)
    {
        if (EventSystem.current == null) return false;
        var ped = new PointerEventData(EventSystem.current) { position = screenPos };
        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(ped, results);
        foreach (var r in results)
        {
            if (r.gameObject == null) continue;
            if (r.gameObject.GetComponent<OverlayDragHandler>() != null
                || r.gameObject.GetComponentInParent<OverlayDragHandler>() != null)
            {
                return true;
            }
        }
        return false;
    }
}
