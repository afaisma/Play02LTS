using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.IO;
using System.Collections;
using UnityEngine.Networking;
using UnityEngine.UI;
using TMPro; // Include TextMeshPro
using SimpleJSON;

public class Globals : MonoBehaviour
{
    public static string CSVURL = "http://d5wtw8f0w3ire.cloudfront.net/uploads/stories_02/stories.csv";
    public static string baseURL;
    public string csvUrl = "http://d5wtw8f0w3ire.cloudfront.net/uploads/stories_02/stories.csv";
    public string convinienceLocal = "http://localhost:8080/api/files/download/stories/stories.csv";
    public string convinienceAWS = "https://d1lgnf093kp9w0.cloudfront.net/uploads/stories-qa/stories.csv";

    public static List<PRBook> g_listPRBooks;

    public static string g_scriptName;
    public static int g_openedStoriesCount;
    public static PRBook g_prbook;
    public static string g_libraryFilter = "everything";
    public static string g_bookstoreFilter = "everything";

    private static Globals instance;
    public bool IsDownloading { get; private set; } = false;

    [SerializeField] private string targetScene = "_Library";
    [SerializeField] private float minTimeInScene = 2f;
    // Only the launch/loading screen auto-advances to targetScene. Gating on
    // this (rather than "did the active scene change during the wait?") keeps
    // the auto-advance from firing wherever the user happens to be sitting
    // when the one-shot CSV load finishes (e.g. a debug start in _Story).
    [SerializeField] private string loadingSceneName = "_Message";

    // Retry button. No longer Inspector-authored — Globals auto-binds it by
    // GameObject name (`ButtonLoadingRetryContinue`) on first Start and on
    // every scene change. Step 1 of the Globals → prefab/single-instance
    // migration: removing scene-specific Inspector wiring so the Globals
    // GameObject can be lifted out of every scene and onto a prefab.
    private Button buttonLoadingRetryContinue;

    // Game statistics variables
    private float totalMinutesInGame = 0f;
    private int numberOfRuns = 0;
    private int pagesRead = 0;
    private int booksRead = 0;
    private float gameStartTime;
    private int daysSinceFirstRun = 0;
    private Coroutine waitAndNavigateCoroutine; 
    
    // Single instance, populated by Awake() of the prefab-Instantiated
    // Globals (see Bootstrap below). The legacy auto-create fallback was
    // removed in Step 5 of the single-instance migration — Bootstrap fires
    // BeforeSceneLoad and is the only path that creates Globals.
    public static Globals Instance
    {
        get
        {
            if (instance == null)
            {
                // Should be unreachable in normal flow. Most likely cause:
                // Resources/Globals.prefab is missing (Bootstrap logged an
                // error at app launch). Defensive log so the next failure
                // is diagnosable, not silent.
                Debug.LogError("Globals.Instance accessed before Bootstrap " +
                               "instantiated the prefab — see Bootstrap log.");
            }
            return instance;
        }
    }

    /// <summary>Step 3 of the Globals → single-instance migration.
    /// Instantiate the canonical Globals from <c>Resources/Globals.prefab</c>
    /// before any scene's Awake fires. This becomes the prefab-loaded
    /// singleton, and the existing Awake duplicate-guard below destroys
    /// any scene-resident Globals on first scene load — so the prefab's
    /// Inspector values are the source of truth from now on.
    ///
    /// Coexists harmlessly with the scene-resident Globals during the
    /// migration: if a scene still has a Globals component (Step 4 hasn't
    /// removed it yet), its Awake will fire after this method, find
    /// <c>instance != null</c>, and self-destroy. No behavior change is
    /// visible until Step 4 lets the prefab values take effect by removing
    /// the in-scene copies.
    ///
    /// If <c>Resources/Globals.prefab</c> is missing (e.g. a fresh clone
    /// before Step 2 was completed), we log an error and fall through to
    /// the legacy scene-resident path — the app still works.</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null) return;
        var prefab = Resources.Load<GameObject>("Globals");
        if (prefab == null)
        {
            Debug.LogError("Globals.Bootstrap: Resources/Globals.prefab not found; " +
                           "falling back to scene-resident Globals (Step 2 of the " +
                           "migration may be incomplete).");
            return;
        }
        var go = Instantiate(prefab);
        go.name = "Globals";   // strip the "(Clone)" suffix
        // DontDestroyOnLoad happens inside the new instance's Awake().
    }

    void Awake()
    {
        // Awake runs exactly once — Globals is instantiated once by Bootstrap
        // (BeforeSceneLoad) and lives for the lifetime of the app via
        // DontDestroyOnLoad. No scene-resident Globals exist after Step 4 of
        // the single-instance migration, so the legacy duplicate-guard was
        // removed in Step 5.
        instance = this;
        DontDestroyOnLoad(gameObject);

        // Re-bind the loading-screen button on every scene activation —
        // catches the scene that owns it (typically _Message in the current
        // dev workflow). No-op for scenes without the button; existing
        // binding is preserved if still alive.
        //
        // We previously also called Resources.UnloadUnusedAssets() here as
        // a memory reclaim, but it tripped URP 17.x Render Graph mid-frame
        // with `PostProcessPassRenderGraph IndexOutOfRangeException` — the
        // pooled intermediate textures URP holds via internal handles (not
        // long-lived C# refs) look orphaned to the sweep and get freed
        // between record-time and execute-time. The cap bumps on
        // cacheImages (100) and CacheAudioAndTimingsStructs (50) already
        // bound the leak to ~15 MB GPU + ~50 MB CPU peak, which is
        // acceptable on the target devices. If we ever need to reclaim
        // again, defer to WaitForEndOfFrame + a real yield on the
        // AsyncOperation, but test thoroughly against URP rendering — the
        // straightforward inline call is unsafe in Unity 6's Render Graph.
        SceneManager.activeSceneChanged += (oldScene, newScene) =>
        {
            TryBindLoadingButton();
        };
    }

    /// <summary>Locate the loading-screen button in the active scene by
    /// GameObject name and bind it. Cheap (one GameObject.Find per scene
    /// change, ~O(N) over root objects, ~microseconds at this scene size).
    /// No-op if the field is already bound to a live object or if no scene
    /// currently contains the button. Replaces the prior [SerializeField]
    /// Inspector wiring so Globals can live on a prefab rather than in
    /// every scene.</summary>
    private void TryBindLoadingButton()
    {
        // Already-bound and still alive? Nothing to do.
        if (buttonLoadingRetryContinue != null) return;
        var go = GameObject.Find("ButtonLoadingRetryContinue");
        if (go != null)
        {
            buttonLoadingRetryContinue = go.GetComponent<Button>();
            // Binding only stores the reference. The label and click handler
            // must be (re)applied from current state on every fresh bind: the
            // one-shot CSV download no longer re-runs once the catalog is
            // cached at app launch, so it can't be relied on to configure the
            // freshly-loaded button when a scene (e.g. _Message) appears.
            ConfigureLoadingButton();
        }
    }

    /// <summary>Set the loading button's label, interactability and click
    /// handler from the current catalog/download state. Safe to call on every
    /// (re)bind and whenever that state changes. Per product decision this
    /// button always goes straight to the Library — never via
    /// targetScene/LoadTargetScene.</summary>
    private void ConfigureLoadingButton()
    {
        if (buttonLoadingRetryContinue == null) return;
        // Never stack handlers across scene changes / state transitions.
        buttonLoadingRetryContinue.onClick.RemoveAllListeners();
        if (IsDownloading)
        {
            SetButtonText("Loading Library Catalog");
            buttonLoadingRetryContinue.interactable = false;
        }
        else if (g_listPRBooks != null)
        {
            SetButtonText("Enter Library");
            buttonLoadingRetryContinue.interactable = true;
            buttonLoadingRetryContinue.onClick.AddListener(Library);
        }
        else
        {
            // Not downloading and no catalog: the last attempt failed.
            SetButtonText("Connect to the Internet and Retry");
            buttonLoadingRetryContinue.interactable = true;
            buttonLoadingRetryContinue.onClick.AddListener(RetryDownload);
        }
    }

    void Start()
    {
        gameStartTime = Time.time; // Initialize game start time for statistics
        CSVURL = csvUrl;
        baseURL = PRUtils.RemoveFileNameFromUrl(csvUrl);

        // Try to bind the button in case the current scene already contains
        // it (otherwise the activeSceneChanged hook above catches it on the
        // next scene activation).
        TryBindLoadingButton();

        // Initialize retry button and set it non-interactable
        if (buttonLoadingRetryContinue != null)
        {
            SetButtonText("Loading Library Catalog");  // Call SetButtonText to display initial message
            buttonLoadingRetryContinue.interactable = false;  // Disable interaction initially
        }

        // Initialize statistics
        InitializeGameStatistics();

        // Start loading books
        PreLoadBooks();
    }

    private void OnDestroy()
    {
    }

    void OnApplicationQuit()
    {
        UpdateGameStatistics(); // Update statistics before the application quits
    }

    // H6: OnApplicationQuit is unreliable on mobile (especially when the user kills the app
    // from the recent-apps switcher). OnApplicationPause(true) fires reliably on iOS and Android
    // when the app is backgrounded, so we persist stats there as well. UpdateGameStatistics is
    // idempotent — running it on both pause and quit is safe.
    void OnApplicationPause(bool paused)
    {
        if (paused)
        {
            UpdateGameStatistics();
            // M-R3-5: flush all PlayerPrefs writes (settings, page progress, etc.)
            // to disk. Without this, settings tweaked just before a force-kill
            // from the Android recents switcher can be lost.
            PlayerPrefs.Save();
        }
    }

    void InitializeGameStatistics()
    {
        totalMinutesInGame = PlayerPrefs.GetFloat("TotalMinutesInGame", 0f);
        numberOfRuns = PlayerPrefs.GetInt("NumberOfRuns", 0);
        pagesRead = PlayerPrefs.GetInt("PagesRead", 0);
        booksRead = PlayerPrefs.GetInt("BooksRead", 0);

        string firstRunDateStr = PlayerPrefs.GetString("FirstRunDate", "");
        if (string.IsNullOrEmpty(firstRunDateStr))
        {
            // First time running the app, set the current date
            firstRunDateStr = DateTime.UtcNow.ToString("o");
            PlayerPrefs.SetString("FirstRunDate", firstRunDateStr);
            daysSinceFirstRun = 0;
        }
        else
        {
            DateTime firstRunDate = DateTime.Parse(firstRunDateStr);
            daysSinceFirstRun = (int)(DateTime.UtcNow - firstRunDate).TotalDays;
        }

        numberOfRuns++;
        PlayerPrefs.SetInt("NumberOfRuns", numberOfRuns);
    }

    void UpdateGameStatistics()
    {
        // Calculate the time elapsed since the last save (not since app start).
        // H6: reset gameStartTime so successive calls (e.g. pause then quit) don't double-count.
        float sessionMinutes = (Time.time - gameStartTime) / 60f;
        totalMinutesInGame += sessionMinutes;
        gameStartTime = Time.time;
        PlayerPrefs.SetFloat("TotalMinutesInGame", totalMinutesInGame);

        // Update other statistics as needed (for example, pagesRead, booksRead)
        PlayerPrefs.SetInt("PagesRead", pagesRead);
        PlayerPrefs.SetInt("BooksRead", booksRead);
    }

    public void IncrementPagesRead(int pages)
    {
        pagesRead += pages;
        PlayerPrefs.SetInt("PagesRead", pagesRead);
    }

    public void IncrementBooksRead()
    {
        booksRead++;
        PlayerPrefs.SetInt("BooksRead", booksRead);
    }

    public void PreLoadBooks()
    {
        Debug.Log("PreLoadBooks");
        if (g_listPRBooks != null)
        {
            if (!string.IsNullOrEmpty(targetScene))
            {
                // Store the coroutine reference
                waitAndNavigateCoroutine = StartCoroutine(WaitAndNavigate(targetScene, minTimeInScene));
            }
            return;
        }

        float startTime = Time.time;

        StartDownloadCSV(csvUrl, (csv) =>
        {
            List<PRBook> prbooks = ParseCatalog(csvUrl, csv);
            g_listPRBooks = prbooks;

            if (!string.IsNullOrEmpty(targetScene) && (g_listPRBooks != null))
            {
                float elapsedTime = Time.time - startTime;
                float delay = Mathf.Max(0, minTimeInScene - elapsedTime); // Calculate remaining delay
                // Store the coroutine reference
                waitAndNavigateCoroutine = StartCoroutine(WaitAndNavigate(targetScene, delay));
            }
        });
    }

    private IEnumerator WaitAndNavigate(string targetScene, float delay)
    {
        yield return new WaitForSeconds(delay);
        // Auto-advance only from the launch/loading screen. Since Globals boots
        // at app launch (BeforeSceneLoad via Bootstrap) and outlives every
        // scene, this positive check is what keeps the timer from yanking the
        // user out of _Story (debug start) or anywhere else they happen to be.
        if (SceneManager.GetActiveScene().name != loadingSceneName) yield break;
        LoadTargetScene();
    }
    
    // Cancel any queued startup catalog-load navigation. Called when the user
    // opens a book: a late CSV download must not yank the reader out of _Story
    // back to targetScene (the Library). Clearing targetScene also disarms the
    // PreLoadBooks early-return branch and download callback, which both gate
    // on a non-empty targetScene.
    public void CancelPendingNavigation()
    {
        if (waitAndNavigateCoroutine != null)
        {
            StopCoroutine(waitAndNavigateCoroutine);
            waitAndNavigateCoroutine = null;
        }
        targetScene = "";
    }

    public void LoadTargetScene()
    {
        if (SceneManager.GetActiveScene().name == targetScene)
            return;

        if (!string.IsNullOrEmpty(targetScene))
        {
            // Stop the coroutine if it is running
            if (waitAndNavigateCoroutine != null)
            {
                StopCoroutine(waitAndNavigateCoroutine);
            }
            Navigation.GoToScene(targetScene);
        }
    }

    public void RetryDownload()
    {
        if (buttonLoadingRetryContinue != null)
        {
            buttonLoadingRetryContinue.interactable = false;  // Disable interaction during retry
            SetButtonText("Loading Library Catalog...");  // Reset text when retrying
        }
        PreLoadBooks(); // Retry the book download
    }
    
    public void Library()
    {
        if (!IsDownloading)
        {
            Navigation.GoToLibrary();
        }
        else
        {
            Debug.Log("Download in progress. Please wait.");
        }
    }

    public static bool IsTablet()
    {
        // Calculate the screen's diagonal size in inches
        float screenDiagonal =
            Mathf.Sqrt(Mathf.Pow(Screen.width / Screen.dpi, 2) + Mathf.Pow(Screen.height / Screen.dpi, 2));

        // If the screen size is 6.5 inches or larger, consider it a tablet
        return screenDiagonal >= 6.5f;
    }

    public static string ageGroupLabelFromPRBook(PRBook prBook)
    {
        // Book level - add book level 2-3 YOA, 3-5YOA, 4-7YOA, 5-10YOA
        string ageGroup = "Any Age";
        if (prBook.ageFrom == 2)
        {
            ageGroup = "2-4 years";
        }
        else if (prBook.ageFrom == 3)
        {
            ageGroup = "3-6 years";
        }
        else if (prBook.ageFrom == 4)
        {
            ageGroup = "4-8 years";
        }
        else if (prBook.ageFrom == 5)
        {
            ageGroup = "5-12 years";
        }

        return ageGroup;
    }

    public static int defaultAudioRateFromPRBook(PRBook prBook)
    {
        if (prBook == null)
            return 0;

        // Book level - add book level 2-3 YOA, 3-5YOA, 4-7YOA, 5-10YOA
        int rate = -30;
        if (prBook.ageFrom == 2)
        {
            rate = -20;
        }
        else if (prBook.ageFrom == 3)
        {
            rate = -10;
        }
        else if (prBook.ageFrom == 4)
        {
            rate = 0;
        }
        else if (prBook.ageFrom == 5)
        {
            rate = 10;
        }

        return rate;
    }

    public static string getReadingRate()
    {
        int nSetReadingSpeedByBooksAgeGroup = PlayerPrefs.GetInt("g_bSetReadingSpeedByBooksAgeGroup", 1);
        string rate = PlayerPrefs.GetString("g_Rate", "0"); // -30, -20, -10, 0, 10
        if (nSetReadingSpeedByBooksAgeGroup == 1)
            return "" + defaultAudioRateFromPRBook(g_prbook);
        else
            return rate;
    }

    public static string Prefs_BookUrl_To_Page_Key(string book_url)
    {
        return book_url + "_" + "page";
    }

    public static string Prefs_BookUrl_To_BookDone_Key(string book_url)
    {
        return book_url + "_" + "done";
    }

    public static void Prefs_Set_Book_Page(string book_url, int page)
    {
        PlayerPrefs.SetInt(Prefs_BookUrl_To_Page_Key(book_url), page);
    }

    public static int Prefs_Get_Book_Page(string book_url)
    {
        int page = PlayerPrefs.GetInt(Prefs_BookUrl_To_Page_Key(book_url), 0);
        return page;
    }

    public static void Prefs_Set_Book_Done(string book_url, int done)
    {
        PlayerPrefs.SetInt(Prefs_BookUrl_To_BookDone_Key(book_url), done);
    }

    public static int Prefs_Get_Book_Done(string book_url)
    {
        int done = PlayerPrefs.GetInt(Prefs_BookUrl_To_BookDone_Key(book_url), 0);
        return done;
    }

    public static void GotoPrBook(PRBook prBook)
    {
        g_scriptName = prBook.bookFullUrl;
        g_prbook = prBook;
        // Opening a book cancels any pending startup catalog-load navigation so
        // a late CSV download can't yank the reader back to the Library.
        if (Instance != null)
            Instance.CancelPendingNavigation();
        // Tablet/phone branches are currently identical; kept as a branch in
        // case we re-introduce a form-factor-specific Story scene.
        if (IsTablet())
            Navigation.GoToStory();
        else
            Navigation.GoToStory();
    }

    public static void GotoBook(string name)
    {
        if (g_listPRBooks == null)
            return;
        PRBook prBook = g_listPRBooks.Find(s => s.bookName == name);
        if (prBook != null)
        {
            g_scriptName = prBook.bookFullUrl;
            g_prbook = prBook;
            if (IsTablet())
                Navigation.GoToStory();
            else
                Navigation.GoToStory();
        }
    }

    public static void GotoLibrary(string libraryFilter)
    {
        g_libraryFilter = libraryFilter;
        Navigation.GoToLibrary();
    }

    public static List<PRBook> ParseCSV(string csv)
    {
        List<PRBook> parsedPRBooks = new List<PRBook>();
        StringReader reader = new StringReader(csv);
        reader.ReadLine(); // Skip header line

        string line;
        int counter = 0;
        int badRowCount = 0;
        while ((line = reader.ReadLine()) != null)
        {
            if (line.Trim() == "") continue;

            // C2 (Level 1): isolate per-row parse failures. A malformed row (too few columns,
            // non-numeric ageFrom/ageTo, etc.) used to throw and abort the entire catalog load,
            // leaving g_listPRBooks null and the user staring at an empty library. Now bad rows
            // are logged and skipped; the remaining good rows still load.
            // Note: counter (book.number) only advances on successful parse, so numbering stays
            // contiguous regardless of how many rows were skipped.
            try
            {
                string[] values = line.Split(',');

                PRBook book = new PRBook
                {
                    bookName = values[0].Trim(),
                    bookAuthor = values[1].Trim(),
                    bookImageUrl = values[2].Trim(),
                    bookUrl = values[3].Trim(),
                    ageFrom = int.Parse(values[4].Trim()),
                    ageTo = int.Parse(values[5].Trim()),
                    genre = values[6].Trim(),
                    notesForParents = values[7].Trim(),
                    id = values[8].Trim(),
                    bookStoreUrlPrinted = values.Length > 9 ? values[9].Trim() : "",
                    bookStoreUrlKindle = values.Length > 10 ? values[10].Trim() : "",
                    number = counter++,
                    currentPage = Prefs_Get_Book_Page(values[3].Trim()),
                    book_done = Prefs_Get_Book_Done(values[3].Trim())
                };
                book.bookFullUrl = book.bookUrl;
                if (book.bookFullUrl.StartsWith("http") == false)
                {
                    book.bookFullUrl = baseURL + book.bookFullUrl;
                }

                parsedPRBooks.Add(book);
                //Debug.Log("Added book: " + book.bookName + "");
            }
            catch (Exception ex)
            {
                badRowCount++;
                Debug.LogWarning($"ParseCSV: skipping malformed row #{counter + badRowCount} ({ex.GetType().Name}: {ex.Message}). Row content: \"{line}\"");
            }
        }

        if (badRowCount > 0)
        {
            Debug.LogWarning($"ParseCSV: loaded {parsedPRBooks.Count} books, skipped {badRowCount} malformed row(s).");
        }

        return parsedPRBooks;
    }

    // Dispatch a downloaded catalog to the right parser by URL extension: stories.json
    // → ParseJSON, anything else (stories.csv) → ParseCSV. Kept as the single dispatch
    // point so the prefab-URL rollback to stories.csv needs no code change.
    public static List<PRBook> ParseCatalog(string url, string content)
    {
        if (url != null && url.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            return ParseJSON(content);
        return ParseCSV(content);
    }

    // Highest catalog schema_version this build knows how to map. A newer catalog is
    // logged (not rejected) and parsed with the current field mapping — unknown fields
    // are ignored everywhere, which is what keeps the NEXT schema change painless.
    private const int KnownSchemaVersion = 1;

    // Parse the generated stories.json catalog into the same List<PRBook> ParseCSV
    // produces, so the library UI, filters and PlayerPrefs progress keys need zero
    // changes. Uses the vendored SimpleJSON (already used by AudioAndTextPlayer) rather
    // than JsonUtility, which can't handle a top-level object wrapping an array of
    // objects with optional fields. ParseCSV stays the rollback path and is untouched.
    public static List<PRBook> ParseJSON(string json)
    {
        List<PRBook> parsedPRBooks = new List<PRBook>();

        JSONNode root = JSON.Parse(json);
        if (root == null || !root.IsObject)
        {
            Debug.LogWarning("ParseJSON: catalog JSON did not parse to an object; returning empty catalog.");
            return parsedPRBooks;
        }

        // Tolerate (don't reject) a catalog newer than this build understands.
        int schemaVersion = root["schema_version"].AsInt;
        if (schemaVersion > KnownSchemaVersion)
        {
            Debug.LogWarning($"ParseJSON: catalog schema_version {schemaVersion} is newer than known " +
                             $"version {KnownSchemaVersion}; parsing with current mapping and ignoring unknown fields.");
        }

        JSONNode booksNode = root["books"];
        int counter = 0;
        int badBookCount = 0;
        for (int i = 0; i < booksNode.Count; i++)
        {
            JSONNode b = booksNode[i];

            // Per-book isolation, mirroring ParseCSV's C2 row isolation: one malformed
            // book entry is logged and skipped, the rest of the catalog still loads.
            // counter (book.number) only advances on a successful parse, so numbering
            // stays contiguous regardless of how many entries were skipped.
            try
            {
                // bookUrl = the "script" string VERBATIM. It keys PlayerPrefs progress
                // ({bookUrl}_page / {bookUrl}_done); any drift silently resets a child's
                // progress for that book — the critical invariant of this migration.
                string scriptUrl = b["script"].Value;

                PRBook book = new PRBook
                {
                    bookName = b["name"].Value,
                    bookAuthor = b["author"].Value,
                    bookImageUrl = b["cover"].Value,
                    bookUrl = scriptUrl,
                    ageFrom = b["age_from"].AsInt,
                    ageTo = b["age_to"].AsInt,
                    // genres array → legacy " : " joined string, so Filter.Conforms and
                    // the library UI keep working unchanged.
                    genre = JoinGenres(b["genres"]),
                    notesForParents = b["notes_for_parents"].Value,
                    id = b["id"].Value,
                    // Missing optional fields resolve to "" via SimpleJSON's lazy node,
                    // matching the CSV path's defaults.
                    bookStoreUrlPrinted = b["store_url_printed"].Value,
                    bookStoreUrlKindle = b["store_url_kindle"].Value,
                    // Learn-to-read ladder fields. Missing → 0 / "" (SimpleJSON defaults),
                    // matching the CSV path where these columns don't exist.
                    level = b["level"].AsInt,
                    phonicsFocus = b["phonics_focus"].Value,
                    // Non-empty action = a navigation tile (Nav address), not a book.
                    // Missing key → "" (SimpleJSON default) → normal book.
                    action = b["action"].Value,
                    number = counter++,
                    currentPage = Prefs_Get_Book_Page(scriptUrl),
                    book_done = Prefs_Get_Book_Done(scriptUrl)
                    // Unknown / not-yet-consumed fields (content_rev, read_to_me)
                    // are parsed-and-ignored for now.
                };
                book.bookFullUrl = book.bookUrl;
                if (book.bookFullUrl.StartsWith("http") == false)
                {
                    book.bookFullUrl = baseURL + book.bookFullUrl;
                }

                parsedPRBooks.Add(book);
            }
            catch (Exception ex)
            {
                badBookCount++;
                Debug.LogWarning($"ParseJSON: skipping malformed book entry #{counter + badBookCount} " +
                                 $"({ex.GetType().Name}: {ex.Message}).");
            }
        }

        if (badBookCount > 0)
        {
            Debug.LogWarning($"ParseJSON: loaded {parsedPRBooks.Count} books, skipped {badBookCount} malformed entry(ies).");
        }

        return parsedPRBooks;
    }

    // Join a JSON genres array into the legacy " : " separated string PRBook.genre holds.
    // A missing/non-array node (optional field absent) yields "", matching the CSV default.
    private static string JoinGenres(JSONNode genresNode)
    {
        if (genresNode == null || !genresNode.IsArray)
            return "";

        List<string> genres = new List<string>();
        foreach (JSONNode g in genresNode.Children)
            genres.Add(g.Value);
        return string.Join(" : ", genres);
    }

    public void StartDownloadCSV(string url, Action<string> onComplete)
    {
        StartCoroutine(DownloadCSV(url, onComplete));
    }

    private IEnumerator DownloadCSV(string url, Action<string> onComplete)
    {
        Debug.Log("Downloading CSV from: " + url);
        IsDownloading = true;
        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.timeout = 20;  // CSV is startup-critical; fail fast so the
                                   // retry button surfaces instead of hanging.
            yield return request.SendWebRequest();
            // Clear the downloading flag before configuring the button so the
            // shared ConfigureLoadingButton() resolves to the settled state
            // (catalog ready → "Enter Library", or failed → retry).
            IsDownloading = false;
            if (request.result == UnityWebRequest.Result.ConnectionError || request.result == UnityWebRequest.Result.ProtocolError)
            {
                Debug.Log(request.error);
                ConfigureLoadingButton();  // catalog still null → shows retry
            }
            else
            {
                onComplete(request.downloadHandler.text);
                ConfigureLoadingButton();  // catalog now loaded → "Enter Library"
            }
        }
    }

    // Helper method to set button text, supporting both legacy Text and TextMeshProUGUI
    private void SetButtonText(string newText)
    {
        Debug.Log("Setting button text to: " + newText);
        if (buttonLoadingRetryContinue != null)
        {
            // Check if the button has a Text component (legacy UI)
            Text uiText = buttonLoadingRetryContinue.GetComponentInChildren<Text>();
            if (uiText != null)
            {
                uiText.text = newText;
                return;
            }

            // Check if the button has a TextMeshProUGUI component
            TextMeshProUGUI tmpText = buttonLoadingRetryContinue.GetComponentInChildren<TextMeshProUGUI>();
            if (tmpText != null)
            {
                tmpText.text = newText;
                return;
            }

            Debug.LogWarning("No Text or TextMeshProUGUI component found on the button.");
        }
    }
}
