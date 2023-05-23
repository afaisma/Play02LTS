using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Specialized;

class AudioStruct
{
    public string audioURL;
    public AudioClip audioClip;
}

public class SoundBar : MonoBehaviour
{
    [SerializeField] private AudioSource audioSource;
    public List<Button> _buttons = new List<Button>();
    public List<string> _sounds = new List<string>();

    public static int maxCacheSize = 30;
    private static  OrderedDictionary cacheAcudioStructs = new OrderedDictionary();

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        Clear();
        //AddSound("http://localhost:8080/api/files/download/stories/GoodPeople/chunk_2.wav");
    }
    
    private IEnumerator LoadAudio(string audioURL)
    {
        AudioStruct acudioStruct = null;
        if (cacheAcudioStructs.Contains(audioURL))
        {
            acudioStruct = cacheAcudioStructs[audioURL] as AudioStruct;
        }
        else
        {
            acudioStruct = new AudioStruct();
            acudioStruct.audioURL = audioURL;
            using (var www = new WWW(audioURL))
            {
                yield return www;

                if (www.error == null)
                {
                    AudioClipStruct audioClipStruct = new AudioClipStruct
                    {
                        audioClip = www.GetAudioClip()
                    };
                    acudioStruct.audioClip = audioClipStruct.audioClip;
                    //audioSource.clip = audioClipStruct.audioClip;
                }
                else
                {
                    Debug.LogError($"Error loading audio clip: {www.error}");
                }
            }

            AddToCache(audioURL, acudioStruct);
        }

        audioSource.clip = acudioStruct.audioClip;
        audioSource.Play();
    }

    private static void AddToCache(string audioURL, AudioStruct audioAndTimingsStruct)
    {
        if (cacheAcudioStructs.Count >= maxCacheSize)
        {
            cacheAcudioStructs.RemoveAt(0);
        }
        cacheAcudioStructs[audioURL] = audioAndTimingsStruct;
    }


    public void Clear()
    {
        audioSource.Stop();
        for (int i = 0; i < _buttons.Count; i++)
        {
            _buttons[i].gameObject.SetActive(false);
        }
        _sounds.Clear();
    }
    
    public void AddSound(string sound)
    {
        if (_sounds.Count >= _buttons.Count)
            return;
        
        _sounds.Add(sound);
        _buttons[_sounds.Count - 1].gameObject.SetActive(true);
    }

    public void PlaySound(ButtonSound buttonSound)
    {
        int index = _buttons.IndexOf(buttonSound.GetComponent<Button>());
        if (index >= 0 && index < _sounds.Count)
            StartCoroutine(LoadAudio(_sounds[index]));
    }
}
