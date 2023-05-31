using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using QFSW.QC;

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
    List<DraggableImage> draggableImages = new List<DraggableImage>();
    List<DraggableText> draggableTexts = new List<DraggableText>();

    [Command]
    public DraggableImage AddImage(float x, float y, float width, float height, string url)
    {
        DraggableImage newImage = DraggableImage.Create("text", canvas,0.5f, 0.5f, 0.5f,0.5f, url, true);
        draggableImages.Add(newImage);
        return newImage;
    }

    [Command]
    public DraggableText AddText( float x, float y, float width, float height, string text)
    {
        DraggableText newText = DraggableText.Create("text", canvas,x, y, width, height, text, true);
        newText.SetColor(0, 0, 0, 1);
        draggableTexts.Add(newText);
        return newText;
    }
    
    public DraggableImage AddDraggableImage(string id, float x, float y, float width, float height, string url, bool bDraggable)
    {
        DraggableImage newImage = DraggableImage.Create(id, canvas, x, y, width, height, url, bDraggable);
        draggableImages.Add(newImage);
        return newImage;
    }

    public DraggableText AddDraggableText(string id, float x, float y, float width, float height, string text, bool bDraggable)
    {
        DraggableText newText = DraggableText.Create(id, canvas, x, y, width, height, text, bDraggable);
        draggableTexts.Add(newText);
        return newText;
    }

    public void CleanUp()
    {
        foreach(DraggableImage image in draggableImages)
        {
            Destroy(image.gameObject);
        }
        draggableImages.Clear();

        foreach(DraggableText text in draggableTexts)
        {
            Destroy(text.gameObject);
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
