using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using TMPro;
using System.Collections.Specialized;
using System.Text.RegularExpressions;
using Miniscript;
using UnityEngine.Rendering.Universal;

public class CellStructure
{
    public int x;
    public int y;
    public int nX;
    public int nY;
}

public class VVScene : MonoBehaviour
{
    public string scriptURL;
    private List<Scriptlet> _scriptlets;
    private Settings _settings;
    Dictionary<string, Scriptlet> _mapEvents = new Dictionary<string, Scriptlet>();
    public string baseURL = "";
    public string convScene = "http://localhost:8080/api/files/download/stories/VScene/VScene.txt";
    public string embeddedScript;
    readonly int _maxCacheImagesSize = 30;
    static readonly OrderedDictionary CacheImages = new OrderedDictionary();
    GameObject _goX0Y0, _goX0Y1, _goX1Y0, _goX1Y1;
    Vector2 _v2X0Y0, _v2X1Y1;
    private Interpreter _interpreter;
    private Dictionary<string, GameObject> gameObjectMap = new Dictionary<string, GameObject>();

    readonly float _textZOffset = -0.01f;
    readonly float _spriteZOffset = -0.05f;
    float _lastSpriteZ = -2;
    
    float NextSpriteZ()
    {
        _lastSpriteZ += _spriteZOffset;
        return _lastSpriteZ;
    }

    public void Start()
    {
        PositionSpheresAtCorners();
        AddToGOMap("", gameObject);
        baseURL = PRUtils.RemoveFileNameFromUrl(scriptURL);
        StartCoroutine(PRUtils.DownloadFile(scriptURL, (content) => { parse(content); }));
    }

        private void parse(string script)
    {
        Globals.g_openedStoriesCount = PlayerPrefs.GetInt("g_openedStoriesCount", 0);
        PlayerPrefs.SetInt("g_openedStoriesCount", Globals.g_openedStoriesCount + 1);
        
        //Debug.Log("PRScript::parse: " + script);
        List<string> lines = PRUtils.SplitStringIntoLines(script);

        _settings = new Settings();
        _scriptlets = new List<Scriptlet>();
        _mapEvents = new Dictionary<string, Scriptlet>();

        bool bSettingsSectionEnded = false;
        int index = 0;
        while (index < lines.Count)
        {
            if (lines[index].StartsWith("////////[chunk"))
            {
                bSettingsSectionEnded = true;
                Scriptlet scriptlet = new Scriptlet();
                scriptlet.Content = "";
                scriptlet.Content += lines[index] + "\n";
                index++;
                while (index < lines.Count && !lines[index].StartsWith("////////["))
                {
                    scriptlet.Content += lines[index] + "\n";
                    index++;
                }
                _scriptlets.Add(scriptlet);
            }
            else if (lines[index].StartsWith("////////[event"))
            {
                Match match = Regex.Match(lines[index], @"\[event (\w+)");
                string eventName = match.Groups[1].Value;
                bSettingsSectionEnded = true;
                Scriptlet scriptlet = new Scriptlet();
                scriptlet.Content = "";
                scriptlet.Content += lines[index] + "\n";
                index++;
                while (index < lines.Count && !lines[index].StartsWith("////////["))
                {
                    scriptlet.Content += lines[index] + "\n";
                    index++;
                }
                _mapEvents[eventName] = scriptlet;
            }
            else
            {
                if (!bSettingsSectionEnded)
                {
                    _settings.Content += lines[index] + "\n";
                }

                index++;
            }
        }

        RunScript(_settings.Content);
    }

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
    
    public void CleanUp()
    {
        foreach (var goentry in gameObjectMap)
        {
            if (goentry.Key != "")
            {
                GameObject toBeDeleted = gameObjectMap[goentry.Key];
                Destroy(toBeDeleted); // Destroy the GameObject
            }
        }
        gameObjectMap.Clear();
        AddToGOMap("", gameObject);
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

    public void DisplayError(string runnedScript, string message)
    {
        AlertDialogManager.Instance.ShowAlertDialog("error in script: " + runnedScript + "\n The script content:\n <color=#bb00bb>" + _interpreter.source +"</color>");
    }
    public bool existingId(string id)
    {
        GameObject go = FindInGOMap(id);
        if (go == null)
        {
            return false;
        }
        return true;
    }
    
    void SetupInterpreter()
    {
        _interpreter = new Interpreter();
        Intrinsic f = Intrinsic.Create("AddVSprite");
        f.AddParam("id", "");
        f.AddParam("idParent", "");
        f.AddParam("url", "");
        f.AddParam("x0", 0f);
        f.AddParam("y0", 0f);
        f.AddParam("x1", 0f);
        f.AddParam("y1", 0f);
        f.AddParam("keepAspect", 1);
        f.code = (context, partialResult) =>
        {
            string id = context.GetVar("id").ToString();
            string idParent = context.GetVar("idParent").ToString();
            string url = context.GetVar("url").ToString();
            float x0 = context.GetVar("x0").FloatValue();
            float y0 = context.GetVar("y0").FloatValue();
            float x1 = context.GetVar("x1").FloatValue();
            float y1 = context.GetVar("y1").FloatValue();
            int keepAspect = context.GetVar("keepAspect").IntValue();
            if (!existingId(idParent))
            {
                DisplayError(embeddedScript, "could not find parent" + idParent);
                return new Intrinsic.Result(ValNumber.one);
            }
            if (existingId(id))
            {
                DisplayError(embeddedScript, "VSprite " + id + " already exists ");
                return new Intrinsic.Result(ValNumber.one);
            }
            AddVSprite(id, idParent, url, XFromGrid(x0), YFromGrid(y0), XFromGrid(x1), YFromGrid(y1), keepAspect != 0);
            return new Intrinsic.Result(ValNumber.one);
        };
        f = Intrinsic.Create("PositionSphere");
        f.AddParam("x", 0f);
        f.AddParam("y", 0f);
        f.code = (context, partialResult) =>
        {
            float x = context.GetVar("x").FloatValue();
            float y = context.GetVar("y").FloatValue();
            PositionSphere(XFromGrid(x), YFromGrid(y), "Sphere_"+ x+"_"+y);
            return new Intrinsic.Result(ValNumber.one);
        };
        f = Intrinsic.Create("AddText");
        f.AddParam("idText", "");
        f.AddParam("idParent", "");
        f.AddParam("text", "");
        f.AddParam("fontsize", 20);
        f.code = (context, partialResult) =>
        {
            string idText = context.GetVar("idText").ToString();
            string idParent = context.GetVar("idParent").ToString();
            string text = context.GetVar("text").ToString();
            int fontsize = context.GetVar("fontsize").IntValue();
            if (!existingId(idParent))
            {
                DisplayError(embeddedScript, "couldnot find parent" + idParent);
                return new Intrinsic.Result(ValNumber.one);
            }
            if (existingId(idText))
            {
                DisplayError(embeddedScript, " id already exists " + idText);
                return new Intrinsic.Result(ValNumber.one);
            }

            AddText(idText, idParent, text, fontsize);
            return new Intrinsic.Result(ValNumber.one);
        };
        f = Intrinsic.Create("AddLight");
        f.AddParam("idLight", "");
        f.AddParam("idParent", "");
        f.AddParam("lightType", "");
        f.AddParam("intensity", 0f);
        f.AddParam("innerRadius", 0f);
        f.AddParam("outerRadius", 0f);
        f.AddParam("fallOffStrength", 0f);
        f.code = (context, partialResult) =>
        {
            string idLight = context.GetVar("idLight").ToString();
            string idParent = context.GetVar("idParent").ToString();
            string lightType = context.GetVar("lightType").ToString();
            float intensity = context.GetVar("intensity").FloatValue();
            float innerRadius = context.GetVar("innerRadius").FloatValue();
            float outerRadius = context.GetVar("outerRadius").FloatValue();
            float fallOffStrength = context.GetVar("fallOffStrength").FloatValue();
            if (!existingId(idParent))
            {
                DisplayError(embeddedScript, "couldnot find parent" + idParent);
                return new Intrinsic.Result(ValNumber.one);
            }
            if (existingId(idLight))
            {
                DisplayError(embeddedScript, " id already exists " + idLight);
                return new Intrinsic.Result(ValNumber.one);
            }

            AddLight(FindInGOMap(idParent), idLight, lightType, intensity, WidthFromGrid(innerRadius), WidthFromGrid(outerRadius), fallOffStrength);
            return new Intrinsic.Result(ValNumber.one);
        };
        f = Intrinsic.Create("AddHoveringBehaviour");
        f.AddParam("id", "");
        f.AddParam("hoverSpeed", 0f);
        f.AddParam("hoverRadius", 0f);
        f.AddParam("hoverHeight", 0f);
        f.AddParam("randomFactor", 0f);
        f.code = (context, partialResult) =>
        {
            string id = context.GetVar("id").ToString();
            float hoverSpeed = context.GetVar("hoverSpeed").FloatValue();
            float hoverRadius = context.GetVar("hoverRadius").FloatValue();
            float hoverHeight = context.GetVar("hoverHeight").FloatValue();
            float randomFactor = context.GetVar("randomFactor").FloatValue();
            if (!existingId(id))
                return new Intrinsic.Result(ValNumber.one);
            AddHovering(FindInGOMap(id), hoverSpeed, WidthFromGrid(hoverRadius), HeightFromGrid(hoverHeight), randomFactor);
            return new Intrinsic.Result(ValNumber.one);
        };
        f = Intrinsic.Create("SetDragTypeForGameObject");
        f.AddParam("id", "");
        f.AddParam("dragType", "");
        f.AddParam("minX", 0f);
        f.AddParam("minY", 0f);
        f.AddParam("maxX", 0f);
        f.AddParam("maxY", 0f);
        f.code = (context, partialResult) =>
        {
            string id = context.GetVar("id").ToString();
            string dragType = context.GetVar("dragType").ToString();
            float minX = context.GetVar("minX").FloatValue();
            float minY = context.GetVar("minY").FloatValue();
            float maxX = context.GetVar("maxX").FloatValue();
            float maxY = context.GetVar("maxY").FloatValue();
            if (!existingId(id))
                return new Intrinsic.Result(ValNumber.one);
            SetDragTypeForGameObject(FindInGOMap(id), dragType, XFromGrid(minX), YFromGrid(minY), XFromGrid(maxX), YFromGrid(maxY));
            return new Intrinsic.Result(ValNumber.one);
        };
        f = Intrinsic.Create("CreateVSpriteGrid");
        f.AddParam("id", "");
        f.AddParam("nx", 0);
        f.AddParam("ny", 0);
        f.code = (context, partialResult) =>
        {
            string id = context.GetVar("id").ToString();
            int nx = context.GetVar("nx").IntValue();
            int ny = context.GetVar("ny").IntValue();
            if (!existingId(id))
                return new Intrinsic.Result(ValNumber.one);
            CreateVSpriteGrid(id, nx, ny);
            return new Intrinsic.Result(ValNumber.one);
        };
        f = Intrinsic.Create("ShowVSprite");
        f.AddParam("id", "");
        f.AddParam("bShow", 1);
        f.code = (context, partialResult) =>
        {
            string id = context.GetVar("id").ToString();
            int bShow = context.GetVar("bShow").IntValue();
            if (!existingId(id))
                return new Intrinsic.Result(ValNumber.one);
            //go.GetComponent<Renderer>().enabled = false;
            //ShowVSprite "id1*", 1
            return new Intrinsic.Result(ValNumber.one);
        };

        ConfigOutput();
    }

    private void CreateVSpriteGrid(string id, int nx, int ny)
    {
        GameObject gameObject = FindInGOMap(id);
        VSprite vSprite = gameObject.GetComponent<VSprite>();
        if (vSprite == null)
        {
            DisplayError(embeddedScript, "could not find VSprite for " + id);
            return;
        }
        string url = vSprite.url;
        Sprite originalSprite = CacheImages[url] as Sprite;
        if (originalSprite == null)
        {
            DisplayError(embeddedScript, "could not find cached sprite for " + url);
            return;
        }
        for (int y = 0; y < ny; y++)
        {
            for (int x = 0; x < ny; x++)
            {
                string cellId = id + "_" + x + "_" + y;
                Sprite cellSprite = CreateSubspriteCell(originalSprite, x, y, nx, ny);
                GameObject goCell = new GameObject(cellId);
                GameObject parent = FindInGOMap(id);
                if (parent != null)
                    goCell.transform.parent = parent.transform;
                else
                    goCell.transform.parent = transform;
                goCell.transform.Translate(0, 0, NextSpriteZ());
                SpriteRenderer spriteRenderer = goCell.AddComponent<SpriteRenderer>();
                spriteRenderer.sprite = cellSprite;
                VSprite vsprite = goCell.AddComponent<VSprite>();
                vsprite.url = url;
                AddToGOMap(cellId, goCell);
            }
        }
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

    float XFromGrid(float xgrid)
    {
        float x = (_v2X1Y1.x - _v2X0Y0.x) * (xgrid / 1000) + _v2X0Y0.x;
        return x;
    }
    
    float YFromGrid(float ygrid)
    {
        float y = (_v2X1Y1.y - _v2X0Y0.y) * (ygrid / 1000) + _v2X0Y0.y;
        return y;
    }
    float WidthFromGrid(float xgrid)
    {
        float x = (_v2X1Y1.x - _v2X0Y0.x) * (xgrid/1000);
        return x;
    }
    
    float HeightFromGrid(float ygrid)
    {
        float y = (_v2X1Y1.y - _v2X0Y0.y) * (ygrid/1000);
        return y;
    }
    public void PositionSphere(float x, float y, string goname)
    {
        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.name = goname;
        sphere.transform.position = new Vector3(x, y, 0f);
        _goX1Y1.transform.localScale = new Vector3(2f, 2f, 2f);
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

        _goX0Y0 = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        _goX0Y0.name = "_x0Y0";
        _goX0Y0.transform.position = new Vector3(x0Y0Corner.x, x0Y0Corner.y, 0f);
        _goX0Y0.transform.localScale = new Vector3(2f, 2f, 2f);
        _v2X0Y0 = new Vector2(x0Y0Corner.x, x0Y0Corner.y); 

        _goX0Y1 = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        _goX0Y1.name = "_x0Y1";
        _goX0Y1.transform.position = new Vector3(x0Y0Corner.x, x0Y1Corner.y, 0f);
        _goX0Y1.transform.localScale = new Vector3(2f, 2f, 2f);

        _goX1Y1 = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        _goX1Y1.name = "_x1Y1";
        _goX1Y1.transform.position = new Vector3(x1Y1Corner.x, x0Y1Corner.y, 0f);
        _goX1Y1.transform.localScale = new Vector3(2f, 2f, 2f);
        _v2X1Y1 = new Vector2(x1Y1Corner.x, x0Y1Corner.y);
            
        _goX1Y0 = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        _goX1Y0.name = "_x1Y0";
        _goX1Y0.transform.position = new Vector3(x1Y1Corner.x, x0Y0Corner.y, 0f);
        _goX1Y0.transform.localScale = new Vector3(2f, 2f, 2f);
    }

    public void RunScript()
    {
        RunScript(embeddedScript);
    }
    
    void AddVSprite(string id, string idParent, string url, float x0, float y0, float x1, float y1, bool keepAspect = true, CellStructure cellStructure = null)
    {
        string imageFullPath = baseURL + url;
        if (url.IndexOf('/') == -1)
            imageFullPath = PRUtils.UrlUp(baseURL, 2) + "/defaultImages/" + url;

        if (!existingId(idParent) || existingId(id))
            return;
        GameObject go = new GameObject(id);
        GameObject parent = FindInGOMap(idParent);
        if (parent != null)
            go.transform.parent = parent.transform;
        else
            go.transform.parent = transform;
        AddToGOMap(id, go);

        StartCoroutine(CoAddVSprite(go, idParent, imageFullPath, x0, y0, x1, y1, keepAspect, cellStructure));
    }

    public void AddText(string id, string idParent, string text, int fontSize)
    {
        GameObject goParent = FindInGOMap(idParent);
        if (goParent == null)
            return;

        AddText(goParent, id, text, fontSize);
    }
    
    public IEnumerator CoAddVSprite(GameObject go, string idParent, string url, float x0, float y0, float x1, float y1,
        bool keepAspect = true, CellStructure cellStructure = null)
    {
        SpriteRenderer spriteRenderer = go.AddComponent<SpriteRenderer>();
        StartCoroutine(DownloadSprite(url, go, x0, y0, x1, y1, keepAspect, cellStructure));
        VSprite vsprite = go.AddComponent<VSprite>();
        vsprite.url = url;
        
        yield return null;
    }

    void PostDownload(GameObject go, float x0, float y0, float x1, float y1, bool keepAspect = true)
    {
        if (keepAspect)
            MoveAndResizeSpriteAspect(go, new Vector2(x0, y0), new Vector2(x1, y1));
        else
            MoveAndResizeSprite(go, new Vector2(x0, y0), new Vector2(x1, y1));
        go.AddComponent<BoxCollider2D>();
    }

    IEnumerator DownloadSprite(string url, GameObject go, float x0, float y0, float x1, float y1, bool keepAspect = true, CellStructure cellStructure = null)
    {
        SpriteRenderer spriteRenderer = go.GetComponent<SpriteRenderer>();
        if (CacheImages.Contains(url))
        {
            Sprite sprite = CacheImages[url] as Sprite;
            if (cellStructure == null)            
                spriteRenderer.sprite = sprite;
            else 
                spriteRenderer.sprite = CreateSubspriteCell(sprite, cellStructure.x, cellStructure.y, cellStructure.nX, cellStructure.nY);
            spriteRenderer.sprite = sprite;
            PostDownload(go, x0, y0, x1, y1, keepAspect);
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
            // CreateSubspriteCell(Sprite originalSprite, int x, int y, int nX, int nY)
            Texture2D texture = DownloadHandlerTexture.GetContent(www);
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100.0f);
            AddToCacheImages(url, sprite);
            if (cellStructure == null)            
                spriteRenderer.sprite = sprite;
            else 
                spriteRenderer.sprite = CreateSubspriteCell(sprite, cellStructure.x, cellStructure.y, cellStructure.nX, cellStructure.nY);
            PostDownload(go, x0, y0, x1, y1, keepAspect);

            // Yield the sprite to the caller
            spriteRenderer.sprite = sprite;
            yield return sprite;
        }
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
        go.transform.position = new Vector3(position.x, position.y, go.transform.position.z + NextSpriteZ());

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

    public void AddText(GameObject go, string id, string text, int fontSize)
    {
        // Create a new TextMeshProUGUI object
        GameObject textObject = new GameObject(id);
        gameObjectMap.Add(id, textObject);

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
    
    public void AddLight(GameObject go, string id, string lightType, float intensity, float innerRadius, float outerRadius, float fallOffStrength)
    {
        GameObject lightObject = new GameObject(id);
        gameObjectMap.Add(id, lightObject);
        lightObject.transform.parent = go.transform;
        lightObject.transform.localPosition = Vector3.zero;

        Light2D light2D = lightObject.AddComponent<Light2D>();

        light2D.intensity = intensity;
        light2D.pointLightInnerRadius = innerRadius;
        light2D.pointLightOuterRadius = outerRadius;
        light2D.falloffIntensity = fallOffStrength;

        switch (lightType.ToLower())
        {
            case "global":
                light2D.lightType = Light2D.LightType.Global;
                break;
            case "spot":
                light2D.lightType = Light2D.LightType.Point;
                break;
            case "freeform":
                light2D.lightType = Light2D.LightType.Freeform;
                break;
            case "sprite":
                light2D.lightType = Light2D.LightType.Sprite;
                SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
                if (sr != null)
                {
                    light2D.lightCookieSprite = sr.sprite;
                }
                else
                {
                    Debug.LogWarning("The GameObject does not have a SpriteRenderer component. Cannot use sprite light type.");
                }
                break;
            default:
                Debug.LogWarning("Invalid light type passed. Please use either 'global', 'spot', 'freeform', or 'sprite'.");
                break;
        }
    }
    
    public void AddHovering(GameObject go, float hoverSpeed, float hoverRadius, float hoverHeight, float randomFactor)
    {
        if (go.GetComponent<HoveringBehavior2D>() != null)
        {
            Debug.LogWarning("The GameObject already has a HoveringBehavior2D component.");
            return;
        }

        // Add the HoveringBehavior2D component
        HoveringBehavior2D hoveringBehavior = go.AddComponent<HoveringBehavior2D>();

        // Set the properties
        hoveringBehavior.hoverSpeed = hoverSpeed;
        hoveringBehavior.hoverRadius = hoverRadius;
        hoveringBehavior.hoverHeight = hoverHeight;
        hoveringBehavior.randomFactor = randomFactor;
    }
    
    void SetDragTypeForGameObject(GameObject go, string type, float minX = 0, float minY = 0, float maxX = 0, float maxY = 0)
    {
        VSprite vSprite = go.GetComponent<VSprite>();

        if (vSprite != null)
        {
            VSprite.DragType dragType;
        
            try 
            {
                dragType = (VSprite.DragType)System.Enum.Parse(typeof(VSprite.DragType), type, true);
            }
            catch (System.ArgumentException)
            {
                Debug.LogError($"Invalid drag type: {type}");
                return;
            }

            vSprite.SetDragType(dragType, minX, maxX, minY, maxY);
        }
        else
        {
            Debug.Log("GameObject does not have a VSprite component.");
        }
    }

    Sprite CreateSubSprite(Sprite originalSprite, int x, int y, int width, int height)
    {
        Texture2D spriteTexture = originalSprite.texture;
        Rect spriteRect = new Rect(x, y, width, height);
        Vector2 pivot = new Vector2(0.5f, 0.5f); // Pivot in the center
        Sprite newSprite = Sprite.Create(spriteTexture, spriteRect, pivot, originalSprite.pixelsPerUnit);

        return newSprite;
    }

    Sprite CreateSubspriteCell(Sprite originalSprite, int x, int y, int nX, int nY)
    {
        // Calculate the width and height of each cell
        float cellWidthFloat = originalSprite.rect.width / nX;
        float cellHeightFloat = originalSprite.rect.height / nY;

        // Calculate the starting position of the desired cell
        int startX = (int)(x * cellWidthFloat);
        int startY = (int)(y * cellHeightFloat);

        // Calculate the width and height considering the remaining pixels for edge cells
        int cellWidth = (int)(x == nX - 1 ? originalSprite.rect.width - startX : cellWidthFloat);
        int cellHeight = (int)(y == nY - 1 ? originalSprite.rect.height - startY : cellHeightFloat);

        // Use the CreateSubSprite function to create the subsprite for the desired cell
        return CreateSubSprite(originalSprite, startX, startY, cellWidth, cellHeight);
    }

    List<Sprite> CreateAllSubspriteCells(Sprite originalSprite, int nX, int nY)
    {
        List<Sprite> subsprites = new List<Sprite>();

        for (int y = 0; y < nY; y++)
        {
            for (int x = 0; x < nX; x++)
            {
                Sprite cellSprite = CreateSubspriteCell(originalSprite, x, y, nX, nY);
                subsprites.Add(cellSprite);
            }
        }

        return subsprites;
    }

}