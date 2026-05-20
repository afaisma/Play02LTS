using System;
using System.Collections;
using System.Collections.Generic;
using QFSW.QC;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;



public class BookstoreScrollView : MonoBehaviour
{
    [SerializeField]
    private Transform scrollViewContent;
    
    [SerializeField]
    private GameObject bookPrefab;
    
    public ScrollRect scrollRectToStoreTheScrollPosition;
    private static Vector2 storedScrollPosition = new Vector2(-1, -1);

    private List<PRBook> prBooks;
    private Filter filter = new Filter();

    // Throttled cover downloads — same pattern as BooksScrollView.
    // Without this, opening the bookstore fires one parallel HTTP request
    // per book, which can trip AWS WAF rate-based rules / macOS HTTP
    // stack caps and produce intermittent 403s.
    private const int MAX_INFLIGHT_COVERS = 8;
    private int _inflightCovers = 0;
    private readonly Queue<(string url, Image image)> _pendingCovers =
        new Queue<(string, Image)>();

    private void OnDestroy()
    {
        if (scrollRectToStoreTheScrollPosition != null)
            storedScrollPosition = scrollRectToStoreTheScrollPosition.normalizedPosition;
    }

    public void AddBook(PRBook prBook)
    {
        if (prBook.bookstoreViewItem != null)
        {
            prBook.bookstoreViewItem.gameObject.SetActive(true);
            return;
        }

        GameObject newBookGameObject = Instantiate(bookPrefab, scrollViewContent);

        if (newBookGameObject.TryGetComponent<BookstoreViewItem>(out BookstoreViewItem bookstoreViewItem))
        {
            bookstoreViewItem.prBook = prBook;
            string imageBookUrl = Globals.baseURL + prBook.bookImageUrl;
            EnqueueCoverDownload(imageBookUrl, bookstoreViewItem.imageBook);
            bookstoreViewItem.SetBookProperties(prBook);
            prBook.bookstoreViewItem = bookstoreViewItem;
        }
    }

    /// <summary>Throttled wrapper around PRUtils.DownloadImage for
    /// bookstore covers. Either kicks off the download immediately or
    /// queues it for later, based on how many requests are already in
    /// flight. Same pattern as BooksScrollView.</summary>
    private void EnqueueCoverDownload(string url, Image image)
    {
        if (_inflightCovers < MAX_INFLIGHT_COVERS)
        {
            _inflightCovers++;
            StartCoroutine(DownloadCoverThrottled(url, image));
        }
        else
        {
            _pendingCovers.Enqueue((url, image));
        }
    }

    private IEnumerator DownloadCoverThrottled(string url, Image image)
    {
        // suppressAlert=true so a single failed thumbnail doesn't pop a
        // modal dialog — the NoImage placeholder is sufficient feedback.
        yield return PRUtils.DownloadImage(url, image, true, true);
        _inflightCovers--;
        if (_pendingCovers.Count > 0 && _inflightCovers < MAX_INFLIGHT_COVERS)
        {
            var next = _pendingCovers.Dequeue();
            _inflightCovers++;
            StartCoroutine(DownloadCoverThrottled(next.url, next.image));
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
            BookstoreViewItem bvi1 = t1.GetComponent<BookstoreViewItem>();
            BookstoreViewItem bvi2 = t2.GetComponent<BookstoreViewItem>();

            if (bvi1 != null && bvi2 != null)
            {
                if (bAscending)
                    return bvi1.prBook.ageFrom.CompareTo(bvi2.prBook.ageFrom);
                else
                    return bvi2.prBook.ageFrom.CompareTo(bvi1.prBook.ageFrom);
            }
            return 0;  // Consider how you wish to handle the case where BookstoreViewItem component is missing.
        });

        for (int i = 0; i < children.Count; i++)
        {
            children[i].SetSiblingIndex(i);
        }
    }

    public void ResetScrollPosition()
    {
        storedScrollPosition = new Vector2(-1, -1);
        scrollRectToStoreTheScrollPosition.normalizedPosition = new Vector2(0, 1);
    }
    
}
