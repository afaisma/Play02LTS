using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DeviceAdjuster : MonoBehaviour
{
    public GameObject _gameObjectGallery;
    public GameObject _gameObjecText;
    
    public float _fGalleryAnchorMinY = 0.4f;
    public float _fGalleryAnchorMaxY = 0.99f;
    public float _fTextAnchorMinY = 0.01f;
    public float _fTextAnchorMaxY = 0.33f;
    
    void Start()
    {
        if (Globals.IsTablet())
        {
            _gameObjectGallery.GetComponent<RectTransform> ().anchorMin = new Vector2(_gameObjectGallery.GetComponent<RectTransform>().anchorMin.x,_fGalleryAnchorMinY);
            _gameObjectGallery.GetComponent<RectTransform> ().anchorMax = new Vector2(_gameObjectGallery.GetComponent<RectTransform>().anchorMax.x,_fGalleryAnchorMaxY);
            _gameObjecText.GetComponent<RectTransform> ().anchorMin = new Vector2(_gameObjecText.GetComponent<RectTransform>().anchorMin.x,_fTextAnchorMinY);
            _gameObjecText.GetComponent<RectTransform> ().anchorMax = new Vector2(_gameObjecText.GetComponent<RectTransform>().anchorMax.x,_fTextAnchorMaxY);
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
