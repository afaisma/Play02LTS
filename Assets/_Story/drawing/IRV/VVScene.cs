using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using UnityEngine.UI;
using TMPro;
using System.Collections.Specialized;
using Miniscript;

public class VVScene : MonoBehaviour
{
    [Multiline] public string script;
    readonly int _maxCacheImagesSize = 30;
    static readonly OrderedDictionary CacheImages = new OrderedDictionary();
    GameObject _x0Y0, _x0Y1, _x1Y0, _x1Y1;
    private Interpreter _interpreter;
    private Dictionary<string, GameObject> gameObjectMap = new Dictionary<string, GameObject>();

    readonly float _textZOffset = -0.01f;
    readonly float _spriteZOffset = -0.05f;
    float _lastSpriteZ = -2;

    private void AddToCacheImages(string url, Sprite sprite)
    {
        if (CacheImages.Count >= _maxCacheImagesSize)
            CacheImages.RemoveAt(0);
        CacheImages[url] = sprite;
    }

    public void AddToGOMap(string id, GameObject go)
    {
        if (!gameObjectMap.ContainsKey(id))
        {
            gameObjectMap.Add(id, go);
        }
        else
        {
            Debug.Log($"GameObject with ID {id} already exists.");
        }
    }

    // Remove a GameObject from the map by ID
    public void RemoveFromGOMap(string id)
    {
        if (gameObjectMap.ContainsKey(id))
        {
            GameObject toBeDeleted = gameObjectMap[id];
            gameObjectMap.Remove(id);
            GameObject.Destroy(toBeDeleted); // Destroy the GameObject
        }
        else
        {
            Debug.Log($"No GameObject found with ID {id}.");
        }
    }

    public GameObject FindInGOMap(string id)
    {
        if (gameObjectMap.ContainsKey(id))
        {
            return gameObjectMap[id];
        }
        else
        {
            Debug.Log($"No GameObject found with ID {id}.");
            return null;
        }
    }
    public void Start()
    {
        PositionSpheresAtCorners();
    }

    void OnDestroy()
    {
        _interpreter?.Reset();
        Debug.Log("OnDestroy PRScript");
    }

    void RunScript(string sScript)
    {
        SetupInterpreter();
        _interpreter.Reset(sScript);
        _interpreter.Compile();
        _interpreter.RunUntilDone(10);
    }

    void SetupInterpreter()
    {
        _interpreter = new Interpreter();
        Intrinsic f = Intrinsic.Create("AddVSprite");
        f.AddParam("id", "");
        f.AddParam("idParent", "");
        f.AddParam("url", "");
        f.AddParam("x0Rel", 0f);
        f.AddParam("y0Rel", 0f);
        f.AddParam("x1Rel", 0f);
        f.AddParam("y1Rel", 0f);
        f.AddParam("keepAspect", 1);
        f.code = (context, partialResult) =>
        {
            string id = context.GetVar("id").ToString();
            string idParent = context.GetVar("idParent").ToString();
            string url = context.GetVar("url").ToString();
            float x0Rel = context.GetVar("x0Rel").FloatValue();
            float y0Rel = context.GetVar("y0Rel").FloatValue();
            float x1Rel = context.GetVar("x1Rel").FloatValue();
            float y1Rel = context.GetVar("y1Rel").FloatValue();
            int keepAspect = context.GetVar("keepAspect").IntValue();
            AddVSprite(id, idParent, url, x0Rel, y0Rel, x1Rel, y1Rel, keepAspect != 0);
            return new Intrinsic.Result(ValNumber.one);
        };
        f = Intrinsic.Create("AddText");
        f.AddParam("id", "");
        f.AddParam("text", "");
        f.AddParam("fontsize", 20);
        f.code = (context, partialResult) =>
        {
            string id = context.GetVar("id").ToString();
            string text = context.GetVar("text").ToString();
            int fontsize = context.GetVar("fontsize").IntValue();
            AddText(id, text, fontsize);
            return new Intrinsic.Result(ValNumber.one);
        };
        ConfigOutput();
    }

    void ConfigOutput()
    {
        _interpreter.standardOutput = (s) => Debug.Log(s);
        _interpreter.implicitOutput = (s) => Debug.Log("<color=#66bb66>" + s + "</color>");
        _interpreter.errorOutput = (s) =>
        {
            AlertDialogManager.Instance.ShowAlertDialog("error in script: " + s + 
                                                        "\n The script content:\n <color=#bb00bb>" + 
                                                        _interpreter.source +"</color>");
            Debug.LogWarning(s);
            Debug.Log("<color=red>" + s + "</color>");
            // ...and in case of error, we'll also stop the interpreter.
            _interpreter.Stop();
        };
    }

    public void PositionSpheresAtCorners()
    {
        float screenHeight = Screen.height;
        float screenWidth = Screen.width;

        Vector3 x0Y0Corner = Camera.main.ScreenToWorldPoint(new Vector3(0, 0, Camera.main.nearClipPlane));
        Vector3 x1Y0Corner = Camera.main.ScreenToWorldPoint(new Vector3(screenWidth, 0, Camera.main.nearClipPlane));
        Vector3 x0Y1Corner = Camera.main.ScreenToWorldPoint(new Vector3(0, screenHeight, Camera.main.nearClipPlane));
        Vector3 x1Y1Corner =
            Camera.main.ScreenToWorldPoint(new Vector3(screenWidth, screenHeight, Camera.main.nearClipPlane));

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

    public void RunScript()
    {
        RunScript(script);
    }

    public void AddVSprite()
    {
        float x0Rel = 0.1f;
        float y0Rel = 0.1f;
        float x1Rel = 0.9f;
        float y1Rel = 0.9f;
        bool keepAspect = false;
        //StartCoroutine(AddBackground("background", "http://localhost:8080/api/files/download/stories/defaultImages/Bg1.jpg"));
        // StartCoroutine(AddBackground("background", "http://localhost:8080/api/files/download/stories/defaultImages/squared.jpg"));
        // StartCoroutine(CoAddSprite("background", "testParent",
        //     "http://localhost:8080/api/files/download/stories/defaultImages/circle_shape_geometry.PNG", x0Rel, y0Rel,
        //     x1Rel, y1Rel, keepAspect));
        AddVSprite("id", "idParent", "http://localhost:8080/api/files/download/stories/defaultImages/circle_shape_geometry.PNG", x0Rel, y0Rel, x1Rel, y1Rel, keepAspect);
    }

    public void AddVSprite(string id, string idParent, string url, float x0Rel, float y0Rel, float x1Rel, float y1Rel, bool keepAspect = true)
    {
        if (FindInGOMap(id))
        {
            string s = "id already exists: " + id;
            AlertDialogManager.Instance.ShowAlertDialog("error in script: " + s + 
                                                        "\n The script content:\n <color=#bb00bb>" + 
                                                        _interpreter.source +"</color>");
            Debug.LogWarning("id already exists: " + id);
            return;
        }
        StartCoroutine(CoAddVSprite(id, idParent, url, x0Rel, y0Rel, x1Rel, y1Rel, keepAspect));
    }

    public void AddText(string id, string text, int fontSize)
    {
        GameObject go = FindInGOMap(id);
        if (go == null)
        {
            return;
        }
        AddText(go, text, fontSize);
    }
        



    public IEnumerator CoAddVSprite(string id, string idParent, string url, float x0Rel, float y0Rel, float x1Rel, float y1Rel,
        bool keepAspect = true)
    {
        GameObject go = new GameObject(id);
        GameObject parent = FindInGOMap(idParent);
        if (parent != null)
            go.transform.parent = parent.transform;
        else
            go.transform.parent = transform;

        SpriteRenderer spriteRenderer = go.AddComponent<SpriteRenderer>();
        StartCoroutine(DownloadSprite(url, go, x0Rel, y0Rel, x1Rel, y1Rel, keepAspect));
        VSprite instance = go.AddComponent<VSprite>();
        AddToGOMap(id, go);
        yield return null;
    }

    void PostDownload(GameObject go, float x0Rel, float y0Rel, float x1Rel, float y1Rel, bool keepAspect = true)
    {
        MoveAndResizeSpriteRel(go, x0Rel, y0Rel, x1Rel, y1Rel, keepAspect);
        go.AddComponent<BoxCollider2D>();
    }

    IEnumerator DownloadSprite(string url, GameObject go, float x0Rel, float y0Rel, float x1Rel, float y1Rel,
        bool keepAspect = true)
    {
        SpriteRenderer spriteRenderer = go.GetComponent<SpriteRenderer>();
        if (CacheImages.Contains(url))
        {
            Sprite sprite = CacheImages[url] as Sprite;
            spriteRenderer.sprite = sprite;
            PostDownload(go, x0Rel, y0Rel, x1Rel, y1Rel, keepAspect);
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
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                new Vector2(0.5f, 0.5f), 100.0f);
            spriteRenderer.sprite = sprite;
            AddToCacheImages(url, sprite);
            PostDownload(go, x0Rel, y0Rel, x1Rel, y1Rel, keepAspect);

            // Yield the sprite to the caller
            spriteRenderer.sprite = sprite;
            yield return sprite;
        }
    }

    public void MoveAndResizeSpriteRel(GameObject go, float x0Rel, float y0Rel, float x1Rel, float y1Rel,
        bool keepAspect = true)
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
        if (keepAspect)
            MoveAndResizeSpriteAspect(go, new Vector2(x0, y0), new Vector2(x1, y1));
        else
            MoveAndResizeSprite(go, new Vector2(x0, y0), new Vector2(x1, y1));
    }

    void MoveAndResizeSprite(GameObject go, Vector2 x0Y0, Vector2 x1Y1)
    {
        SpriteRenderer spriteRenderer = go.GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
        {
            Debug.LogError("SpriteRenderer component not found on the GameObject.");
            return;
        }

        // Calculate the position and size of the sprite based on the target coordinates
        Vector2 position = (x0Y0 + x1Y1) / 2f;
        Vector2 size = new Vector2(Mathf.Abs(x1Y1.x - x0Y0.x), Mathf.Abs(x1Y1.y - x0Y0.y));

        // Set the position of the GameObject
        go.transform.position = new Vector3(position.x, position.y, go.transform.position.z);

        // Calculate the scale of the sprite based on the size
        float spriteWidth = spriteRenderer.sprite.bounds.size.x;
        float spriteHeight = spriteRenderer.sprite.bounds.size.y;

        Vector2 scale = new Vector2(size.x / spriteWidth, size.y / spriteHeight);
        spriteRenderer.transform.localScale = new Vector3(scale.x, scale.y, 1f);
    }

    public void MoveAndResizeSpriteAspect(GameObject go, Vector2 x0Y0, Vector2 x1Y1)
    {
        SpriteRenderer spriteRenderer = go.GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
        {
            Debug.LogError("SpriteRenderer component not found on the GameObject.");
            return;
        }

        // Calculate the position and size of the sprite based on the target coordinates
        Vector2 position = (x0Y0 + x1Y1) / 2f;
        Vector2 size = new Vector2(Mathf.Abs(x1Y1.x - x0Y0.x), Mathf.Abs(x1Y1.y - x0Y0.y));

        // Set the position of the GameObject
        _lastSpriteZ = _lastSpriteZ + _spriteZOffset;
        go.transform.position = new Vector3(position.x, position.y, go.transform.position.z + _lastSpriteZ);

        // Calculate the scale of the sprite while maintaining its aspect ratio
        float spriteAspectRatio = spriteRenderer.sprite.bounds.size.x / spriteRenderer.sprite.bounds.size.y;
        float targetAspectRatio = size.x / size.y;

        float scaleFactor = targetAspectRatio / spriteAspectRatio;

        if (scaleFactor > 1f)
        {
            // The target aspect ratio is wider, so adjust the width
            size.x /= scaleFactor;
        }
        else
        {
            // The target aspect ratio is narrower, so adjust the height
            size.y *= scaleFactor;
        }

        // Calculate the scale of the sprite based on the adjusted size
        float spriteWidth = spriteRenderer.sprite.bounds.size.x;
        float spriteHeight = spriteRenderer.sprite.bounds.size.y;

        Vector2 scale = new Vector2(size.x / spriteWidth, size.y / spriteHeight);
        spriteRenderer.transform.localScale = new Vector3(scale.x, scale.y, 1f);
    }

    public void AddText(GameObject go, string text, int fontSize)
    {
        // Create a new TextMeshProUGUI object
        GameObject textObject = new GameObject("TextMeshPro Text");

        // Attach it to the parent object
        textObject.transform.SetParent(go.transform);

        // Reset the transform
        textObject.transform.localPosition = new Vector3(0.0f, 0.0f, _textZOffset); //Vector3.zero;
        textObject.transform.localRotation = Quaternion.identity;
        textObject.transform.localScale = Vector3.one;

        // Add the TextMeshPro component and configure it
        TextMeshPro tmp = textObject.AddComponent<TextMeshPro>();
        tmp.text = text;
        tmp.fontSize = fontSize;

        // Set alignment and wrapping
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.enableAutoSizing = true;
        tmp.enableWordWrapping = true;

        // Set color to black
        tmp.color = Color.white;
    }
}