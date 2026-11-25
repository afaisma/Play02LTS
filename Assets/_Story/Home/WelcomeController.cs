using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// ============================================================================================
// Welcome / loading scene (_Welcome) — kid-friendly intro that replaces the old _Message launcher.
// Light & playful (UiTheme): warm cream page, bright pastel demo cards, rounded Fredoka font.
// It is the app's first scene: while the catalog downloads, the shared loading button advances to
// _Home. The rich intro (demo cards + connectivity note) is shown on every launch; first launch
// waits for a Continue tap (Story Comprehension: that tap shows the demo question first); on
// returning launches a bar inside the button counts down 5 s from the moment it becomes
// "Continue", then the page auto-continues (a tap still works early).
// UI is built in Awake() so the loading button exists before Globals.Start() binds it.
// ============================================================================================
public class WelcomeController : MonoBehaviour
{
    [SerializeField] private TMP_FontAsset uiFont; // rounded kid font (Fredoka); falls back to default

    private const string SeenPref = "welcome_seen";
    private const float AutoAdvanceSeconds = 5f; // returning users auto-continue after this many seconds

    private bool _firstRun;

    // Returning-launch countdown: the shared loading button plus the bar inside it.
    // Set in BuildLoadingButton; the bar stays inactive until the button says "Continue".
    private Button _continueButton;
    private TMP_Text _continueLabel;
    private Image _countdownBar;

    // subShort: the shorter line used only when the page does not fit the screen (UseShortWordingIfNeeded).
    private struct Demo { public string imgA, imgB, title, sub, subShort; public Demo(string a, string b, string t, string s, string sShort = null){imgA=a;imgB=b;title=t;sub=s;subShort=sShort;} }
    // Story Comprehension: what the app does, in the parent's words. No pictures yet.
    // The shorter lines were checked by a second model on 2026-11-24 (round 7 brief).
    private static readonly Demo[] TalkDemos =
    {
        new Demo("", "", "Every answer counts",            "Encourage your child to engage with the story, not just listen.",
                                                           "Help your child take part in the story."),
        new Demo("", "", "Responses stay on your phone",   "No audio is saved or sent anywhere."),
        new Demo("", "", "You choose the level",           "Four levels, from touch only to full answers. Change it any time.",
                                                           "Four levels. Change any time."),
    };
    private const string TalkSubtitle = "The app reads a picture book aloud. After a few pages, it asks a question. Your child can respond by touching the screen or speaking.";
    private const string TalkSubtitleShort = "The app reads a picture book aloud and asks questions. Your child can answer by touch or voice.";
    private const string TalkDownloadNote = "After you choose the level, the books download to your phone (about 250 MB, Wi-Fi recommended). Then they also work without internet.";
    private const string TalkDownloadNoteShort = "After you choose the level, the books download (about 250 MB, Wi-Fi recommended). Then they work without internet too.";

    // Everything above the Continue button is in a scroll area (BuildUI). Story Comprehension's
    // texts with a shorter line, and that line.
    private RectTransform _scrollViewport, _scrollContent;
    private readonly List<(TMP_Text text, string shortText)> _shortWording = new List<(TMP_Text, string)>();
    private static readonly Demo[] Demos =
    {
        new Demo("welcome/demo_read_a", "welcome/demo_read_b", "Read along together",           "Words light up as it's read — tap any word to hear it"),
        new Demo("welcome/demo_speech_a", "welcome/demo_speech_b", "Speech recognition",     "The app follows the child's reading, highlighting each word in real time"),
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
        UseShortWordingIfNeeded();
        if (!_firstRun) StartCoroutine(AutoAdvanceReturning());
        else if (Edition.Current.questionBooksOnly) StartCoroutine(DemoOnContinue());
    }

    // Story Comprehension: the shorter lines replace the full ones only on a screen where the page
    // does not fit with the full ones (a 3:4 iPad, for example). Still too tall after that, the
    // page scrolls. Runs before the first frame, so the full lines never flash.
    private void UseShortWordingIfNeeded()
    {
        Canvas.ForceUpdateCanvases();
        float room = _scrollViewport.rect.height;
        float need = LayoutUtility.GetPreferredHeight(_scrollContent);
        if (_shortWording.Count == 0 || need <= room || room <= 0f)
        {
            Debug.Log($"[Welcome] page needs {need:F0} of {room:F0}: full wording");
            return;
        }
        foreach (var (text, shortText) in _shortWording) text.text = shortText;
        Canvas.ForceUpdateCanvases();
        Debug.Log($"[Welcome] page needs {need:F0} of {room:F0}: short wording, now {LayoutUtility.GetPreferredHeight(_scrollContent):F0}");
    }

    // The shared loading button says "Continue": the catalog is loaded and the button is live.
    private bool ContinueShown() =>
        _continueButton != null && _continueButton.interactable &&
        _continueLabel != null && _continueLabel.text == "Continue";

    // ---------------------------------------------------------------- the demo question

    // Story Comprehension, first start: the page stays until the parent taps Continue, so it can
    // be read. That tap slides one real question up over this page, so the parent sees and tries
    // what the child will do. It is the book dialogue itself (DialogueView and DialogueFlow),
    // answered by touch, with pictures inside the app; no sound, no microphone.
    public const string DemoQuestion = "Touch the bear.";
    public const string DemoCaption = "Try it: your child will see questions like this";
    public const string DemoPraise = "Yes! That is the bear.";
    public const string DemoNextLabel = "Choose the level";

    private DialogueView _demo;
    private DialogueFlow _demoFlow;

    // Until the catalog is loaded the button keeps Globals' own handler ("Loading Library Catalog",
    // "Connect to the Internet and Retry"). Globals sets the handler once per catalog download, and a
    // new download starts only from the Retry state, so the handler set here stays.
    private IEnumerator DemoOnContinue()
    {
        while (!ContinueShown()) yield return null;
        Debug.Log("[Welcome] first launch: Continue shown, waiting for the tap");
        _continueButton.onClick.RemoveAllListeners();
        _continueButton.onClick.AddListener(ShowDemoQuestion);
    }

    private void ShowDemoQuestion()
    {
        if (_demo != null) return; // the sheet covers the button; a second tap must not stack a second one
        Debug.Log("[Welcome] Continue tapped: demo question");
        var spec = new DialogueSpec { question = DemoQuestion, answer = "bear", praiseText = DemoPraise };
        spec.choices.Add(new DialogueChoice { id = "girl", text = "Girl", image = "res://welcome/talk_girl" });
        spec.choices.Add(new DialogueChoice { id = "bear", text = "Bear", image = "res://welcome/talk_bear" });
        spec.settings.input = DialogueInput.Touch;
        spec.settings.skip = true;
        _demoFlow = new DialogueFlow(spec);
        _demo = DialogueView.Create();
        _demo.Show(spec, voiceOn: false, choicesShown: true, pictureBottomScreenY: -1f, resolveUrl: url => url,
            onChoice: DemoAnswer, onReplay: () => { }, onSkip: DemoDone, onNext: DemoDone, onTap: () => { });
        _demo.Refresh(_demoFlow, DemoCaption);
    }

    private void DemoAnswer(string id)
    {
        switch (_demoFlow.Answer(id))
        {
            case DialogueFlow.Reaction.Correct:
                _demo.ShowPraise(_demoFlow, DemoPraise, DemoNextLabel);
                break;
            case DialogueFlow.Reaction.Retry:
                _demo.Refresh(_demoFlow, DialogueController.CaptionLookAgain);
                break;
        }
    }

    // "Choose the level" and Skip: Home, where the level sheet opens. The catalog is loaded (the demo
    // comes only from the real Continue), so this is what Continue does without the demo.
    private void DemoDone()
    {
        if (_demo != null) _demo.Hide();
        Debug.Log("[Welcome] demo question done, going Home");
        Globals.Instance.Home();
    }

    // Returning users: once the shared loading button has turned into "Continue" (catalog loaded,
    // button interactable), show a countdown bar inside it that empties over AutoAdvanceSeconds,
    // then go to _Home. No bar and no countdown while the button says "Loading Library Catalog"
    // or "Connect to the Internet and Retry". The Continue button is live throughout, so a tap
    // skips the wait (the scene change kills this coroutine).
    private IEnumerator AutoAdvanceReturning()
    {
        while (!ContinueShown()) yield return null;

        Debug.Log("[Welcome] Continue shown, countdown " + AutoAdvanceSeconds + "s");
        _countdownBar.fillAmount = 1f;
        _countdownBar.gameObject.SetActive(true);
        // realtimeSinceStartup, not unscaledTime: unscaledTime is the frame-START time, and the
        // scene-activation frame is long, so 5 s measured from it can end ~0.1 s after the label
        // actually switched to "Continue". Sampled here, after the switch, the 5 s hold strictly.
        float t0 = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - t0 < AutoAdvanceSeconds)
        {
            _countdownBar.fillAmount = Mathf.Clamp01(1f - (Time.realtimeSinceStartup - t0) / AutoAdvanceSeconds);
            yield return null;
        }
        _countdownBar.fillAmount = 0f;
        Debug.Log("[Welcome] countdown over, going Home");
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

        var page = new GameObject("Page", typeof(RectTransform), typeof(VerticalLayoutGroup));
        page.transform.SetParent(canvasGO.transform, false);
        Stretch(page.GetComponent<RectTransform>());
        var vlg = page.GetComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(72, 72, 120, 84);
        // The scroll area (headline first) is the first child and the Continue button the last, so
        // one call keeps the headline off the island and the button off the home indicator.
        SafeAreaInsets.ApplyLayoutPadding(vlg);
        vlg.spacing = 30;
        vlg.childControlWidth = true; vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
        vlg.childAlignment = TextAnchor.UpperCenter;

        // Everything above the button scrolls, so a screen too short for the texts can still show
        // all of them; the button stays at the bottom. Where the page fits, nothing moves.
        var scrollGO = new GameObject("Scroll", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect), typeof(LayoutElement));
        scrollGO.transform.SetParent(page.transform, false);
        scrollGO.GetComponent<LayoutElement>().flexibleHeight = 1f;
        scrollGO.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f); // takes the drag between the cards
        var content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        content.transform.SetParent(scrollGO.transform, false);
        _scrollContent = content.GetComponent<RectTransform>();
        _scrollContent.anchorMin = new Vector2(0f, 1f); _scrollContent.anchorMax = new Vector2(1f, 1f);
        _scrollContent.pivot = new Vector2(0.5f, 1f);
        _scrollContent.offsetMin = Vector2.zero; _scrollContent.offsetMax = Vector2.zero;
        var cvlg = content.GetComponent<VerticalLayoutGroup>();
        cvlg.spacing = 30;
        cvlg.childControlWidth = true; cvlg.childControlHeight = true;
        cvlg.childForceExpandWidth = true; cvlg.childForceExpandHeight = false;
        cvlg.childAlignment = TextAnchor.UpperCenter;
        content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var scroll = scrollGO.GetComponent<ScrollRect>();
        scroll.horizontal = false; scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 28f;
        _scrollViewport = scrollGO.GetComponent<RectTransform>();
        scroll.viewport = _scrollViewport;
        scroll.content = _scrollContent;

        if (firstRun)
        {
            // One centred headline instead of a brand line + greeting. The word "ReadingBuddy" stays
            // branded via rich text; the rest is regular headline colour. Wrapping is on and the box
            // is two lines tall, so "Welcome to" / "ReadingBuddy!" may break without clipping (and
            // MakeText's auto-sizing shrinks it further on narrower screens).
            bool talk = Edition.Current.questionBooksOnly;
            var hi = MakeText(content.transform, "Welcome",
                talk ? "Books that ask your child questions"
                     : "Welcome to <color=#" + ColorUtility.ToHtmlStringRGB(UiTheme.Primary) + ">ReadingBuddy</color>!",
                talk ? 72 : 104, TextAlignmentOptions.Center);
            hi.fontStyle = FontStyles.Bold; hi.color = UiTheme.TextPrimary;
            hi.enableWordWrapping = true;
            hi.gameObject.AddComponent<LayoutElement>().preferredHeight = 260f; // room for two 104pt lines

            var sub = MakeText(content.transform, "Subtitle",
                talk ? TalkSubtitle : "A few things that make reading here a little magical", 40, TextAlignmentOptions.Center);
            sub.color = UiTheme.TextSecondary;
            sub.enableWordWrapping = true; // as tall as its lines
            if (talk) _shortWording.Add((sub, TalkSubtitleShort));

            Demo[] demos = talk ? TalkDemos : Demos;
            for (int i = 0; i < demos.Length; i++) BuildDemoRow(content.transform, demos[i], i);
            BuildConnectivityNote(content.transform);
        }
        else
        {
            var brand = MakeText(content.transform, "Brand", Edition.Current.appName, 58, TextAlignmentOptions.Center);
            brand.fontStyle = FontStyles.Bold; brand.color = UiTheme.Primary;
            brand.gameObject.AddComponent<LayoutElement>().preferredHeight = 72f;

            var sub = MakeText(content.transform, "Subtitle", "Welcome back!", 40, TextAlignmentOptions.Center);
            sub.color = UiTheme.TextSecondary;
            sub.gameObject.AddComponent<LayoutElement>().preferredHeight = 56f;
        }

        BuildLoadingButton(page.transform);
    }

    private void BuildDemoRow(Transform parent, Demo d, int idx)
    {
        var palette = UiTheme.Card(idx);

        var row = new GameObject("Demo_" + d.imgA, typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        row.transform.SetParent(parent, false);
        row.GetComponent<LayoutElement>().minHeight = 230f; // taller when the text needs it
        var rimg = row.GetComponent<Image>();
        rimg.sprite = RoundedSprite(); rimg.type = Image.Type.Sliced;
        rimg.color = palette.fill;
        var hlg = row.GetComponent<HorizontalLayoutGroup>();
        hlg.padding = new RectOffset(20, 24, 18, 18);
        hlg.spacing = 22;
        hlg.childControlWidth = true; hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = true;
        hlg.childAlignment = TextAnchor.MiddleLeft;

        if (!string.IsNullOrEmpty(d.imgA)) // a row without pictures (Talk) is text only
        {
            var thumbGO = new GameObject("Thumb", typeof(RectTransform), typeof(LayoutElement), typeof(RectMask2D));
            thumbGO.transform.SetParent(row.transform, false);
            thumbGO.GetComponent<LayoutElement>().preferredWidth = 250f;
            thumbGO.GetComponent<LayoutElement>().minWidth = 250f; // a long line beside it must not squeeze it

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
        }
        else row.GetComponent<LayoutElement>().minHeight = 170f;

        var col = new GameObject("Col", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
        col.transform.SetParent(row.transform, false);
        col.GetComponent<LayoutElement>().flexibleWidth = 1f;
        var cvl = col.GetComponent<VerticalLayoutGroup>();
        cvl.spacing = 6; cvl.childAlignment = TextAnchor.MiddleLeft;
        cvl.childControlWidth = true; cvl.childControlHeight = true;
        cvl.childForceExpandWidth = true; cvl.childForceExpandHeight = false;

        var t = MakeText(col.transform, "Title", d.title, 48, TextAlignmentOptions.Left);
        t.fontStyle = FontStyles.Bold; t.color = palette.accent;
        t.gameObject.AddComponent<LayoutElement>().preferredHeight = 60f;
        var s = MakeText(col.transform, "Sub", d.sub, 40, TextAlignmentOptions.Left);
        s.color = UiTheme.TextSecondary; s.enableWordWrapping = true;
        if (d.subShort != null) _shortWording.Add((s, d.subShort));
    }

    private void BuildConnectivityNote(Transform parent)
    {
        // As tall as its text: a layout group around the text instead of a fixed height.
        var note = new GameObject("Connectivity", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
        note.transform.SetParent(parent, false);
        var nimg = note.GetComponent<Image>();
        nimg.sprite = RoundedSprite(); nimg.type = Image.Type.Sliced;
        nimg.color = UiTheme.Card(4).fill; // soft blue
        var nvl = note.GetComponent<VerticalLayoutGroup>();
        nvl.padding = new RectOffset(24, 24, 16, 16);
        nvl.childControlWidth = true; nvl.childControlHeight = true;
        nvl.childForceExpandWidth = true; nvl.childForceExpandHeight = false;

        bool talk = Edition.Current.downloadAllBooks;
        var t = MakeText(note.transform, "Text",
            talk
                ? TalkDownloadNote
                : "A good internet connection is recommended — each story downloads its narration and pictures the first time it's opened. Books you've opened before keep working offline.",
            40, TextAlignmentOptions.Left);
        t.color = UiTheme.Card(4).accent;
        t.enableWordWrapping = true;
        if (talk) _shortWording.Add((t, TalkDownloadNoteShort));
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

        // Countdown bar for returning launches: a lighter strip along the bottom edge, inside the
        // rounded corners, under the label. Filled from the left, so the remaining part stays
        // anchored at the left edge and the right end moves left as time runs out (Andrey's
        // feedback on build 335). Inactive until AutoAdvanceReturning sees the button become
        // "Continue".
        var barGO = new GameObject("CountdownBar", typeof(RectTransform), typeof(Image));
        barGO.transform.SetParent(go.transform, false);
        var barRT = barGO.GetComponent<RectTransform>();
        barRT.anchorMin = new Vector2(0f, 0f);
        barRT.anchorMax = new Vector2(1f, 0f);
        barRT.offsetMin = new Vector2(26f, 12f);
        barRT.offsetMax = new Vector2(-26f, 24f);
        _countdownBar = barGO.GetComponent<Image>();
        // A sprite is required: with a null sprite, Image ignores type Filled and draws a full quad.
        _countdownBar.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
        _countdownBar.color = new Color(1f, 1f, 1f, 0.55f); // lighter than UiTheme.Primary, label stays clear of it
        _countdownBar.type = Image.Type.Filled;
        _countdownBar.fillMethod = Image.FillMethod.Horizontal;
        _countdownBar.fillOrigin = (int)Image.OriginHorizontal.Left;
        _countdownBar.fillAmount = 1f;
        _countdownBar.raycastTarget = false;
        barGO.SetActive(false);

        var label = MakeText(go.transform, "Text", "Loading Library Catalog", 48, TextAlignmentOptions.Center);
        label.color = UiTheme.OnPrimary; label.fontStyle = FontStyles.Bold;
        Stretch(label.rectTransform);

        _continueButton = go.GetComponent<Button>();
        _continueLabel = label;
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
        else img.enabled = false; // screenshot not in Resources (yet) — draw nothing, not a blank box
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
        tmp.fontSizeMin = Mathf.Max(UiTheme.AdultTextMin, size * 0.6f); // never below 40 (adults read this page)
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
