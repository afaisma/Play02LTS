using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class StartSceneCombo : MonoBehaviour
{
    public TMP_Dropdown dropdown;  // Using TMP_Dropdown instead of the standard Dropdown

    
    private Dictionary<string, string> mappingNamesToSceneNames = new Dictionary<string, string>
    {
        { "Map", "_Map" },
        { "Library", "_Library" }
    };

    public string GetSceneNameFromReadableName(string key)
    {
        return mappingNamesToSceneNames.ContainsKey(key) ? mappingNamesToSceneNames[key] : "";
    }

    public string GetReadableNameFromSceneName(string value)
    {
        KeyValuePair<string, string> entry = mappingNamesToSceneNames.FirstOrDefault(pair => pair.Value == value);
        return entry.Equals(default(KeyValuePair<string, string>)) ? "" : entry.Key;
    }
    
    private void Start()
    {
        // Load the previously saved dropdown selection by its string value
        string savedOptionScene = PlayerPrefs.GetString("startSceneName", "");
        string savedOptionReadable = GetReadableNameFromSceneName(savedOptionScene);
        
        if (!string.IsNullOrEmpty(savedOptionReadable))
        {
            int savedIndex = dropdown.options.FindIndex(option => option.text == savedOptionReadable);
            if (savedIndex >= 0)
            {
                dropdown.value = savedIndex;
            }
        }

        dropdown.onValueChanged.AddListener(delegate {
            DropdownValueChanged(dropdown);
        });
    }

    void DropdownValueChanged(TMP_Dropdown change)
    {
        string selectedOptionReadable = change.options[change.value].text;
        string selectedOptionSceneName = GetSceneNameFromReadableName(selectedOptionReadable);
        PlayerPrefs.SetString("startSceneName", selectedOptionSceneName);
        PlayerPrefs.Save();
    }
}
