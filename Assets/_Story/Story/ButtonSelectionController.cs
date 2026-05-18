using QFSW.QC;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class ButtonSelectionController : MonoBehaviour
{
    public Button[] buttons; // Array to hold references to all buttons
    private Button _currentlySelected; // Holds the currently selected button
    public Color selectedColor = new Color(1f, 1f, 1f, 1f); // Selected button color
    public Color unselectedColor = new Color(0.7f, 0.7f, 0.7f, 0.7f); // Unselected button color
    public Button buttonToggle;
    
    public UnityEvent<string> OnSelectionChanged; // UnityEvent for notifying listeners
    public UnityEvent<string> OnSomethingClicked; // UnityEvent for notifying listeners

    private void Start()
    {
        if (buttons == null || buttons.Length == 0)
        {
            Debug.LogWarning("Button array is not properly set up.");
            return;
        }

        foreach (Button button in buttons)
        {
            string buttonName = button.name.ToLower();
            button.onClick.AddListener(() => NotifyButtonSelected(buttonName, button));
        }

    }

    private void OnDestroy()
    {
        // L-R3-6: guard against `buttons` being null (Inspector never wired).
        if (buttons == null) return;
        foreach (Button button in buttons)
        {
            if (button != null)
                button.onClick.RemoveAllListeners();
        }
    }


    private void NotifyButtonSelected(string buttonName, Button button)
    {
        OnButtonClicked(button, buttonName);
    }

    private void OnButtonClicked(Button button, string buttonName)
    {
        Debug.Log("Button clicked: " + button.name);
        OnSomethingClicked?.Invoke(buttonName);

        // if (_currentlySelected == button)
        // {
        //     return; // Already selected
        // }

        // H-R3-3: null-guard the debug log. _currentlySelected is set just below,
        // so it's null on the very first call (before ButtonOptions has run).
        Debug.Log("***2222 currently selected button: " + (_currentlySelected != null ? _currentlySelected.name : "<none>"));
        _currentlySelected = button;
        foreach (Button b in buttons)
        {
            SetButtonColor(b, b == _currentlySelected);
        }

        // Update the image of buttonToggle to match the selected button's image
        if (buttonToggle != null && _currentlySelected != null && _currentlySelected.image != null)
        {
            buttonToggle.image.sprite = _currentlySelected.image.sprite;
            Debug.Log("ButtonTogglePanel image set to the image of: " + _currentlySelected.name);
        }

        OnSelectionChanged?.Invoke(buttonName);
    }

    private void SetButtonColor(Button button, bool isSelected)
    {
        Image buttonImage = button.image;
        if (buttonImage != null)
        {
            buttonImage.color = isSelected ? selectedColor : unselectedColor;
        }
    }

    [Command]
    public void ButtonOptions(bool[] visibilityStates)
    {
        if (visibilityStates.Length != buttons.Length)
        {
            Debug.LogWarning("Mismatch between visibility states and buttons array length.");
            return;
        }

        int firstVisibleButton = -1;
        for (int i = 0; i < buttons.Length; i++)
        {
            buttons[i].gameObject.SetActive(visibilityStates[i]);
            if (visibilityStates[i] && (firstVisibleButton == -1))
            {
                // OnButtonClicked(buttons[i], buttons[i].name.ToLower());
                firstVisibleButton = i;
            }
        }

        if (firstVisibleButton != -1)
        {
            _currentlySelected = buttons[firstVisibleButton];
            OnButtonClicked(buttons[firstVisibleButton], buttons[firstVisibleButton].name.ToLower());
            Debug.Log("***111 currently selected button: " + buttons[firstVisibleButton].name);
        }
    }

    public string GetSelectedButtonName()
    {
        return _currentlySelected != null ? _currentlySelected.name.ToLower() : "";
    }
}
