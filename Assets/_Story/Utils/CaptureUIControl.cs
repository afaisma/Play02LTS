using UnityEngine;
using UnityEngine.UI;

public class CaptureUIControl : MonoBehaviour
{
    public Graphic uiControl; // The UI control you want to capture

  public Sprite Capture(float addedWidth, float addedHeight)
{
    // Calculate the size of the UI control
    RectTransform rectTransform = uiControl.GetComponent<RectTransform>();
    int originalWidth = Mathf.RoundToInt(rectTransform.rect.width);
    int originalHeight = Mathf.RoundToInt(rectTransform.rect.height);

    // Calculate the new dimensions with added width and height
    int textureWidth = Mathf.RoundToInt(originalWidth * (1 + addedWidth / 100));
    int textureHeight = Mathf.RoundToInt(originalHeight * (1 + addedHeight / 100));

    // Create a new RenderTexture
    RenderTexture renderTexture = new RenderTexture(textureWidth, textureHeight, 24);
    renderTexture.Create();

    // Save the current RenderTexture active in the RenderTexture.active property
    RenderTexture previousRenderTexture = RenderTexture.active;

    // Set the created RenderTexture as the active one
    RenderTexture.active = renderTexture;

    // Create a temporary camera and set its target texture to the created RenderTexture
    GameObject tempCameraGO = new GameObject("TempCamera");
    Camera tempCamera = tempCameraGO.AddComponent<Camera>();
    tempCamera.CopyFrom(Camera.main);
    tempCamera.targetTexture = renderTexture;
    tempCamera.clearFlags = CameraClearFlags.Color;
    tempCamera.backgroundColor = Color.clear;

    // Set the temporary camera's position and rotation to match the UI control
    tempCamera.transform.position = rectTransform.position;
    tempCamera.transform.rotation = rectTransform.rotation;
    tempCamera.orthographicSize = Mathf.Max(textureWidth, textureHeight) / 2f;

    // Render the UI control with the temporary camera
    tempCamera.Render();

    // Read the pixels from the RenderTexture into a Texture2D
    Texture2D texture = new Texture2D(renderTexture.width, renderTexture.height);
    texture.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
    texture.Apply();

    // Reset the RenderTexture.active property to the previous value
    RenderTexture.active = previousRenderTexture;

    // Clean up
    Destroy(tempCameraGO);
    Destroy(renderTexture);

    // Create a Sprite from the Texture2D
    Rect spriteRect = new Rect(0, 0, texture.width, texture.height);
    Vector2 spritePivot = new Vector2(0.5f, 0.5f);
    Sprite capturedSprite = Sprite.Create(texture, spriteRect, spritePivot);

    return capturedSprite;
}

}
