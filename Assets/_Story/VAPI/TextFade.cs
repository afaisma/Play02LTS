using UnityEngine;
using TMPro;
using System.Collections;

public class TextFade : MonoBehaviour
{
    public TextMeshPro textMesh;
    public float duration = 2.0f; // Duration of the fade

    private void Start()
    {
        textMesh.color = new Color(textMesh.color.r, textMesh.color.g, textMesh.color.b, 1); // Start fully visible
        StartCoroutine(FadeTextToZeroAlpha());  // Start by fading out
    }

    public IEnumerator FadeTextToZeroAlpha()
    {
        while (textMesh.color.a > 0.0f)
        {
            textMesh.color = new Color(textMesh.color.r, textMesh.color.g, textMesh.color.b, textMesh.color.a - (Time.deltaTime / duration));
            yield return null;
        }
        StartCoroutine(FadeTextToFullAlpha());
    }

    public IEnumerator FadeTextToFullAlpha()
    {
        while (textMesh.color.a < 1.0f)
        {
            textMesh.color = new Color(textMesh.color.r, textMesh.color.g, textMesh.color.b, textMesh.color.a + (Time.deltaTime / duration));
            yield return null;
        }
        StartCoroutine(FadeTextToZeroAlpha());
    }
}
