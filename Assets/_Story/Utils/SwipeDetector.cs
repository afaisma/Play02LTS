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
                            PointerEventData ped = new PointerEventData(null);
                            ped.position = touch.position;
                            EventSystem.current.RaycastAll(ped, results);

                            foreach (RaycastResult result in results)
                            {
                                SwipeableObject swipeable = result.gameObject.GetComponent<SwipeableObject>();
                                if (swipeable != null)
                                {
                                    if (swipeDirection.x > 0)
                                    {
                                        prScript.RightSwipe(swipeable);
                                    }
                                    else
                                    {
                                        prScript.LeftSwipe(swipeable);
                                    }
                                }
                                // if (swipeable != null)
                                // {
                                //     if (swipeDirection.x > 0)
                                //     {
                                //         swipeable.OnSwipeRight();
                                //     }
                                //     else
                                //     {
                                //         swipeable.OnSwipeLeft();
                                //     }
                                // }
                            }
                        }
                    }
                    break;
            }
        }
    }
}
