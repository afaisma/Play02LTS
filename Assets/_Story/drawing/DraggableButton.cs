using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;
using UnityEngine.Networking;

public class DraggableButton : Button, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    string id;
    RectTransform rectTransform;
    Canvas canvas;
    bool draggable;
    Vector3 offset;

    public static DraggableButton Create(string id, Canvas canvas, float x, float y, float width, float height, string url, bool bDraggable)
    {
        GameObject buttonObject = new GameObject(id);
        buttonObject.transform.SetParent(canvas.transform);

        DraggableButton draggableButton = buttonObject.AddComponent<DraggableButton>();
        draggableButton.Load(canvas, x, y, width, height, url);
        draggableButton.id = id;
        draggableButton.draggable = bDraggable;

        return draggableButton;
    }

    public void Load(Canvas canvas, float x, float y, float width, float height, string url)
    {
        this.canvas = canvas;
        rectTransform = GetComponent<RectTransform>();

        // Position
        rectTransform.anchorMin = new Vector2(x, y);
        rectTransform.anchorMax = new Vector2(x + width, y + height);
        rectTransform.anchoredPosition = Vector2.zero;
        rectTransform.sizeDelta = Vector2.zero;

        // Load Image from URL
        StartCoroutine(LoadSprite(url));
    }

    IEnumerator LoadSprite(string url)
    {
        UnityWebRequest www = UnityWebRequestTexture.GetTexture(url);
        yield return www.SendWebRequest();

        if (www.result == UnityWebRequest.Result.Success)
        {
            Texture2D texture = ((DownloadHandlerTexture)www.downloadHandler).texture;
            Image image = GetComponent<Image>();
            image.sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
        }
        else
        {
            Debug.Log(www.error);
        }
    }

    public void SetColor(float r, float g, float b, float a)
    {
        Image image = GetComponent<Image>();
        image.color = new Color(r, g, b, a);
    }

    public void OnTouch(PointerEventData eventData)
    {
        // Implement your OnTouch logic
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (draggable)
        {
            offset = rectTransform.position - Input.mousePosition;
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
            // Implement your OnEndDrag logic
        }
    }
}
