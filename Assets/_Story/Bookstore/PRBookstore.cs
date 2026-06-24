using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class PRBookstore : MonoBehaviour
{
    //public string csvUrl;
    public static List<PRBook> prbooks;
    [FormerlySerializedAs("booksScrollView")] [SerializeField] BookstoreScrollView bookstoreScrollView;
    public Image imgBackground;
    public MovingRatingsOptionsPanel movingRatingsOptionsPanel;
    
    Toggle toggleFairytales;
    Toggle toggleScience;
    Toggle toggleSounds;

    public FilterContainer filterContainer;
    public TextMeshProUGUI txtTitle;

    public static List<(string SceneName, string Settings)> bookCategories = new List<(string SceneName, string Settings)>()
    {
        ("_Bookstore", "everything")
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
        Debug.Log("PRBookstore Start, currentCategory: " + currentCategory);
        
        LoadBooks(this);
        GotoCategory();
        bookstoreScrollView.ResetScrollPosition();
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
        prbooks = Globals.g_listPRBooks.FindAll(book => 
            !string.IsNullOrEmpty(book.bookStoreUrlPrinted) || !string.IsNullOrEmpty(book.bookStoreUrlKindle));
        bookstoreScrollView.AddBooks(prbooks);
        
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
        
        if (Globals.g_bookstoreFilter != "everything")
        {
            SetFilter(Globals.g_bookstoreFilter);
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
    
    // Public wrapper methods preserved so scene-side button onClick
    // wirings keep working. Bodies delegated to Navigation (see
    // Story/Navigation.cs).
    public void Settings() => Navigation.GoToSettings();
    public void Map()      => Navigation.GoToMap();
    public void Parents()  => Navigation.GoToParents();

    public void SetFilter(string filter)
    {
        return;
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
    
    public void Home()
    {
        Globals.g_scriptName = "";
        Navigation.GoToHome();
    }


}


