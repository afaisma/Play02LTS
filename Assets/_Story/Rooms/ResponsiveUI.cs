using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine;

public class ResponsiveUI : MonoBehaviour
{
    void Start()
    {
        RectTransform rect = GetComponent<RectTransform>();
        float aspectRatio = (float)Screen.width / Screen.height;

        // Adjust size and position based on aspect ratio
        if (aspectRatio < 0.5)  // More tall
        {
            rect.sizeDelta = new Vector2(rect.sizeDelta.x * 0.8f, rect.sizeDelta.y * 0.8f);
        }
        else if (aspectRatio > 0.75) // More wide
        {
            rect.sizeDelta = new Vector2(rect.sizeDelta.x * 1.2f, rect.sizeDelta.y * 1.2f);
        }
    }
}
