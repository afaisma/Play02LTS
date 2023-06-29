using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class BlinkingOutlineHint : MonoBehaviour
{
    public Outline outline; // Reference to the Outline component
    public float blinkDuration = 0.7f; // Duration of each blink in seconds
    public float totalDuration = 7f; // Total duration of the blinking effect
    public string hintId = "changeMe";
    public int hintTotalTimes = 5;
    public int hintTimes = 0;
    public float delayBeforeStart = 5f; // Delay before the blinking starts
    private Coroutine blinkCoroutine = null;

    // Use this for initialization
    void Start()
    {
        if (outline == null) // If no Outline component is attached, try getting it
        {
            outline = GetComponent<Outline>();
        }

        hintTimes = PlayerPrefs.GetInt(hintId, 0);

        if (outline != null && hintTimes < hintTotalTimes) // If there is an Outline component, start blinking
        {
            blinkCoroutine = StartCoroutine(BlinkOutline());
            hintTimes++;
            PlayerPrefs.SetInt(hintId, hintTimes);
        }
        else
        {
            //Debug.LogError("No Outline component found on this object.");
        }
       
    }

    IEnumerator BlinkOutline()
    {
        yield return new WaitForSeconds(delayBeforeStart);

        float blinkEnd = Time.time + totalDuration; // Calculate when to end blinking
        bool active = false; // Is the outline currently active?

        while (Time.time < blinkEnd)
        {
            active = !active; // Flip the active state
            outline.enabled = active; // Set the outline's active state

            // Wait for the blink duration then continue the loop
            yield return new WaitForSeconds(blinkDuration);
        }

        // Ensure the outline is disabled at the end
        outline.enabled = false;
    }
    
    public void StopBlinking()
    {
        if (blinkCoroutine != null)
        {
            StopCoroutine(blinkCoroutine);
            blinkCoroutine = null;
        }
        outline.enabled = false;
    }
}