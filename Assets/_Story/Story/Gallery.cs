using System;
using System.Collections;
using System.Collections.Generic;
using QFSW.QC;
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
        
        DisplayCurrentItem();
    }

    public void addGallerySound(string url)
    {
        if (_galleryItems.Count == 0)
            return;
        Debug.Log("addGallerySound " + url);
        GalleryItem galleryItem = _galleryItems[_galleryItems.Count - 1];
        galleryItem.AddSound(url);
        SetupSounds();
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

    public void SetupSounds()
    {
        _soundBar.Clear();
        if (_galleryItems.Count == 0)
            return;
        
        foreach (var url in _galleryItems[_currentGalleryItemIndex]._sounds)
        {   
            _soundBar.AddSound(url);
        }
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
        SetupSounds();
    } 
    
    public void DisplayNextItem()
    {
        _currentGalleryItemIndex++;
        if (_currentGalleryItemIndex > _galleryItems.Count - 1)
            _currentGalleryItemIndex = 0;
        SetupUI();
        DisplayCurrentItem();
    }
    
    public void DisplayPreviousItem()
    {
        _currentGalleryItemIndex--;
        if (_currentGalleryItemIndex < 0)
            _currentGalleryItemIndex = 0;
        SetupUI();
        DisplayCurrentItem();
    }
    
    public void DisplayMainImage(string imageUrl)
    {
        Debug.Log("DisplayMainImage " + imageUrl);
        clearUpGalleryItems();
        addGalleryItem(imageUrl, GalleryItemType.Image);
        //SetupUI();
        //DisplayCurrentItem();
    }

    [Command]
    public void SetNextPrevImages(string imageUrl)
    {
        SetButtonImage(btnPrevious, imageUrl);
        SetButtonImage(btnNext, imageUrl);
    }
    
    public void SetButtonImage(Button buttonObj, string imageUrl)
    {
        // Download and set the button's image
        Image buttonImage = buttonObj.GetComponent<Image>();
        if (imageUrl != "")
            StartCoroutine(PRUtils.DownloadImage(imageUrl, buttonImage));
    }


}
