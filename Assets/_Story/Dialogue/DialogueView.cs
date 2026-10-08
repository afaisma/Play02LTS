using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// The dialogue sheet: a card that slides up over the page text and leaves the picture visible.
/// Built in code with the shared kit (UiTheme, UiGlyphs, DialogChrome), like the reading-mode
/// picker. It only draws and reports taps; every rule lives in DialogueFlow / DialogueController.
///
/// Layout, top to bottom: [speaker button + (caption) + question] / choices / footer
/// (microphone sign or "Tap to answer", and Skip). After the right answer the header shows a
/// check mark and the praise, and the footer becomes the "Next page" button.
/// </summary>
public class DialogueView : MonoBehaviour
{
    // Fixed screen texts (reviewed for this audience: literal, no idioms, never "wrong").
    public const string TextSayOrTap = "Say it or tap it";
    public const string TextTapToAnswer = "Tap to answer";
    public const string TextSkip = "Skip";
    public const string TextNextPage = "Next page";
    public const string TextDone = "Done";

    private static readonly Color SageDark = Hex(0x566B43), SageFill = Hex(0xDDE6CF), SageBorder = Hex(0xB9C9A6);
    private static readonly Color BlueInk = Hex(0x436069), BlueFill = Hex(0xD7E2E6), BlueBorder = Hex(0xA9BEC5);
    private static readonly Color BlueSoft = Hex(0xE6EDEF), BlueMid = Hex(0xC5D6DB);
    private static readonly Color CardBg = Hex(0xFFFDF8), CaptionInk = Hex(0x5F5A4E);

    private const float HiddenBelow = 60f;   // the sheet's bottom corners sit below the screen edge
    private const float Offscreen = 1400f;
    private const float MinBody = 330f;      // a Yes / No card: padding, glyph, word

    private class ChoiceVisual
    {
        public GameObject go;
        public Outline outline;
        public CanvasGroup group;
        public Button button;
        public Color border;
    }

    private Canvas _canvas;
    private RectTransform _sheet, _content, _body, _footer;
    private RectTransform _fx, _starPill, _summary;   // rewards: confetti layer, stars on the sheet, end of book
    private CanvasGroup _sheetGroup;
    private TMP_Text _caption, _question;
    private Button _speakerButton;
    private GameObject _speakerIdle, _speakerPlaying, _checkBadge;
    private Image _speakerBg;
    private readonly Dictionary<string, ChoiceVisual> _choices = new Dictionary<string, ChoiceVisual>();
    private readonly List<RectTransform> _levelBars = new List<RectTransform>();
    private DialogueSpec _spec;
    private float _contentWidth, _bodyHeight;
    private Action<string> _onChoice;
    private Action _onReplay, _onSkip, _onNext, _onTap;

    public bool Visible => _sheet != null && _sheet.gameObject.activeSelf;

    public static DialogueView Create()
    {
        if (EventSystem.current == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        var go = new GameObject("DialogueCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 900; // above the story page, below the reading-mode picker (1001)
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 0.5f;

        var view = go.AddComponent<DialogueView>();
        view._canvas = canvas;
        view.BuildSheet();
        return view;
    }

    // ---------------------------------------------------------------- showing

    /// <param name="pictureBottomScreenY">Screen y (pixels) of the page picture's lower edge; the
    /// sheet's top edge goes there, so the picture stays visible. Negative = unknown.</param>
    public void Show(DialogueSpec spec, bool voiceOn, float pictureBottomScreenY, Func<string, string> resolveUrl,
                     Action<string> onChoice, Action onReplay, Action onSkip, Action onNext, Action onTap)
    {
        _spec = spec;
        _onChoice = onChoice; _onReplay = onReplay; _onSkip = onSkip; _onNext = onNext; _onTap = onTap;
        ClearStarPill();
        // First of all: a sheet that is still sliding away is not interactable, and a button
        // built under it would start in its greyed "disabled" look and keep it.
        _sheetGroup.interactable = true;

        Canvas.ForceUpdateCanvases();
        var canvasRect = (RectTransform)_canvas.transform;
        float canvasH = canvasRect.rect.height, canvasW = canvasRect.rect.width;
        float height = pictureBottomScreenY >= 0f ? pictureBottomScreenY / _canvas.scaleFactor + 40f : canvasH * 0.52f;
        height = Mathf.Clamp(height, canvasH * 0.42f, canvasH * 0.62f);

        // Side margins: 60 on a phone; wider on a tablet so the content is not stretched.
        float side = Mathf.Max(60f, (canvasW - 1000f) * 0.5f);
        float bottom = SafeAreaInsets.ForRect(_sheet).Bottom + 36f + HiddenBelow;
        // Around the choices: the top padding (56), the header (170), the footer (130) and the two
        // gaps of 40. The choices themselves need MinBody, or the footer is pushed off the screen
        // (a tablet in portrait: the picture is tall, so the sheet under it would be too short).
        float around = bottom + 56f + 170f + 130f + 80f - HiddenBelow;
        height = Mathf.Max(height, around + MinBody);
        _sheet.sizeDelta = new Vector2(0f, height + HiddenBelow);
        _content.offsetMin = new Vector2(side, bottom);
        _content.offsetMax = new Vector2(-side, -56f);
        _contentWidth = canvasW - 2f * side;
        _bodyHeight = height - around;

        _question.text = spec.question;
        SetCaption("");
        _checkBadge.SetActive(false);
        _speakerButton.gameObject.SetActive(true);
        SetSpeaking(false);

        BuildBody(spec, resolveUrl);
        BuildFooter(spec, voiceOn, praise: false, nextLabel: null);

        _sheet.gameObject.SetActive(true);
        _sheet.DOKill();
        _sheet.anchoredPosition = new Vector2(0f, -Offscreen);
        _sheet.DOAnchorPosY(-HiddenBelow, 0.3f).SetEase(Ease.OutCubic).SetUpdate(true);
    }

    public void Hide()
    {
        if (!Visible) return;
        KillChoiceTweens();
        _sheetGroup.interactable = false; // no button works while the sheet slides away (it still stops the tap)
        _sheet.DOKill();
        _sheet.DOAnchorPosY(-Offscreen, 0.25f).SetEase(Ease.InCubic).SetUpdate(true)
            .OnComplete(() => { if (_sheet != null) _sheet.gameObject.SetActive(false); });
    }

    /// <summary>Fade the choices already tried, outline the answer when a hint is due.</summary>
    public void Refresh(DialogueFlow flow, string caption)
    {
        SetCaption(caption);
        foreach (var kv in _choices)
        {
            ChoiceVisual v = kv.Value;
            bool faded = flow.faded.Contains(kv.Key);
            v.group.alpha = faded ? 0.3f : 1f;
            v.button.interactable = !faded;
            bool hinted = flow.hint && kv.Key == flow.spec.answer && !faded;
            v.outline.effectColor = hinted ? SageDark : v.border;
            v.outline.effectDistance = hinted ? new Vector2(9f, 9f) : new Vector2(4f, 4f);
            v.go.transform.DOKill();
            v.go.transform.localScale = Vector3.one;
            if (hinted)
                v.go.transform.DOScale(1.03f, 0.9f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetUpdate(true);
        }
    }

    /// <summary>After the right answer: check mark + praise, only the chosen choice, a Next button.</summary>
    public void ShowPraise(DialogueFlow flow, string praise, string nextLabel)
    {
        SetCaption("");
        _question.text = praise;
        _speakerButton.gameObject.SetActive(false);
        _checkBadge.SetActive(true);
        foreach (var kv in _choices)
        {
            ChoiceVisual v = kv.Value;
            v.go.transform.DOKill();
            v.go.transform.localScale = Vector3.one;
            v.button.interactable = false;
            bool chosen = kv.Key == flow.chosen;
            v.go.SetActive(chosen);
            v.group.alpha = 1f;
            v.outline.effectColor = SageDark;
            v.outline.effectDistance = new Vector2(9f, 9f);
        }
        BuildFooter(flow.spec, voiceOn: false, praise: true, nextLabel: nextLabel);
    }

    /// <summary>
    /// The reward after the answer (call after ShowPraise): the row of stars on the top edge of
    /// the sheet with the new star (and a caption such as "I heard you!"), a small move of the
    /// chosen card and, in the lively level, confetti.
    /// </summary>
    public void ShowReward(IList<bool> stars, int newIndex, bool glow, string caption, bool confetti)
    {
        ClearStarPill();
        _starPill = DialogueRewardView.BuildStarPill(_sheet, stars, newIndex, glow, caption,
            ((RectTransform)_canvas.transform).rect.width - 120f);
        _starPill.anchorMin = _starPill.anchorMax = new Vector2(0.5f, 1f);
        _starPill.pivot = new Vector2(0.5f, 0.5f);
        _starPill.anchoredPosition = Vector2.zero; // half above the sheet's edge: takes no room inside

        foreach (var kv in _choices)
            if (kv.Value.go != null && kv.Value.go.activeSelf)
                kv.Value.go.transform.DOPunchScale(Vector3.one * 0.05f, 0.5f, 1, 0f).SetUpdate(true);

        if (confetti) DialogueRewardView.Confetti(_fx, 22);
    }

    /// <summary>The end of the book: the collected stars at the top of the screen, with slow confetti.</summary>
    public void ShowSummary(string text, IList<bool> stars)
    {
        RemoveSummary(); // not the confetti: pieces of the last answer may still be falling
        float width = ((RectTransform)_canvas.transform).rect.width;
        _summary = DialogueRewardView.BuildSummary(_fx, text, stars, Mathf.Min(width - 80f, 980f));
        _summary.anchorMin = _summary.anchorMax = new Vector2(0.5f, 1f);
        _summary.pivot = new Vector2(0.5f, 1f);
        _summary.anchoredPosition = new Vector2(0f, -(SafeAreaInsets.ForRect(_fx).Top + 36f));
        _summary.localScale = Vector3.one * 0.8f;
        _summary.DOScale(1f, 0.4f).SetEase(Ease.OutBack).SetUpdate(true);
        RectTransform summary = _summary;
        summary.GetComponent<CanvasGroup>().DOFade(0f, 0.6f).SetDelay(9f).SetUpdate(true)
            .OnComplete(() => { if (summary != null) Destroy(summary.gameObject); });
        DialogueRewardView.Confetti(_fx, 36);
    }

    public void HideSummary()
    {
        if (_fx != null) DialogueRewardView.ClearConfetti(_fx);
        RemoveSummary();
    }

    private void RemoveSummary()
    {
        if (_summary == null) return;
        _summary.DOKill();
        _summary.GetComponent<CanvasGroup>().DOKill();
        Destroy(_summary.gameObject);
        _summary = null;
    }

    private void ClearStarPill()
    {
        if (_starPill == null) return;
        foreach (Transform t in _starPill.GetComponentsInChildren<Transform>(true))
        {
            t.DOKill();
            var img = t.GetComponent<Image>();
            if (img != null) img.DOKill();
        }
        Destroy(_starPill.gameObject);
        _starPill = null;
    }

    public void SetCaption(string text)
    {
        _caption.text = text ?? "";
        _caption.gameObject.SetActive(!string.IsNullOrEmpty(text));
    }

    /// <summary>The speaker button is filled while the question (or praise) is being spoken.</summary>
    public void SetSpeaking(bool speaking)
    {
        if (_speakerBg == null) return;
        _speakerBg.color = speaking ? SageDark : SageFill;
        _speakerPlaying.SetActive(speaking);
        _speakerIdle.SetActive(!speaking);
    }

    /// <summary>Sound mode: the level meter, 0..1.</summary>
    public void SetLevel(float level01)
    {
        int lit = Mathf.RoundToInt(Mathf.Clamp01(level01) * _levelBars.Count);
        for (int i = 0; i < _levelBars.Count; i++)
        {
            bool on = i < lit;
            // A small hill shape, so the lit part reads as a sound wave rather than a ruler.
            float h = on ? 34f + 56f * Mathf.Sin(Mathf.PI * (i + 0.5f) / Mathf.Max(1, lit)) : 22f;
            _levelBars[i].sizeDelta = new Vector2(24f, h);
            _levelBars[i].GetComponent<Image>().color = on ? BlueInk : BlueBorder;
        }
    }

    // ---------------------------------------------------------------- construction

    private void BuildSheet()
    {
        var sheetGO = new GameObject("Sheet", typeof(RectTransform), typeof(Image), typeof(Outline), typeof(CanvasGroup));
        sheetGO.transform.SetParent(transform, false);
        _sheet = sheetGO.GetComponent<RectTransform>();
        _sheetGroup = sheetGO.GetComponent<CanvasGroup>();
        _sheet.anchorMin = new Vector2(0f, 0f);
        _sheet.anchorMax = new Vector2(1f, 0f);
        _sheet.pivot = new Vector2(0.5f, 0f);
        DialogChrome.StyleCard(sheetGO.GetComponent<Image>()); // rounded Surface card that eats taps
        var edge = sheetGO.GetComponent<Outline>();
        edge.effectColor = UiTheme.Track;
        edge.effectDistance = new Vector2(0f, 4f);

        var contentGO = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup));
        contentGO.transform.SetParent(_sheet, false);
        _content = contentGO.GetComponent<RectTransform>();
        _content.anchorMin = Vector2.zero; _content.anchorMax = Vector2.one;
        var vlg = contentGO.GetComponent<VerticalLayoutGroup>();
        vlg.spacing = 40f;
        vlg.childControlWidth = true; vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;

        BuildHeader(contentGO.transform);

        _body = NewRect("Body", contentGO.transform);
        _body.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1f;

        _footer = NewRect("Footer", contentGO.transform);
        var fle = _footer.gameObject.AddComponent<LayoutElement>();
        fle.preferredHeight = 130f; fle.minHeight = 130f;

        _sheet.gameObject.SetActive(false);

        // Rewards are drawn over everything else on this canvas and never take a tap.
        _fx = NewRect("Rewards", transform);
        DialogChrome.Stretch(_fx);
    }

    private void BuildHeader(Transform parent)
    {
        var row = NewRect("Header", parent);
        var hlg = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 36f;
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childControlWidth = true; hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;
        var rle = row.gameObject.AddComponent<LayoutElement>();
        rle.preferredHeight = 170f; rle.minHeight = 170f;

        // Speaker: hear the question again.
        var sp = new GameObject("Speaker", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        sp.transform.SetParent(row, false);
        Fixed(sp.GetComponent<LayoutElement>(), 150f, 150f);
        _speakerBg = sp.GetComponent<Image>();
        _speakerBg.sprite = UiGlyphs.CircleSprite();
        _speakerButton = sp.GetComponent<Button>();
        _speakerButton.transition = Selectable.Transition.None;
        _speakerButton.onClick.AddListener(() => _onReplay?.Invoke());
        TapFeedback.AddPressFeedback(sp);
        _speakerIdle = GlyphHolder(sp.transform, "Idle");
        UiGlyphs.BuildSpeaker(_speakerIdle.transform, SageDark, waves: true, muted: false, size: 84f);
        _speakerPlaying = GlyphHolder(sp.transform, "Playing");
        UiGlyphs.BuildSpeaker(_speakerPlaying.transform, Color.white, waves: true, muted: false, size: 84f);

        // Check mark: shown in place of the speaker after the right answer.
        _checkBadge = new GameObject("Check", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        _checkBadge.transform.SetParent(row, false);
        Fixed(_checkBadge.GetComponent<LayoutElement>(), 150f, 150f);
        var cb = _checkBadge.GetComponent<Image>();
        cb.sprite = UiGlyphs.CircleSprite(); cb.color = SageDark; cb.raycastTarget = false;
        BuildCheck(_checkBadge.transform, Color.white, 96f);
        _checkBadge.SetActive(false);

        var col = NewRect("Text", row);
        col.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
        var cvl = col.gameObject.AddComponent<VerticalLayoutGroup>();
        cvl.spacing = 2f;
        cvl.childAlignment = TextAnchor.MiddleLeft;
        cvl.childControlWidth = true; cvl.childControlHeight = true;
        cvl.childForceExpandWidth = true; cvl.childForceExpandHeight = false;

        _caption = DialogChrome.MakeText(col, "Caption", "", 46f, TextAlignmentOptions.Left, CaptionInk);
        _caption.gameObject.AddComponent<LayoutElement>().preferredHeight = 58f;
        _question = DialogChrome.MakeText(col, "Question", "", 74f, TextAlignmentOptions.Left, UiTheme.TextPrimary);
        _question.fontStyle = FontStyles.Bold;
        _question.enableWordWrapping = true;
        _question.fontSizeMin = 44f;
        var qle = _question.gameObject.AddComponent<LayoutElement>();
        qle.preferredHeight = 170f; qle.flexibleHeight = 1f;
    }

    private void BuildBody(DialogueSpec spec, Func<string, string> resolveUrl)
    {
        KillChoiceTweens();
        Clear(_body);
        _choices.Clear();
        _levelBars.Clear();

        if (spec.IsSound) { BuildSoundBody(); return; }

        bool cards = false;
        foreach (DialogueChoice c in spec.choices) cards |= c.HasPicture || c.IsIcon;

        if (!cards)
        {
            var list = _body.gameObject.AddComponent<VerticalLayoutGroup>();
            list.spacing = 34f;
            list.childAlignment = TextAnchor.UpperCenter;
            list.childControlWidth = true; list.childControlHeight = true;
            list.childForceExpandWidth = true; list.childForceExpandHeight = false;
            foreach (DialogueChoice c in spec.choices) BuildWordRow(_body, c);
            return;
        }

        // Cards side by side; four or more wrap into rows of two. Icon cards (Yes / No) fill the
        // height; picture cards are as tall as their (square) picture plus the label.
        int perRow = spec.choices.Count > 3 ? 2 : spec.choices.Count;
        bool pictures = false;
        foreach (DialogueChoice c in spec.choices) pictures |= c.HasPicture;
        float cardWidth = (_contentWidth - 44f * (perRow - 1)) / perRow;
        int rowCount = (spec.choices.Count + perRow - 1) / perRow;
        // As tall as the picture and its label need, but never more than the sheet has.
        float cardHeight = Mathf.Min(cardWidth + 94f, (_bodyHeight - 40f * (rowCount - 1)) / rowCount);
        var rows = _body.gameObject.AddComponent<VerticalLayoutGroup>();
        rows.spacing = 40f;
        rows.childAlignment = TextAnchor.MiddleCenter;
        rows.childControlWidth = true; rows.childControlHeight = true;
        rows.childForceExpandWidth = true; rows.childForceExpandHeight = !pictures;
        RectTransform row = null;
        for (int i = 0; i < spec.choices.Count; i++)
        {
            if (i % perRow == 0)
            {
                row = NewRect("Row", _body);
                var hlg = row.gameObject.AddComponent<HorizontalLayoutGroup>();
                hlg.spacing = 44f;
                hlg.childControlWidth = true; hlg.childControlHeight = true;
                hlg.childForceExpandWidth = true; hlg.childForceExpandHeight = true;
            }
            BuildCard(row, spec.choices[i], i, resolveUrl, pictures ? cardHeight : -1f);
        }
    }

    private ChoiceVisual NewChoice(Transform parent, DialogueChoice choice, Color fill, Color border)
    {
        var go = new GameObject("Choice_" + choice.id,
            typeof(RectTransform), typeof(Image), typeof(Outline), typeof(Button), typeof(CanvasGroup));
        go.transform.SetParent(parent, false);
        var bg = go.GetComponent<Image>();
        bg.sprite = DialogChrome.RoundedSprite(); bg.type = Image.Type.Sliced; bg.color = fill;
        var outline = go.GetComponent<Outline>();
        outline.effectColor = border;
        outline.effectDistance = new Vector2(4f, 4f);
        var button = go.GetComponent<Button>();
        button.transition = Selectable.Transition.None;
        string id = choice.id;
        button.onClick.AddListener(() => _onChoice?.Invoke(id));
        TapFeedback.AddPressFeedback(go);
        var v = new ChoiceVisual { go = go, outline = outline, group = go.GetComponent<CanvasGroup>(), button = button, border = border };
        _choices[choice.id] = v;
        return v;
    }

    private void BuildWordRow(Transform parent, DialogueChoice choice)
    {
        ChoiceVisual v = NewChoice(parent, choice, CardBg, UiTheme.Track);
        var le = v.go.AddComponent<LayoutElement>();
        le.preferredHeight = 190f; le.minHeight = 130f;
        var label = DialogChrome.MakeText(v.go.transform, "Label", choice.text, 70f, TextAlignmentOptions.Left, UiTheme.TextPrimary);
        var rt = label.rectTransform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(64f, 16f); rt.offsetMax = new Vector2(-40f, -16f);
    }

    private void BuildCard(Transform parent, DialogueChoice choice, int index, Func<string, string> resolveUrl,
                           float height)
    {
        bool icon = choice.IsIcon;
        // Icon cards alternate sage / dusty blue; they also differ by glyph and word, never by colour alone.
        bool sage = index % 2 == 0;
        Color fill = icon ? (sage ? SageFill : BlueFill) : CardBg;
        Color border = icon ? (sage ? SageBorder : BlueBorder) : UiTheme.Track;
        Color ink = icon ? (sage ? Hex(0x3F5230) : Hex(0x33505A)) : UiTheme.TextPrimary;

        ChoiceVisual v = NewChoice(parent, choice, fill, border);
        // Equal widths: without this a card would be as wide as its picture file is large.
        var cle = v.go.AddComponent<LayoutElement>();
        cle.minWidth = 0f; cle.preferredWidth = 0f; cle.flexibleWidth = 1f;
        if (height > 0f) cle.preferredHeight = height;
        var vlg = v.go.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(22, 22, 22, 14);
        vlg.spacing = 10f;
        vlg.childAlignment = TextAnchor.MiddleCenter;
        vlg.childControlWidth = true; vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;

        if (icon)
        {
            var holder = NewRect("Icon", v.go.transform);
            var hle = holder.gameObject.AddComponent<LayoutElement>();
            hle.flexibleHeight = 1f; hle.minHeight = 150f;
            if (choice.IconName == "cross") UiGlyphs.BuildClose(holder, ink, 170f);
            else BuildCheck(holder, ink, 190f);
        }
        else
        {
            var pic = new GameObject("Picture", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            pic.transform.SetParent(v.go.transform, false);
            var ple = pic.GetComponent<LayoutElement>();
            // Explicit: an Image's own preferred size is its sprite's pixel size, which would
            // squeeze the label out of the card.
            ple.preferredHeight = Mathf.Max(60f, height - 138f); ple.minHeight = 60f; ple.flexibleHeight = 0f;
            ple.preferredWidth = 0f; ple.flexibleWidth = 1f;
            var img = pic.GetComponent<Image>();
            img.color = UiTheme.Track; // placeholder tone until the picture arrives
            img.raycastTarget = false;
            string url = resolveUrl != null ? resolveUrl(choice.image) : null;
            if (!string.IsNullOrEmpty(url)) StartCoroutine(LoadPicture(url, img));
        }

        if (choice.text.Length > 0)
        {
            var label = DialogChrome.MakeText(v.go.transform, "Label", choice.text, icon ? 96f : 66f,
                TextAlignmentOptions.Center, ink);
            label.fontStyle = FontStyles.Bold;
            var lle = label.gameObject.AddComponent<LayoutElement>();
            lle.preferredHeight = lle.minHeight = icon ? 130f : 92f;
        }
    }

    private static System.Collections.IEnumerator LoadPicture(string url, Image img)
    {
        yield return PRUtils.DownloadImage(url, img, true, true);
        if (img != null && img.sprite != null) img.color = Color.white;
    }

    private void BuildSoundBody()
    {
        var col = _body.gameObject.AddComponent<VerticalLayoutGroup>();
        col.spacing = 30f;
        col.childAlignment = TextAnchor.MiddleCenter;
        col.childControlWidth = false; col.childControlHeight = false;
        col.childForceExpandWidth = false; col.childForceExpandHeight = false;

        // The microphone is as large as the sheet allows (a small phone and a tablet leave less
        // room than a tall phone); the level meter keeps its height.
        float disc = Mathf.Clamp(_bodyHeight - 96f - 30f, 170f, 420f);
        var outer = Disc("Mic", _body, disc, BlueSoft);
        var mid = Disc("Mid", outer, disc * 0.74f, BlueMid);
        var inner = Disc("Inner", mid, disc * 0.52f, BlueInk);
        BuildMicrophone(UiGlyphs.NewBox(inner, "Glyph", disc * 0.36f), Color.white);

        var bars = NewRect("Level", _body);
        bars.sizeDelta = new Vector2(420f, 96f);
        var hlg = bars.gameObject.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 16f;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childControlWidth = false; hlg.childControlHeight = false;
        hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;
        for (int i = 0; i < 9; i++)
        {
            var bar = new GameObject("Bar" + i, typeof(RectTransform), typeof(Image));
            bar.transform.SetParent(bars, false);
            var img = bar.GetComponent<Image>();
            img.sprite = DialogChrome.RoundedSprite(); img.type = Image.Type.Sliced; img.raycastTarget = false;
            _levelBars.Add(bar.GetComponent<RectTransform>());
        }
        SetLevel(0f);
    }

    private void BuildFooter(DialogueSpec spec, bool voiceOn, bool praise, string nextLabel)
    {
        Clear(_footer);

        if (praise)
        {
            if (string.IsNullOrEmpty(nextLabel)) return;
            var next = new GameObject("Next", typeof(RectTransform), typeof(Image), typeof(Button));
            next.transform.SetParent(_footer, false);
            DialogChrome.Stretch(next.GetComponent<RectTransform>());
            var bg = next.GetComponent<Image>();
            bg.sprite = DialogChrome.RoundedSprite(); bg.type = Image.Type.Sliced; bg.color = SageDark;
            var b = next.GetComponent<Button>();
            b.transition = Selectable.Transition.None;
            b.onClick.AddListener(() => _onNext?.Invoke());
            TapFeedback.AddPressFeedback(next);
            var label = DialogChrome.MakeText(next.transform, "Label", nextLabel, 64f, TextAlignmentOptions.Center, Color.white);
            label.fontStyle = FontStyles.Bold;
            DialogChrome.Stretch(label.rectTransform, 12f);
            return;
        }

        var hlg = _footer.gameObject.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 24f;
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childControlWidth = true; hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;

        if (spec.IsSound)
        {
            // Touch always works, also when the answer is "make a sound".
            var tap = new GameObject("TapToAnswer", typeof(RectTransform), typeof(Image), typeof(Outline), typeof(Button), typeof(LayoutElement));
            tap.transform.SetParent(_footer, false);
            Fixed(tap.GetComponent<LayoutElement>(), 470f, 120f);
            var bg = tap.GetComponent<Image>();
            bg.sprite = DialogChrome.RoundedSprite(); bg.type = Image.Type.Sliced; bg.color = CardBg;
            var o = tap.GetComponent<Outline>(); o.effectColor = UiTheme.Track; o.effectDistance = new Vector2(4f, 4f);
            var b = tap.GetComponent<Button>();
            b.transition = Selectable.Transition.None;
            b.onClick.AddListener(() => _onTap?.Invoke());
            TapFeedback.AddPressFeedback(tap);
            var label = DialogChrome.MakeText(tap.transform, "Label", TextTapToAnswer, 50f, TextAlignmentOptions.Center, UiTheme.TextPrimary);
            DialogChrome.Stretch(label.rectTransform, 10f);
        }
        else if (voiceOn)
        {
            var chip = new GameObject("MicSign", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            chip.transform.SetParent(_footer, false);
            Fixed(chip.GetComponent<LayoutElement>(), 104f, 104f);
            var ci = chip.GetComponent<Image>();
            ci.sprite = UiGlyphs.CircleSprite(); ci.color = BlueFill; ci.raycastTarget = false;
            BuildMicrophone(UiGlyphs.NewBox(chip.transform, "Glyph", 66f), BlueInk);
            var hint = DialogChrome.MakeText(_footer, "SayOrTap", TextSayOrTap, 46f, TextAlignmentOptions.Left, CaptionInk);
            var hle = hint.gameObject.AddComponent<LayoutElement>();
            hle.preferredWidth = 430f; hle.preferredHeight = 104f;
        }

        var spacer = NewRect("Spacer", _footer);
        spacer.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

        if (spec.settings.skip)
        {
            var skip = new GameObject("Skip", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            skip.transform.SetParent(_footer, false);
            Fixed(skip.GetComponent<LayoutElement>(), 190f, 120f);
            skip.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f); // invisible, but takes the tap
            var b = skip.GetComponent<Button>();
            b.transition = Selectable.Transition.None;
            b.onClick.AddListener(() => _onSkip?.Invoke());
            TapFeedback.AddPressFeedback(skip);
            var label = DialogChrome.MakeText(skip.transform, "Label", TextSkip, 46f, TextAlignmentOptions.Right, CaptionInk);
            DialogChrome.Stretch(label.rectTransform);
        }
    }

    // ---------------------------------------------------------------- small parts

    // Check mark: a short and a long bar meeting at the bottom.
    private static void BuildCheck(Transform parent, Color ink, float size)
    {
        var box = UiGlyphs.NewBox(parent, "CheckGlyph", size);
        UiGlyphs.AddShape(box, "Short", null, -21f, -11f, 34f, 12f, -45f, ink);
        UiGlyphs.AddShape(box, "Long", null, 9f, 0f, 66f, 12f, 45f, ink);
    }

    // Microphone, the same drawing as the picker's "I read" row (that one is private there).
    private static void BuildMicrophone(Transform box, Color ink)
    {
        UiGlyphs.AddShape(box, "Capsule", UiGlyphs.CircleSprite(), 0f, 20f, 30f, 48f, 0f, ink);
        var clip = UiGlyphs.AddClip(box, "Bracket", 0f, -4f, 60f, 26f);
        UiGlyphs.AddShape(clip, "Arc", UiGlyphs.RingSprite(), 0f, 13f, 56f, 56f, 0f, ink);
        UiGlyphs.AddShape(box, "Stand", null, 0f, -26f, 8f, 20f, 0f, ink);
        UiGlyphs.AddShape(box, "Base", null, 0f, -38f, 38f, 8f, 0f, ink);
    }

    private static RectTransform Disc(string name, Transform parent, float size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(size, size);
        var img = go.GetComponent<Image>();
        img.sprite = UiGlyphs.CircleSprite(); img.color = color; img.raycastTarget = false;
        return rt;
    }

    private static GameObject GlyphHolder(Transform parent, string name)
    {
        var rt = NewRect(name, parent);
        DialogChrome.Stretch(rt);
        return rt.gameObject;
    }

    private static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }

    private static void Fixed(LayoutElement le, float w, float h)
    {
        le.preferredWidth = w; le.minWidth = w; le.flexibleWidth = 0f;
        le.preferredHeight = h; le.minHeight = h; le.flexibleHeight = 0f;
    }

    private void KillChoiceTweens()
    {
        foreach (var kv in _choices)
            if (kv.Value.go != null) kv.Value.go.transform.DOKill();
    }

    // Removes the children and any layout group (the body and the footer are rebuilt per dialogue).
    private static void Clear(RectTransform rt)
    {
        for (int i = rt.childCount - 1; i >= 0; i--)
        {
            Transform child = rt.GetChild(i);
            child.SetParent(null, false);
            Destroy(child.gameObject);
        }
        var group = rt.GetComponent<LayoutGroup>();
        if (group != null) DestroyImmediate(group);
    }

    private void OnDestroy()
    {
        KillChoiceTweens();
        ClearStarPill();
        HideSummary();
        if (_sheet != null) _sheet.DOKill();
    }

    private static Color Hex(uint rgb) =>
        new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);
}
