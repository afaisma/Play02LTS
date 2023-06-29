using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AdjustImageToSafeArea : MonoBehaviour
{
    private RectTransform iamgeAndtextTransform;
    
    private void Start()
    {
        iamgeAndtextTransform = GetComponent<RectTransform>();
        ApplySafeArea();
    }

    private void ApplySafeArea()
    {
        Vector2 anchorMinOld = iamgeAndtextTransform.anchorMin;
        Vector2 anchorMaxOld = iamgeAndtextTransform.anchorMax;
        
        Rect safeArea = Screen.safeArea;
        Vector2 anchorMin = safeArea.position;
        Vector2 anchorMax = safeArea.position + safeArea.size;
        anchorMin.x = 0; ///= Screen.width;
        anchorMin.y = 0; //Screen.height; //anchorMinOld.y;
        anchorMax.x /= Screen.width;
        anchorMax.y /= Screen.height;
        iamgeAndtextTransform.anchorMin = anchorMin;
        iamgeAndtextTransform.anchorMax = anchorMax;
        
        Vector2 zeroOffset = Vector2.zero;

        // Set the offsetMin and offsetMax properties to the zero offset
        iamgeAndtextTransform.offsetMin = zeroOffset;
        iamgeAndtextTransform.offsetMax = zeroOffset;
    }
}
