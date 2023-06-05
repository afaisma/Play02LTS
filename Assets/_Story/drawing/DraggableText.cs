using UnityEngine;
using TMPro;
using UnityEngine.EventSystems;

/*
 Implement DraggableText in Unity3D. Calculate the position, width and height of the DraggableText the base on the size of the canvas, so x=0, y=-0 will position the DraggableText in the left, bottom corner the canvas, x=1, y=1 will position it in the right, top corner. width=1, height=1 will make the size of the DraggableText the same as the size of the canvas.  

public class DraggableText : TextMeshProUGUI, IBeginDragHandler, IDragHandler, IEndDragHandler
{
	string id;
    public static DraggableText Create(string id, Canvas canvas, float x, float y, float width, float height, string text, bool bDraggable)
    public void Load(Canvas canvas, float x, float y, float width, float height, string text, bool bDraggable)
    public void SetFont(TMPro.TMP_FontAsset fontAsset)
    public void SetColor(float r, float g, float b, float a)
    public void OnTouch(PointerEventData eventData)
    public void OnBeginDrag(PointerEventData eventData)
    public void OnDrag(PointerEventData eventData)
    public void OnEndDrag(PointerEventData eventData)

}
 */

public class DraggableText : TextMeshProUGUI, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    string id;
    bool draggable;
    Vector3 offset;

    public static DraggableText Create(string id, Canvas canvas, float x, float y, float width, float height, string text, bool bDraggable)
    {       
        // Create new GameObject and attach DraggableText and a Rect Transform component
        GameObject go = new GameObject(id);
        go.transform.SetParent(canvas.transform);
        DraggableText draggableText = go.AddComponent<DraggableText>();
        RectTransform rt = go.GetComponent<RectTransform>();
        draggableText.Load(rt, x, y, width, height, text, bDraggable);
        return draggableText;
    }

    public void Load(RectTransform rt, float x, float y, float width, float height, string text, bool bDraggable)
    {
        // Assuming x, y, width and height are normalized values
        rt.anchorMin = new Vector2(x, y);
        rt.anchorMax = new Vector2(x + width, y + height);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        this.text = text;
        draggable = bDraggable;
    }

    public void SetFont(TMP_FontAsset fontAsset)
    {
        this.font = fontAsset;
    }
    
    public void SetFontSize(int fontSize)
    {
        this.fontSize = fontSize;
    }   

    public void SetColor(float r, float g, float b, float a)
    {
        this.color = new Color(r, g, b, a);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (draggable)
        {
            RectTransform canvasRectTransform = canvas.GetComponent<RectTransform>();
            Vector2 localCursor;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRectTransform, eventData.position, null, out localCursor))
            {
                offset = rectTransform.localPosition - new Vector3(localCursor.x, localCursor.y, rectTransform.localPosition.z);
            }
        }
    }


    public void OnDrag(PointerEventData eventData)
    {
        if (draggable)
        {
            RectTransform canvasRectTransform = canvas.GetComponent<RectTransform>();
            Vector2 localCursor;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRectTransform, eventData.position, null, out localCursor))
            {
                rectTransform.localPosition = new Vector3(localCursor.x, localCursor.y, rectTransform.localPosition.z) + offset;
            }
        }
    }


    public void OnEndDrag(PointerEventData eventData)
    {
        if (draggable)
        {
            // implement any behavior that you want to occur when the drag ends
        }
    }
}
