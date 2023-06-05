using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using QFSW.QC;
using UnityEngine.UI;

/*
 Implement CanvasDrawing in Unity3D

class Drawing implements MonoBehaviour {
	Canvas canvas;
	// collection to store created DraggableImage 
	...
	// collection to store created DraggableTexts 
	...
	
	DraggableText AddDraggableText(string id, float x,  float y, float width, fload height, string text, bDraggable)
	Image AddDraggableImage(string id, Canvas canvas, float x,  float y, float width, fload height, string url, bDraggable)

	// clean up all DraggableImages and DraggableTexts
	CleanUp();

	// one of the DraggableImages was clicked on
    public void OnBeginDrag(DraggableImage draggableImage, PointerEventData eventData)
    public void OnDrag(DraggableImage draggableImage, PointerEventData eventData)
    public void OnEndDrag(DraggableImage draggableImage, PointerEventData eventData)
    public void OnTouch(DraggableImage, PointerEventData eventData)

	// one of the DraggableTexts was clicked on
    public void OnBeginDrag(DraggableTexts draggableTexts, PointerEventData eventData)
    public void OnDrag(DraggableTexts draggableTexts, PointerEventData eventData)
    public void OnEndDrag(DraggableTexts draggableTexts, PointerEventData eventData)
    public void OnTouch(DraggableTexts draggableTexts, PointerEventData eventData)
}
 */

public class CanvasDrawing : MonoBehaviour
{
    public Canvas canvas;
    private Dictionary<string, DraggableImage> draggableImages = new Dictionary<string, DraggableImage>();
    private Dictionary<string, DraggableText> draggableTexts = new Dictionary<string, DraggableText>();


    [Command]
    public void Add(float x, float y)
    {
        // Create a new empty GameObject
        GameObject imageObject = new GameObject("CanvasImage");

        // Add an Image component to the GameObject
        Image image = imageObject.AddComponent<Image>();

        // Set the image as a child of the canvas
        image.transform.SetParent(canvas.transform, false);

        // Set the position of the image
        RectTransform rectTransform = image.GetComponent<RectTransform>();

        // Set the anchor and pivot points
        rectTransform.anchorMin = new Vector2(x, y);
        rectTransform.anchorMax = rectTransform.anchorMin;

        // Set the pivot point
        rectTransform.pivot = new Vector2(x, y);

        // Get the size of the parent canvas
        RectTransform parentRect = canvas.GetComponent<RectTransform>();

        // Set the size of the image based on the parent canvas size
        rectTransform.sizeDelta = new Vector2(parentRect.rect.width * 0.25f, parentRect.rect.height * 0.25f);

        // Set the position of the image
        rectTransform.anchoredPosition = new Vector2(0, 0);
    }

    [Command]
    public void Add2(float x, float y, float fWidth, float fHeight)
    {
        // Create a new empty GameObject
        GameObject imageObject = new GameObject("CanvasImage");

        // Add an Image component to the GameObject
        Image image = imageObject.AddComponent<Image>();

        // Set the image as a child of the canvas
        image.transform.SetParent(canvas.transform, false);

        // Set the position of the image
        RectTransform rectTransform = image.GetComponent<RectTransform>();

        // Set the anchor and pivot points
        rectTransform.anchorMin = new Vector2(x, y);
        rectTransform.anchorMax = rectTransform.anchorMin;

        // Set the pivot point
        rectTransform.pivot = new Vector2(x, y);

        // Get the size of the parent canvas
        RectTransform parentRect = canvas.GetComponent<RectTransform>();

        // Set the size of the image based on the parent canvas size
        rectTransform.sizeDelta = new Vector2(parentRect.rect.width * fWidth, parentRect.rect.height * fHeight);

        // Set the position of the image
        rectTransform.anchoredPosition = new Vector2(0, 0);
    }


    [Command]
    public DraggableImage AddImage(float x, float y, float width, float height, string url)
    {
        DraggableImage newImage = DraggableImage.Create("text", canvas, 0.5f, 0.5f, 0.5f, 0.5f, url, true);
        return newImage;
    }

    [Command]
    public DraggableText AddText(float x, float y, float width, float height, string text)
    {
        DraggableText newText = DraggableText.Create("text", canvas, x, y, width, height, text, true);
        newText.SetColor(0, 0, 0, 1);
        return newText;
    }

    public DraggableImage AddDraggableImage(string id, float x, float y, float width, float height, string url,
        bool bDraggable)
    {
        DraggableImage image = DraggableImage.Create(id, canvas, x, y, width, height, url, bDraggable);
        draggableImages[id] = image;
        return image;
    }

    public DraggableText AddDraggableText(string id, float x, float y, float width, float height, string text,
        bool bDraggable)
    {
        DraggableText draggableText = DraggableText.Create(id, canvas, x, y, width, height, text, bDraggable);
        draggableTexts[id] = draggableText;
        return draggableText;
    }

    public DraggableImage FindDraggableImage(string id)
    {
        return draggableImages.ContainsKey(id) ? draggableImages[id] : null;
    }

    public DraggableText FindDraggableText(string id)
    {
        return draggableTexts.ContainsKey(id) ? draggableTexts[id] : null;
    }

    public void CleanUp()
    {
        foreach (var item in draggableImages.Values)
        {
            Destroy(item.gameObject);
        }

        draggableImages.Clear();

        foreach (var item in draggableTexts.Values)
        {
            Destroy(item.gameObject);
        }

        draggableTexts.Clear();
    }

    public void OnBeginDrag(DraggableImage draggableImage, PointerEventData eventData)
    {
    }

    public void OnDrag(DraggableImage draggableImage, PointerEventData eventData)
    {
    }

    public void OnEndDrag(DraggableImage draggableImage, PointerEventData eventData)
    {
    }

    public void OnTouch(DraggableImage draggableImage, PointerEventData eventData)
    {
    }

    public void OnBeginDrag(DraggableText draggableText, PointerEventData eventData)
    {
    }

    public void OnDrag(DraggableText draggableText, PointerEventData eventData)
    {
    }

    public void OnEndDrag(DraggableText draggableText, PointerEventData eventData)
    {
    }

    public void OnTouch(DraggableText draggableText, PointerEventData eventData)
    {
    }
}