using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ButtonController : MonoBehaviour
{
    // List of buttons
    public List<Button> buttons;

    // Method to disable buttons
    public void DisableButtons()
    {
        foreach (Button button in buttons)
        {
            button.interactable = false;
        }
        Debug.Log("DisableButtons");
    }

    // Method to disable buttons for a certain time
    public void DisableButtonsForTime(float timeInSec)
    {
        StartCoroutine(DisableButtonsCoroutine(timeInSec));
        Debug.Log("DisableButtonsForTime " + timeInSec);
    }

    // Coroutine to disable buttons and then enable them after a delay
    private IEnumerator DisableButtonsCoroutine(float timeInSec)
    {
        DisableButtons();
        yield return new WaitForSeconds(timeInSec);
        EnableButtons();
    }

    // Method to enable buttons
    public void EnableButtons()
    {
        foreach (Button button in buttons)
        {
            button.interactable = true;
        }
        Debug.Log("EnableButtons");
    }
}