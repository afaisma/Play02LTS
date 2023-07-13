using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using UnityEngine.UI;
using System.Collections.Specialized;

public class AVScene : MonoBehaviour
{
    readonly int _maxCacheImagesSize = 30;
    static readonly OrderedDictionary CacheImages = new OrderedDictionary();
    GameObject _x0Y0, _x0Y1, _x1Y0, _x1Y1;

    private void AddToCacheImages(string url, Sprite sprite)
    {
        if (CacheImages.Count >= _maxCacheImagesSize)
            CacheImages.RemoveAt(0);
        CacheImages[url] = sprite;
    }

    public void Start()
    {
        Vector3 bottomLeft = Camera.main.ScreenToWorldPoint(new Vector3(0, 0, Camera.main.nearClipPlane));
        Vector3 topRight = Camera.main.ScreenToWorldPoint(new Vector3(Screen.width, Screen.height, Camera.main.nearClipPlane));
        Vector3 screenSizeInWorldCoordinates = topRight - bottomLeft;
        Debug.Log("Screen Width in World Coordinates: " + screenSizeInWorldCoordinates.x);
        Debug.Log("Screen Height in World Coordinates: " + screenSizeInWorldCoordinates.y);
        PositionSpheresAtCorners();
    }

    public void PositionSpheresAtCorners()
    {
        float screenHeight = Screen.height;
        float screenWidth = Screen.width;

        Vector3 x0Y0Corner = Camera.main.ScreenToWorldPoint(new Vector3(0, 0, Camera.main.nearClipPlane));
        Vector3 x1Y0Corner = Camera.main.ScreenToWorldPoint(new Vector3(screenWidth, 0, Camera.main.nearClipPlane));
        Vector3 x0Y1Corner = Camera.main.ScreenToWorldPoint(new Vector3(0, screenHeight, Camera.main.nearClipPlane));
        Vector3 x1Y1Corner = Camera.main.ScreenToWorldPoint(new Vector3(screenWidth, screenHeight, Camera.main.nearClipPlane));

        _x0Y0 = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        _x0Y0.name = "_x0Y0";
        _x0Y0.transform.position = new Vector3(x0Y0Corner.x, x0Y0Corner.y, 0f);
        _x0Y0.transform.localScale = new Vector3(2f, 2f, 2f);

        _x0Y1 = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        _x0Y1.name = "_x0Y1";
        _x0Y1.transform.position = new Vector3(x0Y0Corner.x, x0Y1Corner.y, 0f);
        _x0Y1.transform.localScale = new Vector3(2f, 2f, 2f);

        _x1Y1 = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        _x1Y1.name = "_x1Y1";
        _x1Y1.transform.position = new Vector3(x1Y1Corner.x, x0Y1Corner.y, 0f);
        _x1Y1.transform.localScale = new Vector3(2f, 2f, 2f);

        _x1Y0 = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        _x1Y0.name = "_x1Y0";
        _x1Y0.transform.position = new Vector3(x1Y1Corner.x, x0Y0Corner.y, 0f);
        _x1Y0.transform.localScale = new Vector3(2f, 2f, 2f);
    }

    public void AddBackground()
    {
        StartCoroutine(AddBackground("background", "http://localhost:8080/api/files/download/stories/defaultImages/Bg1.jpg"));
    }

    public IEnumerator AddBackground(string goname, string url)
    {
        GameObject go = new GameObject(goname);
        go.transform.parent = transform;
        SpriteRenderer spriteRenderer = go.AddComponent<SpriteRenderer>();
        StartCoroutine(DownloadSprite(url, go));
        yield return null;
    }
    
    IEnumerator DownloadSprite(string url, GameObject go)
    {
        SpriteRenderer spriteRenderer = go.GetComponent<SpriteRenderer>();
        if (CacheImages.Contains(url))
        {
            Sprite sprite = CacheImages[url] as Sprite;
            spriteRenderer.sprite = sprite;
            PostDownload(go, spriteRenderer);
            yield return sprite;
        }

        UnityWebRequest www = UnityWebRequestTexture.GetTexture(url);

        // Send the request and yield until it completes
        yield return www.SendWebRequest();

        if (www.result == UnityWebRequest.Result.ConnectionError || www.result == UnityWebRequest.Result.ProtocolError)
        {
            Debug.LogError("Error while receiving: " + www.error);
        }
        else
        {
            Texture2D texture = DownloadHandlerTexture.GetContent(www);
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100.0f);
            spriteRenderer.sprite = sprite;
            AddToCacheImages(url, sprite);
            PostDownload(go, spriteRenderer);

            // Yield the sprite to the caller
            spriteRenderer.sprite = sprite;
            yield return sprite;
        }
    }

    public void MoveAndResizeSpriteRel(GameObject go, float x0Rel, float y0Rel, float x1Rel, float y1Rel)
    {
        Vector2 x0Y0Pos = _x0Y0.transform.position;
        Vector2 x0Y1Pos = _x0Y1.transform.position;
        Vector2 x1Y0Pos = _x1Y0.transform.position;
        Vector2 x1Y1Pos = _x1Y1.transform.position;
        float width = x1Y0Pos.x - x0Y0Pos.x;
        float height = x0Y1Pos.y - x0Y0Pos.y;
        float x0 = x0Y0Pos.x + x0Rel * width;        
        float y0 = x0Y0Pos.y + y0Rel * height;
        float x1 = x0Y0Pos.x + x1Rel * width;
        float y1 = x0Y0Pos.y + y1Rel * height;
        MoveAndResizeSprite(go, new Vector2(x0, y0),
            new Vector2(x0, y1),
            new Vector2(x1, y1),
            new Vector2(x1, y0));
    }

    public void MoveAndResizeSprite(GameObject go, Vector2 x0Y0, Vector2 x0Y1, Vector2 x1Y1, Vector2 x1Y0)
    {
        SpriteRenderer spriteRenderer = go.GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
        {
            Debug.LogError("SpriteRenderer component not found on the GameObject.");
            return;
        }

        // Calculate the position and size of the sprite based on the target coordinates
        Vector2 position = (x0Y0 + x0Y1 + x1Y0 + x1Y1) / 4f;
        Vector2 size = new Vector2(Mathf.Abs(x1Y1.x - x0Y0.x), Mathf.Abs(x1Y1.y - x0Y0.y));

        // Set the position of the GameObject
        go.transform.position = new Vector3(position.x, position.y, go.transform.position.z);

        // Calculate the scale of the sprite based on the size
        float spriteWidth = spriteRenderer.sprite.bounds.size.x;
        float spriteHeight = spriteRenderer.sprite.bounds.size.y;

        Vector2 scale = new Vector2(size.x / spriteWidth, size.y / spriteHeight);
        spriteRenderer.transform.localScale = new Vector3(scale.x, scale.y, 1f);
    }

    void PostDownload(GameObject go, SpriteRenderer spriteRenderer)
    {
        float x0 = 0.5f;
        float y0 = 0.5f;
        float x1 = 0.75f;
        float y1 = 0.75f;

        MoveAndResizeSpriteRel(go, x0, y0, x1, y1);
        // MoveAndResizeSprite(go, 
        //     new Vector2(_x0Y0.transform.position.x, _x0Y0.transform.position.y), 
        //     new Vector2(_x0Y1.transform.position.x, _x0Y1.transform.position.y), 
        //     new Vector2(_x1Y1.transform.position.x, _x1Y1.transform.position.y), 
        //     new Vector2(_x1Y0.transform.position.x, _x1Y0.transform.position.y));
    }


}