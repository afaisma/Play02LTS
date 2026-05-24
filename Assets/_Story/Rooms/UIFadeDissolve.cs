using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class UIFadeDissolve : MonoBehaviour
{
    public Graphic uiElement; // This can be any component that inherits from Graphic (e.g., Image, Text)
    public float fadeSpeed = 0.5f;
    public float minTime = 3f;
    public float maxTime = 8f;
    public float minAlpha = 0.7f;  // Minimum alpha value to which the UI element can fade

    private Coroutine fadeOutCoroutine;
    private Coroutine fadeInCoroutine;

    void Start()
    {
        if (uiElement == null)
        {
            Debug.LogError("UIFadeDissolve::Start -- No UI element assigned!");
            return;
        }
        StartCoroutine(ToggleFadeRoutine());
    }

    IEnumerator ToggleFadeRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(Random.Range(minTime, maxTime));

            if (fadeOutCoroutine != null)
            {
                StopCoroutine(fadeOutCoroutine);
                fadeOutCoroutine = null;
                FadeIn();
            }
            else if (fadeInCoroutine != null)
            {
                StopCoroutine(fadeInCoroutine);
                fadeInCoroutine = null;
                FadeOut();
            }
            else if (uiElement.canvasRenderer.GetAlpha() <= minAlpha + 0.1f)  // Using a threshold above minAlpha for switching
                FadeIn();
            else
                FadeOut();
        }
    }

    public IEnumerator FadeOutCoroutine()
    {
        while (uiElement.canvasRenderer.GetAlpha() > minAlpha)
        {
            uiElement.canvasRenderer.SetAlpha(uiElement.canvasRenderer.GetAlpha() - Time.deltaTime * fadeSpeed);
            yield return null;
        }
        uiElement.canvasRenderer.SetAlpha(minAlpha);  // Ensures alpha doesn't drop below minAlpha
    }

    public IEnumerator FadeInCoroutine()
    {
        while (uiElement.canvasRenderer.GetAlpha() < 1)
        {
            uiElement.canvasRenderer.SetAlpha(uiElement.canvasRenderer.GetAlpha() + Time.deltaTime * fadeSpeed);
            yield return null;
        }
    }
    
    public void FadeOut()
    {
        if (fadeInCoroutine != null)
        {
            StopCoroutine(fadeInCoroutine);
            fadeInCoroutine = null;
        }
        fadeOutCoroutine = StartCoroutine(FadeOutCoroutine());
    }
    
    public void FadeIn()
    {
        if (fadeOutCoroutine != null)
        {
            StopCoroutine(fadeOutCoroutine);
            fadeOutCoroutine = null;
        }
        fadeInCoroutine = StartCoroutine(FadeInCoroutine());
    }
}
