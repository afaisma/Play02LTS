using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class PRLibrary : MonoBehaviour
{
    //public string csvUrl;
    public static List<PRBook> prbooks;
    [SerializeField] BooksScrollView booksScrollView;
    public Image imgBackground;
    public MovingRatingsOptionsPanel movingRatingsOptionsPanel;
    
    Toggle toggleFairytales;
    Toggle toggleScience;
    Toggle toggleSounds;

    public FilterContainer filterContainer;
    public TextMeshProUGUI txtTitle;

    public static List<(string SceneName, string Settings)> bookCategories = new List<(string SceneName, string Settings)>()
    {
        ("_Library", "everything"),
        ("_Library", "new"),
        ("_Library", "rhymebooks"),
        ("_Library", "family"),
        ("_Library", "adventure"),
        ("_Library", "science"),
        ("_Library", "fairytales"), 
        ("_Library", "special education"),
        ("_Library", "classic"),
        ("_Library", "art"),
        ("_Library", "sound & speech"),
        ("_Library", "math"),
        ("_Library", "nature"),
        ("_Library", "manners"),
        ("_Library", "learn to read")
        // ,
        // ("_Map", "")
    };
    private static int currentCategory = 0;

    
    private void Start()
    {
        // if (Globals.IsTablet())
        // {
        //     Screen.orientation = LandscapeLeft;
        // }
        // else
        // {
        //     Screen.orientation = Portrait;
        // }
        Debug.Log("PRLibrary Start, currentCategory: " + currentCategory);

        // Hide toolbar buttons that moved/were removed (reversible via AppConfig). Search → removed;
        // Settings + Parents → consolidated into Home's "For grown-ups" door.
        HideToolbarButtons();

        // The toolbar's home button is scene-wired (ButtonHome.onClick -> Home()). Grab it by name so
        // Home() can hold it pressed through the now-async _Home load, and give it the same instant
        // pressed state the code-built hub cards have.
        _homeButton = FindByName("ButtonHome");
        if (_homeButton != null)
        {
            TapFeedback.AddPressFeedback(_homeButton);
            // Top-left is where the centred txtTitle renders, so the button sat on the shelf name.
            // Move it into the bottom toolbar, centred between the category arrows, and give it the
            // shared house look so "go home" reads the same here as in the reader and the grown-up
            // screens. Both are runtime-only: no scene surgery, onClick and size untouched.
            MoveHomeButtonIntoToolbar(_homeButton);
            HomeButton.Apply(_homeButton.GetComponent<Button>());
            var homeLabel = _homeButton.transform.Find("Label");
            if (homeLabel != null) homeLabel.gameObject.SetActive(false); // the house glyph replaces the "⌂ Home" caption
        }

        LoadBooks(this);
        // The incoming Globals.g_libraryFilter is authoritative: LoadBooksWithRetry's
        // SetFilter(g_libraryFilter) applies it directly (including "everything", which
        // resets to the full "All Books" catalog). The stale static currentCategory is
        // no longer consulted on entry — only swipe-between-rooms updates it.
        booksScrollView.ResetScrollPosition();
    }

    public void LoadBooks(MonoBehaviour mb)
    {
        // Start a coroutine to attempt loading books with retries
        StartCoroutine(LoadBooksWithRetry());
    }

private System.Collections.IEnumerator LoadBooksWithRetry()
    {
        int retryCount = 0;
        int maxRetries = 3;
        float waitTime = 2f;  // Time to wait between checks, in seconds

        // Retry up to maxRetries times
        while (Globals.g_listPRBooks == null && retryCount < maxRetries)
        {
            Debug.Log($"Globals.g_listPRBooks is null. Retrying in {waitTime} seconds... (Attempt {retryCount + 1}/{maxRetries})");
            yield return new WaitForSeconds(waitTime);  // Wait for 2 seconds
            retryCount++;
        }

        // Check if we still don't have the book list after all retries
        if (Globals.g_listPRBooks == null)
        {
            Debug.LogWarning("Failed to load books after multiple attempts.");
            // Handle the failure to load books (e.g., show a UI message)
            yield break;  // Exit the coroutine if books could not be loaded
        }

        // If Globals.g_listPRBooks is not null, proceed to load the books
        prbooks = Globals.g_listPRBooks;
        booksScrollView.AddBooks(prbooks);
        
        Globals.g_openedStoriesCount = PlayerPrefs.GetInt("g_openedStoriesCount", 0);
        int askedToBeRated  = PlayerPrefs.GetInt("g_askedToBeRated", 0);
        int wasRated = PlayerPrefs.GetInt("g_wasRated", 0);
        Debug.Log("g_openedStoriesCount: " + Globals.g_openedStoriesCount);
        Debug.Log("askedToBeRated: " + askedToBeRated);
        Debug.Log("wasRated: " + wasRated);
        if ((wasRated == 0) && (Globals.g_openedStoriesCount > askedToBeRated*15 + 10) && (askedToBeRated <= 3))
        {
            //PRUtils.RateUs();
            movingRatingsOptionsPanel.MoveIn();
            PlayerPrefs.SetInt("g_askedToBeRated", askedToBeRated + 1);
        }
        
        SetFilter(Globals.g_libraryFilter);
    }

    public static List<PRBook> FilterByName(string name)
    {
        return prbooks.FindAll(s => s.bookName.ToLower().Contains(name.ToLower()));
    }

    public static List<PRBook> FilterById(string id)
    {
        return prbooks.FindAll(s => s.id == id);
    }

    public static List<PRBook> FilterByAge(int age)
    {
        return prbooks.FindAll(s => s.ageFrom <= age && s.ageTo >= age);
    }

    public static List<PRBook> FilterByGenre(string genre)
    {
        return prbooks.FindAll(s => s.genre.ToLower().Equals(genre.ToLower()));
    }

    public static List<PRBook> FilterByNotesForParents(string notesForParents)
    {
        return prbooks.FindAll(s => s.notesForParents.ToLower().Contains(notesForParents.ToLower()));
    }   
    
    // Public wrapper methods preserved so scene-side button onClick
    // wirings keep working. Bodies delegated to Navigation (see
    // Story/Navigation.cs) which centralizes scene names and removes
    // duplication with PRBookstore / MapManager.
    public void Settings()  => Navigation.GoToSettings();
    public void Map()       => Navigation.GoToMap();
    public void Bookstore() => Navigation.GoToBookstore();
    public void Parents()   => Navigation.GoToParents();

    // Reversibly hide toolbar buttons (search + grown-up icons) per AppConfig. Covers name variants.
    private void HideToolbarButtons()
    {
        var hide = new System.Collections.Generic.HashSet<string>();
        if (!AppConfig.ShowSearch) hide.Add("btnFilter");
        if (!AppConfig.ShowGrownupToolbarIcons)
        { hide.Add("btnParents"); hide.Add("btnParents1"); hide.Add("btnSettings"); hide.Add("btnSettings1"); }
        if (hide.Count == 0) return;
        foreach (var t in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (hide.Contains(t.name)) t.gameObject.SetActive(false);
    }
    // Scene-wired (ButtonHome). _Home loads async and is not a cheap scene, so acknowledge the tap
    // (hold the press, fade a cover in) before firing the navigation — otherwise the ~2s tablet load
    // reads as a dead tap and the child re-taps.
    private GameObject _homeButton;
    public void Home()
    {
        TapFeedback.TapThenGo(_homeButton != null ? _homeButton.transform : null, Navigation.GoToHome);
    }

    // Toolbar objects are found by name (no new serialized fields, no scene surgery), inactive ones
    // included — the same approach HideToolbarButtons uses.
    private static GameObject FindByName(string name)
    {
        foreach (var t in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (t.name == name) return t.gameObject;
        return null;
    }

    // Reparent the scene's ButtonHome into the bottom Toolbar, between btnPrev and btnNext. The
    // Toolbar is a HorizontalLayoutGroup, so once the button takes part in the layout its position
    // is the layout's business — with the grown-up/search icons hidden the only other live children
    // are the two arrows, which leaves Home in the middle.
    private static void MoveHomeButtonIntoToolbar(GameObject homeButton)
    {
        var toolbar = FindByName("Toolbar");
        if (toolbar == null) return;

        var rt = (RectTransform)homeButton.transform;
        Vector2 size = rt.rect.size;   // read before the reparent; the layout group does not resize it
        rt.SetParent(toolbar.transform, false);

        // The serialized LayoutElement carries ignoreLayout=true (it was a free-floating canvas
        // child); inside the toolbar it has to be laid out like the arrows.
        var le = homeButton.GetComponent<LayoutElement>();
        if (le != null) le.ignoreLayout = false;

        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;

        int prev = IndexOfChild(toolbar.transform, "btnPrev");
        rt.SetSiblingIndex(prev >= 0 ? prev + 1 : 0);
    }

    /// <summary>A "levelN" (N = 1..4) Learn-to-Read rung token. These are addressable shelves that
    /// are deliberately not listed in bookCategories (see FilterContainer.OnFilterChanged).</summary>
    private static bool IsLevelToken(string filter)
    {
        return System.Text.RegularExpressions.Regex.IsMatch(
            filter ?? "", @"^level[1-4]$",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }

    private static int IndexOfChild(Transform parent, string name)
    {
        for (int i = 0; i < parent.childCount; i++)
            if (parent.GetChild(i).name == name) return i;
        return -1;
    }

    public void SetFilter(string filter)
    {
        Debug.Log("SetFilter: " + filter);
        int idx = bookCategories.FindIndex(c => c.Settings == filter);
        if (idx >= 0)
            currentCategory = idx;

        // The shelf the child is NOW on. g_libraryFilter is documented (Globals.NextUnreadBook) as
        // "the shelf the child is actually browsing" and BookViewItem reads it to decide whether a
        // row is on the Learn-to-Read ladder — but only the ENTRY paths wrote it, so arrowing from
        // one category to the next left it naming the room they came from. Set before the rows are
        // rebuilt below, which is what reads it.
        //
        // Only a real shelf token is written, for the same reason currentCategory is guarded above:
        // the (currently hidden) search panel also routes age chips like "2-3 years" through here,
        // and those are a narrowing of the shelf, not a shelf.
        if (idx >= 0 || IsLevelToken(filter))
            Globals.g_libraryFilter = filter;

        filterContainer?._SetFilter(filter);

        // The surface that navigated here may carry its own name for this shelf — a Home door's
        // label ("Stories", "Songs & Sounds"), which is what the child actually tapped. Taken ONCE,
        // and applied at the very END of this method so it wins over the per-filter defaults below;
        // the filter chips inside the Library never see it and keep their derived titles.
        string pendingTitle = Globals.ConsumePendingLibraryTitle();

        txtTitle.text = DisplayName(filter);

        if (filter == "rhymebooks")
        {
            imgBackground.sprite = Resources.Load<Sprite>("Library/rhymebook_background");
            txtTitle.color = new Color(0.3f, 0.99f, 0.1f);
        }
        else if (filter == "family")
        {
            imgBackground.sprite = Resources.Load<Sprite>("Library/family1-background");
            txtTitle.color = new Color(0.54f, 0.77f, 0.5f); 
        }
        else if (filter == "adventure")
        {
            imgBackground.sprite = Resources.Load<Sprite>("Library/adventure_background");
            txtTitle.color = new Color(0.99f, 0.99f, 0.99f);
        }
        else if (filter == "science")
        {
            imgBackground.sprite = Resources.Load<Sprite>("Library/science_background");
            txtTitle.color = new Color(0.8f, 0.8f, 0.8f);
        }
        else if (filter == "fairytales")
        {
            imgBackground.sprite = Resources.Load<Sprite>("Library/fairystories_background");
            txtTitle.color = new Color(0.99f, 0.99f, 0.99f);
        }
        else if (filter == "special education")
        {
            imgBackground.sprite =
                Resources.Load<Sprite>("Library/specialeducation_background");
            txtTitle.color = new Color(0.99f, 0.99f, 0.99f);
        }
        else if (filter == "classic")
        {
            imgBackground.sprite = Resources.Load<Sprite>("Library/classics_background");
            txtTitle.color = new Color(0.8f, 0.4f, 0.8f);
        }
        else if (filter == "art")
        {
            imgBackground.sprite = Resources.Load<Sprite>("Library/art_background");
            txtTitle.color = new Color(0.99f, 0.99f, 0.99f);
        }
        else if (filter == "sound & speech")
        {
            imgBackground.sprite =
                Resources.Load<Sprite>("Library/sound_and_speech_background");
            txtTitle.color = new Color(0.6f, 0.6f, 0.6f);
        }
        else if (filter == "math")
        {
            imgBackground.sprite = Resources.Load<Sprite>("Library/math2_background");
            txtTitle.color = new Color(0.99f, 0.99f, 0.99f);
        }
        else if (filter == "nature")
        {
            imgBackground.sprite = Resources.Load<Sprite>("Library/nature_background");
            txtTitle.color = new Color(0.99f, 0.99f, 0.99f);
        }
        else if (filter == "manners")
        {
            imgBackground.sprite = Resources.Load<Sprite>("Library/manners_background");
            txtTitle.color = new Color(0.99f, 0.99f, 0.99f);
        }
        else if (filter == "learn to read")
        {
            // Use the dedicated background if the art has shipped, else fall back to the
            // default Library background (asset can come later; do not block on art).
            Sprite bg = Resources.Load<Sprite>("Library/learn_to_read_background");
            imgBackground.sprite = bg != null ? bg : Resources.Load<Sprite>("Library/Library_background");
            txtTitle.color = new Color(0.99f, 0.99f, 0.99f);
        }
        else
        {
            Debug.Log("filter unknown: " + filter);
            imgBackground.sprite = Resources.Load<Sprite>("Library/Library_background");
            txtTitle.color = new Color(0.99f, 0.99f, 0.99f);
        }

        // Door label wins over every default above (backgrounds and colours stay filter-driven).
        if (!string.IsNullOrEmpty(pendingTitle))
            txtTitle.text = pendingTitle;
    }

    /// <summary>
    /// The name a shelf is known by, per filter token. The Home doors and the in-library category
    /// arrows both land in SetFilter, but only the doors carry a label of their own — the arrows
    /// derive the title from the token, which is how the same room came to be "Fairytales" behind
    /// one and "Fairy Tales" behind the other. This map is the single place a token's display name
    /// is spelled; anything not listed falls back to TitleCase, which is what it always did.
    /// </summary>
    private static readonly Dictionary<string, string> DisplayNames = new Dictionary<string, string>
    {
        { "everything",   "Stories"       },
        { "fairytales",   "Fairy Tales"   },
        { "manners",      "Good Habits"   },
        { "new",          "New Books"     },
        { "learn to read", "Learn to Read" },
    };

    /// <summary>Display name for a filter token: the mapped name, else TitleCase(token).</summary>
    public static string DisplayName(string filter)
    {
        if (string.IsNullOrEmpty(filter)) return string.Empty;
        return DisplayNames.TryGetValue(filter, out string name) ? name : TitleCase(filter);
    }

    /// <summary>
    /// Shelf title derived from a filter token. CapitalizeFirstLetter only touched the first
    /// character, which turned "sound &amp; speech" into "Sound &amp; speech" and "special education"
    /// into "Special education"; every word gets its initial capital here instead. Words that are
    /// not letters ("&amp;") pass through untouched.
    /// </summary>
    public static string TitleCase(string input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;

        var chars = input.ToCharArray();
        bool startOfWord = true;
        for (int i = 0; i < chars.Length; i++)
        {
            if (char.IsWhiteSpace(chars[i])) { startOfWord = true; continue; }
            if (startOfWord) chars[i] = char.ToUpper(chars[i]);
            startOfWord = false;
        }
        return new string(chars);
    }
    
    public void NextCategory()
    {
        currentCategory++;  // Move to the next category
        if (currentCategory >= bookCategories.Count)
            currentCategory = 0;  // Loop back to the first category

        GotoCategory();
    }

    public void PreviousCategory()
    {
        currentCategory--;  // Move to the previous category
        if (currentCategory < 0)
            currentCategory = bookCategories.Count - 1;  // Loop back to the last category

        GotoCategory();
    }

    public void GotoCategory()
    {
        var currentScene = SceneManager.GetActiveScene().name;
        var (sceneName, categorySettings) = bookCategories[currentCategory];

        // Check if we are in a different scene
        if (sceneName != currentScene)
        {
            Navigation.GoToScene(sceneName);  // Load the new scene
        }
        else
        {
            SetFilter(categorySettings);  // If in the same scene, apply the filter
        }
    }
}

[Serializable]
public class PRBook
{
    public string bookName;
    public string bookAuthor;
    public string bookImageUrl;
    public string bookUrl;
    public int ageFrom;
    public int ageTo;
    public string genre;
    public string notesForParents;
    public string bookFullUrl;
    public string id;
    public int level;                 // 0 = not a learn-to-read ladder book (CSV path leaves 0)
    public string phonicsFocus = "";  // "" default; CSV path never sets it, so initialize here
    public string action = "";        // non-empty = navigation tile (Nav address), not a book; CSV path never sets it
    public string contentRev = "";    // catalog content hash; folded into media URLs as ?v= to bust stale caches. CSV path never sets it
    public string added = "";         // catalog publish date, ISO yyyy-MM-dd; "" = unknown. Drives the "new" filter token (Filter.IsNewBook)
    public int number;
    public int book_done;
    public int currentPage;
    public string bookStoreUrlPrinted; 
    public string bookStoreUrlKindle; 
    public System.Collections.Generic.List<string> voices = new() { "tts" };   // e.g. ["human","tts"]; defaults to ["tts"] (every shipped book has TTS). CSV path keeps this default; JSON path overwrites from the "voices" array.
    public bool readToMe = false;     // catalog "read_to_me": book supports the "I read it myself" mode. CSV path leaves false; JSON path sets it.
                                      // No longer gates anything: UnifiedReadingModePicker offers "I read it myself" for every book (2026-08-24).
                                      // Still parsed so the catalog stays unchanged and the flag can return as an opt-OUT if a book must hide the mode.

    public BookViewItem bookViewItem;
    public BookstoreViewItem bookstoreViewItem;
    public void SetAndSaveCurrentPage(int nPage)
    {
        currentPage = nPage;
        Globals.Prefs_Set_Book_Page(bookUrl, currentPage);
    }

    public void SetBookDone(int i)
    {
        Globals.Prefs_Set_Book_Done(bookUrl, i);
    }
}
