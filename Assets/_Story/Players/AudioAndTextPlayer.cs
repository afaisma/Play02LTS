using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.Networking;
using SimpleJSON;
using UnityEngine.Serialization;
using System.Collections.Specialized;
using UnityEngine.Events;
using UnityEngine.UI;

// send content to LoadAudioAndTimings( parameters: set jsonTimingsURL to "" if staticText is true 
// if staticText is true, create single AudioAndTimingsStruct object (and add to cacheAcudioAndTimingsStructs as usual)


class AudioAndTimingsStruct
{
    public string audioURL;
    public string jsonTimingsURL;
    public AudioClip audioClip;
    public JSONNode jsonNodeTimings;
}

[System.Serializable]
public class WordTiming
{
    public string Word;
    public float Time;
}

public class AudioAndTextPlayer : MonoBehaviour
{
    public bool staticText = true;

    public string nextPlayUseVoice = "";
    public bool showHighlight = true;
    public bool audioNameGenerated = true;
    public ButtonSelectionController buttonSelectionController;
    
    [SerializeField] private bool _playAudio = true;  // Backing field for playAudio
    
    public bool _PlayAudio
    {
        get { return _playAudio; }
        set
        {
            if (_playAudio != value)  // Only trigger if value changes
            {
                _playAudio = value;
                UpdateAudioSourceVolume();  // Call method to update volume
            }
        }
    }

    // This method is called when the value of a variable changes in the editor
    void OnValidate()
    {
        // Sync the property with the field when value is changed in the editor
        UpdateAudioSourceVolume();
    }

    private void UpdateAudioSourceVolume()
    {
        if (audioSource != null)
        {
            audioSource.volume = !_PlayAudio ? 0 : 1;
        }
    }

    [SerializeField]
    public UnityEvent OnAutoNextStep;
    private bool triggerNextStep = false;
    [SerializeField] private Toggle nextStepToggle;
    
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

    DateTime dtWasPlaying = DateTime.MinValue;
    
    private void Start()
    {
        wordTimings = new List<WordTiming>();
        currentWordIndex = 0;
        // Add a listener to the toggle to call SetTriggerNextStep when the toggle value changes
        if (nextStepToggle != null)
        {
            nextStepToggle.onValueChanged.AddListener(OnToggleValueChanged);
        }

        // nextPlayUseVoice = "computer";
    }
    
    private void OnDestroy()
    {
        if (nextStepToggle != null)
        {
            nextStepToggle.onValueChanged.RemoveListener(OnToggleValueChanged);
        }
        StopAllCoroutines();
    }

    public void OnSetNextPlayVoiceSetting()
    {
        Debug.Log("OnSetNextPlayVoiceSetting: buttonSelectionController = " + buttonSelectionController);
        nextPlayUseVoice = buttonSelectionController.GetSelectedButtonName();
        Debug.Log("OnSetNextPlayVoiceSetting: nextPlayUseVoice = " + nextPlayUseVoice);
    }
    
    void PreparePlayVoiceSettings()
    {
        nextPlayUseVoice = buttonSelectionController.GetSelectedButtonName();
        Debug.Log("PreparePlayVoiceSettings: nextPlayUseVoice = " + nextPlayUseVoice);
        if (nextPlayUseVoice == "human")
        {
            showHighlight = false;
            audioNameGenerated = false;
            _PlayAudio = true;
        }
        else if (nextPlayUseVoice == "computer" || nextPlayUseVoice == "")
        {
            showHighlight = true;
            audioNameGenerated = true;
            _PlayAudio = true;
        }
        else if (nextPlayUseVoice == "novoice")
        {
            showHighlight = false;
            audioNameGenerated = true;
            _PlayAudio = false;
        }
    }
    
    public void Play(string chunkname, string currentVoicePostfix, string content)
    {
        PreparePlayVoiceSettings();
        string audioURL = $"{chunkname}_{Globals.getReadingRate()}{currentVoicePostfix}.mp3";  ;
        string jsonTimingsURL = $"{chunkname}_{Globals.getReadingRate()}_timings{currentVoicePostfix}.json"; 
        if (!audioNameGenerated)
            audioURL = $"{chunkname}.mp3";
        if (staticText)
            jsonTimingsURL =  $"{chunkname}.json";
        
        currentWordIndex = 0;
        StopAllCoroutines();
        StartCoroutine(LoadAudioAndTimings(
            audioURL != "" ? baseURL + audioURL: "", 
            jsonTimingsURL != "" ? baseURL + jsonTimingsURL: ""));
    }
    
    private void OnToggleValueChanged(bool isOn)
    {
        triggerNextStep = isOn;
    }

    public void SetActive(bool bActive)
    {
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
    public void SetTextAlignment(string alignment, bool nClearText = true)
    {
        if (nClearText)
        {
            uiForeground.text = "";
            uiBackground.text = "";
        }
        
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

        if (audioAndTimingsStruct != null)
        {
            ParseTimings(audioAndTimingsStruct.jsonNodeTimings);
            audioSource.clip = audioAndTimingsStruct.audioClip;
        }

        yield return new WaitForSeconds(0.5f);
        
        audioSource.volume = !_PlayAudio ? 0 : 1;
        audioSource.Play();

        if (jsonTimingsURL != "")
        {
            if (audioSource.isPlaying)
                dtWasPlaying = DateTime.Now;

            while (audioSource.isPlaying)
            {
                UpdateHighlightedText(audioSource.time * 1000 - 500); // Convert to milliseconds, offset playtime back a bit
                yield return null;
            }

            // to reset the text to its original state
            //if ((dtWasPlaying != DateTime.MinValue) && (DateTime.Now - dtWasPlaying > TimeSpan.FromSeconds(2)))
            UpdateHighlightedText(0, false);
            //uiText.text = ""; // Reset the text to its original state.

            // Log a message when the audio stops playing
            Debug.Log("Audio has stopped playing.");

            // Conditionally start the coroutine based on triggerNextStep
            if (triggerNextStep)
            {
                StartCoroutine(WaitAndTriggerNextStep());
            }
        }
    }
    
    private IEnumerator WaitAndTriggerNextStep()
    {
        yield return new WaitForSeconds(2f);
        OnAutoNextStep?.Invoke();
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
            WordTiming wordTiming = new WordTiming();
            wordTiming.Word = timing["word"].Value;
            wordTiming.Time = timing["time"].AsFloat;
            wordTimings.Add(wordTiming);
        }
    }

    Boolean IsWordPunctuation(int i)
    {
        // if (i < 0 || i >= wordTimings.Count)
        //     return false;
        if (wordTimings[i].Word.Trim().Length == 1 && Char.IsPunctuation(wordTimings[i].Word.Trim().ToCharArray()[0]))
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
            //if (PRUtils.AlmostEqual(wordTimings[i].Time, currentAudioTime, 0.1f))
            {
                currentWordIndex = i;
                break;
            }
        }

        // Debug.Log("UpdateHighlightedText1: currentAudioTime = " + currentAudioTime + 
        //           ", currentWordIndex = " + currentWordIndex + ", bHilight = " + bHilight + ", " + 
        //           wordTimings[currentWordIndex].Word);

        string newForegroundText = "";
        string newBsckgroundText = "";
        for (int i = 0; i < wordTimings.Count; i++)
        {
            //if (bHilight && i == currentWordIndex - 1)
            if (showHighlight && bHilight && i == currentWordIndex && !IsWordPunctuation(i))
            {
                newForegroundText += $"<color=#{hilightTextColor}>" + wordTimings[i].Word + "</color>";
                newBsckgroundText += $"<mark=#{hilightBackColor}>" + wordTimings[i].Word + "</mark>";
                //Debug.Log("UpdateHighlightedText2: " + wordTimings[i].Word);
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

