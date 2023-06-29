using UnityEngine;
using QFSW.QC;

using UnityEngine;

public class ResizeGUIElement : MonoBehaviour
{
    public GameObject goToBeResized;

    private void Start()
    {
        //ResizeElementToParentMax();
    }

    private void ResizeElementToParentMax()
    {
        if (goToBeResized == null || goToBeResized.transform.parent == null) return;

        RectTransform parentRectTransform = goToBeResized.transform.parent.GetComponent<RectTransform>();
        RectTransform rectTransform = goToBeResized.GetComponent<RectTransform>();

        rectTransform.anchorMin = new Vector2(0, 0);
        rectTransform.anchorMax = new Vector2(1, 1);
        rectTransform.offsetMin = new Vector2(0, 0);
        rectTransform.offsetMax = new Vector2(0, 0);

        //rectTransform.anchoredPosition = new Vector2(parentRectTransform.rect.width / 2, parentRectTransform.rect.height / 2);
       // rectTransform.sizeDelta = new Vector2(parentRectTransform.rect.width, parentRectTransform.rect.height);
    }
}
