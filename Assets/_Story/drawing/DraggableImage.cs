using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;
using Mono.CSharp;
using UnityEngine.Networking;

/*
 Implement DraggableImage in Unity3D. Calculate the position, width and height of the DraggableImage the base on the size of the canvas, so x=0, y=-0 will position the Image in the left, bottom corner the canvas, x=1, y=1 will position it in the right, top corner. width=1, height=1 will make the size of the DraggableImage the same as the size of the canvas.  
the dragging of the image should be restricted to X0, X1, Y0, Y1.

public class DraggableImage : Image, IBeginDragHandler, IDragHandler, IEndDragHandler
{
	string id;
	floaf  X0, X1, Y0, Y1;
	
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
    float X0, X1, Y0, Y1;
    bool draggable;
    Vector3 offset;

    public Canvas canvas;
    Vector2 originalLocalPointerPosition;
    Vector3 originalPanelLocalPosition;

    public static DraggableImage Create(string id, Canvas canvas, float x, float y, float width, float height, string url, bool bDraggable)
    {
        // Create a new GameObject with a DraggableImage component
        GameObject gameObject = new GameObject("DraggableImage");
        DraggableImage draggableImage = gameObject.AddComponent<DraggableImage>();
    
        // Set the parent to the provided canvas and reset its local transform
        draggableImage.transform.SetParent(canvas.transform, true);
    
        draggableImage.id = id;
        draggableImage.Load(canvas, x, y, width, height, url);
        draggableImage.draggable = bDraggable;
        return draggableImage;
    }

    public void Load(Canvas targetCanvas, float x, float y, float width, float height, string url)
    {
        this.canvas = targetCanvas;
        RectTransform rectTransform = GetComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(x, y);
        rectTransform.anchorMax = new Vector2(x + width, y + height);
        rectTransform.anchoredPosition = Vector2.zero;
        rectTransform.sizeDelta = Vector2.zero;

        // Assume that we use UnityWebRequest to load image from url
        StartCoroutine(LoadImageFromUrl(url));
    }

    IEnumerator LoadImageFromUrl(string url)
    {
        UnityWebRequest request = UnityWebRequestTexture.GetTexture(url);
        yield return request.SendWebRequest();
        if(request.isNetworkError || request.isHttpError) 
        {
            Debug.Log(request.error);
        }
        else 
        {
            Texture2D texture = ((DownloadHandlerTexture)request.downloadHandler).texture;
            this.sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
        }
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

    public void OnTouch(PointerEventData eventData)
    {
        // handle touch event
    }
}
