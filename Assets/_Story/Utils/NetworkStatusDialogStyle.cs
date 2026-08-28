using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// ============================================================================================
// Runtime restyle of the "No internet" dialog (the NetworkStatus prefab's _networkStatusDialog)
// into the current visual language: a rounded UiTheme.Surface card, centred, with Fredoka text
// that explains WHY connectivity matters — the same note the onboarding screen gives — plus the
// shared corner X and the existing "Try again" button.
//
// RESTYLE vs REBUILD: the card's contents are rebuilt in code and only the Try-again Button is
// reused (its persistent onClick already calls NetworkStatus.OnTryAgainClickede). In-place
// restyling was not viable: the dialog is a small hand-placed box whose RectTransform mixes
// stretch anchors with a large sizeDelta and an off-centre offset, it hangs off a plain Transform
// carrying its own position/scale, and the five scenes that instantiate the prefab each override
// those values differently. The decorator therefore re-parents the dialog straight onto the root
// canvas and normalises it, which is what makes it land centred at 1080x1920 everywhere.
//
// Idempotent, and applied at show time rather than in Start so it also lands after SceneThemer
// (which recolours _Settings / _Parents on their own Start).
// ============================================================================================
public static class NetworkStatusDialogStyle
{
    private const string CardName = "NetworkCard";
    private const float CardWidth = 880f;

    private const string Body =
        "Stories download their narration and pictures the first time they're opened, so an " +
        "internet connection is needed for new books. Books you've opened before keep working offline.";

    /// <summary>Restyle the dialog. Safe to call on every showing; the work happens once.</summary>
    public static void Apply(GameObject dialog, Action onDismiss)
    {
        if (dialog == null) return;
        var root = dialog.transform as RectTransform;
        if (root == null) return;
        if (root.Find(CardName) != null) return; // already styled

        // ---- root: an invisible full-screen layer that only positions the card ---------------
        // Deliberately NOT a blocking scrim: being offline must not lock the child out of the
        // books they have already downloaded. Only the card itself takes taps.
        var canvas = dialog.GetComponentInParent<Canvas>();
        if (canvas != null) root.SetParent(canvas.rootCanvas.transform, false);
        root.localScale = Vector3.one;
        root.anchoredPosition = Vector2.zero;
        DialogChrome.Stretch(root);
        root.SetAsLastSibling();
        var rootImg = root.GetComponent<Image>();
        if (rootImg != null)
        {
            rootImg.color = Color.clear;
            rootImg.raycastTarget = false;
        }

        // The scene/prefab's own contents: the Try-again button is adopted, the rest retired.
        var tryAgain = root.GetComponentInChildren<Button>(true);
        var legacy = new Transform[root.childCount];
        for (int i = 0; i < root.childCount; i++) legacy[i] = root.GetChild(i);

        // ---- card ----------------------------------------------------------------------------
        var cardGO = new GameObject(CardName,
            typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        cardGO.transform.SetParent(root, false);
        var card = cardGO.GetComponent<RectTransform>();
        card.anchorMin = card.anchorMax = card.pivot = new Vector2(0.5f, 0.5f);
        card.sizeDelta = new Vector2(CardWidth, 0f);
        card.anchoredPosition = Vector2.zero;
        DialogChrome.StyleCard(cardGO.GetComponent<Image>());

        var vlg = cardGO.GetComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(48, 48, 48, 44);
        vlg.spacing = 26;
        vlg.childControlWidth = true;  vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
        vlg.childAlignment = TextAnchor.UpperCenter;
        cardGO.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var title = DialogChrome.MakeText(cardGO.transform, "Title", "No internet connection",
            48, TextAlignmentOptions.Center, UiTheme.TextPrimary);
        title.fontStyle = FontStyles.Bold;
        title.enableWordWrapping = true;
        title.gameObject.AddComponent<LayoutElement>().preferredHeight = 66f;

        var body = DialogChrome.MakeText(cardGO.transform, "Body", Body,
            30, TextAlignmentOptions.Center, UiTheme.TextSecondary);
        body.enableWordWrapping = true;
        // Fixed box + MakeText's auto-sizing, the same treatment the onboarding note uses: the
        // copy always fits, and shrinks rather than clipping if the canvas is narrower.
        body.gameObject.AddComponent<LayoutElement>().preferredHeight = 180f;

        if (tryAgain != null) AdoptTryAgain(tryAgain, cardGO.transform);

        DialogChrome.AddCloseButton(card, onDismiss);

        // Everything the prefab/scene put in the dialog is retired — except the Try-again button,
        // which by now has been re-parented into the card and so is no longer a child of root.
        foreach (var child in legacy)
            if (child != null && child.parent == root) child.gameObject.SetActive(false);
    }

    // Reuse the prefab's Try-again Button (keeping its persistent OnTryAgainClickede call) as the
    // card's primary action.
    private static void AdoptTryAgain(Button button, Transform card)
    {
        var rt = (RectTransform)button.transform;
        rt.SetParent(card, false);
        rt.localScale = Vector3.one;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);

        var img = button.GetComponent<Image>();
        if (img != null)
        {
            img.sprite = DialogChrome.RoundedSprite();
            img.type = Image.Type.Sliced;
            img.color = UiTheme.Primary;
        }
        button.transition = Selectable.Transition.None;
        TapFeedback.AddPressFeedback(button.gameObject);

        for (int i = rt.childCount - 1; i >= 0; i--)
            UnityEngine.Object.Destroy(rt.GetChild(i).gameObject);

        var label = DialogChrome.MakeText(rt, "Label", "Try again", 38,
            TextAlignmentOptions.Center, UiTheme.OnPrimary);
        label.fontStyle = FontStyles.Bold;
        DialogChrome.Stretch(label.rectTransform);

        var le = button.GetComponent<LayoutElement>();
        if (le == null) le = button.gameObject.AddComponent<LayoutElement>();
        le.preferredHeight = 112f;
        le.flexibleWidth = 0f;
    }
}
