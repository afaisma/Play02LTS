using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// ============================================================================================
// Home hub (staged plan — Stage 1). A PLAIN scene controller: it lives only in the _Home scene,
// is NOT DontDestroyOnLoad, does no FindObjectOfType scanning, and builds its ENTIRE UI in code
// in Start() (mirroring UnifiedReadingModePicker's code-built ScreenSpaceOverlay canvas).
//
// Layout, top to bottom:
//   a) Title / logo row.
//   b) "Continue reading" horizontal rail — books in progress (page > 0 AND not done). Hidden
//      when there are none. Each card = cover + name, tap -> Nav.GoToBook(book).
//   c) Grid of big section tiles (label -> filter), tap -> Nav.GoToLibrary(filter). Tiles whose
//      filter yields no books in the current catalog are dropped (verified via Filter.Conforms,
//      the SAME predicate the Library uses).
//
// Covers load EXACTLY as the Library does (BooksScrollView.AddBook): baseURL + bookImageUrl,
// cache-busted by the book's own contentRev via Globals.WithContentRev, then PRUtils.DownloadImage
// with suppressAlert:true. If the catalog isn't loaded yet, a "Loading…" label shows and a short-
// interval coroutine retries until g_listPRBooks is ready.
// ============================================================================================
public class HomeController : MonoBehaviour
{
    // A section tile maps a button label to a Library filter address. Serialized so the set/order
    // can be tuned in the Inspector; sensible defaults below keep the scene working with no setup.
    [System.Serializable]
    public struct SectionTile
    {
        public string label;
        public string filter;
        public SectionTile(string label, string filter) { this.label = label; this.filter = filter; }
    }

    [SerializeField]
    private SectionTile[] sectionTiles =
    {
        new SectionTile("Learn to Read", "learn to read"),
        new SectionTile("Stories",       "fairytales"),
        new SectionTile("First Words",   "rhymebooks"),
        new SectionTile("Discover",      "science"),
        new SectionTile("All Books",     "everything"),
    };

    [SerializeField] private TMP_FontAsset uiFont; // rounded kid font (Fredoka); falls back to default

    // How often the catalog-not-ready guard re-checks g_listPRBooks.
    private const float RetryInterval = 0.5f;

    // ---- built UI ----
    private GameObject _canvasRoot;
    private RectTransform _contentRoot; // vertical stack: title, rail, grid
    private GameObject _loadingLabel;

    private void Start()
    {
        BuildCanvas();
        StartCoroutine(BuildWhenCatalogReady());
    }

    // Guard: the catalog is normally ready before this scene, but if not, show "Loading…" and
    // retry on a short interval rather than building an empty hub.
    private IEnumerator BuildWhenCatalogReady()
    {
        while (Globals.g_listPRBooks == null || Globals.g_listPRBooks.Count == 0)
        {
            ShowLoading(true);
            yield return new WaitForSeconds(RetryInterval);
        }
        ShowLoading(false);
        BuildContent();
    }

    // ---------------------------------------------------------------- canvas / chrome

    private void BuildCanvas()
    {
        if (_canvasRoot != null) return;

        if (EventSystem.current == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        _canvasRoot = new GameObject("HomeCanvas",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = _canvasRoot.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var scaler = _canvasRoot.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 0.5f;

        // Full-screen background fill.
        var bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
        bg.transform.SetParent(_canvasRoot.transform, false);
        Stretch(bg.GetComponent<RectTransform>());
        bg.GetComponent<Image>().color = UiTheme.Bg;

        // Vertical content stack with screen padding.
        var content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup));
        content.transform.SetParent(_canvasRoot.transform, false);
        _contentRoot = content.GetComponent<RectTransform>();
        Stretch(_contentRoot);
        var vlg = content.GetComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(48, 48, 90, 48);
        vlg.spacing = 40;
        vlg.childControlWidth = true; vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
        vlg.childAlignment = TextAnchor.UpperCenter;
    }

    private void ShowLoading(bool show)
    {
        if (_loadingLabel == null)
        {
            var t = MakeText(_canvasRoot.transform, "Loading", "Loading…", 48, TextAlignmentOptions.Center);
            Stretch(t.rectTransform);
            _loadingLabel = t.gameObject;
        }
        _loadingLabel.SetActive(show);
    }

    // ---------------------------------------------------------------- content

    private void BuildContent()
    {
        // Idempotent: clear any prior children (defensive; the coroutine builds once).
        for (int i = _contentRoot.childCount - 1; i >= 0; i--)
            Destroy(_contentRoot.GetChild(i).gameObject);

        BuildTitleRow(_contentRoot);
        BuildAgeRow(_contentRoot);
        BuildContinueRail(_contentRoot);
        BuildSectionGrid(_contentRoot);
    }

    // Age filter chips: All 2 3 4 5 6 7 8+. Two-tap RANGE selection:
    //   1st tap on an age  -> single age (range of one),
    //   2nd tap on another -> fills the range between the two ends,
    //   tap the same single -> clears back to All,
    //   tap when a range is already set -> starts a new single from that age,
    //   "All" -> clears the filter.
    // Selection is stored as an inclusive [lo, hi] in Globals (shared with the Library).
    // The Continue rail is intentionally NOT age-filtered — a book already in progress
    // should never disappear because of the age band.
    private const int MaxAgeChip = 8; // "8+"

    private void BuildAgeRow(Transform parent)
    {
        int lo = Globals.GetAgeLo();
        int hi = Globals.GetAgeHi();

        var rowGO = new GameObject("AgeRow", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        rowGO.transform.SetParent(parent, false);
        var hlg = rowGO.GetComponent<HorizontalLayoutGroup>();
        hlg.spacing = 12;
        hlg.childControlWidth = true; hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;
        hlg.childAlignment = TextAnchor.MiddleLeft;
        rowGO.GetComponent<LayoutElement>().preferredHeight = 76f;

        AddAgeChip(rowGO.transform, "All", 0, lo, hi);
        for (int a = 2; a <= 7; a++)
            AddAgeChip(rowGO.transform, a.ToString(), a, lo, hi);
        AddAgeChip(rowGO.transform, "8+", MaxAgeChip, lo, hi);
    }

    private void AddAgeChip(Transform parent, string label, int value, int lo, int hi)
    {
        // "All" highlights only when nothing is selected; an age highlights when it
        // falls inside the current [lo, hi] selection.
        bool selected = value == 0 ? (lo == 0 && hi == 0) : (lo > 0 && lo <= value && value <= hi);

        var chip = new GameObject("Age_" + label,
            typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        chip.transform.SetParent(parent, false);
        var le = chip.GetComponent<LayoutElement>();
        le.preferredWidth = value == 0 ? 96f : 64f;
        le.preferredHeight = 64f;
        var img = chip.GetComponent<Image>();
        img.sprite = RoundedSprite();
        img.type = Image.Type.Sliced;
        img.color = selected ? UiTheme.Primary : UiTheme.Surface;

        var t = MakeText(chip.transform, "Label", label, 32, TextAlignmentOptions.Center);
        t.color = selected ? UiTheme.OnPrimary : UiTheme.TextSecondary;
        Stretch(t.rectTransform);

        int captured = value;
        chip.GetComponent<Button>().onClick.AddListener(() => OnAgeChipTapped(captured));
    }

    // Two-tap range state machine. See BuildAgeRow for the rules.
    private void OnAgeChipTapped(int value)
    {
        if (value == 0) { Globals.ClearAgeRange(); BuildContent(); return; }

        int lo = Globals.GetAgeLo();
        int hi = Globals.GetAgeHi();

        if (lo == 0)                       // nothing selected -> single end
            Globals.SetAgeRange(value, value);
        else if (lo == hi)                 // a single age is selected -> second tap
        {
            if (value == lo) Globals.ClearAgeRange();        // same chip toggles off
            else Globals.SetAgeRange(lo, value);             // fill the range
        }
        else                               // a range exists -> restart from this age
            Globals.SetAgeRange(value, value);

        BuildContent();
    }

    // (a) Title / logo row.
    private void BuildTitleRow(Transform parent)
    {
        var rowGO = new GameObject("TitleRow", typeof(RectTransform), typeof(LayoutElement));
        rowGO.transform.SetParent(parent, false);
        rowGO.GetComponent<LayoutElement>().preferredHeight = 110f;

        var title = MakeText(rowGO.transform, "Title", "ReadingBuddy", 64, TextAlignmentOptions.Left);
        title.fontStyle = FontStyles.Bold;
        title.color = UiTheme.Primary;
        Stretch(title.rectTransform);
    }

    // (b) "Continue reading" horizontal rail. In progress = page > 0 (past the first 0-based step)
    // AND not done. Hidden when empty. Nav tiles (books with an action) are never "in progress".
    private void BuildContinueRail(Transform parent)
    {
        var inProgress = new List<PRBook>();
        foreach (var b in Globals.g_listPRBooks)
        {
            if (b == null || string.IsNullOrEmpty(b.bookUrl)) continue;
            if (!string.IsNullOrEmpty(b.action)) continue; // navigation tile, not a real book
            if (Globals.Prefs_Get_Book_Page(b.bookUrl) > 0 && Globals.Prefs_Get_Book_Done(b.bookUrl) == 0)
                inProgress.Add(b);
        }
        if (inProgress.Count == 0) return; // hide the rail entirely

        var section = new GameObject("ContinueSection", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
        section.transform.SetParent(parent, false);
        var svlg = section.GetComponent<VerticalLayoutGroup>();
        svlg.spacing = 14;
        svlg.childControlWidth = true; svlg.childControlHeight = true;
        svlg.childForceExpandWidth = true; svlg.childForceExpandHeight = false;
        section.GetComponent<LayoutElement>().preferredHeight = 430f;

        var heading = MakeText(section.transform, "Heading", "Continue reading", 38, TextAlignmentOptions.Left);
        heading.fontStyle = FontStyles.Bold;
        heading.gameObject.AddComponent<LayoutElement>().preferredHeight = 60f;

        // Horizontal scroll rail (viewport + horizontally-fitted content).
        var scrollGO = new GameObject("Rail",
            typeof(RectTransform), typeof(ScrollRect), typeof(Image), typeof(RectMask2D), typeof(LayoutElement));
        scrollGO.transform.SetParent(section.transform, false);
        scrollGO.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.04f);
        scrollGO.GetComponent<LayoutElement>().preferredHeight = 360f;

        var viewport = new GameObject("Viewport", typeof(RectTransform));
        viewport.transform.SetParent(scrollGO.transform, false);
        Stretch(viewport.GetComponent<RectTransform>());

        var railContent = new GameObject("RailContent",
            typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
        railContent.transform.SetParent(viewport.transform, false);
        var rcrt = railContent.GetComponent<RectTransform>();
        rcrt.anchorMin = new Vector2(0f, 0f);
        rcrt.anchorMax = new Vector2(0f, 1f);
        rcrt.pivot = new Vector2(0f, 0.5f);
        var hlg = railContent.GetComponent<HorizontalLayoutGroup>();
        hlg.spacing = 24;
        hlg.padding = new RectOffset(4, 4, 4, 4);
        hlg.childControlWidth = true; hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;
        hlg.childAlignment = TextAnchor.MiddleLeft;
        railContent.GetComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

        var scroll = scrollGO.GetComponent<ScrollRect>();
        scroll.content = rcrt;
        scroll.viewport = viewport.GetComponent<RectTransform>();
        scroll.horizontal = true;
        scroll.vertical = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 20f;

        foreach (var book in inProgress)
            BuildBookCard(railContent.transform, book);
    }

    private void BuildBookCard(Transform parent, PRBook book)
    {
        var cardGO = new GameObject("Card_" + book.bookName,
            typeof(RectTransform), typeof(Image), typeof(Button), typeof(VerticalLayoutGroup), typeof(LayoutElement));
        cardGO.transform.SetParent(parent, false);
        cardGO.GetComponent<Image>().color = UiTheme.Surface;
        var cle = cardGO.GetComponent<LayoutElement>();
        cle.preferredWidth = 240f; cle.preferredHeight = 340f;
        var cvlg = cardGO.GetComponent<VerticalLayoutGroup>();
        cvlg.padding = new RectOffset(12, 12, 12, 12);
        cvlg.spacing = 10;
        cvlg.childControlWidth = true; cvlg.childControlHeight = true;
        cvlg.childForceExpandWidth = true; cvlg.childForceExpandHeight = false;
        cvlg.childAlignment = TextAnchor.UpperCenter;

        // Cover.
        var coverGO = new GameObject("Cover", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        coverGO.transform.SetParent(cardGO.transform, false);
        var coverLe = coverGO.GetComponent<LayoutElement>();
        coverLe.preferredHeight = 240f;
        var coverImg = coverGO.GetComponent<Image>();
        coverImg.color = Color.white;
        coverImg.preserveAspect = true;
        coverImg.raycastTarget = false;
        LoadCover(book, coverImg);

        // Name.
        var name = MakeText(cardGO.transform, "Name", book.bookName, 26, TextAlignmentOptions.Center);
        name.gameObject.AddComponent<LayoutElement>().preferredHeight = 56f;
        name.enableWordWrapping = true;

        var captured = book;
        cardGO.GetComponent<Button>().onClick.AddListener(() => Nav.GoToBook(captured));
    }

    // (c) Grid of big section tiles. A tile whose filter yields no books in the current catalog is
    // dropped (verified via the SAME Filter.Conforms predicate the Library uses).
    private void BuildSectionGrid(Transform parent)
    {
        var live = new List<SectionTile>();
        foreach (var tile in sectionTiles)
            if (!string.IsNullOrEmpty(tile.filter) && FilterHasBooks(tile.filter))
                live.Add(tile);
        if (live.Count == 0) return;

        var gridGO = new GameObject("Sections", typeof(RectTransform), typeof(GridLayoutGroup), typeof(LayoutElement));
        gridGO.transform.SetParent(parent, false);
        var grid = gridGO.GetComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(472f, 220f);
        grid.spacing = new Vector2(40f, 40f);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 2;
        grid.childAlignment = TextAnchor.UpperCenter;
        // Two columns: ceil(n/2) rows of 220 + spacing.
        int rows = (live.Count + 1) / 2;
        gridGO.GetComponent<LayoutElement>().preferredHeight = rows * 220f + (rows - 1) * 40f;

        for (int i = 0; i < live.Count; i++)
            BuildSectionTile(gridGO.transform, live[i], i);
    }

    private void BuildSectionTile(Transform parent, SectionTile tile, int idx)
    {
        var palette = UiTheme.Card(idx);
        var tileGO = new GameObject("Tile_" + tile.filter,
            typeof(RectTransform), typeof(Image), typeof(Button), typeof(VerticalLayoutGroup));
        tileGO.transform.SetParent(parent, false);
        var tImg = tileGO.GetComponent<Image>();
        tImg.sprite = RoundedSprite(); tImg.type = Image.Type.Sliced;
        tImg.color = palette.fill;
        var vlg = tileGO.GetComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(24, 24, 24, 24);
        vlg.childAlignment = TextAnchor.MiddleCenter;
        vlg.childControlWidth = true; vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = true;

        var label = MakeText(tileGO.transform, "Label", tile.label, 44, TextAlignmentOptions.Center);
        label.fontStyle = FontStyles.Bold;
        label.color = palette.accent;

        var captured = tile.filter;
        // The "learn to read" tile opens the dedicated ladder (Stage 3) rather than a flat
        // filtered Library list; every other tile goes to the Library as before.
        bool isLearnToRead = !string.IsNullOrEmpty(captured) &&
                             captured.Trim().ToLowerInvariant() == "learn to read";
        tileGO.GetComponent<Button>().onClick.AddListener(() =>
        {
            if (isLearnToRead) Navigation.GoToLearnToRead();
            else Nav.GoToLibrary(captured);
        });
    }

    // ---------------------------------------------------------------- helpers

    // Does any catalog book conform to this filter? Uses the Library's own Filter.Conforms so the
    // home tiles match exactly what the Library would show (genre substring, "everything", "levelN").
    private bool FilterHasBooks(string filter)
    {
        var f = new Filter();
        f.SetFilter(0, 0, filter);
        f.ageLoSel = Globals.GetAgeLo();
        f.ageHiSel = Globals.GetAgeHi();
        foreach (var b in Globals.g_listPRBooks)
            if (b != null && f.Conforms(b)) return true;
        return false;
    }

    // Load a cover the SAME way BooksScrollView.AddBook does: resolve against baseURL, cache-bust by
    // the book's own contentRev, suppress the per-thumbnail failure alert.
    private void LoadCover(PRBook book, Image image)
    {
        string url = Globals.baseURL + book.bookImageUrl;
        url = Globals.WithContentRev(url, book.contentRev);
        StartCoroutine(PRUtils.DownloadImage(url, image, true, true));
    }

    // Procedural rounded-rect (9-sliced) sprite so the age chips read as pills without
    // shipping an art asset. Built once and reused. The 9-slice border keeps the corner
    // radius crisp at any chip width.
    private static Sprite _chipSprite;
    private static Sprite RoundedSprite()
    {
        if (_chipSprite != null) return _chipSprite;
        const int r = 30;
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
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                px[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(r - d + 0.5f));
            }
        tex.SetPixels(px);
        tex.Apply();
        _chipSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f),
            100f, 0, SpriteMeshType.FullRect, new Vector4(r, r, r, r));
        return _chipSprite;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    // Mirrors UnifiedReadingModePicker.MakeText: TMP label using the project default font, auto-size.
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
