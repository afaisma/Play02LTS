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
// One mutually-exclusive reading mode (radio) + a dependent Autopage row. Visible tiles are
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
    private const string AutopageKey = "reading_autopage";        // global, 0/1
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
    private Toggle _autopageToggle;
    private GameObject _autopageRow;
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
        _autopageToggle = null;
        _autopageRow = null;
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
        if (book != null && book.readToMe) _available.Add(Mode.IRead);
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
        SyncAutopageRow();
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

    private bool AutopagePref() => PlayerPrefs.GetInt(AutopageKey, 0) == 1;

    private void OnAutopageChanged(bool on)
    {
        PlayerPrefs.SetInt(AutopageKey, on ? 1 : 0);
        PlayerPrefs.Save();
        bool autopageCapable = _currentMode == Mode.AppVoice || _currentMode == Mode.Storyteller;
        if (autopageCapable) _player?.SetAutopage(on);
    }

    // ---------------------------------------------------------------- auto-open (spec §4)

    private void MaybeAutoOpen()
    {
        // Only auto-open when there's a real decision to surface; otherwise just start reading in the
        // resolved mode and leave changes to the mode button. Storyteller/App Reads/Silent never pop
        // the modal on their own.
        bool firstRun = !PlayerPrefs.HasKey(PickerSeenKey);

        // Discovery of the read-yourself (Follow-along) mode — surface once when a book first offers it.
        bool firstFollowAlong = _available.Contains(Mode.IRead)
            && PlayerPrefs.GetInt(ModeSeenPrefix + ModeStr(Mode.IRead), 0) == 0;

        // A silently-resolved iread can't apply (no surprise mic) — surface so the pick is explicit.
        bool rememberedIReadUnarmed = _currentMode == Mode.IRead && !_ireadArmed;

        if (firstRun || firstFollowAlong || rememberedIReadUnarmed)
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

        // Tiles grid.
        var gridGO = new GameObject("Tiles", typeof(RectTransform), typeof(GridLayoutGroup));
        gridGO.transform.SetParent(panelGO.transform, false);
        _tilesGrid = gridGO.GetComponent<RectTransform>();
        var grid = gridGO.GetComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(330f, 200f);
        grid.spacing = new Vector2(20f, 20f);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 2;
        grid.childAlignment = TextAnchor.UpperCenter;
        var gridFit = gridGO.AddComponent<ContentSizeFitter>();
        gridFit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        BuildAutopageRow(panelGO.transform);

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

    private void BuildAutopageRow(Transform parent)
    {
        _autopageRow = new GameObject("AutopageRow",
            typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        _autopageRow.transform.SetParent(parent, false);
        var hlg = _autopageRow.GetComponent<HorizontalLayoutGroup>();
        hlg.childControlWidth = true; hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.spacing = 18;
        _autopageRow.GetComponent<LayoutElement>().preferredHeight = 70f;

        // Minimal code-built toggle: a box whose checkmark child reflects state.
        var tglGO = new GameObject("Toggle", typeof(RectTransform), typeof(Toggle), typeof(LayoutElement));
        tglGO.transform.SetParent(_autopageRow.transform, false);
        var tle = tglGO.GetComponent<LayoutElement>();
        tle.preferredWidth = 64f; tle.preferredHeight = 64f;

        var boxGO = new GameObject("Box", typeof(RectTransform), typeof(Image));
        boxGO.transform.SetParent(tglGO.transform, false);
        var boxRt = boxGO.GetComponent<RectTransform>();
        boxRt.anchorMin = Vector2.zero; boxRt.anchorMax = Vector2.one;
        boxRt.offsetMin = Vector2.zero; boxRt.offsetMax = Vector2.zero;
        var boxImg = boxGO.GetComponent<Image>();
        boxImg.sprite = RoundedSprite(); boxImg.type = Image.Type.Sliced;
        boxImg.color = UiTheme.Track;

        var checkGO = new GameObject("Check", typeof(RectTransform), typeof(Image));
        checkGO.transform.SetParent(boxGO.transform, false);
        var checkRt = checkGO.GetComponent<RectTransform>();
        checkRt.anchorMin = new Vector2(0.15f, 0.15f);
        checkRt.anchorMax = new Vector2(0.85f, 0.85f);
        checkRt.offsetMin = Vector2.zero; checkRt.offsetMax = Vector2.zero;
        checkGO.GetComponent<Image>().color = UiTheme.Primary;

        _autopageToggle = tglGO.GetComponent<Toggle>();
        _autopageToggle.targetGraphic = boxGO.GetComponent<Image>();
        _autopageToggle.graphic = checkGO.GetComponent<Image>();
        _autopageToggle.isOn = AutopagePref();
        _autopageToggle.onValueChanged.AddListener(OnAutopageChanged);

        var label = MakeText(_autopageRow.transform, "Label", "Turn the page for me", 30, TextAlignmentOptions.Left);
        var lle = label.gameObject.AddComponent<LayoutElement>();
        lle.flexibleWidth = 1f;
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

    private void BuildTile(Mode mode)
    {
        var tileGO = new GameObject("Tile_" + ModeStr(mode),
            typeof(RectTransform), typeof(Image), typeof(Button), typeof(Outline), typeof(VerticalLayoutGroup));
        tileGO.transform.SetParent(_tilesGrid, false);
        var bg = tileGO.GetComponent<Image>();
        bg.sprite = RoundedSprite(); bg.type = Image.Type.Sliced;
        bg.color = UiTheme.Surface;
        var outline = tileGO.GetComponent<Outline>();
        outline.effectColor = UiTheme.Primary;
        outline.effectDistance = new Vector2(3f, 3f);
        outline.enabled = false;

        var vlg = tileGO.GetComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(18, 18, 22, 22);
        vlg.spacing = 8;
        vlg.childAlignment = TextAnchor.MiddleCenter;
        vlg.childControlWidth = true; vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;

        var title = MakeText(tileGO.transform, "title", TileLabel(mode), 36, TextAlignmentOptions.Center);
        title.fontStyle = FontStyles.Bold;
        var subL = MakeText(tileGO.transform, "sub", SubLabel(mode), 24, TextAlignmentOptions.Center);
        subL.color = UiTheme.TextSecondary;

        var captured = mode;
        tileGO.GetComponent<Button>().onClick.AddListener(() => OnTileSelected(captured));

        _tileVisuals.Add((mode, bg, outline));
    }

    private void SyncTileSelection()
    {
        foreach (var (mode, bg, outline) in _tileVisuals)
        {
            bool sel = mode == _currentMode;
            if (outline != null) outline.enabled = sel;
            if (bg != null) bg.color = sel ? UiTheme.Card(0).fill : UiTheme.Surface;
        }
    }

    private void SyncAutopageRow()
    {
        if (_autopageRow == null) return;
        // Contextual: only the audio-narrated modes auto-advance on narration end.
        bool show = _currentMode == Mode.AppVoice || _currentMode == Mode.Storyteller;
        _autopageRow.SetActive(show);
        if (show && _autopageToggle != null && _autopageToggle.isOn != AutopagePref())
            _autopageToggle.SetIsOnWithoutNotify(AutopagePref());
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
        SyncAutopageRow();
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
        Mode.Storyteller => "Real voice · listen",
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
