using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Home of ReadingBuddy Talk (_HomeTalk): one screen with the books of the chosen question level.
/// Top: the app name and the level chip; under it a line that says what the level means; then the
/// book tiles (cover, name, how many questions the child has answered); at the bottom the
/// "For grown-ups" door to Settings, behind the same small sum as ReadingBuddy's.
///
/// The level sheet (QuestionLevel A-D in plain words) opens by itself on the first start, before
/// the first book, and later from the chip. Built in code, like HomeController; no prefab.
/// </summary>
public class HomeTalkController : MonoBehaviour
{
    private const float RetryInterval = 0.5f;

    private GameObject _canvasRoot;
    private RectTransform _contentRoot;
    private GameObject _loadingLabel;
    private GameObject _sheet;
    private GameObject _gate;

    private void Start()
    {
        BuildInfo.LogOnce();
        MemProbe.InstallLowMemoryHandler();
        MemProbe.Log("scene " + gameObject.scene.name);
        BuildCanvas();
        StartCoroutine(BuildWhenCatalogReady());
    }

    private IEnumerator BuildWhenCatalogReady()
    {
        while (Globals.g_allPRBooks == null || Globals.g_allPRBooks.Count == 0)
        {
            ShowLoading(true);
            yield return new WaitForSeconds(RetryInterval);
        }
        ShowLoading(false);
        BuildContent();
        if (!QuestionLevel.IsChosen) ShowLevelSheet(true);
    }

    // ---------------------------------------------------------------- canvas

    private void BuildCanvas()
    {
        if (_canvasRoot != null) return;
        if (EventSystem.current == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        _canvasRoot = new GameObject("HomeTalkCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = _canvasRoot.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var scaler = _canvasRoot.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 0.5f;

        var bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
        bg.transform.SetParent(_canvasRoot.transform, false);
        DialogChrome.Stretch(bg.GetComponent<RectTransform>());
        bg.GetComponent<Image>().color = UiTheme.Bg;

        var scrollGO = new GameObject("Scroll", typeof(RectTransform), typeof(ScrollRect), typeof(RectMask2D));
        scrollGO.transform.SetParent(_canvasRoot.transform, false);
        DialogChrome.Stretch(scrollGO.GetComponent<RectTransform>());
        var scroll = scrollGO.GetComponent<ScrollRect>();
        scroll.horizontal = false; scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 28f;

        var content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        content.transform.SetParent(scrollGO.transform, false);
        _contentRoot = content.GetComponent<RectTransform>();
        _contentRoot.anchorMin = new Vector2(0f, 1f); _contentRoot.anchorMax = new Vector2(1f, 1f);
        _contentRoot.pivot = new Vector2(0.5f, 1f);
        _contentRoot.offsetMin = Vector2.zero; _contentRoot.offsetMax = Vector2.zero;
        var vlg = content.GetComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(48, 48, 90, 60);
        SafeAreaInsets.ApplyLayoutPadding(vlg);
        vlg.spacing = 28;
        vlg.childControlWidth = true; vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
        vlg.childAlignment = TextAnchor.UpperCenter;
        content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.viewport = scrollGO.GetComponent<RectTransform>();
        scroll.content = _contentRoot;
    }

    private void ShowLoading(bool show)
    {
        if (_loadingLabel == null)
        {
            var t = DialogChrome.MakeText(_canvasRoot.transform, "Loading", "Loading…", 48, TextAlignmentOptions.Center, UiTheme.TextPrimary);
            DialogChrome.Stretch(t.rectTransform);
            _loadingLabel = t.gameObject;
        }
        _loadingLabel.SetActive(show);
    }

    // ---------------------------------------------------------------- content

    private void BuildContent()
    {
        for (int i = _contentRoot.childCount - 1; i >= 0; i--)
            Destroy(_contentRoot.GetChild(i).gameObject);

        BuildTitleRow(_contentRoot);
        BuildLevelLine(_contentRoot);
        BuildBookGrid(_contentRoot);
        BuildGrownupsFooter(_contentRoot);
        LayoutRebuilder.ForceRebuildLayoutImmediate(_contentRoot);
    }

    private static int LevelIndex => System.Array.IndexOf(QuestionLevel.Letters, QuestionLevel.Current);

    private void BuildTitleRow(Transform parent)
    {
        var row = new GameObject("TitleRow", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        row.transform.SetParent(parent, false);
        row.GetComponent<LayoutElement>().preferredHeight = 100f;
        var hlg = row.GetComponent<HorizontalLayoutGroup>();
        hlg.spacing = 16;
        hlg.childControlWidth = true; hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = true;
        hlg.childAlignment = TextAnchor.MiddleLeft;

        var title = DialogChrome.MakeText(row.transform, "Title", Edition.Current.appName, 60, TextAlignmentOptions.Left, UiTheme.Primary);
        title.fontStyle = FontStyles.Bold;
        title.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

        // The level chip: letter, "Level", a small arrow. Opens the level sheet.
        var chip = new GameObject("LevelChip", typeof(RectTransform), typeof(Image), typeof(Button), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        chip.transform.SetParent(row.transform, false);
        var cle = chip.GetComponent<LayoutElement>(); cle.preferredWidth = 290f; cle.preferredHeight = 96f;
        var cimg = chip.GetComponent<Image>(); cimg.sprite = DialogChrome.RoundedSprite(); cimg.type = Image.Type.Sliced; cimg.color = UiTheme.Track;
        var chl = chip.GetComponent<HorizontalLayoutGroup>();
        chl.padding = new RectOffset(12, 24, 10, 10); chl.spacing = 14;
        chl.childControlWidth = true; chl.childControlHeight = true;
        chl.childForceExpandWidth = false; chl.childForceExpandHeight = true;
        chl.childAlignment = TextAnchor.MiddleLeft;

        var disc = new GameObject("Letter", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        disc.transform.SetParent(chip.transform, false);
        disc.GetComponent<LayoutElement>().preferredWidth = 76f;
        var dimg = disc.GetComponent<Image>(); dimg.sprite = UiGlyphs.CircleSprite(); dimg.color = UiTheme.Primary; dimg.raycastTarget = false; dimg.preserveAspect = true;
        var letter = DialogChrome.MakeText(disc.transform, "Text", QuestionLevel.Current.ToUpperInvariant(), 44, TextAlignmentOptions.Center, UiTheme.OnPrimary);
        letter.fontStyle = FontStyles.Bold;
        DialogChrome.Stretch(letter.rectTransform);

        var word = DialogChrome.MakeText(chip.transform, "Word", "Level", 34, TextAlignmentOptions.Left, UiTheme.TextPrimary);
        word.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
        // A small triangle pointing down (the font has no arrow glyph).
        var arrowBox = new GameObject("Arrow", typeof(RectTransform), typeof(LayoutElement));
        arrowBox.transform.SetParent(chip.transform, false);
        arrowBox.GetComponent<LayoutElement>().preferredWidth = 28f;
        UiGlyphs.AddShape(arrowBox.transform, "Triangle", UiGlyphs.TriangleSprite(), 0f, 0f, 24f, 20f, 90f, UiTheme.TextPrimary);

        chip.GetComponent<Button>().transition = Selectable.Transition.None;
        chip.GetComponent<Button>().onClick.AddListener(() => ShowLevelSheet(false));
        TapFeedback.AddPressFeedback(chip);
    }

    private void BuildLevelLine(Transform parent)
    {
        int i = Mathf.Max(0, LevelIndex);
        var line = DialogChrome.MakeText(parent, "LevelLine",
            "Level " + QuestionLevel.Letters[i].ToUpperInvariant() + " · " + QuestionLevel.TextAbout[i],
            30, TextAlignmentOptions.Left, UiTheme.TextSecondary);
        line.gameObject.AddComponent<LayoutElement>().preferredHeight = 40f;
    }

    private void BuildBookGrid(Transform parent)
    {
        var books = new List<PRBook>();
        if (Globals.g_listPRBooks != null)
            foreach (var b in Globals.g_listPRBooks)
                if (b != null && string.IsNullOrEmpty(b.action)) books.Add(b);

        if (books.Count == 0)
        {
            var none = DialogChrome.MakeText(parent, "NoBooks", "No books for this level yet.", 36, TextAlignmentOptions.Center, UiTheme.TextSecondary);
            none.gameObject.AddComponent<LayoutElement>().preferredHeight = 120f;
            return;
        }

        // Two columns that fit the screen: the canvas is 1080 units wide only on a 16:9 screen,
        // narrower on today's taller phones. Worked out from the screen size (the content rect is
        // not laid out yet when the scene has just loaded), the way CanvasScaler does with match 0.5.
        float scale = Mathf.Pow(2f, 0.5f * (Mathf.Log(Screen.width / 1080f, 2f) + Mathf.Log(Screen.height / 1920f, 2f)));
        float width = Screen.width / scale - 96f;
        float cellW = Mathf.Floor((width - 24f) / 2f);
        float cellH = cellW + 120f;
        var grid = new GameObject("Grid", typeof(RectTransform), typeof(GridLayoutGroup), typeof(LayoutElement));
        grid.transform.SetParent(parent, false);
        var g = grid.GetComponent<GridLayoutGroup>();
        g.cellSize = new Vector2(cellW, cellH);
        g.spacing = new Vector2(24f, 24f);
        g.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        g.constraintCount = 2;
        g.childAlignment = TextAnchor.UpperCenter;
        int rows = (books.Count + 1) / 2;
        grid.GetComponent<LayoutElement>().preferredHeight = rows * cellH + (rows - 1) * 24f;

        foreach (var book in books) BuildBookTile(grid.transform, book);
    }

    private void BuildBookTile(Transform parent, PRBook book)
    {
        var tile = new GameObject("Tile_" + book.id, typeof(RectTransform), typeof(Image), typeof(Button), typeof(VerticalLayoutGroup));
        tile.transform.SetParent(parent, false);
        var timg = tile.GetComponent<Image>(); timg.sprite = DialogChrome.RoundedSprite(); timg.type = Image.Type.Sliced; timg.color = UiTheme.Surface;
        var vl = tile.GetComponent<VerticalLayoutGroup>();
        vl.padding = new RectOffset(14, 14, 14, 14); vl.spacing = 10;
        vl.childControlWidth = true; vl.childControlHeight = true;
        vl.childForceExpandWidth = true; vl.childForceExpandHeight = false;
        vl.childAlignment = TextAnchor.UpperCenter;

        var coverGO = new GameObject("Cover", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        coverGO.transform.SetParent(tile.transform, false);
        coverGO.GetComponent<LayoutElement>().flexibleHeight = 1f;
        var cover = coverGO.GetComponent<Image>();
        cover.color = Color.white; cover.preserveAspect = true; cover.raycastTarget = false;
        string url = Globals.WithContentRev(Globals.baseURL + book.bookImageUrl, book.contentRev);
        StartCoroutine(PRUtils.DownloadImage(url, cover, true, true));

        string name = book.bookName.Replace(" (with questions)", "");
        var label = DialogChrome.MakeText(tile.transform, "Name", name, 30, TextAlignmentOptions.Center, UiTheme.TextPrimary);
        label.fontStyle = FontStyles.Bold;
        label.enableWordWrapping = true;
        label.gameObject.AddComponent<LayoutElement>().preferredHeight = 84f;

        // What the child has done with this book on this device (read from the prefs, not from
        // the catalog copy, which is not updated while the app runs).
        int answered = DialogueResults.AnsweredCount(book.id);
        int page = Globals.Prefs_Get_Book_Page(book.bookUrl);
        bool done = Globals.Prefs_Get_Book_Done(book.bookUrl) != 0;
        string progress = "";
        if (answered > 0) progress = "★ " + answered + (answered == 1 ? " answer" : " answers");
        if (page > 0 && !done)
            progress += (progress.Length > 0 ? " · " : "") + "page " + (page + 1);
        var prog = DialogChrome.MakeText(tile.transform, "Progress", progress, 26, TextAlignmentOptions.Center, UiTheme.TextSecondary);
        prog.gameObject.AddComponent<LayoutElement>().preferredHeight = 36f;

        var captured = book;
        tile.GetComponent<Button>().transition = Selectable.Transition.None;
        TapFeedback.AddPressFeedback(tile);
        tile.GetComponent<Button>().onClick.AddListener(
            () => TapFeedback.TapThenGo(tile.transform, () => Nav.GoToBook(captured)));
    }

    // ---------------------------------------------------------------- level sheet

    private void ShowLevelSheet(bool firstTime)
    {
        if (_sheet != null) Destroy(_sheet);
        _sheet = new GameObject("LevelSheet", typeof(RectTransform), typeof(Image), typeof(Button));
        _sheet.transform.SetParent(_canvasRoot.transform, false);
        DialogChrome.Stretch(_sheet.GetComponent<RectTransform>());
        _sheet.GetComponent<Image>().color = new Color(0.29f, 0.27f, 0.24f, 0.6f);
        var sheet = _sheet;
        _sheet.GetComponent<Button>().transition = Selectable.Transition.None;
        if (!firstTime) _sheet.GetComponent<Button>().onClick.AddListener(() => Destroy(sheet)); // tap outside closes

        // The card takes its own taps (a Button with nothing to do), so a tap on its title or
        // between the options does not reach the backdrop and close the sheet.
        var card = new GameObject("Card", typeof(RectTransform), typeof(Image), typeof(Button), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        card.transform.SetParent(_sheet.transform, false);
        card.GetComponent<Button>().transition = Selectable.Transition.None;
        var crt = card.GetComponent<RectTransform>();
        crt.anchorMin = new Vector2(0f, 0f); crt.anchorMax = new Vector2(1f, 0f); crt.pivot = new Vector2(0.5f, 0f);
        crt.offsetMin = Vector2.zero; crt.offsetMax = Vector2.zero;
        DialogChrome.StyleCard(card.GetComponent<Image>());
        var cvl = card.GetComponent<VerticalLayoutGroup>();
        cvl.padding = new RectOffset(44, 44, 44, 60);
        cvl.spacing = 18; cvl.childAlignment = TextAnchor.UpperCenter;
        cvl.childControlWidth = true; cvl.childControlHeight = true; cvl.childForceExpandWidth = true; cvl.childForceExpandHeight = false;
        card.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        SafeAreaInsets.ApplyBottom(crt);

        var title = DialogChrome.MakeText(card.transform, "Title", "How does your child talk now?", 48, TextAlignmentOptions.Left, UiTheme.TextPrimary);
        title.fontStyle = FontStyles.Bold; title.enableWordWrapping = true;
        title.gameObject.AddComponent<LayoutElement>().preferredHeight = 64f;
        var sub = DialogChrome.MakeText(card.transform, "Sub",
            firstTime ? "Pick the closest one. You can change it later from the Home screen." : "Pick the closest one.",
            28, TextAlignmentOptions.Left, UiTheme.TextSecondary);
        sub.enableWordWrapping = true;
        sub.gameObject.AddComponent<LayoutElement>().preferredHeight = 40f;

        string chosen = QuestionLevel.Current;
        var fills = new Image[QuestionLevel.Letters.Length];
        var discs = new Image[QuestionLevel.Letters.Length];
        TMP_Text go = null;
        void Paint()
        {
            int ci = System.Array.IndexOf(QuestionLevel.Letters, chosen);
            for (int i = 0; i < fills.Length; i++)
            {
                fills[i].color = i == ci ? new Color(0.94f, 0.96f, 0.91f) : UiTheme.Surface;
                discs[i].color = i == ci ? UiTheme.Primary : UiTheme.Track;
            }
            if (go != null) go.text = (firstTime ? "Start with level " : "Use level ") + chosen.ToUpperInvariant();
        }

        for (int i = 0; i < QuestionLevel.Letters.Length; i++)
        {
            string letter = QuestionLevel.Letters[i];
            var opt = new GameObject("Level_" + letter, typeof(RectTransform), typeof(Image), typeof(Button), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            opt.transform.SetParent(card.transform, false);
            opt.GetComponent<LayoutElement>().preferredHeight = 150f;
            fills[i] = opt.GetComponent<Image>();
            fills[i].sprite = DialogChrome.RoundedSprite(); fills[i].type = Image.Type.Sliced;
            var hl = opt.GetComponent<HorizontalLayoutGroup>();
            hl.padding = new RectOffset(22, 22, 18, 18); hl.spacing = 20;
            hl.childControlWidth = true; hl.childControlHeight = true;
            hl.childForceExpandWidth = false; hl.childForceExpandHeight = true;
            hl.childAlignment = TextAnchor.MiddleLeft;

            var disc = new GameObject("Disc", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            disc.transform.SetParent(opt.transform, false);
            disc.GetComponent<LayoutElement>().preferredWidth = 96f;
            discs[i] = disc.GetComponent<Image>(); discs[i].sprite = UiGlyphs.CircleSprite(); discs[i].raycastTarget = false; discs[i].preserveAspect = true;
            var l = DialogChrome.MakeText(disc.transform, "Letter", letter.ToUpperInvariant(), 48, TextAlignmentOptions.Center, UiTheme.TextPrimary);
            l.fontStyle = FontStyles.Bold; DialogChrome.Stretch(l.rectTransform);

            var col = new GameObject("Col", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
            col.transform.SetParent(opt.transform, false);
            col.GetComponent<LayoutElement>().flexibleWidth = 1f;
            var cv = col.GetComponent<VerticalLayoutGroup>();
            cv.spacing = 4; cv.childAlignment = TextAnchor.MiddleLeft;
            cv.childControlWidth = true; cv.childControlHeight = true; cv.childForceExpandWidth = true; cv.childForceExpandHeight = false;
            var t = DialogChrome.MakeText(col.transform, "Title", QuestionLevel.TextChild[i], 34, TextAlignmentOptions.Left, UiTheme.TextPrimary);
            t.fontStyle = FontStyles.Bold;
            t.gameObject.AddComponent<LayoutElement>().preferredHeight = 44f;
            var m = DialogChrome.MakeText(col.transform, "More", QuestionLevel.TextChildMore[i], 26, TextAlignmentOptions.Left, UiTheme.TextSecondary);
            m.enableWordWrapping = true;
            m.gameObject.AddComponent<LayoutElement>().preferredHeight = 64f;

            opt.GetComponent<Button>().transition = Selectable.Transition.None;
            opt.GetComponent<Button>().onClick.AddListener(() => { chosen = letter; Paint(); });
            TapFeedback.AddPressFeedback(opt);
        }

        var ok = new GameObject("LevelOk", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        ok.transform.SetParent(card.transform, false);
        ok.GetComponent<LayoutElement>().preferredHeight = 112f;
        var oimg = ok.GetComponent<Image>(); oimg.sprite = DialogChrome.RoundedSprite(); oimg.type = Image.Type.Sliced; oimg.color = UiTheme.Primary;
        go = DialogChrome.MakeText(ok.transform, "Text", "", 38, TextAlignmentOptions.Center, UiTheme.OnPrimary);
        go.fontStyle = FontStyles.Bold; DialogChrome.Stretch(go.rectTransform);
        ok.GetComponent<Button>().transition = Selectable.Transition.None;
        ok.GetComponent<Button>().onClick.AddListener(() =>
        {
            QuestionLevel.Current = chosen; // also re-filters the book list
            Destroy(sheet);
            BuildContent();
        });
        TapFeedback.AddPressFeedback(ok);
        if (firstTime)
        {
            var hint = DialogChrome.MakeText(card.transform, "Hint", "Not sure? Start low. Moving up is one tap.", 26, TextAlignmentOptions.Center, UiTheme.TextSecondary);
            hint.gameObject.AddComponent<LayoutElement>().preferredHeight = 36f;
        }
        Paint();
        _sheet.transform.SetAsLastSibling();
    }

    // ---------------------------------------------------------------- grown-ups

    private void BuildGrownupsFooter(Transform parent)
    {
        var go = new GameObject("GrownupsFooter", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        go.GetComponent<LayoutElement>().preferredHeight = 96f;
        var img = go.GetComponent<Image>(); img.sprite = DialogChrome.RoundedSprite(); img.type = Image.Type.Sliced; img.color = UiTheme.Track;
        var label = DialogChrome.MakeText(go.transform, "Label", "For grown-ups", 34, TextAlignmentOptions.Center, UiTheme.TextPrimary);
        DialogChrome.Stretch(label.rectTransform);
        go.GetComponent<Button>().transition = Selectable.Transition.None;
        go.GetComponent<Button>().onClick.AddListener(() => ShowGate(Navigation.GoToSettings));
        TapFeedback.AddPressFeedback(go);
    }

    private static readonly (int a, int b)[] GateQuestions = { (3, 4), (4, 5), (3, 5), (4, 4), (5, 5) };

    // The same small sum as ReadingBuddy's grown-ups door: far out of reach of the children, one
    // glance for an adult.
    private void ShowGate(System.Action onPass)
    {
        if (_gate != null) Destroy(_gate);
        var question = GateQuestions[Random.Range(0, GateQuestions.Length)];
        int correct = question.a * question.b;
        var opts = new List<int> { correct };
        while (opts.Count < 3) { int w = correct + Random.Range(-9, 10); if (w > 0 && !opts.Contains(w)) opts.Add(w); }
        for (int i = opts.Count - 1; i > 0; i--) { int j = Random.Range(0, i + 1); (opts[i], opts[j]) = (opts[j], opts[i]); }

        _gate = new GameObject("Gate", typeof(RectTransform), typeof(Image), typeof(Button));
        _gate.transform.SetParent(_canvasRoot.transform, false);
        DialogChrome.Stretch(_gate.GetComponent<RectTransform>());
        _gate.GetComponent<Image>().color = new Color(0.29f, 0.27f, 0.24f, 0.6f);
        var gate = _gate;
        _gate.GetComponent<Button>().transition = Selectable.Transition.None;
        _gate.GetComponent<Button>().onClick.AddListener(() => Destroy(gate));

        var card = new GameObject("Card", typeof(RectTransform), typeof(Image), typeof(Button), typeof(VerticalLayoutGroup));
        card.transform.SetParent(_gate.transform, false);
        card.GetComponent<Button>().transition = Selectable.Transition.None; // taps on the card stay on it
        var crt = card.GetComponent<RectTransform>();
        crt.anchorMin = crt.anchorMax = crt.pivot = new Vector2(0.5f, 0.5f);
        crt.sizeDelta = new Vector2(840f, 720f);
        DialogChrome.StyleCard(card.GetComponent<Image>());
        var cvl = card.GetComponent<VerticalLayoutGroup>();
        cvl.padding = new RectOffset(48, 48, 44, 44); cvl.spacing = 22; cvl.childAlignment = TextAnchor.UpperCenter;
        cvl.childControlWidth = true; cvl.childControlHeight = true; cvl.childForceExpandWidth = true; cvl.childForceExpandHeight = false;

        var title = DialogChrome.MakeText(card.transform, "Title", "Grown-ups only", 46, TextAlignmentOptions.Center, UiTheme.TextPrimary);
        title.fontStyle = FontStyles.Bold;
        title.gameObject.AddComponent<LayoutElement>().preferredHeight = 64f;
        var q = DialogChrome.MakeText(card.transform, "Q", "What is " + question.a + " x " + question.b + " ?", 40, TextAlignmentOptions.Center, UiTheme.TextSecondary);
        q.gameObject.AddComponent<LayoutElement>().preferredHeight = 58f;

        for (int i = 0; i < opts.Count; i++)
        {
            int val = opts[i];
            GateButton(card.transform, val.ToString(), UiTheme.Card(i).fill, UiTheme.Card(i).accent,
                () => { if (val == correct) { Destroy(gate); onPass(); } else { ShowGate(onPass); } });
        }
        GateButton(card.transform, "Cancel", UiTheme.Track, UiTheme.TextPrimary, () => Destroy(gate));
        _gate.transform.SetAsLastSibling();
    }

    private static void GateButton(Transform parent, string text, Color fill, Color textColor, UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject("GBtn_" + text, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        go.GetComponent<LayoutElement>().preferredHeight = 108f;
        var img = go.GetComponent<Image>(); img.sprite = DialogChrome.RoundedSprite(); img.type = Image.Type.Sliced; img.color = fill;
        var label = DialogChrome.MakeText(go.transform, "Label", text, 40, TextAlignmentOptions.Center, textColor);
        label.fontStyle = FontStyles.Bold;
        DialogChrome.Stretch(label.rectTransform);
        go.GetComponent<Button>().transition = Selectable.Transition.None;
        go.GetComponent<Button>().onClick.AddListener(onClick);
        TapFeedback.AddPressFeedback(go);
    }
}
