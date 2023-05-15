using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Serialization;

public class PRLibrary : MonoBehaviour
{
    public string csvUrl;
    public List<PRBook> prbooks;
    [SerializeField] BooksScrollView booksScrollView;
    public static string baseURL;

    public string convinienceLocal = "http://localhost:8080/api/files/download/stories/stories.csv";
    public string convinienceS3 = "http://d5wtw8f0w3ire.cloudfront.net/uploads/stories/stories.csv";
    public string convinienceEC2 = "http://35.90.126.120:8080/api/files/download/stories/stories.csv";
    
    private void Start()
    {
        LoadBooksFromCSV(csvUrl);
    }

    public void LoadBooksFromCSV(string url)
    {
        baseURL = PRUtils.RemoveFileNameFromUrl(csvUrl); 
        StartCoroutine(DownloadCSV(url, (csv) => {
            prbooks = ParseCSV(csv);
            booksScrollView.AddBooks(prbooks);
        }));
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
        while ((line = reader.ReadLine()) != null)
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

    public List<PRBook> FilterByName(string name)
    {
        return prbooks.FindAll(s => s.bookName.ToLower().Contains(name.ToLower()));
    }

    public List<PRBook> FilterByAge(int age)
    {
        return prbooks.FindAll(s => s.ageFrom <= age && s.ageTo >= age);
    }

    public List<PRBook> FilterByGenre(string genre)
    {
        return prbooks.FindAll(s => s.genre.ToLower().Equals(genre.ToLower()));
    }

    public List<PRBook> FilterByNotesForParents(string notesForParents)
    {
        return prbooks.FindAll(s => s.notesForParents.ToLower().Contains(notesForParents.ToLower()));
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
}
