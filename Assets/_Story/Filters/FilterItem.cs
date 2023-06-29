using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class FilterItem : MonoBehaviour
{
    public FilterContainer filterContainer;
    public string filter;
    private void Start()
    {
        // Get the Toggle component and add a listener for the onValueChanged event
        Toggle toggle = GetComponent<Toggle>();
        toggle.onValueChanged.AddListener((isOn) => 
        {
            filterContainer.OnToggleValueChanged(isOn, toggle);
        });
    }

}
