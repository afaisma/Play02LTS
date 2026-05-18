using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class AutoplayToggle : MonoBehaviour
{
    public Toggle toggle;
    public Button buttonNext;
    public Image backgroundImage;
    // H-R3-1: use Color32 (byte 0-255) — the original Color(255,255,255,128)
    // syntax was wrong because UnityEngine.Color takes float 0-1, so both
    // states rendered as opaque white. Color32 implicitly converts to Color.
    // L-R3-1: removed the duplicate `using UnityEngine;` line.
    public Color uncheckedColor = new Color32(255, 255, 255, 128);
    public Color checkedColor   = new Color32(100, 255, 100, 128);
    public TMPro.TextMeshProUGUI textLabel;

    void Start()
    {
        // Set the initial color based on the current state of the toggle
        UpdateColorAndLabel(toggle.isOn);
        
        // Add a listener to detect changes in the toggle's state
        toggle.onValueChanged.AddListener(delegate {
            UpdateColorAndLabel(toggle.isOn);
        });
    }

    void UpdateColorAndLabel(bool isChecked)
    {
        // Change the background color based on whether the toggle is checked
        backgroundImage.color = isChecked ? checkedColor : uncheckedColor;
        if (buttonNext != null)
        {
            buttonNext.image.color = isChecked ? checkedColor : uncheckedColor;
        }
        
        // Change the label text based on whether the toggle is checked
        textLabel.text = isChecked ? "Autopage On" : "Autopage Off";
    }
}