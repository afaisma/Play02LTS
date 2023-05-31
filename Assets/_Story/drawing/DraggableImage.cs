using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;

/*
 Implement DraggableImage in Unity3D. Calculate the position, width and height of the DraggableImage the base on the size of the canvas, so x=0, y=-0 will position the Image in the left, bottom corner the canvas, x=1, y=1 will position it in the right, top corner. width=1, height=1 will make the size of the DraggableImage the same as the size of the canvas.  

public class DraggableImage : Image, IBeginDragHandler, IDragHandler, IEndDragHandler
{
	string id;
    public static DraggableImage Create(string id, Canvas canvas, float x, float y, float width, float height, string url, bDraggable)
    public void Load(Canvas canvas, float x, float y, float width, float height, string url)
    public void SetColor(float r, float g, float b, float a)
    public void OnTouch(PointerEventData eventData)
    public void OnBeginDrag(PointerEventData eventData)
    public void OnDrag(PointerEventData eventData)
    public void OnEndDrag(PointerEventData eventData)
}
 */

public class DraggableImage : Image, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    string id;
    bool draggable;
    Vector3 offset;

    public static DraggableImage Create(string id, Canvas canvas, float x, float y, float width, float height, string url, bool bDraggable)
    {
        // Create new GameObject and attach DraggableImage and a Rect Transform component
        GameObject go = new GameObject(id);
        go.transform.SetParent(canvas.transform);
        DraggableImage draggableImage = go.AddComponent<DraggableImage>();
        RectTransform rt = go.GetComponent<RectTransform>();
        draggableImage.Load(rt, x, y, width, height, url);
        draggableImage.draggable = bDraggable;
        return draggableImage;
    }

    public void Load(RectTransform rt, float x, float y, float width, float height, string url)
    {
        // Assuming x, y, width and height are normalized values
        rt.anchorMin = new Vector2(x, y);
        rt.anchorMax = new Vector2(x + width, y + height);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        // Assuming url is a path to a local file
        StartCoroutine(LoadImage(url));
    }

    public void SetColor(float r, float g, float b, float a)
    {
        this.color = new Color(r, g, b, a);
    }

    IEnumerator LoadImage(string url)
    {
        WWW www = new WWW(url);
        yield return www;

        if (www.error == null)
        {
            this.sprite = Sprite.Create(www.texture, new Rect(0, 0, www.texture.width, www.texture.height), new Vector2(0.5f, 0.5f));
        }
        else
        {
            Debug.LogError("Failed to load texture at " + url);
        }
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
