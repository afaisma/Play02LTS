using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.IO;
using System.Collections;
using UnityEngine.Networking;
using UnityEngine.UI;
using TMPro; // Include TextMeshPro

public class Globals : MonoBehaviour
{
    public static string CSVURL = "http://d5wtw8f0w3ire.cloudfront.net/uploads/stories_02/stories.csv";
    public static string baseURL;
    public string csvUrl = "http://d5wtw8f0w3ire.cloudfront.net/uploads/stories_02/stories.csv";
    public string convinienceLocal = "http://localhost:8080/api/files/download/stories/stories.csv";

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

    // Retry button and message
    [SerializeField] private Button buttonLoadingRetryContinue; // Assign through Inspector, may be null

    // Game statistics variables
    private float totalMinutesInGame = 0f;
    private int numberOfRuns = 0;
    private int pagesRead = 0;
    private int booksRead = 0;
    private float gameStartTime;
    private int daysSinceFirstRun = 0;
    private Coroutine waitAndNavigateCoroutine; 
    
    public static Globals Instance
    {
        get
        {
            if (instance == null)
            {
                GameObject go = new GameObject("Globals");
                instance = go.AddComponent<Globals>();
                DontDestroyOnLoad(go);
            }
            return instance;
        }
    }

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
            // Reclaim engine-side asset memory at scene boundaries.
            // Background: cacheImages (PRUtils) and CacheAudioAndTimingsStructs
            // (AudioAndTextPlayer) are private-static OrderedDictionaries that
            // survive scene unload. When the user leaves _Library / _Story, the
            // UI components referencing previously-cached Sprites/AudioClips are
            // destroyed, but Unity does NOT auto-call UnloadUnusedAssets on
            // SceneManager.LoadScene (post-5.x behaviour). Without this hook,
            // every orphaned Texture2D / AudioClip stays in memory until the
            // process exits — a small but monotonic leak over a long session.
            //
            // Items still referenced by either cache are kept (UnloadUnusedAssets
            // is reference-aware), so a Library → Story → Library round-trip
            // doesn't force a reload of warm covers. The subscription happens
            // exactly once because Globals' singleton guard above destroys any
            // duplicate Globals before its Awake reaches this line.
            //
            // Temporary Debug.Log so first on-device deploys surface the hitch
            // length; drop the timing once we know the real number on the
            // slowest target device.
            SceneManager.activeSceneChanged += (oldScene, newScene) =>
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                Resources.UnloadUnusedAssets();
                sw.Stop();
                Debug.Log($"UnloadUnusedAssets after {oldScene.name} → {newScene.name}: {sw.ElapsedMilliseconds} ms");
            };
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        gameStartTime = Time.time; // Initialize game start time for statistics
        CSVURL = csvUrl;
        baseURL = PRUtils.RemoveFileNameFromUrl(csvUrl);
        
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
            List<PRBook> prbooks = ParseCSV(csv);
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
        LoadTargetScene();
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
            SceneManager.LoadScene(targetScene);
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
            SceneManager.LoadScene("_Library");
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
        if (IsTablet())
        {
            SceneManager.LoadScene("_Story");
        }
        else
        {
            SceneManager.LoadScene("_Story");
        }
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
            {
                SceneManager.LoadScene("_Story");
            }
            else
            {
                SceneManager.LoadScene("_Story");
            }
        }
    }

    public static void GotoLibrary(string libraryFilter)
    {
        g_libraryFilter = libraryFilter;
        SceneManager.LoadScene("_Library");
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
            if (request.result == UnityWebRequest.Result.ConnectionError || request.result == UnityWebRequest.Result.ProtocolError)
            {
                Debug.Log(request.error);

                // On error, enable the retry button and change the text
                if (buttonLoadingRetryContinue != null)
                {
                    SetButtonText("Connect to the Internet and Retry");  // Call SetButtonText to update message
                    buttonLoadingRetryContinue.interactable = true;  // Enable interaction
                    buttonLoadingRetryContinue.onClick.RemoveAllListeners();  // Clear previous listeners
                    buttonLoadingRetryContinue.onClick.AddListener(RetryDownload);  // Add listener for retry
                }
            }
            else
            {
                onComplete(request.downloadHandler.text);

                // On success, change the button text to "Continue" and enable interaction
                if (buttonLoadingRetryContinue != null)
                {
                    SetButtonText("Continue");  // Call SetButtonText to update message
                    buttonLoadingRetryContinue.interactable = true;  // Enable interaction
                    buttonLoadingRetryContinue.onClick.RemoveAllListeners();  // Clear previous listeners
                    buttonLoadingRetryContinue.onClick.AddListener(LoadTargetScene);  // Add listener to load the target scene
                }
            }
        }
        IsDownloading = false;
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
