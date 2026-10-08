using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// The question for the grown-up before a book with spoken questions starts: may the app use the
/// microphone? Asked once, before page 1, so the system's own permission question never comes up
/// in the middle of a story (where a child may tap "Don't Allow", and iOS asks only once).
///
/// "Allow microphone" -> the system question. "Tap only" (or a refusal of the system question)
/// switches the microphone for dialogues off (DialogueController.MicPrefKey); the book then works
/// by touch and this screen does not come back.
/// </summary>
public class DialogueMicAsk : MonoBehaviour
{
    public const string TitleText = "This book asks questions";
    public const string BodyText =
        "Children can answer by speaking or tapping. To hear spoken answers, the app needs the microphone. " +
        "Listening happens on this device, and no audio is saved or sent. " +
        "Choose Tap only and we won\u2019t ask again.";
    public const string AllowText = "Allow microphone";
    public const string TouchText = "Tap only";

    /// <summary>True while the screen is up (page swipes are off, as under the reading-mode picker).</summary>
    public static bool IsOpen { get; private set; }

    /// <summary>Should this book start with the question?</summary>
    public static bool Needed(string script)
    {
#if UNITY_WEBGL
        return false;
#else
        return DialogueController.MicAllowed
               && !DialogueMicPermission.Granted
               && DialogueScript.WantsMicrophone(script);
#endif
    }

    /// <summary>Shows the screen and waits for the answer (and for the system question after "Allow").</summary>
    public static IEnumerator Ask()
    {
        bool answered = false, allow = false;
        DialogueMicAsk screen = Show(a => { allow = a; answered = true; });
        while (!answered) yield return null;

        bool refused = false;
        if (allow)
        {
            yield return DialogueMicPermission.Request();
            allow = DialogueMicPermission.Granted;
            refused = !allow;
        }
        if (!allow)
        {
            PlayerPrefs.SetInt(DialogueController.MicPrefKey, 0);
            // "Allow" was chosen but the system said no (it asks only once): remembered, so the
            // microphone comes back by itself when it is allowed in the system settings.
            if (refused) PlayerPrefs.SetInt(DialogueController.MicRefusedPrefKey, 1);
            else PlayerPrefs.DeleteKey(DialogueController.MicRefusedPrefKey);
            PlayerPrefs.Save();
        }
        Debug.Log("[Dialogue] microphone question: " + (allow ? "allowed" : "tap only"));
        if (screen != null) Destroy(screen.gameObject);
        IsOpen = false;
    }

    private void OnDestroy() => IsOpen = false; // the scene went away with the screen up

    // ---------------------------------------------------------------- the screen

    public static DialogueMicAsk Show(Action<bool> onAnswer)
    {
        if (EventSystem.current == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        var root = new GameObject("DialogueMicAsk", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1002; // above the reading-mode picker (1001): this question comes first
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 0.5f;
        var screen = root.AddComponent<DialogueMicAsk>();

        // Backdrop: dims the page and takes every tap.
        var back = new GameObject("Backdrop", typeof(RectTransform), typeof(Image));
        back.transform.SetParent(root.transform, false);
        DialogChrome.Stretch((RectTransform)back.transform);
        back.GetComponent<Image>().color = new Color(0.29f, 0.27f, 0.24f, 0.75f);

        var card = new GameObject("Card", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup),
            typeof(ContentSizeFitter));
        card.transform.SetParent(root.transform, false);
        var crt = (RectTransform)card.transform;
        crt.anchorMin = crt.anchorMax = crt.pivot = new Vector2(0.5f, 0.5f);
        crt.sizeDelta = new Vector2(900f, 0f);
        DialogChrome.StyleCard(card.GetComponent<Image>());
        var vlg = card.GetComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(64, 64, 64, 56);
        vlg.spacing = 36f;
        vlg.childControlWidth = true; vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
        card.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        TMP_Text title = DialogChrome.MakeText(card.transform, "Title", TitleText, 64f,
            TextAlignmentOptions.Center, UiTheme.TextPrimary);
        title.fontStyle = FontStyles.Bold;
        title.enableAutoSizing = false;

        TMP_Text body = DialogChrome.MakeText(card.transform, "Body", BodyText, 44f,
            TextAlignmentOptions.Center, UiTheme.TextPrimary);
        body.enableAutoSizing = false;
        body.lineSpacing = 12f;

        MakeButton(card.transform, "MicAllow", AllowText, new Color32(0x56, 0x6B, 0x43, 0xFF), UiTheme.OnPrimary,
            () => onAnswer(true));
        MakeButton(card.transform, "MicTouchOnly", TouchText, UiTheme.Track, UiTheme.TextPrimary,
            () => onAnswer(false));

        IsOpen = true;
        return screen;
    }

    private static void MakeButton(Transform parent, string name, string text, Color fill, Color ink, Action onClick)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.sprite = DialogChrome.RoundedSprite(); img.type = Image.Type.Sliced; img.color = fill;
        var le = go.GetComponent<LayoutElement>();
        le.preferredHeight = 130f; le.minHeight = 130f;
        var button = go.GetComponent<Button>();
        button.transition = Selectable.Transition.None; // TapFeedback owns the press feedback
        bool done = false; // one answer only
        button.onClick.AddListener(() => { if (done) return; done = true; onClick(); });
        TapFeedback.AddPressFeedback(go);
        TMP_Text label = DialogChrome.MakeText(go.transform, "Label", text, 52f, TextAlignmentOptions.Center, ink);
        label.fontStyle = FontStyles.Bold;
        DialogChrome.Stretch(label.rectTransform, 12f);
    }
}
