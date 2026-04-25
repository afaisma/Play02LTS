using System;
using System.Collections;
using System.Collections.Generic;
using QFSW.QC;
using UnityEngine;
using UnityEngine.UI;
using ReadingBuddy.UI;

public enum GalleryItemType
{
    Image,
    Video
}
public class GalleryItem
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
    public List<GalleryItem> _galleryItems = new List<GalleryItem>();
    public int _currentGalleryItemIndex = 0;
    public PuzzleImage imgMain;
    public Button btnPrevious;
    public Button btnNext;
    public Button btnPuzzle;

    private Sprite[] _puzzleButtonSprites;

    private void Start()
    {
        _puzzleButtonSprites = Resources.LoadAll<Sprite>("PuzzleButtons");
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
        //Debug.Log("addGallerySound " + url);
        GalleryItem galleryItem = _galleryItems[_galleryItems.Count - 1];
        galleryItem.AddSound(url);
        SetupSounds();
    }

    public void clearUpGalleryItems()
    {
        _currentGalleryItemIndex = 0;
        _galleryItems.Clear();
        _soundBar.Clear();
        imgMain.IsPuzzled = false;
    }

    public void ShowPuzzleButton(bool show)
    {
        if (btnPuzzle == null) return;
        btnPuzzle.gameObject.SetActive(show);
        if (show)
        {
            var label = btnPuzzle.GetComponentInChildren<TMPro.TextMeshProUGUI>();
            if (label != null) label.text = "";

            if (_puzzleButtonSprites != null && _puzzleButtonSprites.Length > 0)
            {
                var img = btnPuzzle.GetComponent<Image>();
                if (img != null)
                    img.sprite = _puzzleButtonSprites[UnityEngine.Random.Range(0, _puzzleButtonSprites.Length)];
            }
        }
    }

    public void TogglePuzzle()
    {
        imgMain.IsPuzzled = !imgMain.IsPuzzled;
        if (btnPuzzle != null)
        {
            var label = btnPuzzle.GetComponentInChildren<TMPro.TextMeshProUGUI>();
            if (label != null)
                label.text = "";
        }
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
