using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using QFSW.QC;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using UnityEngine.UI;
using static UnityEngine.ScreenOrientation;

public class PRLibrary : MonoBehaviour
{
    public string csvUrl;
    public static List<PRBook> prbooks;
    [SerializeField] BooksScrollView booksScrollView;
    public static string baseURL;

    public string convinienceLocal = "http://localhost:8080/api/files/download/stories/stories.csv";
    public string convinienceS3 = "http://d5wtw8f0w3ire.cloudfront.net/uploads/stories/stories.csv";
    public string convinienceEC2 = "http://35.90.126.120:8080/api/files/download/stories/stories.csv";
    
    Toggle toggleFairytales;
    Toggle toggleScience;
    Toggle toggleSounds;
    
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
        LoadBooks();
    }

    [Command()]
    public void LoadBooks()
    {
        baseURL = PRUtils.RemoveFileNameFromUrl(csvUrl);
        if (prbooks == null)
        {
            StartCoroutine(DownloadCSV(csvUrl, (csv) =>
            {
                prbooks = ParseCSV(csv);
                booksScrollView.AddBooks(prbooks);
            }));
        }
        else
        {
            booksScrollView.AddBooks(prbooks);
        }
    }

    private IEnumerator DownloadCSV(string url, Action<string> onComplete)
    {
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
    }

    private List<PRBook> ParseCSV(string csv)
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
                number = counter++
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

    public void Parents()
    {
        SceneManager.LoadScene("_Parents");
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
}
