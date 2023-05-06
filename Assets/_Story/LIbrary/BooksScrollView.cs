using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class BooksScrollView : MonoBehaviour
{
    [SerializeField]
    private Transform scrollViewContent;
    
    [SerializeField]
    private GameObject bookPrefab;
    
    
    public void AddBook(PRBook prBook)
    {
        GameObject newBook = Instantiate(bookPrefab, scrollViewContent);
        if (newBook.TryGetComponent<BookViewItem>(out BookViewItem item))
        {
            item.prBook = prBook;
            string imageBookUrl = PRLibrary.baseURL + prBook.bookImageUrl;
            StartCoroutine(PRUtils.DownloadImage(imageBookUrl, item.imageBook));
            item.SetBookNameAndAuthor(prBook);
        }
    }

    public void AddBooks(List<PRBook> prBooks)
    {
        foreach (PRBook prBook in prBooks)
        {
            AddBook(prBook);
        }
    }
    
}
