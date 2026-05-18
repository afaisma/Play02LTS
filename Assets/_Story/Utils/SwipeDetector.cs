using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;

public class SwipeDetector : MonoBehaviour
{
    private Vector2 startPos;
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
                    break;

                case TouchPhase.Ended:
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
}
