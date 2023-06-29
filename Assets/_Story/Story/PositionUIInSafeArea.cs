using UnityEngine;
using UnityEngine.Serialization;

public class PositionUIInSafeArea : MonoBehaviour
{
    public RectTransform gallery;
    public RectTransform text;
    public RectTransform toolbar;

    private void Start()
    {
        // Get the safe area rectangle of the screen
        Rect safeArea = Screen.safeArea;

        // Calculate the new height based on the width
        float newHeight = safeArea.width;

        // Set the position and size of the UI element to match the safe area
        gallery.anchorMin = new Vector2(safeArea.xMin / Screen.width, (safeArea.yMax - newHeight) / Screen.height);
        gallery.anchorMax = new Vector2(safeArea.xMax / Screen.width, safeArea.yMax / Screen.height);
        gallery.offsetMin = Vector2.zero;
        gallery.offsetMax = Vector2.zero;

        Vector2 anchorMaxPrev = text.anchorMax;
        Vector2 anchorMinPrev = text.anchorMin;
        text.anchorMin = new Vector2(gallery.anchorMin.x, toolbar.anchorMax.y);
        text.anchorMax = new Vector2(gallery.anchorMax.x, gallery.anchorMin.y);// - 0.05f); // for sound bar
    }
    
}