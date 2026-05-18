using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;


public class MovingVoiceOptionsPanel : MonoBehaviour
{
    public ButtonSelectionController buttonSelectionController;
    public RectTransform rectTransformFilter;
    public string currentFilter = "";

    private Vector2 initialRectTransformFilterPosition;
    private bool bIn;
    
    void Start()
    {
        initialRectTransformFilterPosition = rectTransformFilter.anchoredPosition;
        if (buttonSelectionController != null)
        {
            buttonSelectionController.OnSomethingClicked.AddListener(OnSomethingClickedHandler);
        }
    }

    private void OnSomethingClickedHandler(string selectedValue)
    {
        //MoveOut();
    }
    
    private void OnDestroy()
    {
        if (buttonSelectionController != null)
        {
            buttonSelectionController.OnSomethingClicked.RemoveListener(OnSomethingClickedHandler);
        }
    }
    
    public void MoveIn()
    {
        rectTransformFilter.DOAnchorPos(Vector2.zero, 0.35f);
        bIn = true;
    }

    public void MoveOut()
    {
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
