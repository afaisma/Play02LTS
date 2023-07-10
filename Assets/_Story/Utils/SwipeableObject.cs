using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SwipeableObject : MonoBehaviour
{
    public void OnSwipeLeft()
    {
        Debug.Log("SwipeableObject::OnSwipeLeft");
    }

    public void OnSwipeRight()
    {
        Debug.Log("SwipeableObject::OnSwipeRight");
    }
}
