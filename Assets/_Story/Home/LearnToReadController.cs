using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// ============================================================================================
// Learn-to-Read ladder (Stage 3). A PLAIN scene controller: it lives only in the
// _LearnToRead scene, is NOT DontDestroyOnLoad, and builds its ENTIRE UI in code in Start()
// (mirroring HomeController). Four rungs, one per reading Level (1..4). Each rung shows its
// theme, age band, and progress (books "done" / total), and on tap opens that level's books
// in the Library via Nav.GoToLibrary("levelN") (the existing Filter.level path, which orders
// the shelf by level then number).
//
// Soft progression: nothing is locked. The ladder is driven by reading LEVEL, so it
// deliberately ignores the Home age filter (the level IS the progression).
// ============================================================================================
public class LearnToReadController : MonoBehaviour
{
    // The catalog carries `level` but no theme label, so the four rung names are defined here.
    private static readonly string[] RungLabels =
    {
        "First Sounds",      // level 1
        "Blends and Friends",// level 2
        "Long Vowels",       // level 3
        "Confident Reader",  // level 4
    };
    private const int Levels = 4;
    private const float RetryInterval = 0.5f;

    private GameObject _canvasRoot;
    private RectTransform _contentRoot;
    private GameObject _loadingLabel;

    private void Start()
    {
        BuildCanvas();
        StartCoroutine(BuildWhenCatalogReady());
    }

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

        _canvasRoot = new GameObject("LadderCanvas",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = _canvasRoot.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var scaler = _canvasRoot.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 0.5f;

        var bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
        bg.transform.SetParent(_canvasRoot.transform, false);
        Stretch(bg.GetComponent<RectTransform>());
        bg.GetComponent<Image>().color = new Color(0.10f, 0.11f, 0.16f, 1f);

        var content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup));
        content.transform.SetParent(_canvasRoot.transform, false);
        _contentRoot = content.GetComponent<RectTransform>();
        Stretch(_contentRoot);
        var vlg = content.GetComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(48, 48, 90, 48);
        vlg.spacing = 28;
        vlg.childControlWidth = true; vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
        vlg.childAlignment = TextAnchor.UpperCenter;
    }

    private void ShowLoading(bool show)
    {
        if (_loadingLabel == null)
        {
            var t = MakeText(_canvasRoot.transform, "Loading", "Loading...", 48, TextAlignmentOptions.Center);
            Stretch(t.rectTransform);
            _loadingLabel = t.gameObject;
        }
        _loadingLabel.SetActive(show);
    }

    // ---------------------------------------------------------------- content

    private void BuildContent()
    {
        for (int i = _contentRoot.childCount - 1; i >= 0; i--)
            Destroy(_contentRoot.GetChild(i).gameObject);

        BuildHeader(_contentRoot);
        for (int lvl = 1; lvl <= Levels; lvl++)
            BuildRung(_contentRoot, lvl);
    }

    // Header row: back-to-Home button + title.
    private void BuildHeader(Transform parent)
    {
        var rowGO = new GameObject("Header", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        rowGO.transform.SetParent(parent, false);
        rowGO.GetComponent<LayoutElement>().preferredHeight = 96f;
        var hlg = rowGO.GetComponent<HorizontalLayoutGroup>();
        hlg.spacing = 16;
        hlg.childControlWidth = true; hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = true;
        hlg.childAlignment = TextAnchor.MiddleLeft;

        var backGO = new GameObject("BackHome", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        backGO.transform.SetParent(rowGO.transform, false);
        var ble = backGO.GetComponent<LayoutElement>();
        ble.preferredWidth = 150f; ble.preferredHeight = 76f;
        var bimg = backGO.GetComponent<Image>();
        bimg.sprite = RoundedSprite(); bimg.type = Image.Type.Sliced;
        bimg.color = new Color(1f, 1f, 1f, 0.10f);
        var bt = MakeText(backGO.transform, "Label", "< Home", 30, TextAlignmentOptions.Center);
        bt.color = Color.white; Stretch(bt.rectTransform);
        backGO.GetComponent<Button>().onClick.AddListener(() => Navigation.GoToHome());

        var title = MakeText(rowGO.transform, "Title", "Learn to Read", 56, TextAlignmentOptions.Left);
        title.fontStyle = FontStyles.Bold;
        title.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
    }

    // One rung = one reading level. Counts use the level-only predicate, matching the
    // GoToLibrary("levelN") destination the rung opens, so the "done / total" is accurate.
    private void BuildRung(Transform parent, int level)
    {
        int total = 0, done = 0, ageLo = int.MaxValue, ageHi = 0;
        foreach (var b in Globals.g_listPRBooks)
        {
            if (b == null || b.level != level) continue;
            total++;
            if (b.ageFrom < ageLo) ageLo = b.ageFrom;
            if (b.ageTo > ageHi) ageHi = b.ageTo;
            if (!string.IsNullOrEmpty(b.bookUrl) && Globals.Prefs_Get_Book_Done(b.bookUrl) > 0) done++;
        }
        if (total == 0) return; // no books at this level -> no rung

        // Capped / threshold progress: a level is "mastered" after reading `target` books,
        // where target = min(books in level, masteryGoal). This caps progress at 100% and
        // scales to large levels (read any N, not all). Small levels stay completable.
        const int masteryGoal = 5;
        int target = Mathf.Clamp(Mathf.Min(total, masteryGoal), 1, total);
        int pct = Mathf.Min(100, Mathf.RoundToInt(100f * done / target));
        bool mastered = done >= target;
        string theme = (level >= 1 && level <= RungLabels.Length) ? RungLabels[level - 1] : ("Level " + level);

        var card = new GameObject("Rung_" + level,
            typeof(RectTransform), typeof(Image), typeof(Button), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        card.transform.SetParent(parent, false);
        card.GetComponent<LayoutElement>().preferredHeight = 150f;
        var cimg = card.GetComponent<Image>();
        cimg.sprite = RoundedSprite(); cimg.type = Image.Type.Sliced;
        cimg.color = new Color(1f, 1f, 1f, 0.06f);
        var chl = card.GetComponent<HorizontalLayoutGroup>();
        chl.padding = new RectOffset(20, 24, 16, 16);
        chl.spacing = 18;
        chl.childControlWidth = true; chl.childControlHeight = true;
        chl.childForceExpandWidth = false; chl.childForceExpandHeight = true;
        chl.childAlignment = TextAnchor.MiddleLeft;

        // Circular badge: level number (green when the whole level is done).
        var badge = new GameObject("Badge", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        badge.transform.SetParent(card.transform, false);
        var badgeLe = badge.GetComponent<LayoutElement>();
        badgeLe.preferredWidth = 96f; badgeLe.preferredHeight = 96f;
        var badgeImg = badge.GetComponent<Image>();
        badgeImg.sprite = CircleSprite();
        badgeImg.color = mastered ? new Color(0.36f, 0.88f, 0.71f, 1f) : new Color(0.45f, 0.8f, 1f, 1f);
        var badgeText = MakeText(badge.transform, "N", level.ToString(), 46, TextAlignmentOptions.Center);
        badgeText.color = new Color(0.03f, 0.14f, 0.24f, 1f);
        badgeText.fontStyle = FontStyles.Bold;
        Stretch(badgeText.rectTransform);

        // Text column: theme + age band.
        var col = new GameObject("Col", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
        col.transform.SetParent(card.transform, false);
        col.GetComponent<LayoutElement>().flexibleWidth = 1f;
        var cvl = col.GetComponent<VerticalLayoutGroup>();
        cvl.spacing = 4;
        cvl.childControlWidth = true; cvl.childControlHeight = true;
        cvl.childForceExpandWidth = true; cvl.childForceExpandHeight = false;
        cvl.childAlignment = TextAnchor.MiddleLeft;

        var name = MakeText(col.transform, "Theme", "Level " + level + " - " + theme, 34, TextAlignmentOptions.Left);
        name.fontStyle = FontStyles.Bold;
        name.gameObject.AddComponent<LayoutElement>().preferredHeight = 44f;

        string ageHint = ageLo <= ageHi ? ("ages " + ageLo + "-" + ageHi) : "";
        var sub = MakeText(col.transform, "Age", ageHint, 24, TextAlignmentOptions.Left);
        sub.color = new Color(1f, 1f, 1f, 0.6f);
        sub.gameObject.AddComponent<LayoutElement>().preferredHeight = 28f;

        // Progress bar filled to pct. Plain rects (no 9-slice) to avoid corner distortion on a thin bar.
        var track = new GameObject("Bar", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        track.transform.SetParent(col.transform, false);
        track.GetComponent<LayoutElement>().preferredHeight = 16f;
        var trackImg = track.GetComponent<Image>();
        trackImg.color = new Color(1f, 1f, 1f, 0.12f);
        trackImg.raycastTarget = false;
        var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fill.transform.SetParent(track.transform, false);
        var fr = fill.GetComponent<RectTransform>();
        fr.anchorMin = new Vector2(0f, 0f);
        fr.anchorMax = new Vector2(Mathf.Clamp01(pct / 100f), 1f);
        fr.offsetMin = Vector2.zero; fr.offsetMax = Vector2.zero;
        var fillImg = fill.GetComponent<Image>();
        fillImg.color = mastered ? new Color(0.36f, 0.88f, 0.71f, 1f) : new Color(0.45f, 0.8f, 1f, 1f);
        fillImg.raycastTarget = false;

        // Status on the right: "Mastered" or "<read> / <target>".
        var prog = MakeText(card.transform, "Status", mastered ? "Mastered" : (done + " / " + target), 28, TextAlignmentOptions.Right);
        prog.color = mastered ? new Color(0.36f, 0.88f, 0.71f, 1f) : new Color(0.45f, 0.8f, 1f, 1f);
        prog.gameObject.AddComponent<LayoutElement>().preferredWidth = 130f;

        int captured = level;
        card.GetComponent<Button>().onClick.AddListener(() => Nav.GoToLibrary("level" + captured));
    }

    // ---------------------------------------------------------------- helpers

    // Procedural rounded-rect (9-sliced) sprite for cards/buttons. Built once, reused.
    private static Sprite _roundedSprite;
    private static Sprite RoundedSprite()
    {
        if (_roundedSprite != null) return _roundedSprite;
        const int r = 24;
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
        tex.SetPixels(px);
        tex.Apply();
        _roundedSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f),
            100f, 0, SpriteMeshType.FullRect, new Vector4(r, r, r, r));
        return _roundedSprite;
    }

    // Procedural filled circle for the level badge.
    private static Sprite _circleSprite;
    private static Sprite CircleSprite()
    {
        if (_circleSprite != null) return _circleSprite;
        const int size = 128;
        float r = size / 2f;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = x + 0.5f - r, dy = y + 0.5f - r;
                px[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(r - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f));
            }
        tex.SetPixels(px);
        tex.Apply();
        _circleSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        return _circleSprite;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    private TMP_Text MakeText(Transform parent, string name, string text, float size, TextAlignmentOptions align)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        var font = TMP_Settings.defaultFontAsset;
        if (font != null) tmp.font = font;
        tmp.text = text;
        tmp.fontSize = size;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = size * 0.6f;
        tmp.fontSizeMax = size;
        tmp.alignment = align;
        tmp.color = Color.white;
        tmp.raycastTarget = false;
        return tmp;
    }
}
