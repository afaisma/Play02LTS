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
    // Full-screen dim created by RateAppPanelStyle next to (not inside) the sliding panel, so it
    // stays put while the card moves. Tapping it closes down the same RateLater path as the X.
    private GameObject backdrop;
    
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
        backdrop = RateAppPanelStyle.Apply(rectTransformFilter, rateTheApp);
        if (backdrop != null) backdrop.SetActive(true);
        rateTheApp.RateApplication(0);
        rectTransformFilter.DOAnchorPos(Vector2.zero, 0.35f);
        bIn = true;
    }

    public void MoveOut()
    {
        Debug.Log("MoveOut");
        // Drop the dim immediately — the card takes another 0.25s + 1s to slide away, and taps
        // must not stay blocked for that whole time.
        if (backdrop != null) backdrop.SetActive(false);
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
