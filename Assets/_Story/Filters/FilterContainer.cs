using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class FilterContainer : MonoBehaviour
{
    public List<string> filtersSelected = new List<string>();
    public BooksScrollView booksScrollView;
    
    public void OnToggleValueChanged(bool isOn, Toggle toggle)
    {
        // Access the FilterItem component on the same GameObject as the Toggle
        FilterItem filterItem = toggle.GetComponent<FilterItem>();

        // If the filterItem component exists
        if (filterItem != null)
        {
            // If the toggle is turned on, add the filter to the list of selected filters
            if (isOn && !filtersSelected.Contains(filterItem.filter))
            {
                filtersSelected.Add(filterItem.filter);
            }
            // If the toggle is turned off, remove the filter from the list of selected filters
            else
            {
                filtersSelected.Remove(filterItem.filter);
            }
        }
        Debug.Log(string.Join(", ", filtersSelected));
    }
}
