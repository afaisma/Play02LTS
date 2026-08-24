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
//   b) "Recent reads" horizontal rail — books the child has opened, newest first, finished ones
//      included (with a check chip) because re-reading is the point. Hidden when there are none.
//      Each card = cover + name, tap -> Nav.GoToBook(book).
//   c) Grid of illustrated DOOR cards (art + label + accent bar -> filter), tap -> the same
//      navigation the old label+glyph tiles performed. The door set is content: it comes from
//      home_doors.json (see HomeDoors.cs) with the compiled-in SectionTile list as the floor.
//      Doors whose filter yields no books in the current catalog are dropped (verified via
//      Filter.Conforms, the SAME predicate the Library uses), as are doors outside the age chips.
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
        public string iconKey; // optional Resources/Icons/Rooms key; empty -> derived from the filter
        public SectionTile(string label, string filter) { this.label = label; this.filter = filter; this.iconKey = null; }
    }

    [SerializeField]
    private SectionTile[] sectionTiles =
    {
        new SectionTile("Learn to Read", "learn to read"),
        new SectionTile("Rhymebooks",    "rhymebooks"),
        new SectionTile("Fairytales",    "fairytales"),
        new SectionTile("Adventure",     "adventure"),
        new SectionTile("Nature",        "nature"),
        new SectionTile("Manners",       "manners"),
        new SectionTile("Science",       "science"),
        new SectionTile("Art",           "art"),
        new SectionTile("All Books",     "everything"),
    };

    [SerializeField] private TMP_FontAsset uiFont; // rounded kid font (Fredoka); falls back to default

    // How often the catalog-not-ready guard re-checks g_listPRBooks.
    private const float RetryInterval = 0.5f;

    // ---- built UI ----
    private GameObject _canvasRoot;
    private RectTransform _contentRoot; // vertical stack: title, rail, grid
    private GameObject _loadingLabel;

    // The active door set for the rooms area: home_doors.json (network) → DiskCache copy →
    // the compiled-in SectionTile list. See HomeDoors.cs.
    private List<HomeDoor> _doors;
    private bool _builtOnce; // first paint done → a later door delivery re-paints in place

    private void Start()
    {
        BuildInfo.LogOnce(); // one greppable [BUILD] line per session
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

        // Door set. Load() hands back a DiskCache copy synchronously (before its first yield), so a
        // returning child paints the real doors on frame one; the network refresh re-paints only if
        // the published set actually changed. Nothing cached yet → the compiled-in room set stands in.
        var fallbackDoors = HomeDoorsConfig.FromSectionTiles(sectionTiles);
        StartCoroutine(HomeDoorsConfig.Load(fallbackDoors, OnDoorsLoaded));
        if (_doors == null) _doors = fallbackDoors;

        _builtOnce = true;
        BuildContent();
    }

    // A door set arrived (cache first, then possibly a fresher download). Re-paint once the screen
    // is up; before that the pending set is simply what the first BuildContent will use.
    private void OnDoorsLoaded(List<HomeDoor> doors)
    {
        if (doors == null || doors.Count == 0) return;
        _doors = doors;
        if (_builtOnce) BuildContent();
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

        // Scrollable vertical content (the reading-rooms list can exceed one screen).
        var scrollGO = new GameObject("Scroll", typeof(RectTransform), typeof(ScrollRect), typeof(RectMask2D));
        scrollGO.transform.SetParent(_canvasRoot.transform, false);
        Stretch(scrollGO.GetComponent<RectTransform>());
        var scroll = scrollGO.GetComponent<ScrollRect>();
        scroll.horizontal = false; scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 28f;

        var content = new GameObject("Content",
            typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        content.transform.SetParent(scrollGO.transform, false);
        _contentRoot = content.GetComponent<RectTransform>();
        _contentRoot.anchorMin = new Vector2(0f, 1f); _contentRoot.anchorMax = new Vector2(1f, 1f);
        _contentRoot.pivot = new Vector2(0.5f, 1f);
        _contentRoot.offsetMin = Vector2.zero; _contentRoot.offsetMax = Vector2.zero;
        var vlg = content.GetComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(48, 48, 90, 60);
        vlg.spacing = 40;
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
        BuildDoorRooms(_contentRoot);
        BuildGrownupsFooter(_contentRoot);
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

    // (a) Title / logo row: just the title. No home button — this IS home; the control only ever
    // navigated to the screen the child was already looking at. The Library / ladder / reader home
    // buttons stay.
    private void BuildTitleRow(Transform parent)
    {
        var rowGO = new GameObject("TitleRow", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        rowGO.transform.SetParent(parent, false);
        rowGO.GetComponent<LayoutElement>().preferredHeight = 110f;
        var hlg = rowGO.GetComponent<HorizontalLayoutGroup>();
        hlg.spacing = 16;
        hlg.childControlWidth = true; hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;
        hlg.childAlignment = TextAnchor.MiddleLeft;

        var title = MakeText(rowGO.transform, "Title", "ReadingBuddy", 64, TextAlignmentOptions.Left);
        title.fontStyle = FontStyles.Bold;
        title.color = UiTheme.Primary;
        title.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
    }

    // (b) "Recent reads" horizontal rail — the one surface for "a book I've already met".
    // It deliberately keeps FINISHED books: re-reading is core behavior at this age, and the
    // rail's tap already lands on page 1 for a done book (PRScript's resume guard), which IS
    // "read again". Membership: a last-opened stamp, OR page progress, OR done — the latter two
    // keep installs that predate the stamp populated on their first run after updating.
    // Order: most recently opened first; unstamped books sink to the end, ordered by how far
    // through them the child got. Nav tiles (books with an action) are never listed.
    private const int RecentRailMax = 10;

    private void BuildContinueRail(Transform parent)
    {
        var recent = new List<(PRBook book, long opened, int page, bool done)>();
        foreach (var b in Globals.g_listPRBooks)
        {
            if (b == null || string.IsNullOrEmpty(b.bookUrl)) continue;
            if (!string.IsNullOrEmpty(b.action)) continue; // navigation tile, not a real book
            long opened = Globals.Prefs_Get_Book_LastOpened(b.bookUrl);
            int page = Globals.Prefs_Get_Book_Page(b.bookUrl);
            bool done = Globals.Prefs_Get_Book_Done(b.bookUrl) == 1;
            if (opened > 0 || page > 0 || done)
                recent.Add((b, opened, page, done));
        }
        if (recent.Count == 0) return; // hide the rail entirely

        recent.Sort((x, y) =>
        {
            if (x.opened != y.opened) return y.opened.CompareTo(x.opened); // newest first; 0 sinks
            if (x.page != y.page) return y.page.CompareTo(x.page);         // then furthest along
            return string.CompareOrdinal(x.book.bookUrl, y.book.bookUrl);  // stable, catalog-order independent
        });
        if (recent.Count > RecentRailMax) recent.RemoveRange(RecentRailMax, recent.Count - RecentRailMax);

        var section = new GameObject("ContinueSection", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
        section.transform.SetParent(parent, false);
        var svlg = section.GetComponent<VerticalLayoutGroup>();
        svlg.spacing = 14;
        svlg.childControlWidth = true; svlg.childControlHeight = true;
        svlg.childForceExpandWidth = true; svlg.childForceExpandHeight = false;
        section.GetComponent<LayoutElement>().preferredHeight = 430f;

        var heading = MakeText(section.transform, "Heading", "Recent reads", 38, TextAlignmentOptions.Left);
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

        foreach (var entry in recent)
            BuildBookCard(railContent.transform, entry.book, entry.done);
    }

    private void BuildBookCard(Transform parent, PRBook book, bool done)
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

        // Finished books stay on the rail, so they need to read as finished at a glance.
        if (done) BuildDoneChip(cardGO.transform);

        var captured = book;
        // Instant pressed state + a rendered beat before the (async) _Story load — a book tap
        // used to sit dead for the whole scene load.
        TapFeedback.AddPressFeedback(cardGO);
        cardGO.GetComponent<Button>().onClick.AddListener(
            () => TapFeedback.TapThenGo(cardGO.transform, () => Nav.GoToBook(captured)));
    }

    // (c) Illustrated door cards. Each door carries book art, its label and an accent bar in its own
    // colour, and opens EXACTLY what the old label+glyph tile with that filter opened. A door is
    // dropped when its filter yields no books in the current catalog (the SAME Filter.Conforms
    // predicate the Library uses) or when its optional min/max age misses the age chips.
    //
    // Layout: a GridLayoutGroup cannot span columns, so "wide" doors are emitted as full-width rows
    // in the parent vertical stack and each run of narrow doors between them becomes its own
    // 2-column grid block — which keeps the configured order exactly as authored.
    private void BuildDoorRooms(Transform parent)
    {
        if (_doors == null) return;

        int ageLo = Globals.GetAgeLo(), ageHi = Globals.GetAgeHi();
        var live = new List<HomeDoor>();
        foreach (var door in _doors)
        {
            if (door == null || string.IsNullOrEmpty(door.filter)) continue;
            if (!door.MatchesAgeRange(ageLo, ageHi)) continue;
            if (!IsAddress(door.filter) && !FilterHasBooks(door.filter)) continue;
            live.Add(door);
        }
        if (live.Count == 0)
        {
            // Every door filtered out — an age band no door declares books for, or a published set
            // whose filters find nothing in this catalog. Fall back to the compiled-in room set
            // UNFILTERED rather than render an empty rooms section: the worst case is then exactly
            // the pre-redesign Home, which is the floor this whole screen is allowed to degrade to.
            Debug.LogWarning("HomeController: no door survived the age/catalog filters; " +
                             "falling back to the compiled-in room set.");
            live = HomeDoorsConfig.FromSectionTiles(sectionTiles);
            if (live.Count == 0) return;
        }

        var heading = MakeText(parent, "RoomsHeading", "Reading rooms", 36, TextAlignmentOptions.Left);
        heading.color = UiTheme.TextSecondary;
        heading.gameObject.AddComponent<LayoutElement>().preferredHeight = 50f;

        int slot = 0;
        int i = 0;
        while (i < live.Count)
        {
            if (live[i].wide)
            {
                BuildDoorCard(parent, live[i], slot++, true);
                i++;
                continue;
            }

            int runStart = i;
            while (i < live.Count && !live[i].wide) i++;

            var gridGO = new GameObject("Doors", typeof(RectTransform), typeof(GridLayoutGroup), typeof(LayoutElement));
            gridGO.transform.SetParent(parent, false);
            var grid = gridGO.GetComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(DoorCellW, DoorCellH);
            grid.spacing = new Vector2(DoorGap, DoorGap);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 2;
            grid.childAlignment = TextAnchor.UpperCenter;
            int rows = (i - runStart + 1) / 2;
            gridGO.GetComponent<LayoutElement>().preferredHeight = rows * DoorCellH + (rows - 1) * DoorGap;

            for (int k = runStart; k < i; k++)
                BuildDoorCard(gridGO.transform, live[k], slot++, false);
        }
    }

    // ---- door card metrics (1080x1920 reference space) ----
    private const float DoorGap     = 40f;
    private const float DoorCellW   = 472f;                             // half the 984pt content width
    private const float DoorPad     = 14f;
    private const float DoorArtH    = (DoorCellW - 2f * DoorPad) / 1.5f; // 3:2 art -> 296
    private const float DoorLabelH  = 56f;
    // The mock's 4-6 CSS px bottom border, scaled from its 380px phone to the 1080pt reference (x2.84).
    private const float DoorAccentH = 14f;
    private const float DoorSpacing = 8f;
    private const float DoorCellH   = DoorPad * 2f + DoorArtH + DoorSpacing + DoorLabelH
                                      + DoorSpacing + DoorAccentH;      // 410 — well over the 150pt floor
    private const float WideArtW    = 420f;                             // ~46% of the content width, as in the mock
    private const float WideArtH    = WideArtW / 1.5f;                  // 280
    private const float WideCellH   = DoorPad * 2f + WideArtH + DoorSpacing + DoorAccentH; // 330

    // One door: Surface card, rounded book art on top (left, for a wide door), label, accent bar.
    private void BuildDoorCard(Transform parent, HomeDoor door, int slot, bool wide)
    {
        var palette = UiTheme.Card(slot);

        var cardGO = new GameObject("Door_" + door.id,
            typeof(RectTransform), typeof(Image), typeof(Button), typeof(VerticalLayoutGroup));
        cardGO.transform.SetParent(parent, false);
        var cardImg = cardGO.GetComponent<Image>();
        cardImg.sprite = RoundedSprite(); cardImg.type = Image.Type.Sliced;
        cardImg.color = UiTheme.Surface;
        var vlg = cardGO.GetComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset((int)DoorPad, (int)DoorPad, (int)DoorPad, (int)DoorPad);
        vlg.spacing = DoorSpacing;
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childControlWidth = true; vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
        if (wide) cardGO.AddComponent<LayoutElement>().preferredHeight = WideCellH;

        // Wide doors lay art and label side by side; narrow doors stack them.
        Transform body = cardGO.transform;
        if (wide)
        {
            var row = new GameObject("Row", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            row.transform.SetParent(cardGO.transform, false);
            var hlg = row.GetComponent<HorizontalLayoutGroup>();
            hlg.spacing = 24f;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = true; hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = true;
            row.GetComponent<LayoutElement>().preferredHeight = WideArtH;
            body = row.transform;
        }

        GameObject artSlot;
        Image art = BuildDoorArt(body, door, wide, out artSlot);

        var label = MakeText(body, "Label", door.label, wide ? 48f : 34f,
                             wide ? TextAlignmentOptions.Left : TextAlignmentOptions.Center);
        label.fontStyle = FontStyles.Bold;
        label.color = UiTheme.TextPrimary;
        label.enableWordWrapping = true;
        var labelLe = label.gameObject.AddComponent<LayoutElement>();
        if (wide) labelLe.flexibleWidth = 1f;
        else labelLe.preferredHeight = DoorLabelH;

        // Accent bar hugging the card's bottom edge — the door's identity colour.
        var bar = new GameObject("Accent", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        bar.transform.SetParent(cardGO.transform, false);
        bar.GetComponent<LayoutElement>().preferredHeight = DoorAccentH;
        var barImg = bar.GetComponent<Image>();
        barImg.sprite = RoundedSprite(); barImg.type = Image.Type.Sliced;
        barImg.color = door.Accent(slot);
        barImg.raycastTarget = false;

        string captured = door.filter;
        string capturedLabel = door.label;
        TapFeedback.AddPressFeedback(cardGO);
        cardGO.GetComponent<Button>().onClick.AddListener(
            () => TapFeedback.TapThenGo(cardGO.transform, () => OpenDoor(captured, capturedLabel)));

        StartCoroutine(LoadDoorArt(door, art, artSlot, cardImg, label, palette, wide));
    }

    // The art block: a rounded-masked 3:2 frame plus (optionally) the rotating cover badge. The badge
    // is a SIBLING of the masked frame so the white ring is never clipped by the rounded corners.
    private Image BuildDoorArt(Transform parent, HomeDoor door, bool wide, out GameObject artSlot)
    {
        artSlot = new GameObject("Art", typeof(RectTransform), typeof(LayoutElement));
        artSlot.transform.SetParent(parent, false);
        var le = artSlot.GetComponent<LayoutElement>();
        le.preferredWidth  = wide ? WideArtW : DoorCellW - 2f * DoorPad;
        le.preferredHeight = wide ? WideArtH : DoorArtH;
        le.flexibleWidth = 0f; le.flexibleHeight = 0f;

        // Rounded corners: the frame's rounded sprite is used purely as a stencil (Mask), so the
        // cover underneath is clipped to the same radius as the card.
        var frame = new GameObject("Frame", typeof(RectTransform), typeof(Image), typeof(Mask));
        frame.transform.SetParent(artSlot.transform, false);
        Stretch(frame.GetComponent<RectTransform>());
        var frameImg = frame.GetComponent<Image>();
        frameImg.sprite = RoundedSprite(); frameImg.type = Image.Type.Sliced;
        frameImg.color = UiTheme.Track;                   // drawn under the art: placeholder while it loads
        frameImg.raycastTarget = false;
        frame.GetComponent<Mask>().showMaskGraphic = true;

        var artGO = new GameObject("Cover", typeof(RectTransform), typeof(Image));
        artGO.transform.SetParent(frame.transform, false);
        Stretch(artGO.GetComponent<RectTransform>());
        var art = artGO.GetComponent<Image>();
        art.color = Color.white;
        art.preserveAspect = true;
        art.raycastTarget = false;

        BuildDoorBadge(artSlot.transform, door, wide ? WideArtW : DoorCellW);
        return art;
    }

    // Art is best-effort: an empty or unreachable imageUrl degrades the card to the pre-redesign
    // glyph tile — never a blank card or a broken-image box. PRUtils.DownloadImage plants the shared
    // "NoImage" placeholder on failure, which is exactly what we detect here.
    private IEnumerator LoadDoorArt(HomeDoor door, Image art, GameObject artSlot, Image cardImg,
                                    TMP_Text label, (Color fill, Color accent) palette, bool wide)
    {
        string url = ResolveDoorImageUrl(door.imageUrl);
        if (!string.IsNullOrEmpty(url))
        {
            yield return PRUtils.DownloadImage(url, art, true, true);
            if (art == null) yield break;                                  // card destroyed mid-load
            if (art.sprite != null && art.sprite != Resources.Load<Sprite>("NoImage"))
                yield break;                                               // art is up — done
            Debug.LogWarning("HomeController: door art unavailable (" + url + "); using the glyph tile.");
        }
        DegradeToGlyph(door, artSlot, cardImg, label, palette, wide);
    }

    // The no-art floor: exactly the look this screen shipped with — palette-filled card, tinted room
    // glyph above a centred accent-coloured label.
    private void DegradeToGlyph(HomeDoor door, GameObject artSlot, Image cardImg,
                                TMP_Text label, (Color fill, Color accent) palette, bool wide)
    {
        if (artSlot != null) artSlot.SetActive(false);
        if (cardImg != null) cardImg.color = palette.fill;

        // With the art block gone the card must read as the old tile, not as a door card missing its
        // picture: centre the glyph + label in the cell instead of leaving them huddled under the top
        // padding, and pin the accent bar to the very bottom edge so it can't float mid-card. The cell
        // size itself is untouched, so nothing reflows around it.
        var cardTf = cardImg != null ? cardImg.transform : null;
        if (cardTf != null)
        {
            var cardVlg = cardTf.GetComponent<VerticalLayoutGroup>();
            if (cardVlg != null) cardVlg.childAlignment = TextAnchor.MiddleCenter;

            var accent = cardTf.Find("Accent"); // built by BuildDoorCard under this exact name
            if (accent != null)
            {
                var accentLe = accent.GetComponent<LayoutElement>();
                if (accentLe != null) accentLe.ignoreLayout = true; // out of the centred stack…
                var accentRt = (RectTransform)accent;
                accentRt.anchorMin = new Vector2(0f, 0f);
                accentRt.anchorMax = new Vector2(1f, 0f);
                accentRt.pivot     = new Vector2(0.5f, 0f);
                accentRt.offsetMin = Vector2.zero;
                accentRt.offsetMax = new Vector2(0f, DoorAccentH);  // …flush along the bottom edge
            }
        }

        if (label == null) return;

        label.color = palette.accent;
        label.alignment = TextAlignmentOptions.Center;

        var icon = AddTileIcon(label.transform.parent, door.iconKey, door.filter, palette.accent, wide ? 80f : 64f);
        if (icon != null) icon.transform.SetSiblingIndex(0);               // glyph above the label
    }

    // A small "you finished this" chip on a rail card's top-right corner: the door badge's circle
    // styling in UiTheme.Primary, with a check drawn from two rotated bars rather than a glyph —
    // no font in the project is guaranteed to carry U+2713, and a tofu box would be worse than
    // no chip at all. ignoreLayout keeps it an overlay instead of a row in the card's stack.
    private void BuildDoneChip(Transform card)
    {
        const float size = 56f;

        var chip = new GameObject("DoneChip", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        chip.transform.SetParent(card, false);
        chip.GetComponent<LayoutElement>().ignoreLayout = true;
        var rt = chip.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 1f);   // top-right of the card
        rt.sizeDelta = new Vector2(size, size);
        rt.anchoredPosition = new Vector2(-10f, -10f);
        var img = chip.GetComponent<Image>();
        img.sprite = CircleSprite();
        img.color = UiTheme.Primary;
        img.raycastTarget = false;                                      // the card keeps the tap

        // Both strokes start at the check's vertex and rotate away from it, so the joint is exact.
        var vertex = new Vector2(-size * 0.09f, -size * 0.10f);
        AddCheckStroke(chip.transform, vertex, 135f, size * 0.30f, size * 0.12f);
        AddCheckStroke(chip.transform, vertex, 45f,  size * 0.54f, size * 0.12f);
    }

    private static void AddCheckStroke(Transform parent, Vector2 vertex, float angle, float length, float thickness)
    {
        var go = new GameObject("Stroke", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0f, 0.5f);                               // rotate about the vertex end
        rt.sizeDelta = new Vector2(length, thickness);
        rt.anchoredPosition = vertex;
        rt.localRotation = Quaternion.Euler(0f, 0f, angle);
        var img = go.GetComponent<Image>();
        img.color = UiTheme.OnPrimary;
        img.raycastTarget = false;
    }

    // ---- badge groundwork (OFF by default: every shipped door uses badgePolicy "none") ----
    // A circular cover chip on the art's top-right corner — roughly a quarter of the card width, in a
    // white ring — advertising a real book behind the door. Purely data-driven: setting
    // "badge_policy": "rotateDaily" on a door in home_doors.json lights it up with no code change.
    // hostWidth = the art block's width (the card's own width for a narrow door), so the badge stays
    // ~1/4 of the picture it sits on in both card shapes.
    private void BuildDoorBadge(Transform artSlot, HomeDoor door, float hostWidth)
    {
        if (!door.RotatesBadgeDaily) return;
        PRBook book = PickDailyBook(door.filter);
        if (book == null) return;

        float size = hostWidth * 0.25f;
        const float ring = 6f;

        var badge = new GameObject("Badge", typeof(RectTransform), typeof(Image));
        badge.transform.SetParent(artSlot, false);
        var brt = badge.GetComponent<RectTransform>();
        brt.anchorMin = brt.anchorMax = brt.pivot = new Vector2(1f, 1f);    // top-right of the art
        brt.sizeDelta = new Vector2(size, size);
        brt.anchoredPosition = new Vector2(-12f, -12f);
        var bimg = badge.GetComponent<Image>();
        bimg.sprite = CircleSprite();
        bimg.color = Color.white;                                          // the ring
        bimg.raycastTarget = false;

        var inner = new GameObject("Clip", typeof(RectTransform), typeof(Image), typeof(Mask));
        inner.transform.SetParent(badge.transform, false);
        var irt = inner.GetComponent<RectTransform>();
        irt.anchorMin = Vector2.zero; irt.anchorMax = Vector2.one;
        irt.offsetMin = new Vector2(ring, ring); irt.offsetMax = new Vector2(-ring, -ring);
        var iimg = inner.GetComponent<Image>();
        iimg.sprite = CircleSprite(); iimg.color = Color.white; iimg.raycastTarget = false;
        inner.GetComponent<Mask>().showMaskGraphic = false;

        // 3:2 cover sized to 1.5x the circle's width so it fills edge to edge and the circle crops
        // the sides (the mock's object-fit:cover) instead of letterboxing.
        float inner_ = size - ring * 2f;
        var coverGO = new GameObject("Cover", typeof(RectTransform), typeof(Image));
        coverGO.transform.SetParent(inner.transform, false);
        var crt = coverGO.GetComponent<RectTransform>();
        crt.anchorMin = crt.anchorMax = crt.pivot = new Vector2(0.5f, 0.5f);
        crt.sizeDelta = new Vector2(inner_ * 1.5f, inner_);
        var cimg = coverGO.GetComponent<Image>();
        cimg.color = Color.white; cimg.preserveAspect = true; cimg.raycastTarget = false;
        LoadCover(book, cimg);
    }

    // Deterministic "book of the day" for a door: every device that shares a date shows the same
    // cover — day-of-year (UTC) indexes the door's own books in a stable ordinal order. No RNG, so
    // nothing depends on install time or launch order.
    private PRBook PickDailyBook(string filter)
    {
        if (Globals.g_listPRBooks == null || IsAddress(filter)) return null;

        var f = new Filter();
        f.SetFilter(0, 0, filter);
        f.ageLoSel = Globals.GetAgeLo();
        f.ageHiSel = Globals.GetAgeHi();

        var matches = new List<PRBook>();
        foreach (var b in Globals.g_listPRBooks)
            if (b != null && string.IsNullOrEmpty(b.action) && f.Conforms(b))
                matches.Add(b);
        if (matches.Count == 0) return null;

        matches.Sort((a, b) => string.CompareOrdinal(a.bookUrl, b.bookUrl)); // catalog-order independent
        return matches[System.DateTime.UtcNow.DayOfYear % matches.Count];
    }

    // Where a door leads — unchanged from the tiles it replaces: the learn-to-read door opens the
    // dedicated ladder, a full Nav address ("library?filter=level1") routes through Nav.Go, and every
    // other token opens the filtered Library list.
    // `label` is the caption the child just tapped. It travels with the navigation so the shelf
    // that opens is titled with the door's own name ("Stories", "Songs & Sounds") instead of the
    // name derived from the filter token ("All Books", "Rhymebooks"). The Library consumes it once;
    // filter chips inside the Library keep their existing derived titles.
    private static void OpenDoor(string filter, string label)
    {
        if (string.IsNullOrEmpty(filter)) return;
        if (IsAddress(filter)) { Nav.Go(filter, label); return; }
        if (filter.Trim().ToLowerInvariant() == "learn to read") { Navigation.GoToLearnToRead(); return; }
        Nav.GoToLibrary(filter, label);
    }

    // A door target is a Nav address (scene + query) rather than a plain Library filter token.
    private static bool IsAddress(string filter) =>
        !string.IsNullOrEmpty(filter) && filter.Contains("?");

    // Door art may be absolute or catalog-relative, resolved against the catalog's own directory
    // exactly like book covers are.
    private static string ResolveDoorImageUrl(string imageUrl)
    {
        if (string.IsNullOrEmpty(imageUrl)) return "";
        if (imageUrl.StartsWith("http", System.StringComparison.OrdinalIgnoreCase)) return imageUrl;
        return Globals.baseURL + imageUrl;
    }

    // ---------------------------------------------------------------- "For grown-ups" door
    // One low-key footer on Home opens a chooser for Settings + the Parents letter — replacing the
    // two ambiguous toolbar icons with a single adult entry point.
    private GameObject _grownups;

    private void BuildGrownupsFooter(Transform parent)
    {
        var go = new GameObject("GrownupsFooter",
            typeof(RectTransform), typeof(Image), typeof(Button), typeof(VerticalLayoutGroup), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        // 140pt, not 96: this is a thumb target for a child, at the very bottom of a scrolling
        // page. The row's own Image is the raycast target and the parent VerticalLayoutGroup
        // force-expands width, so the whole full-width band is tappable (MakeText labels have
        // raycastTarget off and never steal the hit).
        go.GetComponent<LayoutElement>().preferredHeight = 140f;
        var img = go.GetComponent<Image>();
        img.sprite = RoundedSprite(); img.type = Image.Type.Sliced; img.color = UiTheme.Track;
        var vlg = go.GetComponent<VerticalLayoutGroup>();
        vlg.childAlignment = TextAnchor.MiddleCenter;
        vlg.childControlWidth = true; vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = true;
        var label = MakeText(go.transform, "Label", "For grown-ups", 36, TextAlignmentOptions.Center);
        label.color = UiTheme.TextSecondary;
        go.GetComponent<Button>().onClick.AddListener(() => ShowGrownups(true));
    }

    private void ShowGrownups(bool show)
    {
        if (_grownups == null) BuildGrownupsPanel();
        _grownups.SetActive(show);
        if (show) _grownups.transform.SetAsLastSibling();
    }

    private void BuildGrownupsPanel()
    {
        _grownups = new GameObject("GrownupsPanel", typeof(RectTransform), typeof(Image), typeof(Button));
        _grownups.transform.SetParent(_canvasRoot.transform, false);
        Stretch(_grownups.GetComponent<RectTransform>());
        _grownups.GetComponent<Image>().color = new Color(0.29f, 0.27f, 0.24f, 0.55f); // dim backdrop
        _grownups.GetComponent<Button>().onClick.AddListener(() => ShowGrownups(false)); // tap outside closes

        var card = new GameObject("Card", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
        card.transform.SetParent(_grownups.transform, false);
        var crt = card.GetComponent<RectTransform>();
        crt.anchorMin = crt.anchorMax = crt.pivot = new Vector2(0.5f, 0.5f);
        crt.sizeDelta = new Vector2(840f, 780f);
        var cimg = card.GetComponent<Image>(); cimg.sprite = RoundedSprite(); cimg.type = Image.Type.Sliced; cimg.color = UiTheme.Surface;
        var cvl = card.GetComponent<VerticalLayoutGroup>();
        cvl.padding = new RectOffset(48, 48, 48, 48); cvl.spacing = 26; cvl.childAlignment = TextAnchor.UpperCenter;
        cvl.childControlWidth = true; cvl.childControlHeight = true;
        cvl.childForceExpandWidth = true; cvl.childForceExpandHeight = false;

        var title = MakeText(card.transform, "Title", "For grown-ups", 48, TextAlignmentOptions.Center);
        title.fontStyle = FontStyles.Bold; title.color = UiTheme.TextPrimary;
        title.gameObject.AddComponent<LayoutElement>().preferredHeight = 72f;
        var sub = MakeText(card.transform, "Sub", "Settings, our science, and printed books", 28, TextAlignmentOptions.Center);
        sub.color = UiTheme.TextSecondary;
        sub.gameObject.AddComponent<LayoutElement>().preferredHeight = 40f;

        BuildGrownupsButton(card.transform, "Settings", 0, () => { ShowGrownups(false); Navigation.GoToSettings(); });
        BuildGrownupsButton(card.transform, "Our Science", 2, () => { ShowGrownups(false); Navigation.GoToParents(); });
        // Commerce path (leads to Amazon) → gated behind a quick grown-up check, per store policy.
        BuildGrownupsButton(card.transform, "Our printed books", 3, () => { ShowGrownups(false); ShowGate(() => Navigation.GoToBookstore()); });
        BuildGrownupsButton(card.transform, "Back to books", -1, () => ShowGrownups(false));

        // Tiny build stamp so "which version do you have?" is answerable at a glance
        // (testers were re-testing stale APKs without any way to tell).
        var stamp = MakeText(card.transform, "BuildStamp", BuildInfo.Line(), 20, TextAlignmentOptions.Center);
        stamp.color = UiTheme.TextSecondary;
        stamp.gameObject.AddComponent<LayoutElement>().preferredHeight = 30f;

        _grownups.SetActive(false);
    }

    // A simple "ask a grown-up" multiple-choice math gate. On the correct answer it runs onPass.
    // Used in front of the Bookstore (external/purchase links must sit behind a parental gate).
    private GameObject _gate;

    // The question comes from this fixed set rather than a random 2-9 x 2-9 grid, which could ask
    // 7 x 8 or 9 x 6 — enough to make a grown-up stop and work it out with an impatient child at
    // their elbow. These five are answerable at a glance and still far out of reach of the
    // pre-readers the gate exists to stop.
    private static readonly (int a, int b)[] GateQuestions =
    {
        (3, 4), (4, 5), (3, 5), (4, 4), (5, 5)
    };

    private void ShowGate(System.Action onPass)
    {
        if (_gate != null) Destroy(_gate);
        var question = GateQuestions[UnityEngine.Random.Range(0, GateQuestions.Length)];
        int a = question.a, b = question.b;
        int correct = a * b;
        var opts = new List<int> { correct };
        while (opts.Count < 3) { int w = correct + UnityEngine.Random.Range(-9, 10); if (w > 0 && !opts.Contains(w)) opts.Add(w); }
        for (int i = opts.Count - 1; i > 0; i--) { int j = UnityEngine.Random.Range(0, i + 1); (opts[i], opts[j]) = (opts[j], opts[i]); }

        _gate = new GameObject("Gate", typeof(RectTransform), typeof(Image), typeof(Button));
        _gate.transform.SetParent(_canvasRoot.transform, false);
        Stretch(_gate.GetComponent<RectTransform>());
        _gate.GetComponent<Image>().color = new Color(0.29f, 0.27f, 0.24f, 0.6f);
        var gate = _gate;
        _gate.GetComponent<Button>().onClick.AddListener(() => Destroy(gate)); // tap outside cancels

        var card = new GameObject("Card", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
        card.transform.SetParent(_gate.transform, false);
        var crt = card.GetComponent<RectTransform>();
        crt.anchorMin = crt.anchorMax = crt.pivot = new Vector2(0.5f, 0.5f);
        crt.sizeDelta = new Vector2(840f, 720f);
        var cimg = card.GetComponent<Image>(); cimg.sprite = RoundedSprite(); cimg.type = Image.Type.Sliced; cimg.color = UiTheme.Surface;
        var cvl = card.GetComponent<VerticalLayoutGroup>();
        cvl.padding = new RectOffset(48, 48, 44, 44); cvl.spacing = 22; cvl.childAlignment = TextAnchor.UpperCenter;
        cvl.childControlWidth = true; cvl.childControlHeight = true; cvl.childForceExpandWidth = true; cvl.childForceExpandHeight = false;

        var title = MakeText(card.transform, "Title", "Grown-ups only", 46, TextAlignmentOptions.Center);
        title.fontStyle = FontStyles.Bold; title.color = UiTheme.TextPrimary;
        title.gameObject.AddComponent<LayoutElement>().preferredHeight = 64f;
        var q = MakeText(card.transform, "Q", "What is " + a + " x " + b + " ?", 40, TextAlignmentOptions.Center);
        q.color = UiTheme.TextSecondary; q.gameObject.AddComponent<LayoutElement>().preferredHeight = 58f;

        for (int i = 0; i < opts.Count; i++)
        {
            int val = opts[i];
            BuildGrownupsButton(card.transform, val.ToString(), i,
                () => { if (val == correct) { Destroy(gate); onPass(); } else { ShowGate(onPass); } });
        }
        BuildGrownupsButton(card.transform, "Cancel", -1, () => Destroy(gate));
        _gate.transform.SetAsLastSibling();
    }

    private void BuildGrownupsButton(Transform parent, string text, int colorIdx, UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject("GBtn_" + text,
            typeof(RectTransform), typeof(Image), typeof(Button), typeof(VerticalLayoutGroup), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        go.GetComponent<LayoutElement>().preferredHeight = 108f;
        var img = go.GetComponent<Image>();
        img.sprite = RoundedSprite(); img.type = Image.Type.Sliced;
        img.color = colorIdx >= 0 ? UiTheme.Card(colorIdx).fill : UiTheme.Track;
        var vlg = go.GetComponent<VerticalLayoutGroup>();
        vlg.childAlignment = TextAnchor.MiddleCenter;
        vlg.childControlWidth = true; vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = true;
        var label = MakeText(go.transform, "Label", text, 38, TextAlignmentOptions.Center);
        label.fontStyle = FontStyles.Bold;
        label.color = colorIdx >= 0 ? UiTheme.Card(colorIdx).accent : UiTheme.TextSecondary;
        go.GetComponent<Button>().onClick.AddListener(onClick);
    }

    // Optional room icon above the label: a single-colour rounded glyph from Resources/Icons/Rooms,
    // tinted to match the card. Resolves by the tile's explicit iconKey, falling back to the filter
    // token; a no-op when no matching sprite ships, so iconless tiles keep their label-only look.
    private GameObject AddTileIcon(Transform parent, string iconKey, string filter, Color tint, float size)
    {
        var sprite = ResolveTileIcon(iconKey, filter);
        if (sprite == null) return null;

        var go = new GameObject("Icon", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        var le = go.GetComponent<LayoutElement>();
        le.preferredWidth = size; le.preferredHeight = size;
        le.flexibleWidth = 0f; le.flexibleHeight = 0f;
        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.color = tint;
        img.preserveAspect = true;
        img.raycastTarget = false;
        return go;
    }

    private Sprite ResolveTileIcon(string iconKey, string filter)
    {
        string key = !string.IsNullOrEmpty(iconKey) ? iconKey : IconKeyFromFilter(filter);
        return string.IsNullOrEmpty(key) ? null : Resources.Load<Sprite>("Icons/Rooms/" + key);
    }

    // Filter tokens map to icon file names by keeping only letters/digits (e.g. "learn to read" ->
    // "learntoread", "sound & speech" -> "soundspeech", "everything" -> "everything").
    private static string IconKeyFromFilter(string filter)
    {
        if (string.IsNullOrEmpty(filter)) return "";
        var sb = new System.Text.StringBuilder(filter.Length);
        foreach (char c in filter.ToLowerInvariant())
            if (char.IsLetterOrDigit(c)) sb.Append(c);
        return sb.ToString();
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

    // Procedural filled circle, used for the badge's ring and its circular clip. Same anti-aliased
    // coverage trick as RoundedSprite, without the 9-slice (a circle must never be stretched).
    private static Sprite _circleSprite;
    private static Sprite CircleSprite()
    {
        if (_circleSprite != null) return _circleSprite;
        const int size = 128;
        const float r = size * 0.5f;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = x + 0.5f - r, dy = y + 0.5f - r;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                px[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(r - d + 0.5f));
            }
        tex.SetPixels(px);
        tex.Apply();
        _circleSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        return _circleSprite;
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
