using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.IO;
using System.Collections;
using UnityEngine.Networking;

public class Preferences
{
    private static Preferences instance;
    public string g_Rate = "10";
    public int g_bSetReadingSpeedByBooksAgeGroup;

    private Preferences()
    {
        g_Rate = PlayerPrefs.GetString("g_Rate", "0"); // -30, -20, -10, 0, 10
        g_bSetReadingSpeedByBooksAgeGroup = PlayerPrefs.GetInt("g_bSetReadingSpeedByBooksAgeGroup", 1);
    }

    public static Preferences GetInstance()
    {
        if (instance == null)
        {
            instance = new Preferences();
        }
        return instance;
    }
}

public class Globals : MonoBehaviour
{
    public static string CSVURL = "http://d5wtw8f0w3ire.cloudfront.net/uploads/stories/stories.csv";
    public static string baseURL;
    public string csvUrl = "http://d5wtw8f0w3ire.cloudfront.net/uploads/stories/stories.csv";
    public string convinienceLocal = "http://localhost:8080/api/files/download/stories/stories.csv";
    public string convinienceS3 = "http://d5wtw8f0w3ire.cloudfront.net/uploads/stories/stories.csv";
    public string convinienceEC2 = "http://35.90.126.120:8080/api/files/download/stories/stories.csv";

    public static List<PRBook> g_listPRBooks;

    public static string g_scriptName;
    public static int g_openedStoriesCount;
    public Slider sliderRate;
    public Toggle toggleSetReadingSpeedByBooksAgeGroup;
    public TMP_Text txtReadingSpeedDescr;
    public TMP_Text versionText;
    public static PRBook g_prbook;
    public static int g_askedToBeRated;
    public static string g_libraryFilter = "";

    private static Globals instance;
    public bool IsDownloading { get; private set; } = false;

    [SerializeField] private string targetScene = "_Library";
    [SerializeField] private float minTimeInScene = 2f;

    // Game statistics variables
    private float totalMinutesInGame = 0f;
    private int numberOfRuns = 0;
    private int pagesRead = 0;
    private int booksRead = 0;
    private float gameStartTime;
    private int daysSinceFirstRun = 0;

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
        
        // Initialize statistics
        InitializeGameStatistics();

        if (versionText != null)
            versionText.text = "Version: " + Application.version;

        if (sliderRate != null)
        {
            switch (Preferences.GetInstance().g_Rate)
            {
                case "-30":
                    sliderRate.value = 0;
                    break;
                case "-20":
                    sliderRate.value = 1;
                    break;
                case "-10":
                    sliderRate.value = 2;
                    break;
                case "0":
                    sliderRate.value = 3;
                    break;
                case "10":
                    sliderRate.value = 4;
                    break;
            }
        }

        if (toggleSetReadingSpeedByBooksAgeGroup != null)
        {
            toggleSetReadingSpeedByBooksAgeGroup.isOn =
                Preferences.GetInstance().g_bSetReadingSpeedByBooksAgeGroup == 1;
            if (toggleSetReadingSpeedByBooksAgeGroup.isOn)
                sliderRate.interactable = false;
            else
                sliderRate.interactable = true;
        }

        DisplayReadingSpeedDescr();
        PreLoadBooks();
    }

    void OnApplicationQuit()
    {
        UpdateGameStatistics(); // Update statistics before the application quits
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

        // Increment the number of runs
        numberOfRuns++;
        PlayerPrefs.SetInt("NumberOfRuns", numberOfRuns);
    }

    void UpdateGameStatistics()
    {
        // Calculate the total time in the game for this run
        float sessionMinutes = (Time.time - gameStartTime) / 60f;
        totalMinutesInGame += sessionMinutes;
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
        if (g_listPRBooks != null)
        {
            if (!string.IsNullOrEmpty(targetScene))
                StartCoroutine(WaitAndNavigate(targetScene, minTimeInScene));
            return;
        }

        float startTime = Time.time;

        StartDownloadCSV(csvUrl, (csv) =>
        {
            List<PRBook> prbooks = ParseCSV(csv);
            g_listPRBooks = prbooks;

            if (!string.IsNullOrEmpty(targetScene))
            {
                float elapsedTime = Time.time - startTime;
                float delay = Mathf.Max(0, minTimeInScene - elapsedTime); // Calculate remaining delay
                StartCoroutine(WaitAndNavigate(targetScene, delay));
            }
        });
    }

    private IEnumerator WaitAndNavigate(string targetScene, float delay)
    {
        yield return new WaitForSeconds(delay);
        //if (SceneManager.GetActiveScene().name != targetScene)
            SceneManager.LoadScene(targetScene);
    }

    public void DisplayReadingSpeedDescr()
    {
        if (txtReadingSpeedDescr != null)
        {
            string descr = "";
            switch (Preferences.GetInstance().g_Rate)
            {
                case "-30":
                    descr = "Beginner";
                    break;
                case "-20":
                    descr = "Intermediate";
                    break;
                case "-10":
                    descr = "Proficient";
                    break;
                case "0":
                    descr = "Advanced";
                    break;
                case "10":
                    descr = "Expert";
                    break;
            }

            if (!toggleSetReadingSpeedByBooksAgeGroup.isOn)
                txtReadingSpeedDescr.text = descr;
            else
                txtReadingSpeedDescr.text = "(Set by book age group)";
        }
    }

    public void HandleRateValueChange(Slider slider)
    {
        switch (slider.value)
        {
            case 0:
                Preferences.GetInstance().g_Rate = "-30";
                break;
            case 1:
                Preferences.GetInstance().g_Rate = "-20";
                break;
            case 2:
                Preferences.GetInstance().g_Rate = "-10";
                break;
            case 3:
                Preferences.GetInstance().g_Rate = "0";
                break;
            case 4:
                Preferences.GetInstance().g_Rate = "10";
                break;
            default:
                Preferences.GetInstance().g_Rate = "0";
                break;
        }

        PlayerPrefs.SetString("g_Rate", Preferences.GetInstance().g_Rate);

        DisplayReadingSpeedDescr();
    }

    public void HandleSetReadingSpeedByBooksAgeGroupChange(Toggle toggle)
    {
        if (toggle.isOn)
        {
            Preferences.GetInstance().g_bSetReadingSpeedByBooksAgeGroup = 1;
            sliderRate.interactable = false;
        }
        else
        {
            Preferences.GetInstance().g_bSetReadingSpeedByBooksAgeGroup = 0;
            sliderRate.interactable = true;
        }

        DisplayReadingSpeedDescr();
        PlayerPrefs.SetInt("g_bSetReadingSpeedByBooksAgeGroup",
            Preferences.GetInstance().g_bSetReadingSpeedByBooksAgeGroup);
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
        if (Preferences.GetInstance().g_bSetReadingSpeedByBooksAgeGroup == 1)
            return "" + defaultAudioRateFromPRBook(g_prbook);
        else
            return Preferences.GetInstance().g_Rate;
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

    public static void GotoLibrary(String libraryFilter)
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
        while ((line = reader.ReadLine()) != null)
        {
            if (line.Trim() == "") continue;

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

        return parsedPRBooks;
    }

    public void StartDownloadCSV(string url, Action<string> onComplete)
    {
        StartCoroutine(DownloadCSV(url, onComplete));
    }

    private IEnumerator DownloadCSV(string url, Action<string> onComplete)
    {
        IsDownloading = true;
        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            yield return request.SendWebRequest();
            if (request.result == UnityWebRequest.Result.ConnectionError || request.result == UnityWebRequest.Result.ProtocolError)
            {
                Debug.LogError(request.error);
            }
            else
            {
                onComplete(request.downloadHandler.text);
            }
        }
        IsDownloading = false;
    }
}
