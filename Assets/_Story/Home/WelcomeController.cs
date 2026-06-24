using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// ============================================================================================
// Welcome / loading scene (_Welcome) — kid-friendly intro that replaces the old _Message launcher.
// Light & playful (UiTheme): warm cream page, bright pastel demo cards, rounded Fredoka font.
// It is the app's first scene: while the catalog downloads, the shared loading button advances to
// _Home. The rich intro (demo cards + connectivity note) is shown on every launch; first launch
// waits for a Continue tap, returning launches auto-continue after ~5s (Continue still works early).
// UI is built in Awake() so the loading button exists before Globals.Start() binds it.
// ============================================================================================
public class WelcomeController : MonoBehaviour
{
    [SerializeField] private TMP_FontAsset uiFont; // rounded kid font (Fredoka); falls back to default

    private const string SeenPref = "welcome_seen";
    private const float AutoAdvanceSeconds = 5f; // returning users auto-continue after this many seconds

    private bool _firstRun;

    private struct Demo { public string imgA, imgB, title, sub; public Demo(string a, string b, string t, string s){imgA=a;imgB=b;title=t;sub=s;} }
    private static readonly Demo[] Demos =
    {
        new Demo("welcome/demo_read_a", "welcome/demo_read_b", "Read along together",           "Words light up as it's read — tap any word to hear it"),
        new Demo("welcome/demo_ladder", "",                    "Learn to read, level by level", "A gentle path from first sounds up"),
        new Demo("welcome/demo_age_a",  "welcome/demo_age_b",  "Just-right for their age",       "Pick an age and the library tunes itself"),
    };

    private void Awake()
    {
        // The rich intro (demo cards + connectivity note) is shown on every launch. First launch
        // waits for the user to tap Continue; returning launches auto-continue after a few seconds.
        _firstRun = PlayerPrefs.GetInt(SeenPref, 0) == 0;
        BuildUI(true);
        if (_firstRun) { PlayerPrefs.SetInt(SeenPref, 1); PlayerPrefs.Save(); }
    }

    private void Start()
    {
        if (!_firstRun) StartCoroutine(AutoAdvanceReturning());
    }

    // Returning users: linger on the intro for AutoAdvanceSeconds, then go to _Home. We also wait
    // for the catalog so _Home isn't empty — so the delay is "at least" AutoAdvanceSeconds. The
    // Continue button is still live throughout, so the user can skip the wait by tapping it.
    private IEnumerator AutoAdvanceReturning()
    {
        float t0 = Time.time;
        while (Globals.g_listPRBooks == null || Globals.g_listPRBooks.Count == 0)
            yield return new WaitForSeconds(0.25f);
        float remaining = AutoAdvanceSeconds - (Time.time - t0);
        if (remaining > 0f) yield return new WaitForSeconds(remaining);
        Navigation.GoToHome();
    }

    private void BuildUI(bool firstRun)
    {
        if (EventSystem.current == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        var canvasGO = new GameObject("WelcomeCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 0.5f;

        var bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
        bg.transform.SetParent(canvasGO.transform, false);
        Stretch(bg.GetComponent<RectTransform>());
        bg.GetComponent<Image>().color = UiTheme.Bg;

        var content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup));
        content.transform.SetParent(canvasGO.transform, false);
        Stretch(content.GetComponent<RectTransform>());
        var vlg = content.GetComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(72, 72, 120, 84);
        vlg.spacing = 30;
        vlg.childControlWidth = true; vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
        vlg.childAlignment = TextAnchor.UpperCenter;

        var brand = MakeText(content.transform, "Brand", "ReadingBuddy", 58, TextAlignmentOptions.Left);
        brand.fontStyle = FontStyles.Bold; brand.color = UiTheme.Primary;
        brand.gameObject.AddComponent<LayoutElement>().preferredHeight = 72f;

        if (firstRun)
        {
            var hi = MakeText(content.transform, "Welcome", "Welcome!", 92, TextAlignmentOptions.Left);
            hi.fontStyle = FontStyles.Bold; hi.color = UiTheme.TextPrimary;
            hi.gameObject.AddComponent<LayoutElement>().preferredHeight = 112f;

            var sub = MakeText(content.transform, "Subtitle",
                "A few things that make reading here a little magical", 32, TextAlignmentOptions.Left);
            sub.color = UiTheme.TextSecondary;
            sub.gameObject.AddComponent<LayoutElement>().preferredHeight = 46f;

            for (int i = 0; i < Demos.Length; i++) BuildDemoRow(content.transform, Demos[i], i);
            BuildConnectivityNote(content.transform);
        }
        else
        {
            var sub = MakeText(content.transform, "Subtitle", "Welcome back!", 40, TextAlignmentOptions.Left);
            sub.color = UiTheme.TextSecondary;
            sub.gameObject.AddComponent<LayoutElement>().preferredHeight = 56f;
        }

        // Flexible spacer pushes the Continue button to the bottom and fills the page.
        var spacer = new GameObject("Spacer", typeof(RectTransform), typeof(LayoutElement));
        spacer.transform.SetParent(content.transform, false);
        spacer.GetComponent<LayoutElement>().flexibleHeight = 1f;

        BuildLoadingButton(content.transform);
    }

    private void BuildDemoRow(Transform parent, Demo d, int idx)
    {
        var palette = UiTheme.Card(idx);

        var row = new GameObject("Demo_" + d.imgA, typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        row.transform.SetParent(parent, false);
        row.GetComponent<LayoutElement>().preferredHeight = 230f;
        var rimg = row.GetComponent<Image>();
        rimg.sprite = RoundedSprite(); rimg.type = Image.Type.Sliced;
        rimg.color = palette.fill;
        var hlg = row.GetComponent<HorizontalLayoutGroup>();
        hlg.padding = new RectOffset(20, 24, 18, 18);
        hlg.spacing = 22;
        hlg.childControlWidth = true; hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = true;
        hlg.childAlignment = TextAnchor.MiddleLeft;

        var thumbGO = new GameObject("Thumb", typeof(RectTransform), typeof(LayoutElement), typeof(RectMask2D));
        thumbGO.transform.SetParent(row.transform, false);
        thumbGO.GetComponent<LayoutElement>().preferredWidth = 250f;

        var front = MakeThumbImage(thumbGO.transform, d.imgA);
        var anim = thumbGO.AddComponent<DemoCardAnim>();
        anim.front = front;
        if (!string.IsNullOrEmpty(d.imgB))
        {
            var back = MakeThumbImage(thumbGO.transform, d.imgB);
            var bc = back.color; bc.a = 0f; back.color = bc;
            anim.back = back;
        }
        else anim.zoom = true;

        var col = new GameObject("Col", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
        col.transform.SetParent(row.transform, false);
        col.GetComponent<LayoutElement>().flexibleWidth = 1f;
        var cvl = col.GetComponent<VerticalLayoutGroup>();
        cvl.spacing = 6; cvl.childAlignment = TextAnchor.MiddleLeft;
        cvl.childControlWidth = true; cvl.childControlHeight = true;
        cvl.childForceExpandWidth = true; cvl.childForceExpandHeight = false;

        var t = MakeText(col.transform, "Title", d.title, 42, TextAlignmentOptions.Left);
        t.fontStyle = FontStyles.Bold; t.color = palette.accent;
        t.gameObject.AddComponent<LayoutElement>().preferredHeight = 54f;
        var s = MakeText(col.transform, "Sub", d.sub, 28, TextAlignmentOptions.Left);
        s.color = UiTheme.TextSecondary; s.enableWordWrapping = true;
    }

    private void BuildConnectivityNote(Transform parent)
    {
        var note = new GameObject("Connectivity", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        note.transform.SetParent(parent, false);
        note.GetComponent<LayoutElement>().preferredHeight = 150f;
        var nimg = note.GetComponent<Image>();
        nimg.sprite = RoundedSprite(); nimg.type = Image.Type.Sliced;
        nimg.color = UiTheme.Card(4).fill; // soft blue

        var t = MakeText(note.transform, "Text",
            "A good internet connection is recommended — each story downloads its narration and pictures the first time it's opened. Books you've opened before keep working offline.",
            26, TextAlignmentOptions.Left);
        t.color = UiTheme.Card(4).accent;
        t.enableWordWrapping = true;
        var rt = t.rectTransform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(24, 16); rt.offsetMax = new Vector2(-24, -16);
    }

    private void BuildLoadingButton(Transform parent)
    {
        var go = new GameObject("ButtonLoadingRetryContinue",
            typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        go.GetComponent<LayoutElement>().preferredHeight = 128f;
        var img = go.GetComponent<Image>();
        img.sprite = RoundedSprite(); img.type = Image.Type.Sliced;
        img.color = UiTheme.Primary;

        var label = MakeText(go.transform, "Text", "Loading Library Catalog", 40, TextAlignmentOptions.Center);
        label.color = UiTheme.OnPrimary; label.fontStyle = FontStyles.Bold;
        Stretch(label.rectTransform);
    }

    // ---------------------------------------------------------------- helpers

    private static Sprite _rounded;
    private static Sprite RoundedSprite()
    {
        if (_rounded != null) return _rounded;
        const int r = 32;
        int size = r * 2 + 4;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float fx = x + 0.5f, fy = y + 0.5f;
                float cx = Mathf.Clamp(fx, r, size - r);
                float cy = Mathf.Clamp(fy, r, size - r);
                float dx = fx - cx, dy = fy - cy;
                px[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(r - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f));
            }
        tex.SetPixels(px); tex.Apply();
        _rounded = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f),
            100f, 0, SpriteMeshType.FullRect, new Vector4(r, r, r, r));
        return _rounded;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    private static Image MakeThumbImage(Transform parent, string resPath)
    {
        var go = new GameObject("Img", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        Stretch(go.GetComponent<RectTransform>());
        var img = go.GetComponent<Image>();
        img.preserveAspect = true; img.raycastTarget = false;
        var tex = Resources.Load<Texture2D>(resPath);
        if (tex != null) img.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
        else img.color = new Color(0f, 0f, 0f, 0.10f);
        return img;
    }

    private TMP_Text MakeText(Transform parent, string name, string text, float size, TextAlignmentOptions align)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.font = uiFont != null ? uiFont : TMP_Settings.defaultFontAsset;
        tmp.text = text;
        tmp.fontSize = size;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = size * 0.6f;
        tmp.fontSizeMax = size;
        tmp.alignment = align;
        tmp.color = UiTheme.TextPrimary;
        tmp.raycastTarget = false;
        return tmp;
    }
}

// Animates a welcome demo card: ping-pong crossfade between two states (front<->back), or a gentle
// Ken Burns zoom on a single frame. Pure code, no assets.
public class DemoCardAnim : MonoBehaviour
{
    public Image front, back;
    public bool zoom;
    public float period = 3.2f;

    private void Update()
    {
        float t = Mathf.SmoothStep(0f, 1f, Mathf.PingPong(Time.time / period, 1f));
        if (back != null)
        {
            var cf = front.color; cf.a = 1f - t; front.color = cf;
            var cb = back.color;  cb.a = t;      back.color = cb;
        }
        if (zoom && front != null)
            front.rectTransform.localScale = new Vector3(1f + 0.07f * t, 1f + 0.07f * t, 1f);
    }
}
