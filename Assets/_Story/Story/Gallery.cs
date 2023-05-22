using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public enum GalleryItemType
{
    Image,
    Video
}
class GalleryItem
{
    public string url;
    public GalleryItemType type;
    public List<string> _sounds = new List<string>();
    public void AddSound(string sound)
    {
        _sounds.Add(sound);
    }
}
public class Gallery : MonoBehaviour
{
    public SoundBar _soundBar;
    private List<GalleryItem> _galleryItems = new List<GalleryItem>();
    int _currentGalleryItemIndex = 0;
    public Image imgMain;
    public Button btnPrevious;
    public Button btnNext;

    private void Start()
    {
        SetupUI();
    }

    public void addGalleryItem(string url, GalleryItemType type)
    {
        GalleryItem galleryItem = new GalleryItem();
        galleryItem.url = url;
        galleryItem.type = type;
        _galleryItems.Add(galleryItem);
        SetupUI();
    }

    public void addGallerySound(string url)
    {
        _soundBar.AddSound(url); 
    }

    public void clearUpGalleryItems()
    {
        _currentGalleryItemIndex = 0;
        _galleryItems.Clear();
        _soundBar.Clear();
    }

    public void DisplayCurrentItem()
    {
        if (_currentGalleryItemIndex < 0 || _currentGalleryItemIndex >= _galleryItems.Count)
           return;
            
        if ( _galleryItems[_currentGalleryItemIndex].type == GalleryItemType.Image)
            StartCoroutine(PRUtils.DownloadImage( _galleryItems[_currentGalleryItemIndex].url, imgMain));
    }

    public void SetupUI()
    {
        if (_currentGalleryItemIndex < _galleryItems.Count - 1)
            btnNext.gameObject.SetActive(true);
        else
            btnNext.gameObject.SetActive(false);
        
        if (_currentGalleryItemIndex > 0)
            btnPrevious.gameObject.SetActive(true);
        else
            btnPrevious.gameObject.SetActive(false);
        
    } 
    
    public void DisplayNextItem()
    {
        _currentGalleryItemIndex++;
        if (_currentGalleryItemIndex >= _galleryItems.Count)
            _currentGalleryItemIndex = 0;
        SetupUI();
        DisplayCurrentItem();
    }
    
    public void DisplayPreviousItem()
    {
        _currentGalleryItemIndex--;
        if (_currentGalleryItemIndex < 0)
            _currentGalleryItemIndex = _galleryItems.Count - 1;
        DisplayCurrentItem();
    }
    
    public void DisplayMainImage(string imageUrl)
    {
        clearUpGalleryItems();
        addGalleryItem(imageUrl, GalleryItemType.Image);
        DisplayCurrentItem();
        SetupUI();
    }


}
