using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class FilterContainer : MonoBehaviour
{
    public BooksScrollView booksScrollView;
    public RectTransform rectTransformFilter;
    public string currentFilter = "";
    public PRLibrary prLibrary;

    private Vector2 initialRectTransformFilterPosition;
    private bool bIn;
    
    void Start()
    {
        initialRectTransformFilterPosition = rectTransformFilter.anchoredPosition;
    }

    public void OnToggleValueChanged(bool isOn, Toggle toggle)
    {
        // Access the FilterItem component on the same GameObject as the Toggle
        FilterItem filterItem = toggle.GetComponent<FilterItem>();

        // If the filterItem component exists
        if (filterItem != null)
        {
            // If the toggle is turned on, add the filter to the list of selected filters
            //if (isOn && currentFilter != filterItem.filter)
            if (currentFilter != filterItem.filter.ToLower())
            {
                //currentFilter = filterItem.filter.ToLower();
                //OnFilterChanged();
                prLibrary.SetFilter(filterItem.filter.ToLower());
            }
        }
        
        MoveOut();
    }
    
    public void _SetFilter(string filter)
    {
        booksScrollView.ResetScrollPosition();
        currentFilter = filter;
        OnFilterChanged();
    }

    private void OnFilterChanged()
    {
        if (booksScrollView == null)
            return;
        
        // Check if any category in bookCategories has a matching filter (Settings part of the tuple)
        bool filterExists = PRLibrary.bookCategories.Exists(category => category.Settings == currentFilter);
        
        if (filterExists)
        {
            booksScrollView.SetFilter(0, 0, currentFilter);
        }
        else if (currentFilter == "2-3 years")
        {
            booksScrollView.SetFilter(2, 3, "");
        }
        else if (currentFilter == "3-5 years")
        {
            booksScrollView.SetFilter(3, 5, "");
        }
        else if (currentFilter == "4-7 years")
        {
            booksScrollView.SetFilter(4, 7, "");
        }
        else if (currentFilter == "5-10 years")
        {
            booksScrollView.SetFilter(5, 10, "");
        }
        else if (currentFilter == "everything")
        {
            booksScrollView.SetFilter(0, 0, "");
        }
    }
    
    public void MoveIn()
    {
        rectTransformFilter.DOAnchorPos(Vector2.zero, 0.35f);
        bIn = true;
    }

    public void MoveOut()
    {
        Invoke("_MoveOut", 0.5f); 
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
