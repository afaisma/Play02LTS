using System;
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

/// <summary>
/// Gallery hosts the story page's picture area, sound bar, and navigation
/// buttons. The overlay subsystem (videos, sprites, pictures, animation,
/// scheduled callbacks) was lifted out into <see cref="OverlayHost"/> so
/// other scenes can reuse it; Gallery keeps a sibling OverlayHost on the
/// same GameObject and exposes pass-through methods, so the public API
/// that PRScript/StoryStepsUI talk to is unchanged.
/// </summary>
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

    // OverlayHost owns the overlay subsystem. We auto-attach one to the
    // same GameObject if the scene didn't already supply it, so existing
    // _Story scene assets keep working without an Inspector edit.
    private OverlayHost overlayHost;

    /// <summary>
    /// Event fired when an overlay produces a script-visible event
    /// (tap, scheduled callback, ...). Pass-through to OverlayHost.
    /// PRScript subscribes its DispatchEvent here in Start().
    /// </summary>
    public Action<string, string> onOverlayEvent
    {
        get => overlayHost != null ? overlayHost.onOverlayEvent : null;
        set { if (overlayHost != null) overlayHost.onOverlayEvent = value; }
    }

    private void Awake()
    {
        // Awake runs before any Start, so PRScript.Start can safely
        // assign onOverlayEvent through this Gallery.
        overlayHost = GetComponent<OverlayHost>();
        if (overlayHost == null)
            overlayHost = gameObject.AddComponent<OverlayHost>();
    }

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
        overlayHost?.Clear();
    }

    // ── Overlay API pass-throughs ─────────────────────────────────────
    // Same signatures Gallery used to expose; existing callers (StoryStepsUI,
    // PRScript) don't change. Implementation lives in OverlayHost so any
    // other scene can attach an OverlayHost and use the same API directly.

    public void AddOverlayVideo(string name, string url, float x1, float y1, float x2, float y2)
        => overlayHost.AddOverlayVideo(name, url, x1, y1, x2, y2);

    public void AddOverlaySprites(string name, string folderUrl, float x1, float y1, float x2, float y2)
        => overlayHost.AddOverlaySprites(name, folderUrl, x1, y1, x2, y2);

    public void AddOverlayPicture(string name, string url, float x1, float y1, float x2, float y2)
        => overlayHost.AddOverlayPicture(name, url, x1, y1, x2, y2);

    public void SetOverlayProperty(string name, string property, float value)
        => overlayHost.SetOverlayProperty(name, property, value);

    public void ShowOverlay(string name)   => overlayHost.ShowOverlay(name);
    public void HideOverlay(string name)   => overlayHost.HideOverlay(name);
    public void ToggleOverlay(string name) => overlayHost.ToggleOverlay(name);

    public void SetOverlayPosition(string name, float x1, float y1, float x2, float y2)
        => overlayHost.SetOverlayPosition(name, x1, y1, x2, y2);

    public void AnimateOverlayTo(string name, float x1, float y1, float x2, float y2, float duration)
        => overlayHost.AnimateOverlayTo(name, x1, y1, x2, y2, duration);

    public void StopOverlayAnimation(string name)
        => overlayHost.StopOverlayAnimation(name);

    public void PlayOverlayVideoSegment(string name, float fromSec, float toSec)
        => overlayHost.PlayOverlayVideoSegment(name, fromSec, toSec);

    public void Schedule(float seconds, string eventName, string target = "")
        => overlayHost.Schedule(seconds, eventName, target);

    public void CancelSchedule(string eventName, string target = "")
        => overlayHost.CancelSchedule(eventName, target);

    // ── Gallery-specific (non-overlay) ────────────────────────────────

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
                    img.sprite = _puzzleButtonSprites[3];
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
    }

    [Command]
    public void SetNextPrevImages(string imageUrl)
    {
        SetButtonImage(btnPrevious, imageUrl);
        SetButtonImage(btnNext, imageUrl);
    }

    public void SetButtonImage(Button buttonObj, string imageUrl)
    {
        Image buttonImage = buttonObj.GetComponent<Image>();
        if (imageUrl != "")
            StartCoroutine(PRUtils.DownloadImage(imageUrl, buttonImage));
    }
}
