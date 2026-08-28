using TMPro;
using UnityEngine;
using UnityEngine.UI;

// ============================================================================================
// Runtime restyle of the "Rate app" panel (MovingRatingsOptionsPanel + RateTheApp) into the
// current visual language: a rounded UiTheme.Surface card, Fredoka text, the five stars in one
// evenly-spaced row, a primary "Rate" button, a quiet "Maybe later" text button, and the shared
// corner X.
//
// RESTYLE vs REBUILD: the card's *contents* are re-laid-out in code, but every object that
// carries logic is REUSED, not recreated — the five star Buttons (their persistent
// RateApplication(1..5) calls and their single child Image, which is exactly what
// RateTheApp.RateApplication toggles), the Rate button and the Later button are reparented into
// the new layout rather than rebuilt. RateTheApp stays the logic owner and is not modified.
// The scene-built layout could not be restyled in place: the three scenes that carry the panel
// (_Library, _Story, _Bookstore) have diverged into three different sets of anchors, sizes and
// nested wrappers, and the labels are legacy UnityEngine.UI.Text, which cannot take a
// TMP_FontAsset at all.
//
// The only behavioural normalisation is the Rate / Later click wiring: _Story's "Rate" button is
// wired to RateLater (so rating never opened the store there) while the other two scenes wire it
// to RateNow. The persistent calls are switched off and replaced with runtime listeners so all
// three scenes behave the same.
//
// Idempotent: Apply() is called from MoveIn() on every showing and does its work once.
// ============================================================================================
public static class RateAppPanelStyle
{
    private const string ContentName  = "RateCardContent";
    private const string BackdropName = "RateBackdrop";

    // Card metrics, authored against the 1080x1920 portrait reference resolution.
    private const float CardWidth   = 880f;
    private const float CardHeight  = 560f;
    private const float StarSize    = 104f;
    private const float StarsHeight = 140f;
    private const float ButtonsRow  = 112f;

    /// <summary>
    /// Restyle the panel and return the full-screen backdrop that closes it (created next to the
    /// panel, so it does not slide with it). Safe to call repeatedly; the backdrop is created
    /// once and the same instance is returned afterwards.
    /// </summary>
    public static GameObject Apply(RectTransform container, RateTheApp rate)
    {
        if (container == null || rate == null) return null;

        var card = rate.transform as RectTransform;
        if (card == null) return null;

        var backdrop = EnsureBackdrop(container, rate);
        if (card.Find(ContentName) != null) return backdrop; // already styled

        // ---- container: a centred, fixed-size modal instead of three per-scene rects ----------
        // The slide animation is untouched: MovingRatingsOptionsPanel captured its off-screen
        // start position in Start(), and that position is still off-screen under these anchors.
        container.anchorMin = container.anchorMax = new Vector2(0.5f, 0.5f);
        container.pivot = new Vector2(0.5f, 0.5f);
        container.sizeDelta = new Vector2(CardWidth, CardHeight);
        container.localScale = Vector3.one;
        var containerImg = container.GetComponent<Image>();
        if (containerImg != null) containerImg.enabled = false; // the card below draws the surface

        // ---- card ----------------------------------------------------------------------------
        card.localScale = Vector3.one;
        DialogChrome.Stretch(card);
        DialogChrome.StyleCard(card.GetComponent<Image>());
        // The scene card drives its children through its own VerticalLayoutGroup, which would
        // squash the content root below to zero height. The new content brings its own layout.
        DisableSceneLayout(container);
        DisableSceneLayout(card);

        // ---- content -------------------------------------------------------------------------
        var content = new GameObject(ContentName, typeof(RectTransform), typeof(VerticalLayoutGroup));
        content.transform.SetParent(card, false);
        DialogChrome.Stretch(content.GetComponent<RectTransform>());
        var vlg = content.GetComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(48, 48, 48, 44);
        vlg.spacing = 26;
        vlg.childControlWidth = true;  vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
        vlg.childAlignment = TextAnchor.UpperCenter;

        var title = DialogChrome.MakeText(content.transform, "Title", "Enjoying ReadingBuddy?",
            52, TextAlignmentOptions.Center, UiTheme.TextPrimary);
        title.fontStyle = FontStyles.Bold;
        title.gameObject.AddComponent<LayoutElement>().preferredHeight = 72f;

        var sub = DialogChrome.MakeText(content.transform, "Subtitle",
            "Tap the stars to tell us how it's going.", 30, TextAlignmentOptions.Center,
            UiTheme.TextSecondary);
        sub.enableWordWrapping = true;
        sub.gameObject.AddComponent<LayoutElement>().preferredHeight = 46f;

        BuildStarsRow(content.transform, rate);
        BuildButtonsRow(content.transform, rate);

        // Everything the scene put in the card (the old question label, the nested wrappers now
        // emptied by the reparenting above, the unused "email us" panel and the invisible legacy
        // close button) is retired — after the reparenting, so nothing live is hidden with it.
        for (int i = card.childCount - 1; i >= 0; i--)
        {
            var child = card.GetChild(i);
            if (child.gameObject != content) child.gameObject.SetActive(false);
        }

        DialogChrome.AddCloseButton(card, rate.RateLater);
        return backdrop;
    }

    // Switch off any layout driver the scene put on an object we are about to size ourselves.
    private static void DisableSceneLayout(RectTransform rt)
    {
        foreach (var group in rt.GetComponents<LayoutGroup>()) group.enabled = false;
        foreach (var fitter in rt.GetComponents<ContentSizeFitter>()) fitter.enabled = false;
    }

    // The five stars in one evenly-spaced row. Each star keeps its Button and its single child
    // Image — the child is the "filled" star that RateTheApp.RateApplication activates, so it is
    // given the same sprite in the warm accent while the star itself becomes the empty outline.
    private static void BuildStarsRow(Transform parent, RateTheApp rate)
    {
        var rowGO = new GameObject("Stars", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        rowGO.transform.SetParent(parent, false);
        var hlg = rowGO.GetComponent<HorizontalLayoutGroup>();
        hlg.spacing = 0f;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childControlWidth = false; hlg.childControlHeight = false;
        hlg.childForceExpandWidth = true; hlg.childForceExpandHeight = false;
        var starsLe = rowGO.GetComponent<LayoutElement>();
        starsLe.preferredHeight = StarsHeight;
        starsLe.flexibleHeight = 0f;

        Color filled = UiTheme.Card(1).accent; // warm sand — the palette carries no gold

        if (rate.starButton == null) return;
        foreach (var star in rate.starButton)
        {
            if (star == null) continue;
            var srt = (RectTransform)star.transform;
            srt.SetParent(rowGO.transform, false);
            srt.anchorMin = srt.anchorMax = srt.pivot = new Vector2(0.5f, 0.5f);
            srt.sizeDelta = new Vector2(StarSize, StarSize);
            srt.localScale = Vector3.one;

            var outline = star.GetComponent<Image>();
            Sprite shape = outline != null ? outline.sprite : null;
            if (outline != null)
            {
                outline.color = UiTheme.Track;
                outline.preserveAspect = true;
            }
            star.transition = Selectable.Transition.None; // a colour tint would fight the two states
            TapFeedback.AddPressFeedback(star.gameObject);

            // Exactly the children RateApplication toggles — never add anything else under a star.
            foreach (Transform t in srt)
            {
                var fill = t.GetComponent<Image>();
                if (fill == null) continue;
                if (fill.sprite == null) fill.sprite = shape;
                fill.color = filled;
                fill.preserveAspect = true;
                fill.raycastTarget = false;
                DialogChrome.Stretch((RectTransform)t);
            }
        }
    }

    // "Maybe later" (quiet) on the left, "Rate" (primary) on the right.
    private static void BuildButtonsRow(Transform parent, RateTheApp rate)
    {
        var rowGO = new GameObject("Actions", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        rowGO.transform.SetParent(parent, false);
        var hlg = rowGO.GetComponent<HorizontalLayoutGroup>();
        hlg.spacing = 24f;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childControlWidth = true; hlg.childControlHeight = true;
        hlg.childForceExpandWidth = true; hlg.childForceExpandHeight = true;
        var rowLe = rowGO.GetComponent<LayoutElement>();
        rowLe.preferredHeight = ButtonsRow;
        // childForceExpandHeight makes the row itself report a flexible height, which the card's
        // vertical layout would then hand all its slack to. Pin it.
        rowLe.flexibleHeight = 0f;

        if (rate.rateLaterButton != null)
        {
            var later = Adopt(rate.rateLaterButton, rowGO.transform, "Maybe later", 34,
                              Color.clear, UiTheme.TextSecondary, bold: false);
            later.flexibleWidth = 0.8f;
            Rewire(rate.rateLaterButton, rate.RateLater);
        }

        if (rate.rateButton != null)
        {
            var rateEl = Adopt(rate.rateButton, rowGO.transform, "Rate", 38,
                               UiTheme.Primary, UiTheme.OnPrimary, bold: true);
            rateEl.flexibleWidth = 1.2f;
            Rewire(rate.rateButton, rate.RateNow);
            // RateApplication drives `interactable`, so the disabled state has to stay visible:
            // a colour tint fades the sage fill until at least one star is tapped.
            rate.rateButton.transition = Selectable.Transition.ColorTint;
            var colors = rate.rateButton.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = Color.white;
            colors.pressedColor = new Color(0.88f, 0.88f, 0.88f, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(1f, 1f, 1f, 0.35f);
            colors.colorMultiplier = 1f;
            rate.rateButton.colors = colors;
        }
    }

    // Reparent a scene-built button into the new row, repaint it and replace its legacy
    // UnityEngine.UI.Text label with a Fredoka TMP one.
    private static LayoutElement Adopt(Button button, Transform row, string label, float fontSize,
                                       Color fill, Color ink, bool bold)
    {
        var rt = (RectTransform)button.transform;
        rt.SetParent(row, false);
        rt.localScale = Vector3.one;

        var img = button.GetComponent<Image>();
        if (img != null)
        {
            img.sprite = DialogChrome.RoundedSprite();
            img.type = Image.Type.Sliced;
            img.color = fill;
            img.raycastTarget = true; // a fully transparent fill still takes the tap
        }
        // TapFeedback owns the press feedback. The Rate button re-enables ColorTint afterwards,
        // because it is the one button whose `interactable` state has to stay readable.
        button.transition = Selectable.Transition.None;
        TapFeedback.AddPressFeedback(button.gameObject);

        for (int i = rt.childCount - 1; i >= 0; i--)
            Object.Destroy(rt.GetChild(i).gameObject);

        var tmp = DialogChrome.MakeText(rt, "Label", label, fontSize, TextAlignmentOptions.Center, ink);
        if (bold) tmp.fontStyle = FontStyles.Bold;
        DialogChrome.Stretch(tmp.rectTransform);

        var le = button.GetComponent<LayoutElement>();
        if (le == null) le = button.gameObject.AddComponent<LayoutElement>();
        le.preferredHeight = ButtonsRow;
        return le;
    }

    // Silence the scene's persistent call (it points at a different method in _Story than in the
    // other two scenes) and drive the RateTheApp method the button is supposed to drive.
    private static void Rewire(Button button, UnityEngine.Events.UnityAction action)
    {
        for (int i = button.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
            button.onClick.SetPersistentListenerState(i, UnityEngine.Events.UnityEventCallState.Off);
        button.onClick.RemoveListener(action);
        button.onClick.AddListener(action);
    }

    // Full-screen dim behind the panel: tapping outside the card closes it down the same path as
    // the X. It is a sibling of the sliding container, so it does not slide with it.
    private static GameObject EnsureBackdrop(RectTransform container, RateTheApp rate)
    {
        var parent = container.parent;
        if (parent == null) return null;

        var existing = parent.Find(BackdropName);
        if (existing != null) return existing.gameObject;

        var go = new GameObject(BackdropName, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        DialogChrome.Stretch(go.GetComponent<RectTransform>());
        go.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);
        var button = go.GetComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.onClick.AddListener(rate.RateLater);

        // Backdrop first, then the panel — so the panel draws on top of the dim, and both draw
        // on top of whatever else the scene's canvas holds.
        go.transform.SetAsLastSibling();
        container.SetAsLastSibling();
        go.SetActive(false);
        return go;
    }
}
