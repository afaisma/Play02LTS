using System;
using System.Collections;
using System.Collections.Generic;
using QFSW.QC;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;


public class Filter
{
    public int ageFrom = 0;
    public int ageTo = 0;
    public String genre = "";

    public void SetFilter(int ageFrom, int ageTo, String genre)
    {
        this.ageFrom = ageFrom;
        this.ageTo = ageTo;
        this.genre = genre;
    }
    
    public bool Conforms(PRBook prBook)
    {
        if (genre != "")
            if (prBook.genre.ToLower().Contains(genre.ToLower()))
                return true;
            else
                return false;
        if (ageFrom != 0 && ageTo != 0)
            if (ageFrom <= prBook.ageFrom && prBook.ageTo <= ageTo)
                return true;
            else
                return false;

        return true;
    }
}

public class BooksScrollView : MonoBehaviour
{
    [SerializeField]
    private Transform scrollViewContent;
    
    [SerializeField]
    private GameObject bookPrefab;
    
    public ScrollRect scrollRectToStoreTheScrollPosition;
    private static Vector2 storedScrollPosition = new Vector2(-1, -1);

    private List<PRBook> prBooks;
    private Filter filter = new Filter();

    private void OnDestroy()
    {
        if (scrollRectToStoreTheScrollPosition != null)
            storedScrollPosition = scrollRectToStoreTheScrollPosition.normalizedPosition;
    }

    public void AddBook(PRBook prBook)
    {
        GameObject newBook = Instantiate(bookPrefab, scrollViewContent);
        if (newBook.TryGetComponent<BookViewItem>(out BookViewItem item))
        {
            item.prBook = prBook;
            string imageBookUrl = PRLibrary.baseURL + prBook.bookImageUrl;
            StartCoroutine(PRUtils.DownloadImage(imageBookUrl, item.imageBook));
            item.SetBookProperties(prBook);
        }
    }

    public void AddBooks(List<PRBook> prBooks)
    {
        this.prBooks = prBooks;
        ShowBooks(filter);
    }


    public void ShowBooks(Filter filter)
    {
        ClearScrollView();

        foreach (PRBook prBook in prBooks)
        {
            if (this.filter != null && !filter.Conforms(prBook))
                continue;
            AddBook(prBook);
        }
        
        if (storedScrollPosition != new Vector2(-1, -1) && scrollRectToStoreTheScrollPosition != null)
            scrollRectToStoreTheScrollPosition.normalizedPosition = storedScrollPosition;
    }

    [Command()]
    public void ClearScrollView()
    {
        foreach (Transform child in scrollViewContent)
        {
            Destroy(child.gameObject);
        }
    }
    
    [Command] 
    public void SetFilter(int ageFrom, int ageTo, String genre)
    {
        filter.SetFilter(ageFrom, ageTo, genre);
        ShowBooks(filter);
    }
}
