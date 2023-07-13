using UnityEngine;

public class Main : MonoBehaviour
{
    public static float CAMERA_Z = 10;
    public static float g_fDesignHeight = 2048.0f;
    public static float g_fDesignWidth = 1536.0f;
    public static float g_fScaleX;
    public static float g_fScaleY;

    public bool m_bPlayEffects = false;
    private static Color colorWhite = new Color(1.0f, 1.0f, 1.0f, 1.0f);
    private static Color color10PercTransparent = new Color(1.0f, 1.0f, 1.0f, 0.1f);
    private static Color color80PercTransparent = new Color(1.0f, 1.0f, 1.0f, 0.8f);
    private static Color colorTransparent = new Color(1.0f, 1.0f, 1.0f, 0f);
    private static float durLerpCarAnimation = 1.0f; //1f  car movement duration
    private static float currentLerpTimeCarAnim = 100f; //any number greater than zero
    private static float durLerpInsectAnimation = 1.5f; //1f  insect movement duration
    private static float currentLerpTimeInsectAnim = 100f; //any number greater than zero
    private static float durLerpWalkingAnimation = 2f; //walking movement duration
    private static float currentLerpTimeWalkingAnim = 100f;
    private static bool bStaticSpriteBounceAnimation = false, bStaticSpriteLinearScaleAnimation = false;
    private static float durScaleAnimation = 1.0f, durLinearScaleAnimation = 1.0f;
    private static float currentTimeDynamicSpritesBounceAnim = 100f, currentTimeLinearScaleAnim = 100f;
    private bool bInsideDelayedLoadNextScene = false;
    private bool bButtonRepeatInstructionsCovered = false;
    public static bool bListenDoNotMoveSprites = false;
    private static GameObject PanelShortMessage, TextShortMessage, ButtonCloseShortMessage;
    private Renderer GOInBetweenScenes;
    private bool bTestingAllLevels = false;
    private bool bTestingAllNumbersAudio = false;
    private static float startPos; //this is start position of the swipe
    private static int tenthScreenWidth;

    void Awake()
    {
        tenthScreenWidth = Screen.width / 10;
        if (tenthScreenWidth < 100) tenthScreenWidth = 100; //implement swipe to open HomeButton dialog
    }

    void Start()
    {
        g_fScaleX = (float)(Screen.width) / g_fDesignWidth;
        g_fScaleY = (float)(Screen.height) / g_fDesignHeight;
        }

    public static Object InstantiateObject(Object original)
    {
        return Instantiate(original); //Debug.Log("InstantiateObject");
    }

    
    public static GameObject CreateFullScreenGameObject(Sprite sprite)
    {
        // Create GameObject and add a SpriteRenderer
        GameObject fullScreenObject = new GameObject("FullScreenObject");
        SpriteRenderer spriteRenderer = fullScreenObject.AddComponent<SpriteRenderer>();

        // Set the sprite of the SpriteRenderer
        spriteRenderer.sprite = sprite;

        // Calculate scale
        float cameraHeight = Camera.main.orthographicSize * 2;
        Vector2 cameraSize = new Vector2(Camera.main.aspect * cameraHeight, cameraHeight);
        Vector2 spriteSize = spriteRenderer.sprite.bounds.size;

        Vector2 scale = fullScreenObject.transform.localScale;
        if (cameraSize.x >= cameraSize.y)
        { 
            // Landscape (or square)
            scale *= cameraSize.x / spriteSize.x;
        }
        else
        { 
            // Portrait
            scale *= cameraSize.y / spriteSize.y;
        }

        fullScreenObject.transform.position = Camera.main.transform.position;
        fullScreenObject.transform.localScale = scale;

        return fullScreenObject;
    }

    public static Rect RRect(Rect _rect)
    {
        float FilScreenWidth = _rect.width / g_fDesignWidth;
        float rectWidth = FilScreenWidth * Screen.width;
        float FilScreenHeight = _rect.height / g_fDesignHeight;
        float rectHeight = FilScreenHeight * Screen.height;
        float rectX = (_rect.x / g_fDesignWidth) * Screen.width;
        float rectY = (_rect.y / g_fDesignHeight) * Screen.height;
        return new Rect(rectX, rectY, rectWidth, rectHeight);
    }

    public static Vector3 GetWorldPosFromVirtualScreenXY(float x, float y)
    {
        //This function converts m_irsprite.X, m_irsprite.Y to v3position assigned to the sprite
        Vector3 screenPos = new Vector3(x * g_fScaleX, (g_fDesignHeight - y) * g_fScaleY, CAMERA_Z);
        Vector3 worldPos = Camera.main.ScreenToWorldPoint(screenPos);
        return worldPos;
    }

    public static Vector3 GetWorldPosFromScreenXY(float x, float y)
    {
        Vector3 mousePos = new Vector3(x, y, CAMERA_Z);
        Vector3 worldPos = Camera.main.ScreenToWorldPoint(mousePos);
        return worldPos;
    }

    public static Vector3 GetVirtualScreenXYFromWorld(Vector3 worldPos)
    {
        Vector3 screenPoint = Camera.main.WorldToScreenPoint(worldPos);
        Vector3 screenXY = new Vector3(screenPoint.x / g_fScaleX, screenPoint.y / g_fScaleY, 10);
        return screenXY;
    }

    public static Vector2 CameraExtents(Camera camera)
    {
        if (camera.orthographic)
            return new Vector2(2 * camera.orthographicSize * Screen.width / Screen.height, 2 * camera.orthographicSize);
        else
        {
            Debug.LogError("Camera is not orthographic!", camera);
            return new Vector2();
        }
    }

    public static void CreateSphere(float vsx, float vsy)
    {
        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        //sphere.transform.localScale = new Vector3(5.25f, 1.5f, 1.0f);
        sphere.transform.position = GetWorldPosFromVirtualScreenXY(vsx, vsy);
    }

}