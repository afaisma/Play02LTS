using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.Networking;
using SimpleJSON;
using UnityEngine.Serialization;
using System.Collections.Specialized;

class AudioAndTimingsStruct
{
    public string audioURL;
    public string jsonTimingsURL;
    public AudioClip audioClip;
    public JSONNode jsonNodeTimings;
}

public class AudioAndTextPlayer : MonoBehaviour
{
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private TMP_Text uiForeground;
    [SerializeField] private TMP_Text uiBackground;
    
    public string hilightTextColor = "FF55FF";
    public string hilightBackColor = "00FF0033";
    
    public static int maxCacheSize = 30;
    private static  OrderedDictionary cacheAcudioAndTimingsStructs = new OrderedDictionary();

    private List<WordTiming> wordTimings;
    private int currentWordIndex;
    public string baseURL;

    private void Start()
    {
        wordTimings = new List<WordTiming>();
        currentWordIndex = 0;
    }
    
    private void OnDestroy()
    {
        StopAllCoroutines();
    }
    
    public void Play(string audioURL, string jsonTimingsURL)
    {
        currentWordIndex = 0;
        StopAllCoroutines();
        StartCoroutine(LoadAudioAndTimings(
            audioURL != "" ? baseURL + audioURL: "", 
            jsonTimingsURL != "" ? baseURL + jsonTimingsURL: ""));
    }
    
    public void SetActive(bool bActive)
    {
        //Debug.Log("AudioAndTextPlayer::SetActive " + bActive);
        uiForeground.gameObject.SetActive(bActive);
        uiBackground.gameObject.SetActive(bActive);
    }
    
    public void SetFont( string fontName, int size, Color color)
    {
        if (fontName != "")
        {
            TMP_FontAsset fontAsset = Resources.Load<TMP_FontAsset>(fontName);
            uiForeground.font = fontAsset;
            uiBackground.font = fontAsset;
        }
        if (size > 0)
        {
            uiForeground.fontSize = size;
            uiBackground.fontSize = size;
        }
        if (color != null)
        {
            uiForeground.color = color;
            uiBackground.color = color;
        }
    }

    public void SetFontSize( int size)
    {
        if (size > 0)
        {
            uiForeground.fontSize = size;
            uiBackground.fontSize = size;
        }
    }
    public void SetTextAlignment(string alignment)
    {
        if (alignment.ToLower() == "center")
        {
            uiForeground.alignment = TextAlignmentOptions.Center;
            uiBackground.alignment = TextAlignmentOptions.Center;
        }
        else if (alignment.ToLower() == "top")
        {
            uiForeground.alignment = TextAlignmentOptions.Top;
            uiBackground.alignment = TextAlignmentOptions.Top;
        }
        else if (alignment.ToLower() == "topleft")
        {
            uiForeground.alignment = TextAlignmentOptions.TopLeft;
            uiBackground.alignment = TextAlignmentOptions.TopLeft;
        }
        else if (alignment.ToLower() == "right")
        {
            uiForeground.alignment = TextAlignmentOptions.Right;
            uiBackground.alignment = TextAlignmentOptions.Right;
        }
        else
        {
            uiForeground.alignment = TextAlignmentOptions.Left;
            uiBackground.alignment = TextAlignmentOptions.Left;
        }
    }

    public void EnableAutoSize(int enable, int fontSizeMin, int fontSizeMax)
    {
        bool bEnable = enable != 0;
        uiForeground.enableAutoSizing = bEnable;
        uiForeground.fontSizeMin = fontSizeMin;
        uiForeground.fontSizeMax = fontSizeMax;
        
        uiBackground.enableAutoSizing = bEnable;
        uiBackground.fontSizeMin = fontSizeMin;
        uiBackground.fontSizeMax = fontSizeMax;
    }
    
    private IEnumerator LoadAudioAndTimings(string audioURL, string jsonTimingsURL)
    {
        JSONNode timings = null;
        AudioAndTimingsStruct audioAndTimingsStruct = null;
        if (cacheAcudioAndTimingsStructs.Contains(audioURL))
        {
            audioAndTimingsStruct = cacheAcudioAndTimingsStructs[audioURL] as AudioAndTimingsStruct;
        }
        else
        {
            audioAndTimingsStruct = new AudioAndTimingsStruct();
            audioAndTimingsStruct.audioURL = audioURL;
            audioAndTimingsStruct.jsonTimingsURL = jsonTimingsURL;
            using (var www = new WWW(audioURL))
            {
                yield return www;

                if (www.error == null)
                {
                    AudioClipStruct audioClipStruct = new AudioClipStruct
                    {
                        audioClip = www.GetAudioClip()
                    };
                    audioAndTimingsStruct.audioClip = audioClipStruct.audioClip;
                    //audioSource.clip = audioClipStruct.audioClip;
                }
                else
                {
                    Debug.LogError($"Error loading audio clip {audioURL}: {www.error}");
                }
            }

            // could be just an audio file without timings
            if (jsonTimingsURL != "")
            {
                using (UnityWebRequest www = UnityWebRequest.Get(jsonTimingsURL))
                {
                    yield return www.SendWebRequest();

                    if (www.result == UnityWebRequest.Result.ConnectionError ||
                        www.result == UnityWebRequest.Result.ProtocolError)
                    {
                        Debug.Log(www.error);
                    }
                    else
                    {
                        timings = JSON.Parse(www.downloadHandler.text);
                        audioAndTimingsStruct.jsonNodeTimings = timings;
                    }
                }
            }

            AddToCache(audioURL, audioAndTimingsStruct);
        }

        ParseTimings(audioAndTimingsStruct.jsonNodeTimings);
        audioSource.clip = audioAndTimingsStruct.audioClip;
        audioSource.Play();

        if (jsonTimingsURL != "")
        {
            while (audioSource.isPlaying)
            {
                UpdateHighlightedText(audioSource.time * 1000); // Convert to milliseconds
                yield return null;
            }

            // to reset the text to its original state
            UpdateHighlightedText(0, false);
            //uiText.text = ""; // Reset the text to its original state.
        }
    }
    
    private static void AddToCache(string audioURL, AudioAndTimingsStruct audioAndTimingsStruct)
    {
        if (cacheAcudioAndTimingsStructs.Count >= maxCacheSize)
        {
            cacheAcudioAndTimingsStructs.RemoveAt(0);
        }
        cacheAcudioAndTimingsStructs[audioURL] = audioAndTimingsStruct;
    }

    
    private void ParseTimings(JSONNode timings)
    {
        if (timings == null)
            return;

        wordTimings.Clear();

        foreach (JSONNode timing in timings)
        {
            WordTiming wordTiming = new WordTiming
            {
                Word = timing["word"].Value,
                Time = timing["time"].AsFloat
            };
            wordTimings.Add(wordTiming);
        }
    }

    Boolean IsWordPunctuation(int i)
    {
        if (i < 0 || i >= wordTimings.Count)
            return false;
        if (wordTimings[i].Word.Length == 1 && Char.IsPunctuation(wordTimings[i].Word.ToCharArray()[0]))
            return true;
        return false;
    }
    
    private void UpdateHighlightedText(float currentAudioTime, bool bHilight = true)
    {
        if (wordTimings == null)
            return;
        
        for (int i = currentWordIndex; i < wordTimings.Count; i++)
        {
            if (wordTimings[i].Time > currentAudioTime)
            {
                currentWordIndex = i;
                break;
            }
        }

        string newForegroundText = "";
        string newBsckgroundText = "";
        for (int i = 0; i < wordTimings.Count; i++)
        {
            if (bHilight && i == currentWordIndex - 1)
            //if (bHilight && i == currentWordIndex)
            {
                newForegroundText += $"<color=#{hilightTextColor}>" + wordTimings[i].Word + "</color>";
                newBsckgroundText += $"<mark=#{hilightBackColor}>" + wordTimings[i].Word + "</mark>";
            }
            else
            {
                newForegroundText += wordTimings[i].Word;
                newBsckgroundText += wordTimings[i].Word;
            }

            //if (i < wordTimings.Count - 1 && !IsWordPunctuation(i + 1))
            //{
                //newForegroundText += " ";
                //newBsckgroundText += " ";
            //}
        }

        uiForeground.text = newForegroundText.TrimEnd();
        uiBackground.text = newBsckgroundText.TrimEnd();
        //Debug.Log(newText);
    }

    public void SetAudioTextHilightColors(string textColor, string backColor)
    {
        hilightTextColor = textColor;
        hilightBackColor = backColor;
    }
}

[System.Serializable]
public class WordTiming
{
    public string Word;
    public float Time;
}
