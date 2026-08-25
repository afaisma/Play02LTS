using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// ============================================================================================
// Unified Reading Mode Picker (Spec Step 2) — the single code-built modal that chooses HOW a
// book is read, replacing the three scattered controls (the "I Read" pill, the voice panel,
// and the Autopage toggle).
//
// One mutually-exclusive reading mode (radio), presented as full-width rows (icon left, name +
// sub-label right). The Autopage row it used to carry now lives in the Settings scene
// (AutopageSettingRow) against the same preference. Visible rows are
// gated per book off Globals.g_prbook (voices / readToMe). On selection the picker drives the
// two playback engines directly:
//   storyteller -> ReadAlong off + voice "human"
//   appvoice    -> ReadAlong off + voice "computer"
//   pictures    -> ReadAlong off + voice "novoice"
//   iread       -> voice "novoice" + ReadAlong on   (mic only on EXPLICIT selection)
// then replays the current page so the change takes effect immediately.
//
// Self-bootstrapping (mirrors ReadAlongService): one persistent instance scans for the story
// scene's PRScript, builds its own ScreenSpaceOverlay canvas, and hides the retired legacy UI
// GameObjects. Rollback = delete this file (the hidden legacy GameObjects re-activate on the
// next scene load).
// ============================================================================================
public class UnifiedReadingModePicker : MonoBehaviour
{
    private enum Mode { Storyteller, AppVoice, IRead, Pictures }

    // ---- PlayerPrefs keys (mirror the existing {bookUrl}_page / _done pattern) ----
    // GlobalDefaultKey is the single remembered voice preference: ModeStr(Storyteller)/ModeStr(AppVoice).
    private const string GlobalDefaultKey = "reading_mode_default";
    // Global 0/1, DEFAULT ON. The preference moved out of this modal and into the Settings scene
    // (AutopageSettingRow), which reads and writes it through the two static members below.
    // GetInt's default only applies when the key was never written, so a user who explicitly
    // turned page-turning off keeps it off across this change.
    public const string AutopageKey = "reading_autopage";
    private const string PickerSeenKey = "reading_picker_seen";   // first-run flag
    private const string ModeSeenPrefix = "reading_seen_";        // per-mode discovery flag

    private const float Offscreen = 1400f; // panel start/exit Y (slides up from below)

    // ---- wiring ----
    private PRScript _prScript;
    private AudioAndTextPlayer _player;
    private ReadAlongService _readAlong;
    private float _nextScan;

    // ---- model ----
    private readonly List<Mode> _available = new();
    private bool _timmy;          // Timmy edge case (appvoice "Listen" + pictures only)
    private Mode _currentMode = Mode.AppVoice;

    // ---- built UI ----
    private GameObject _canvasRoot;
    private GameObject _modalRoot;
    private CanvasGroup _modalGroup;
    private RectTransform _panel;
    private RectTransform _tilesGrid;
    private Button _entryButton;   // existing reading-bar button ("btnVoiceSelection"), reused
    private TMP_Text _entryLabel;  // optional current-mode label inside that button
    private readonly List<(Mode mode, Image bg, Outline outline)> _tileVisuals = new();
    private bool _open;
    private bool _ireadArmed; // true once iread was explicitly picked (mic enabled); gates the dismiss fallback

    // True while the modal is open. SwipeDetector reads this to suppress page taps/swipes so the
    // dialog's own EventSystem buttons own all input while it is up. Kept in sync with _open.
    public static bool IsOpen { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (FindObjectOfType<UnifiedReadingModePicker>() != null) return;
        new GameObject("UnifiedReadingModePicker").AddComponent<UnifiedReadingModePicker>();
    }

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
        if (_prScript != null) return; // wired; nothing to poll
        if (Time.unscaledTime < _nextScan) return;
        _nextScan = Time.unscaledTime + 0.5f;

        var found = FindObjectOfType<PRScript>();
        if (found == null)
        {
            // Left the story scene: its canvas was destroyed with the scene; drop refs so a
            // re-entry rebuilds cleanly.
            if (_canvasRoot != null || _prScript != null) Teardown();
            return;
        }
        WireToStory(found);
    }

    // ---------------------------------------------------------------- wiring / teardown

    private void WireToStory(PRScript prScript)
    {
        _prScript = prScript;
        _player = prScript.audioAndTextPlayer;
        _readAlong = FindObjectOfType<ReadAlongService>();

        // Both are persistent singletons, so subscribe idempotently (drop any prior handler first).
        if (_readAlong != null)
        {
            _readAlong.Unavailable -= OnReadAlongUnavailable;
            _readAlong.Unavailable += OnReadAlongUnavailable;
        }

        HideLegacyUI();
        BuildUI();
        WireEntryButton();

        // Resolve the mode and apply it so the book starts reading in it (spec §4). The legacy voice
        // buttons that used to clobber nextPlayUseVoice are gone, so SetReadingVoice is the only
        // writer of the playback voice. NEVER replay at wire time: this runs the moment the picker
        // first finds PRScript — before parse() has built _scriptlets — so ReplayCurrenStep() would
        // NRE in SetUIAccordingToCurrentStep(). We don't need it: ApplyMode stages the voice (and
        // SetEnabled) here, and the picker wires before the first page plays, so page 1's natural
        // Play already uses the resolved mode. Explicit picks (OnTileSelected) replay later, after
        // the story is fully loaded.
        ResolveModel();
        BuildTilesForBook();
        ApplyMode(_currentMode, replay: false, allowMicEnable: false);
        MaybeAutoOpen();
    }

    // The retired legacy controls still exist as GameObjects in the story scene (their scripts
    // were removed in this cleanup; the shared MovingVoiceOptionsPanel script is kept for the
    // ratings panel). Hide them by name so the old voice/autopage UI never renders. PanelMoving-
    // RatingsContainer is deliberately left alone. Final step is to delete these GameObjects from
    // _Story.unity in the editor, after which this method can go too.
    private static readonly string[] LegacyUINames =
        { "PanelVoice", "PanelMovingOptionsContainer", "AutoPlayToggle" };

    private static void HideLegacyUI()
    {
        foreach (string name in LegacyUINames)
        {
            var go = GameObject.Find(name);
            if (go != null) go.SetActive(false);
        }
    }

    private void Teardown()
    {
        if (_readAlong != null) _readAlong.Unavailable -= OnReadAlongUnavailable;
        _prScript = null;
        _player = null;
        _readAlong = null;
        _canvasRoot = null; // destroyed with the unloaded scene
        _modalRoot = null;
        _modalGroup = null;
        _panel = null;
        _tilesGrid = null;
        _entryButton = null; // lives in the story scene; destroyed on unload, just drop the ref
        _entryLabel = null;
        _tileVisuals.Clear();
        _open = false;
        IsOpen = false;
    }

    // Safety net: if the picker is disabled/destroyed while open (e.g. scene change), make sure
    // SwipeDetector isn't left gated.
    private void OnDisable()
    {
        IsOpen = false;
    }

    // ---------------------------------------------------------------- model / gating (spec §2,§3)

    private void ResolveModel()
    {
        var book = Globals.g_prbook;
        BuildAvailable(book);
        _currentMode = Resolve(book);
        MaybeShowStorytellerFallbackHint();
    }

    // One-time-per-session note for users who prefer the Storyteller but opened a book without a human
    // recording (so it resolved to App Reads). Non-blocking; brand-new users (no stored preference,
    // and users whose preference is App Reads) never see it.
    private static bool _storytellerFallbackHintShown;
    private void MaybeShowStorytellerFallbackHint()
    {
        if (_storytellerFallbackHintShown) return;
        if (_currentMode != Mode.AppVoice || _available.Contains(Mode.Storyteller)) return; // not a fallback
        if (PlayerPrefs.GetString(GlobalDefaultKey, "") != ModeStr(Mode.Storyteller)) return; // no explicit Storyteller pref
        _storytellerFallbackHintShown = true;
        ShowToast("This book doesn't have a storyteller recording, so the app will read it.");
    }

    private void BuildAvailable(PRBook book)
    {
        _available.Clear();
        _timmy = book != null && !string.IsNullOrEmpty(book.bookName)
                              && book.bookName.ToLower().Contains("timmy");

        if (_timmy)
        {
            // Single MP3 + static text, no per-word timings: only "App voice" (relabelled
            // "Listen", no highlight promise) + "Just pictures". No iread.
            _available.Add(Mode.AppVoice);
            _available.Add(Mode.Pictures);
            return;
        }

        bool hasHuman = book != null && book.voices != null && book.voices.Contains("human");
        bool hasTts = book == null || book.voices == null || book.voices.Contains("tts");

        if (hasHuman) _available.Add(Mode.Storyteller);
        if (hasTts) _available.Add(Mode.AppVoice);
        // "I read it myself" is offered for EVERY book. It used to be gated on book.readToMe, but
        // the flag is opt-in and only a third of the catalog carries it, so most books silently
        // lost the mode the app is named for. The flag is still parsed into PRBook (no catalog
        // change) and is free to come back as an opt-OUT ("this book is not suitable to read
        // aloud") if a book ever needs to hide the mode.
        _available.Add(Mode.IRead);
        _available.Add(Mode.Pictures); // always

        if (_available.Count == 1) // pictures-only guard: always offer app voice too
            _available.Insert(0, Mode.AppVoice);
    }

    private Mode Resolve(PRBook book)
    {
        // The ONLY remembered thing is a voice preference (Storyteller vs App Reads), stored in
        // GlobalDefaultKey as ModeStr(Storyteller)/ModeStr(AppVoice). Follow-along and Silent are
        // per-session actions — never remembered, never auto-resolved. So Resolve only ever returns
        // a voice mode (or, in the pictures-only edge, whatever single mode the book offers).
        string pref = PlayerPrefs.GetString(GlobalDefaultKey, "");           // "" = brand-new user
        if (pref == ModeStr(Mode.AppVoice) && _available.Contains(Mode.AppVoice)) return Mode.AppVoice;
        if (_available.Contains(Mode.Storyteller)) return Mode.Storyteller;  // prefer real voice (also the default)
        if (_available.Contains(Mode.AppVoice))    return Mode.AppVoice;     // automatic fallback
        return _available[0];                                               // pictures-only edge
    }

    // ---------------------------------------------------------------- apply (spec §5,§8)

    private void ApplyMode(Mode mode, bool replay, bool allowMicEnable)
    {
        switch (mode)
        {
            case Mode.Storyteller:
                _readAlong?.SetEnabled(false);
                _player?.SetReadingVoice("human");
                break;
            case Mode.AppVoice:
                _readAlong?.SetEnabled(false);
                _player?.SetReadingVoice("computer");
                break;
            case Mode.Pictures:
                _readAlong?.SetEnabled(false);
                _player?.SetReadingVoice("novoice");
                break;
            case Mode.IRead:
                _player?.SetReadingVoice("novoice");
                // Mic is requested by ReadAlongService.SetEnabled(true) → only on an EXPLICIT
                // user pick (allowMicEnable). On a silent/remembered resolve we leave it off and
                // let MaybeAutoOpen surface the picker so the child taps it themselves (spec §10).
                if (allowMicEnable) _readAlong?.SetEnabled(true);
                else _readAlong?.SetEnabled(false);
                break;
        }

        // Autopage is contextual: meaningful only for the audio-narrated modes.
        bool autopageCapable = mode == Mode.AppVoice || mode == Mode.Storyteller;
        if (autopageCapable) _player?.SetAutopage(AutopagePref());
        else _player?.SetAutopage(false);

        _currentMode = mode;
        // iread counts as "armed" only when explicitly picked (mic on). A silent/remembered iread
        // resolve stays un-armed so a dismiss can fall back to narration (see ClosePicker).
        _ireadArmed = (mode == Mode.IRead && allowMicEnable);
        UpdateEntryLabel();
        SyncTileSelection();

        if (replay) _prScript?.ReplayCurrenStep();
    }

    private void OnTileSelected(Mode mode)
    {
        // Only the voice preference is remembered. Picking Storyteller/App Reads updates the global
        // preference (applies to every book). Follow-along and Silent are session-only — write nothing,
        // so a book never reopens on its own as silent or demanding the mic.
        if (mode == Mode.Storyteller || mode == Mode.AppVoice)
        {
            PlayerPrefs.SetString(GlobalDefaultKey, ModeStr(mode));
            PlayerPrefs.Save();
        }

        // replay:false — ClosePicker() replays once below; replaying here too would run the page's
        // scriptlet twice (duplicate Schedule events, duplicate CreateButton appends).
        ApplyMode(mode, replay: false, allowMicEnable: true);
        ClosePicker(); // selecting is the action; the slide-down is the "done" feedback (spec §7)
    }

    // Mic/recognizer failed (e.g. permission denied) — fall back to narration for THIS session so
    // the page doesn't sit silent. Don't persist: the per-book "iread" pref is untouched, so the
    // book still offers (and remembers) "I read" next time, when the mic may be granted.
    private void OnReadAlongUnavailable()
    {
        if (_currentMode != Mode.IRead) return; // already moved on
        var fallback = _available.Contains(Mode.Storyteller) ? Mode.Storyteller : Mode.AppVoice;
        // replay only when the picker is closed: if it's open, ClosePicker() below does the single
        // replay (an early-returning ClosePicker would otherwise leave this path with none).
        ApplyMode(fallback, replay: !_open, allowMicEnable: false);
        if (_open) ClosePicker();
    }

    /// <summary>Is "turn pages automatically" on? Defaults to ON for a user who never chose.</summary>
    public static bool AutopageEnabled() => PlayerPrefs.GetInt(AutopageKey, 1) == 1;

    /// <summary>Store the preference. Written by the Settings scene's toggle row.</summary>
    public static void SetAutopageEnabled(bool on)
    {
        PlayerPrefs.SetInt(AutopageKey, on ? 1 : 0);
        PlayerPrefs.Save();
    }

    private bool AutopagePref() => AutopageEnabled();

    // ---------------------------------------------------------------- auto-open (spec §4)

    private void MaybeAutoOpen()
    {
        // "How shall we read?" opens on EVERY book start, not just on the old discovery moments
        // (first run / first Follow-along / an unarmed remembered iread). Testers saw the question
        // only sometimes, which made it feel like a glitch rather than a choice; asking every time
        // makes the mode a deliberate, visible decision at the start of each book.
        //
        // The one exception is a book that offers a single mode: there is no decision to make, and a
        // one-tile modal is just a wall between the child and the story. (BuildAvailable always
        // reaches at least two today — the pictures-only edge inserts App voice — so this is a guard
        // on intent, not a branch that currently fires.)
        //
        // Cost to a returning child is one tap: OpenPicker -> SyncTileSelection pre-selects the
        // remembered mode, and Close / backdrop keeps it and starts reading (unchanged behaviour).
        //
        // MarkSeen and the first-run / per-mode discovery flags stay exactly as they were. They no
        // longer gate this call, so they are close to dead, but they still record what the child has
        // been shown and are what a rollback of this behaviour would read.
        if (_available.Count >= 2)
            OpenPicker();
    }

    // ---------------------------------------------------------------- UI construction (spec §6,§7)

    private void BuildUI()
    {
        if (_canvasRoot != null) return;

        if (EventSystem.current == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        _canvasRoot = new GameObject("ReadingModePickerCanvas",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = _canvasRoot.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1001; // above the story UI (and the legacy pill's 1000)
        var scaler = _canvasRoot.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 0.5f;

        BuildModal(_canvasRoot.transform);
    }

    // Reuse the EXISTING reading-bar button ("btnVoiceSelection") rather than spawning our own
    // (which overlapped the gallery's puzzle button and left this one dead). We only ADD a runtime
    // onClick → TogglePicker. The button's stale Inspector-set onClick to the old voice panel is
    // cleared in the editor: onClick.RemoveAllListeners() can't drop persistent listeners, so we
    // don't attempt it here. If the button is missing, warn and no-op (never crash).
    private void WireEntryButton()
    {
        var go = GameObject.Find("btnVoiceSelection");
        if (go == null)
        {
            Debug.LogWarning("UnifiedReadingModePicker: 'btnVoiceSelection' not found — reading-mode button unavailable.");
            return;
        }
        _entryButton = go.GetComponent<Button>();
        if (_entryButton == null)
        {
            Debug.LogWarning("UnifiedReadingModePicker: 'btnVoiceSelection' has no Button — reading-mode button unavailable.");
            return;
        }
        _entryButton.onClick.AddListener(TogglePicker);
        // Optional: show the current mode on the button. No-op if it has no TMP label.
        _entryLabel = go.GetComponentInChildren<TMP_Text>(true);
    }

    private void BuildModal(Transform parent)
    {
        _modalRoot = new GameObject("PickerModal", typeof(RectTransform));
        _modalRoot.transform.SetParent(parent, false);
        var mrt = _modalRoot.GetComponent<RectTransform>();
        mrt.anchorMin = Vector2.zero; mrt.anchorMax = Vector2.one;
        mrt.offsetMin = Vector2.zero; mrt.offsetMax = Vector2.zero;
        _modalGroup = _modalRoot.AddComponent<CanvasGroup>();
        _modalGroup.alpha = 0f;
        _modalGroup.blocksRaycasts = false;
        _modalGroup.interactable = false;

        // Dim backdrop — tap-outside closes (spec §7).
        var backdrop = new GameObject("Backdrop", typeof(RectTransform), typeof(Image), typeof(Button));
        backdrop.transform.SetParent(_modalRoot.transform, false);
        var brt = backdrop.GetComponent<RectTransform>();
        brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one;
        brt.offsetMin = Vector2.zero; brt.offsetMax = Vector2.zero;
        backdrop.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);
        backdrop.GetComponent<Button>().onClick.AddListener(ClosePicker);

        // Panel: vertical stack, capped width, slides up from the bottom.
        var panelGO = new GameObject("Panel",
            typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        panelGO.transform.SetParent(_modalRoot.transform, false);
        _panel = panelGO.GetComponent<RectTransform>();
        _panel.anchorMin = new Vector2(0.5f, 0f);
        _panel.anchorMax = new Vector2(0.5f, 0f);
        _panel.pivot = new Vector2(0.5f, 0f);
        _panel.sizeDelta = new Vector2(760f, 0f);
        var panelImg = panelGO.GetComponent<Image>();
        panelImg.sprite = RoundedSprite(); panelImg.type = Image.Type.Sliced;
        panelImg.color = UiTheme.Surface;

        var vlg = panelGO.GetComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(36, 36, 36, 36);
        vlg.spacing = 24;
        vlg.childControlWidth = true; vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
        vlg.childAlignment = TextAnchor.UpperCenter;
        var fitter = panelGO.GetComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        BuildTitleRow(panelGO.transform);

        // Mode rows, stacked full width. Was a 2-up grid of square tiles; a single column reads
        // top to bottom like a list of choices and leaves room for a glyph beside each name.
        var listGO = new GameObject("Tiles", typeof(RectTransform), typeof(VerticalLayoutGroup));
        listGO.transform.SetParent(panelGO.transform, false);
        _tilesGrid = listGO.GetComponent<RectTransform>();
        var list = listGO.GetComponent<VerticalLayoutGroup>();
        list.spacing = 18f;
        list.childControlWidth = true; list.childControlHeight = true;
        list.childForceExpandWidth = true; list.childForceExpandHeight = false;
        list.childAlignment = TextAnchor.UpperCenter;
        var listFit = listGO.AddComponent<ContentSizeFitter>();
        listFit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // Start hidden below the screen.
        _panel.anchoredPosition = new Vector2(0f, -Offscreen);
        _modalRoot.SetActive(false);
    }

    private void BuildTitleRow(Transform parent)
    {
        var rowGO = new GameObject("TitleRow", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        rowGO.transform.SetParent(parent, false);
        var hlg = rowGO.GetComponent<HorizontalLayoutGroup>();
        hlg.childControlWidth = true; hlg.childControlHeight = true;
        hlg.childForceExpandWidth = true; hlg.childForceExpandHeight = false;
        hlg.childAlignment = TextAnchor.MiddleLeft;
        rowGO.GetComponent<LayoutElement>().preferredHeight = 70f;

        var title = MakeText(rowGO.transform, "Title", "How shall we read?", 40, TextAlignmentOptions.Left);
        title.fontStyle = FontStyles.Bold;

        var xGO = new GameObject("Close", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        xGO.transform.SetParent(rowGO.transform, false);
        var xImg = xGO.GetComponent<Image>();
        // Solid red circular close button — procedural circle sprite (no built-in asset dependency;
        // "UI/Skin/Knob.psd" doesn't resolve in this Unity version). White circle tinted red.
        xImg.sprite = CircleSprite();
        xImg.type = Image.Type.Simple;
        xImg.color = UiTheme.TextSecondary;
        xGO.GetComponent<Button>().onClick.AddListener(ClosePicker);
        var xle = xGO.GetComponent<LayoutElement>();
        xle.preferredWidth = 80f; xle.preferredHeight = 80f; xle.flexibleWidth = 0f;
        // "×" (U+00D7) is present in the project's LiberationSans SDF static atlas, so it renders.
        var xLabel = MakeText(xGO.transform, "x", "×", 44, TextAlignmentOptions.Center);
        xLabel.fontStyle = FontStyles.Bold;
        xLabel.color = Color.white;
        var xrt = xLabel.rectTransform;
        xrt.anchorMin = Vector2.zero; xrt.anchorMax = Vector2.one;
        xrt.offsetMin = Vector2.zero; xrt.offsetMax = Vector2.zero;
    }

    // A solid white circle sprite, generated once and cached. Tinted via Image.color. Avoids any
    // built-in/Resources asset dependency that may be absent across Unity versions.
    private static Sprite _circleSprite;
    private static Sprite CircleSprite()
    {
        if (_circleSprite != null) return _circleSprite;
        const int d = 64;
        var tex = new Texture2D(d, d, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        float r = d * 0.5f;
        var px = new Color32[d * d];
        for (int y = 0; y < d; y++)
            for (int x = 0; x < d; x++)
            {
                float dx = x + 0.5f - r, dy = y + 0.5f - r;
                bool inside = dx * dx + dy * dy <= (r - 0.5f) * (r - 0.5f);
                px[y * d + x] = inside ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
            }
        tex.SetPixels32(px);
        tex.Apply();
        _circleSprite = Sprite.Create(tex, new Rect(0, 0, d, d), new Vector2(0.5f, 0.5f));
        return _circleSprite;
    }

    // Build tiles once for the current book's available modes (gating is fixed per book, so the
    // grid is stable for the whole story scene — no per-open rebuild flicker).
    private void BuildTilesForBook()
    {
        if (_tilesGrid == null) return;
        for (int i = _tilesGrid.childCount - 1; i >= 0; i--)
            DestroyImmediate(_tilesGrid.GetChild(i).gameObject);
        _tileVisuals.Clear();

        foreach (var mode in _available)
            BuildTile(mode);

        SyncTileSelection();
    }

    // Record that the child has now been shown these modes, so the discovery auto-open (spec §4)
    // only fires once per mode. Called when the picker is actually displayed.
    private void MarkSeen()
    {
        foreach (var m in _available)
            PlayerPrefs.SetInt(ModeSeenPrefix + ModeStr(m), 1);
        PlayerPrefs.SetInt(PickerSeenKey, 1);
        PlayerPrefs.Save();
    }

    // ---- mode row metrics ----
    private const float RowHeight = 152f;
    private const float IconBox   = 96f;

    // One mode = one full-width row: glyph on the left, name + sub-label on the right, the whole
    // row outlined like a Home card. The row IS the button (the label/glyph never take the tap).
    private void BuildTile(Mode mode)
    {
        var tileGO = new GameObject("Tile_" + ModeStr(mode),
            typeof(RectTransform), typeof(Image), typeof(Button), typeof(Outline),
            typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        tileGO.transform.SetParent(_tilesGrid, false);
        var bg = tileGO.GetComponent<Image>();
        bg.sprite = RoundedSprite(); bg.type = Image.Type.Sliced;
        bg.color = UiTheme.Surface;
        // The contour is always drawn — SyncTileSelection only changes its colour (and the fill),
        // so an unselected row still reads as a card rather than as flat text on the panel.
        var outline = tileGO.GetComponent<Outline>();
        outline.effectColor = UiTheme.Track;
        outline.effectDistance = new Vector2(3f, 3f);
        outline.enabled = true;

        var le = tileGO.GetComponent<LayoutElement>();
        le.preferredHeight = RowHeight;
        le.minHeight = RowHeight;

        var hlg = tileGO.GetComponent<HorizontalLayoutGroup>();
        hlg.padding = new RectOffset(28, 28, 18, 18);
        hlg.spacing = 24;
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childControlWidth = true; hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;

        BuildModeIcon(tileGO.transform, mode);

        var col = new GameObject("Text", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
        col.transform.SetParent(tileGO.transform, false);
        col.GetComponent<LayoutElement>().flexibleWidth = 1f;
        var cvl = col.GetComponent<VerticalLayoutGroup>();
        cvl.spacing = 4;
        cvl.childAlignment = TextAnchor.MiddleLeft;
        cvl.childControlWidth = true; cvl.childControlHeight = true;
        cvl.childForceExpandWidth = true; cvl.childForceExpandHeight = false;

        var title = MakeText(col.transform, "title", TileLabel(mode), 34, TextAlignmentOptions.Left);
        title.fontStyle = FontStyles.Bold;
        title.gameObject.AddComponent<LayoutElement>().preferredHeight = 48f;
        var subL = MakeText(col.transform, "sub", SubLabel(mode), 24, TextAlignmentOptions.Left);
        subL.color = UiTheme.TextSecondary;
        subL.gameObject.AddComponent<LayoutElement>().preferredHeight = 34f;

        var captured = mode;
        tileGO.GetComponent<Button>().onClick.AddListener(() => OnTileSelected(captured));

        _tileVisuals.Add((mode, bg, outline));
    }

    // ---------------------------------------------------------------- mode glyphs
    // Drawn in code from primitive shapes (the same technique as the Home rail's check chip):
    // plain rects, ellipses, triangles and masked rings, tinted with UiTheme colours. Deliberately
    // NOT emoji or font glyphs — the project's UI font ships a static atlas, so any character it
    // wasn't built with renders as tofu; and NOT image assets, so nothing new has to ship.
    // Every part is laid out in a centred 96x96 box, in that box's local coordinates.
    private static void BuildModeIcon(Transform parent, Mode mode)
    {
        var box = new GameObject("Icon", typeof(RectTransform), typeof(LayoutElement));
        box.transform.SetParent(parent, false);
        var le = box.GetComponent<LayoutElement>();
        le.preferredWidth = IconBox; le.preferredHeight = IconBox;
        le.flexibleWidth = 0f; le.flexibleHeight = 0f;
        Color ink = UiTheme.Primary;

        switch (mode)
        {
            case Mode.AppVoice:    BuildSpeakerGlyph(box.transform, ink, waves: true, muted: false); break;
            // "App Is Silent" is Mode.Pictures' label, so its glyph is the crossed-out speaker
            // that matches what the row SAYS, not a picture frame.
            case Mode.Pictures:    BuildSpeakerGlyph(box.transform, ink, waves: false, muted: true); break;
            case Mode.IRead:       BuildMicrophoneGlyph(box.transform, ink); break;
            case Mode.Storyteller: BuildOpenBookGlyph(box.transform, ink); break;
        }
    }

    // Speaker: driver rect + cone triangle, then either sound arcs (App Reads) or a slash (silent).
    private static void BuildSpeakerGlyph(Transform box, Color ink, bool waves, bool muted)
    {
        AddShape(box, "Driver",  null,             -30f, 0f, 20f, 30f, 0f, ink);
        AddShape(box, "Cone",    TriangleSprite(),  -8f, 0f, 28f, 56f, 0f, ink);
        if (waves)
        {
            // Two concentric rings whose LEFT halves are clipped away, leaving open arcs.
            var clip = AddClip(box, "Waves", 26f, 0f, 34f, 76f);
            AddShape(clip, "Arc1", RingSprite(), -17f, 0f, 44f, 44f, 0f, ink);
            AddShape(clip, "Arc2", RingSprite(), -31f, 0f, 72f, 72f, 0f, ink);
        }
        if (muted)
            AddShape(box, "Slash", null, 8f, 0f, 78f, 8f, -45f, ink);
    }

    // Microphone: capsule head, a bracket arc under it, a stand and a base bar.
    private static void BuildMicrophoneGlyph(Transform box, Color ink)
    {
        AddShape(box, "Capsule", CircleSprite(), 0f, 20f, 30f, 48f, 0f, ink);
        // Ring with its TOP half clipped away = the bracket that cradles the capsule.
        var clip = AddClip(box, "Bracket", 0f, -4f, 60f, 26f);
        AddShape(clip, "Arc", RingSprite(), 0f, 13f, 56f, 56f, 0f, ink);
        AddShape(box, "Stand", null, 0f, -26f, 8f, 20f, 0f, ink);
        AddShape(box, "Base",  null, 0f, -38f, 38f, 8f, 0f, ink);
    }

    // Open book: two page panels tilted away from a central spine.
    private static void BuildOpenBookGlyph(Transform box, Color ink)
    {
        AddShape(box, "PageL", null, -21f, 0f, 36f, 52f, 8f,  ink);
        AddShape(box, "PageR", null,  21f, 0f, 36f, 52f, -8f, ink);
        AddShape(box, "Spine", null,   0f, 0f, 8f,  58f, 0f,  ink);
    }

    // One primitive part. A null sprite draws a plain filled rectangle.
    private static Transform AddShape(Transform parent, string name, Sprite sprite,
                                      float x, float y, float w, float h, float angle, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(w, h);
        rt.anchoredPosition = new Vector2(x, y);
        if (angle != 0f) rt.localRotation = Quaternion.Euler(0f, 0f, angle);
        var img = go.GetComponent<Image>();
        if (sprite != null) img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;   // the ROW owns the tap
        return go.transform;
    }

    // A clipping window: children are shown only where they overlap this rect. Used to cut whole
    // rings down to the arcs the speaker and microphone glyphs need.
    private static Transform AddClip(Transform parent, string name, float x, float y, float w, float h)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(RectMask2D));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(w, h);
        rt.anchoredPosition = new Vector2(x, y);
        return go.transform;
    }

    // Triangle pointing LEFT (apex at the left edge, base along the right edge) — the speaker cone.
    private static Sprite _triangleSprite;
    private static Sprite TriangleSprite()
    {
        if (_triangleSprite != null) return _triangleSprite;
        const int d = 64;
        var tex = new Texture2D(d, d, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[d * d];
        for (int y = 0; y < d; y++)
            for (int x = 0; x < d; x++)
            {
                // Half-height of the cone grows linearly from 0 at the apex to d/2 at the base.
                float half = (x + 0.5f) * 0.5f;
                bool inside = Mathf.Abs(y + 0.5f - d * 0.5f) <= half;
                px[y * d + x] = inside ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
            }
        tex.SetPixels32(px);
        tex.Apply();
        _triangleSprite = Sprite.Create(tex, new Rect(0, 0, d, d), new Vector2(0.5f, 0.5f));
        return _triangleSprite;
    }

    // Annulus (open ring). Clipped by AddClip into the arc a glyph needs.
    private static Sprite _ringSprite;
    private static Sprite RingSprite()
    {
        if (_ringSprite != null) return _ringSprite;
        const int d = 128;
        const float outer = d * 0.5f;
        const float inner = outer * 0.74f;
        var tex = new Texture2D(d, d, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color[d * d];
        for (int y = 0; y < d; y++)
            for (int x = 0; x < d; x++)
            {
                float dx = x + 0.5f - outer, dy = y + 0.5f - outer;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                // Anti-aliased on both edges of the band, same coverage trick as CircleSprite.
                float a = Mathf.Min(Mathf.Clamp01(outer - r + 0.5f), Mathf.Clamp01(r - inner + 0.5f));
                px[y * d + x] = new Color(1f, 1f, 1f, a);
            }
        tex.SetPixels(px);
        tex.Apply();
        _ringSprite = Sprite.Create(tex, new Rect(0, 0, d, d), new Vector2(0.5f, 0.5f));
        return _ringSprite;
    }

    private void SyncTileSelection()
    {
        foreach (var (mode, bg, outline) in _tileVisuals)
        {
            bool sel = mode == _currentMode;
            // Every row keeps its contour; selection promotes it to the accent colour and tints
            // the fill. Same selection logic as the old outline-on/outline-off tiles.
            if (outline != null)
            {
                outline.effectColor = sel ? UiTheme.Primary : UiTheme.Track;
                outline.effectDistance = sel ? new Vector2(5f, 5f) : new Vector2(3f, 3f);
            }
            if (bg != null) bg.color = sel ? UiTheme.Card(0).fill : UiTheme.Surface;
        }
    }

    // ---------------------------------------------------------------- open / close (spec §7)

    private void TogglePicker()
    {
        if (_open) ClosePicker();
        else OpenPicker();
    }

    private void OpenPicker()
    {
        if (_modalRoot == null) return;
        _player?.StopAudio(); // silence any in-flight narration; nothing plays while the picker is open
        MarkSeen();
        SyncTileSelection();
        _open = true;
        IsOpen = true;
        _modalRoot.SetActive(true);
        _modalGroup.blocksRaycasts = true;
        _modalGroup.interactable = true;
        _modalGroup.DOKill();
        _modalGroup.DOFade(1f, 0.2f).SetUpdate(true);
        _panel.DOKill();
        _panel.anchoredPosition = new Vector2(0f, -Offscreen);
        _panel.DOAnchorPosY(SafeAreaInset().y + 40f, 0.3f).SetEase(Ease.OutCubic).SetUpdate(true);
    }

    private void ClosePicker()
    {
        if (_modalRoot == null || !_open) return;

        // Dismissing an un-started "I read" (resolved silently, mic never armed) would leave the
        // book completely silent. Fall back to narration — do NOT overwrite the remembered iread
        // pref; just apply so this page narrates. Explicit iread picks are armed and skip this.
        if (_currentMode == Mode.IRead && !_ireadArmed)
        {
            var fallback = _available.Contains(Mode.Storyteller) ? Mode.Storyteller : Mode.AppVoice;
            // replay:false — the single replay below covers this path too (no double execution).
            ApplyMode(fallback, replay: false, allowMicEnable: false);
        }

        _open = false;
        IsOpen = false;
        // Tear the modal down FIRST: the replay below can throw (e.g. the picker auto-opened while
        // the story was still downloading), and a throw after _open=false but before these lines
        // would strand an invisible fullscreen raycast blocker forever — every later ClosePicker
        // early-returns on !_open.
        _modalGroup.blocksRaycasts = false;
        _modalGroup.interactable = false;
        _modalGroup.DOKill();
        _modalGroup.DOFade(0f, 0.25f).SetUpdate(true);
        _panel.DOKill();
        _panel.DOAnchorPosY(-Offscreen, 0.25f).SetEase(Ease.InCubic).SetUpdate(true)
            .OnComplete(() => { if (_modalRoot != null) _modalRoot.SetActive(false); });

        // Now that the gate is lifted, start/continue the book in the current mode (the page didn't
        // narrate while the picker was up). ReplayCurrenStep no-ops before the script has parsed.
        _prScript?.ReplayCurrenStep();
    }

    // ---------------------------------------------------------------- helpers

    private void UpdateEntryLabel()
    {
        if (_entryLabel != null) _entryLabel.text = TileLabel(_currentMode);
    }

    private static Vector2 SafeAreaInset()
    {
        // Bottom-left / top-right insets in reference-resolution units (canvas is 1080 wide).
        Rect sa = Screen.safeArea;
        float sx = Screen.width <= 0 ? 0f : (Screen.width - sa.width) * 0.5f / Screen.width * 1080f;
        float sy = Screen.height <= 0 ? 0f : (Screen.height - sa.height) * 0.5f / Screen.height * 1920f;
        return new Vector2(sx, sy);
    }

    [SerializeField] private TMP_FontAsset uiFont; // rounded kid font (Fredoka); falls back to default

    // Minimal code-built toast: a rounded UiTheme.Surface pill that fades in, holds, fades out, then
    // self-destroys. Non-blocking (no raycasts). Built on the picker's own canvas so it floats above
    // the story UI, consistent with the rest of this component's code-built UI.
    private void ShowToast(string message)
    {
        if (_canvasRoot == null) return;

        var go = new GameObject("Toast",
            typeof(RectTransform), typeof(Image), typeof(CanvasGroup), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
        go.transform.SetParent(_canvasRoot.transform, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, 180f); // slightly above middle, over the lower image / upper text
        var img = go.GetComponent<Image>();
        img.sprite = RoundedSprite(); img.type = Image.Type.Sliced;
        img.color = UiTheme.Surface;
        var hlg = go.GetComponent<HorizontalLayoutGroup>();
        hlg.padding = new RectOffset(28, 28, 18, 18);
        hlg.childControlWidth = true; hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;
        var fitter = go.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var label = MakeText(go.transform, "Label", message, 36, TextAlignmentOptions.Center);
        label.enableWordWrapping = true;
        label.gameObject.AddComponent<LayoutElement>().preferredWidth = 780f;

        var cg = go.GetComponent<CanvasGroup>();
        cg.alpha = 0f; cg.blocksRaycasts = false; cg.interactable = false;
        DOTween.Sequence().SetUpdate(true)
            .Append(cg.DOFade(1f, 0.25f))
            .AppendInterval(4f)
            .Append(cg.DOFade(0f, 0.5f))
            .OnComplete(() => { if (go != null) Destroy(go); });
    }

    // Procedural rounded-rect (9-sliced) sprite for panel/tiles, matching the other scenes.
    private static Sprite _roundedSprite;
    private static Sprite RoundedSprite()
    {
        if (_roundedSprite != null) return _roundedSprite;
        const int r = 28; int size = r * 2 + 4;
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
        _roundedSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f),
            100f, 0, SpriteMeshType.FullRect, new Vector4(r, r, r, r));
        return _roundedSprite;
    }

    private TMP_Text MakeText(Transform parent, string name, string text, float size, TextAlignmentOptions align)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.font = uiFont != null ? uiFont : UiTheme.Font();
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

    private static string TileLabel(Mode m) => m switch
    {
        Mode.Storyteller => "Storyteller Reads",
        Mode.AppVoice => "App Reads",
        Mode.IRead => "I read it myself",
        Mode.Pictures => "App Is Silent",
        _ => "?"
    };

    private string SubLabel(Mode m) => m switch
    {
        Mode.Storyteller => "A real voice reads",   // no middot: Fredoka's atlas lacks U+00B7 (tofu on device)
        Mode.AppVoice => _timmy ? "Listen" : "Words light up",
        Mode.IRead => "You read, I follow",
        Mode.Pictures => "No sound",
        _ => ""
    };

    private static string ModeStr(Mode m) => m switch
    {
        Mode.Storyteller => "storyteller",
        Mode.AppVoice => "appvoice",
        Mode.IRead => "iread",
        Mode.Pictures => "pictures",
        _ => ""
    };
}
