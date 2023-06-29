using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class UIManager : MonoBehaviour
{
    public RectTransform rectTransformFilter;
    private Vector2 initialRectTransformFilterPosition;
    
    // Start is called before the first frame update
    void Start()
    {
        initialRectTransformFilterPosition = rectTransformFilter.anchoredPosition;
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void MoveIn()
    {
        rectTransformFilter.DOAnchorPos(Vector2.zero, 0.25f);
    }

    public void MoveOut()
    {
        rectTransformFilter.DOAnchorPos(initialRectTransformFilterPosition, 0.25f);
    }

}
