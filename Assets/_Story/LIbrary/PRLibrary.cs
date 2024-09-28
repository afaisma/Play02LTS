using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class PRLibrary : MonoBehaviour
{
    //public string csvUrl;
    public static List<PRBook> prbooks;
    [SerializeField] BooksScrollView booksScrollView;
    public Image imgBackground;
    
    Toggle toggleFairytales;
    Toggle toggleScience;
    Toggle toggleSounds;

    public FilterContainer filterContainer;
    public TextMeshProUGUI txtTitle;

    public static List<string> bookCategories = new List<string>()
    {
        "rhymebooks",
        "family",
        "adventure",
        "science",
        "fairytales",
        "special education",
        "classic",
        "art",
        "sound & speech",
        "math",
        "nature",
        "manners",
        ""
    };
    private int currentCategory = -1;  // Renamed from currentIndex

    
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
        LoadBooks(this);
        booksScrollView.ResetScrollPosition();
    }

    private void OnDestroy()
    {
        Globals.g_libraryFilter = "";
    }

    public void LoadBooks(MonoBehaviour mb)
    {
        prbooks = Globals.g_listPRBooks;
        booksScrollView.AddBooks(prbooks);
        Globals.g_openedStoriesCount = PlayerPrefs.GetInt("g_openedStoriesCount", 0);
        Globals.g_askedToBeRated  = PlayerPrefs.GetInt("g_askedToBeRated", 0);
        if (Globals.g_openedStoriesCount > 10 && Globals.g_askedToBeRated == 0)
        {
            PRUtils.RateUs();
            PlayerPrefs.SetInt("g_askedToBeRated", 1);
        }
        
        if (Globals.g_libraryFilter != "")
        {
            SetFilter(Globals.g_libraryFilter);
        }
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
    
    public void Settings()
    {
        SceneManager.LoadScene("_Settings");
    }

    public void Map()
    {
        SceneManager.LoadScene("_Map");
    }

    public void Parents()
    {
        SceneManager.LoadScene("_Parents");
    }

    public void SetFilter(string filter)
    {
        filterContainer?._SetFilter(filter);
        txtTitle.text = //"ReadingBuddy: " + 
                        PRUtils.CapitalizeFirstLetter(filter);
        if (filter == "")
            txtTitle.text = "All Books";

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
        else
        {
            imgBackground.sprite = Resources.Load<Sprite>("Library/Library_background");
            txtTitle.color = new Color(0.99f, 0.99f, 0.99f);
        }
    } 
    
    public void NextCategory()
    {
        currentCategory++;  // Move to the next category
        if (currentCategory >= bookCategories.Count)
            currentCategory = 0;  // Loop back to the first category

        SetFilter(bookCategories[currentCategory]); 
    }

    // Method to get the previous category with looping
    public void PreviousCategory()
    {
        currentCategory--;  // Move to the previous category
        if (currentCategory < 0)
            currentCategory = bookCategories.Count - 1;  // Loop back to the last category

        SetFilter(bookCategories[currentCategory]); 
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
    public int number;
    public int book_done;
    public int currentPage;

    public BookViewItem bookViewItem;
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
