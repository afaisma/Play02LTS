using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public class LibraryScrollAdjuster : MonoBehaviour
{
    public GameObject _gameObjectLibrary;
    public float _fGalleryAnchorMinX = 0.1f;
    public float _fGalleryAnchorMaxX = 0.9f;
    
    void Start()
    {
        if (Globals.IsTablet()) 
        {
            _gameObjectLibrary.GetComponent<RectTransform> ().anchorMin = new Vector2(_fGalleryAnchorMinX, _gameObjectLibrary.GetComponent<RectTransform>().anchorMin.y);
            _gameObjectLibrary.GetComponent<RectTransform> ().anchorMax = new Vector2(_fGalleryAnchorMaxX, _gameObjectLibrary.GetComponent<RectTransform>().anchorMax.y);
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }

}
