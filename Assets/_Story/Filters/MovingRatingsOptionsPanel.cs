using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;


public class MovingRatingsOptionsPanel : MonoBehaviour
{
    public RectTransform rectTransformFilter;
    public RateTheApp rateTheApp;
    private Vector2 initialRectTransformFilterPosition;
    private bool bIn;
    
    void Start()
    {
        initialRectTransformFilterPosition = rectTransformFilter.anchoredPosition;
    }

    private void OnSomethingClickedHandler(string selectedValue)
    {
        //MoveOut();
    }
    
    private void OnDestroy()
    {
    }
    
    public void MoveIn()
    {
        Debug.Log("MoveIn");
        rateTheApp.RateApplication(0);
        rectTransformFilter.DOAnchorPos(Vector2.zero, 0.35f);
        bIn = true;
    }

    public void MoveOut()
    {
        Debug.Log("MoveOut");
        Invoke("_MoveOut", 0.25f); 
    }

    public void _MoveOut()
    {
        rectTransformFilter.DOAnchorPos(initialRectTransformFilterPosition, 1f);
        bIn = false;
    }

    public void ToggleVisibility()
    {
        if (bIn)
        {
            MoveOut();
        }
        else
        {
            MoveIn();
        }        
    }
}
