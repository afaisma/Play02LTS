using System;
using System.Collections;
using System.Collections.Generic;
using QFSW.QC;
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
        if (prBook.bookViewItem != null)
        {
            prBook.bookViewItem.gameObject.SetActive(true);
            return;
        }

        GameObject newBookGameObject = Instantiate(bookPrefab, scrollViewContent);
        if (newBookGameObject.TryGetComponent<BookViewItem>(out BookViewItem bookViewItem))
        {
            bookViewItem.prBook = prBook;
            string imageBookUrl = Globals.baseURL + prBook.bookImageUrl;
            StartCoroutine(PRUtils.DownloadImage(imageBookUrl, bookViewItem.imageBook));
            bookViewItem.SetBookProperties(prBook);
            prBook.bookViewItem = bookViewItem;
        }
    }

    public void AddBooks(List<PRBook> prBooks)
    {
        this.prBooks = prBooks;
        ShowBooks(filter);
    }


    public void ShowBooks(Filter filter)
    {
        if (prBooks == null)
            return;
        
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
            //Destroy(child.gameObject);
            child.gameObject.SetActive(false);
        }
    }
    
    [Command] 
    public void SetFilter(int ageFrom, int ageTo, String genre)
    {
        filter.SetFilter(ageFrom, ageTo, genre);
        // Debug.Log("Set filter: " + ageFrom + " " + ageTo + " " + genre);
        ShowBooks(filter);
    }
    
    public void SetSortingByAge(bool bAscending)
    {
        List<Transform> children = new List<Transform>();

        foreach (Transform child in scrollViewContent)
        {
            children.Add(child);
        }

        children.Sort((t1, t2) => 
        {
            BookViewItem bvi1 = t1.GetComponent<BookViewItem>();
            BookViewItem bvi2 = t2.GetComponent<BookViewItem>();

            if (bvi1 != null && bvi2 != null)
            {
                if (bAscending)
                    return bvi1.prBook.ageFrom.CompareTo(bvi2.prBook.ageFrom);
                else
                    return bvi2.prBook.ageFrom.CompareTo(bvi1.prBook.ageFrom);
            }
            return 0;  // Consider how you wish to handle the case where BookViewItem component is missing.
        });

        for (int i = 0; i < children.Count; i++)
        {
            children[i].SetSiblingIndex(i);
        }
    }


    
}
