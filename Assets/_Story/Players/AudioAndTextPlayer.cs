using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Networking;
using UnityEngine.UI;
using System.Collections.Specialized;
using SimpleJSON;

class AudioAndTextStruct
{
    public string audioURL;
    public string textURL;
    public AudioClip audioClip;
    public JSONNode jsonNodeTimings;
    public string content;
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

    [SerializeField] private bool _playAudio = true; // Backing field for playAudio

    public bool _PlayAudio
    {
        get { return _playAudio; }
        set
        {
            if (_playAudio != value) // Only trigger if value changes
            {
                _playAudio = value;
                UpdateAudioSourceVolume(); // Call method to update volume
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

    [SerializeField] public UnityEvent OnAutoNextStep;
    [SerializeField] public UnityEvent OnAudioFinished;
    public bool IsAutoplaying => triggerNextStep;
    private bool triggerNextStep = false;
    [SerializeField] private Toggle nextStepToggle;

    [SerializeField] private AudioSource audioSource;
    [SerializeField] private TMP_Text uiForeground;
    [SerializeField] private TMP_Text uiBackground;

    public string hilightTextColor = "FF55FF";
    public string hilightBackColor = "00FF0044";

    // Bumped from 30 — a typical book of 10 pages × ~5 chunks per page
    // already strains a cap of 30. Holding more lets us survive a
    // Library → Story → Library round-trip with audio still warm in cache,
    // and reduces re-decode pressure on a slow device. At ~1 MB per
    // AudioClip × 50 entries the peak is ~50 MB CPU memory, fine on
    // modern hardware.
    private static readonly int MaxAudioCacheSize = 50;
    private static readonly OrderedDictionary CacheAudioAndTimingsStructs = new OrderedDictionary();

    private List<WordTiming> currentWordTimings;
    private int currentWordIndex;
    public string baseURL;

    DateTime dtWasPlaying = DateTime.MinValue;

    private void Start()
    {
        currentWordTimings = new List<WordTiming>();
        currentWordIndex = 0;
        // Add a listener to the toggle to call OnToggleValueChanged when the toggle value changes
        if (nextStepToggle != null)
        {
            nextStepToggle.onValueChanged.AddListener(OnToggleValueChanged);
        }
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
        nextPlayUseVoice = buttonSelectionController.GetSelectedButtonName();
    }

    void PreparePlayVoiceSettings()
    {
        nextPlayUseVoice = buttonSelectionController.GetSelectedButtonName();
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

    public void PlayExt(string audioURL, float fromS, float toS, string textContentURL, int pageNum)
    {
        PreparePlayVoiceSettings();
        currentWordIndex = 0;
        StopAllCoroutines();
        StartCoroutine(LoadAudioAndTimings(
            !string.IsNullOrEmpty(audioURL) ? baseURL + audioURL : "",
            !string.IsNullOrEmpty(textContentURL) ? baseURL + textContentURL : "",
            pageNum,
            "",
            fromS,
            toS
        ));
    }

    public void Play(string chunkname, string currentVoicePostfix, string content, float startTime = -1,
        float endTime = -1)
    {
        PreparePlayVoiceSettings();
        string audioURL = $"{chunkname}_{Globals.getReadingRate()}{currentVoicePostfix}.mp3";
        string jsonTimingsURL = $"{chunkname}_{Globals.getReadingRate()}_timings{currentVoicePostfix}.json";

        // If not generated by TTS or if we want a plain chunk
        if (!audioNameGenerated)
            audioURL = $"{chunkname}.mp3";

        // If static text is true, ignore external JSON timings
        if (staticText)
            jsonTimingsURL = $"{chunkname}.json";

        currentWordIndex = 0;
        StopAllCoroutines();

        // Pass the content into the coroutine in case you want to handle it for static text
        StartCoroutine(LoadAudioAndTimings(
            !string.IsNullOrEmpty(audioURL) ? baseURL + audioURL : "",
            !string.IsNullOrEmpty(jsonTimingsURL) ? baseURL + jsonTimingsURL : "",
            -1,
            content,
            startTime,
            endTime
        ));
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

    public void SetFont(string fontName, int size, Color color)
    {
        if (!string.IsNullOrEmpty(fontName))
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

        uiForeground.color = color;
        uiBackground.color = color;
    }

    public void SetFontSize(int size)
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

        // Convert to lower for easier comparison
        var alignLower = alignment.ToLower();

        if (alignLower == "center")
        {
            uiForeground.alignment = TextAlignmentOptions.Center;
            uiBackground.alignment = TextAlignmentOptions.Center;
        }
        else if (alignLower == "top")
        {
            uiForeground.alignment = TextAlignmentOptions.Top;
            uiBackground.alignment = TextAlignmentOptions.Top;
        }
        else if (alignLower == "topleft")
        {
            uiForeground.alignment = TextAlignmentOptions.TopLeft;
            uiBackground.alignment = TextAlignmentOptions.TopLeft;
        }
        else if (alignLower == "right")
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
        bool bEnable = (enable != 0);
        uiForeground.enableAutoSizing = bEnable;
        uiForeground.fontSizeMin = fontSizeMin;
        uiForeground.fontSizeMax = fontSizeMax;

        uiBackground.enableAutoSizing = bEnable;
        uiBackground.fontSizeMin = fontSizeMin;
        uiBackground.fontSizeMax = fontSizeMax;
    }

    private IEnumerator LoadAudioAndTimings(string audioURL, string textURL, int pageNum, string content,
        float startTime = -1, float endTime = -1)
    {
        JSONNode timings = null;
        AudioAndTextStruct audioAndTextStruct = null;

        // 1) Check Cache
        if (CacheAudioAndTimingsStructs.Contains(audioURL))
        {
            audioAndTextStruct = CacheAudioAndTimingsStructs[audioURL] as AudioAndTextStruct;
            // C3: move to most-recently-used position so frequent items survive eviction.
            CacheAudioAndTimingsStructs.Remove(audioURL);
            CacheAudioAndTimingsStructs[audioURL] = audioAndTextStruct;
        }
        else
        {
            // 2) Create a new struct if not in cache
            audioAndTextStruct = new AudioAndTextStruct
            {
                audioURL = audioURL,
                textURL = textURL
            };

            // --- Download the audio (if audioURL is not empty) ---
            // Disk cache first: if a previous session cached the MP3, decode
            // from the local file via a file:// URL — same code path Unity
            // uses for streaming, just from a local origin. Falls back to
            // network if the disk file is missing or fails to decode.
            if (!string.IsNullOrEmpty(audioURL))
            {
                string diskAudioPath = DiskCache.PathFor(audioURL, "audio", ".mp3");
                if (System.IO.File.Exists(diskAudioPath))
                {
                    using (UnityWebRequest diskReq = UnityWebRequestMultimedia.GetAudioClip(
                        "file://" + diskAudioPath, AudioType.MPEG))
                    {
                        yield return diskReq.SendWebRequest();
                        if (diskReq.result == UnityWebRequest.Result.Success)
                        {
                            AudioClip clip = DownloadHandlerAudioClip.GetContent(diskReq);
                            if (clip != null) audioAndTextStruct.audioClip = clip;
                        }
                    }
                }

                // Network fallback if disk was empty or corrupt.
                if (audioAndTextStruct.audioClip == null)
                {
                    using (UnityWebRequest uwr = UnityWebRequestMultimedia.GetAudioClip(audioURL, AudioType.MPEG))
                    {
                        uwr.timeout = 60;  // narration MP3s are larger than other content
                        yield return uwr.SendWebRequest();

                        if (uwr.result == UnityWebRequest.Result.ConnectionError ||
                            uwr.result == UnityWebRequest.Result.ProtocolError)
                        {
                            // Asset-level failure: page still renders text without audio.
                            Debug.LogWarning($"AudioAndTextPlayer: audio fetch failed — {uwr.error}  (url={audioURL})");
                        }
                        else
                        {
                            AudioClip clip = DownloadHandlerAudioClip.GetContent(uwr);
                            if (clip != null)
                            {
                                audioAndTextStruct.audioClip = clip;
                                // Persist for future sessions. Unity exposes the raw
                                // download bytes on downloadHandler.data even for the
                                // multimedia handler.
                                DiskCache.WriteBytes(audioURL, "audio", ".mp3",
                                    uwr.downloadHandler.data, DiskCache.MaxAudios);
                            }
                            else
                            {
                                // Asset-level failure: page still renders text without audio.
                                Debug.LogWarning($"AudioAndTextPlayer: decoded AudioClip is null  (url={audioURL})");
                            }
                        }
                    }
                }
            }

            // --- Download JSON Timings if available ---
            if (!string.IsNullOrEmpty(textURL))
            {
                // Disk cache first.
                string diskTimings = DiskCache.TryReadText(textURL, "timings", ".json");
                if (diskTimings != null)
                {
                    content = diskTimings;
                    audioAndTextStruct.content = content;
                    if (pageNum == -1)
                    {
                        timings = JSON.Parse(content);
                        audioAndTextStruct.jsonNodeTimings = timings;
                    }
                }
                else
                {
                    using (UnityWebRequest www = UnityWebRequest.Get(textURL))
                    {
                        www.timeout = 20;  // small JSON timings file
                        yield return www.SendWebRequest();

                        if (www.result == UnityWebRequest.Result.ConnectionError ||
                            www.result == UnityWebRequest.Result.ProtocolError)
                        {
                            // Asset-level failure: text still renders, just without word-level highlighting.
                            Debug.LogWarning($"AudioAndTextPlayer: timings fetch failed — {www.error}  (url={textURL})");
                        }
                        else
                        {
                            content = www.downloadHandler.text;
                            audioAndTextStruct.content = content;
                            // Persist for future sessions.
                            DiskCache.WriteText(textURL, "timings", ".json",
                                content, DiskCache.MaxTimings);
                            // if page number == -1, we're dealing with JSON timings
                            if (pageNum == -1)
                            {
                                timings = JSON.Parse(content);
                                audioAndTextStruct.jsonNodeTimings = timings;
                            }
                            // we're dealing with a page from static text
                            else
                            {
                                /*
                                String page = GetPageFromTextContent(content, pageNum);
                                var singleChunk = new JSONArray();
                                JSONNode singleNode = new JSONObject();
                                singleNode["word"] = page;
                                singleNode["time"] = 0.0f;
                                singleChunk.Add(singleNode);

                                audioAndTextStruct.jsonNodeTimings = singleChunk;
                            */
                            }
                        }
                    }
                }
            }
            else
            {
                // SIMPLE STATIC TEXT CASE:
                var singleChunk = new JSONArray();
                JSONNode singleNode = new JSONObject();
                singleNode["word"] = content;
                singleNode["time"] = 0.0f;
                singleChunk.Add(singleNode);

                audioAndTextStruct.jsonNodeTimings = singleChunk;
            }

            // 3) Add to cache
            AddToCache(audioURL, audioAndTextStruct);
        }

        // 4) Apply Timings & Audio
        if (audioAndTextStruct != null)
        {
            // Handle fragment timings
            if (startTime < 0) startTime = 0;
            if (endTime < 0 && audioAndTextStruct.audioClip != null)
                endTime = audioAndTextStruct.audioClip.length;

            ParseTimings(audioAndTextStruct, pageNum);

            // Set up audio clip
            if (audioAndTextStruct.audioClip != null)
            {
                AudioClip originalClip = audioAndTextStruct.audioClip;

                // H4: free the previous fragment clip (if any) before assigning a new one.
                // Cached originals are not destroyed — they don't start with "Fragment_".
                if (audioSource.clip != null && audioSource.clip.name.StartsWith("Fragment_"))
                {
                    Destroy(audioSource.clip);
                }

                // If we're playing a fragment, create a new clip
                if (startTime > 0 || endTime < originalClip.length)
                {
                    int startSample = Mathf.FloorToInt(startTime * originalClip.frequency);
                    int endSample = Mathf.FloorToInt(endTime * originalClip.frequency);
                    int fragmentLength = endSample - startSample;

                    AudioClip fragmentClip = AudioClip.Create(
                        "Fragment_" + originalClip.name,
                        fragmentLength,
                        originalClip.channels,
                        originalClip.frequency,
                        false
                    );

                    float[] samples = new float[fragmentLength * originalClip.channels];
                    originalClip.GetData(samples, startSample);
                    fragmentClip.SetData(samples, 0);

                    audioSource.clip = fragmentClip;
                }
                else
                {
                    audioSource.clip = originalClip;
                }
            }
        }

        // Small delay before playing
        yield return new WaitForSeconds(0.5f);

        audioSource.volume = !_PlayAudio ? 0 : 1;
        audioSource.Play();

        // If we have an actual JSON timings URL, show real highlighting
        if (!string.IsNullOrEmpty(textURL))
        {
            if (audioSource.isPlaying)
                dtWasPlaying = DateTime.Now;

            // Highlight loop
            while (audioSource.isPlaying)
            {
                UpdateHighlightedText(audioSource.time * 1000 - 500); // offset in ms
                yield return null;
            }

            // Reset highlight once audio finishes
            UpdateHighlightedText(0, false);
            Debug.Log("Audio has stopped playing.");
            OnAudioFinished?.Invoke();

            // Conditionally trigger next step
            if (triggerNextStep)
            {
                StartCoroutine(WaitAndTriggerNextStep());
            }
        }
        else
        {
            // Static text scenario
            UpdateHighlightedText(0, false);
            Debug.Log("Static text – no JSON timings used.");
            OnAudioFinished?.Invoke();

            if (triggerNextStep)
            {
                StartCoroutine(WaitAndTriggerNextStep());
            }
        }
    }

    private string GetPageFromTextContent(string content, int pageNum)
    {
        string[] pages = content.Split(new string[] { "###" }, StringSplitOptions.None);
        if (pageNum >= 0 && pageNum < pages.Length)
        {
            return pages[pageNum].Trim();
        }

        return "";
    }

    private IEnumerator WaitAndTriggerNextStep()
    {
        yield return new WaitForSeconds(0.5f);
        OnAutoNextStep?.Invoke();
    }


    private static void AddToCache(string audioURL, AudioAndTextStruct audioAndTextStruct)
    {
        if (CacheAudioAndTimingsStructs.Count >= MaxAudioCacheSize)
        {
            // Remove the oldest entry
            CacheAudioAndTimingsStructs.RemoveAt(0);
        }

        CacheAudioAndTimingsStructs[audioURL] = audioAndTextStruct;
    }


    private void ParseTimings(AudioAndTextStruct audioAndTextStruct, int pageNum)
    {
        if (pageNum != -1)
        {
            string content = audioAndTextStruct.content;
            String page = GetPageFromTextContent(content, pageNum);
            var singleChunk = new JSONArray();
            JSONNode singleNode = new JSONObject();
            singleNode["word"] = page;
            singleNode["time"] = 0.0f;
            singleChunk.Add(singleNode);
            audioAndTextStruct.jsonNodeTimings = singleChunk;
        }

        JSONNode timings = audioAndTextStruct.jsonNodeTimings;
        if (timings == null) return;

        currentWordTimings = new List<WordTiming>();
        foreach (JSONNode timing in timings)
        {
            WordTiming wt = new WordTiming
            {
                Word = timing["word"].Value,
                Time = timing["time"].AsFloat
            };
            currentWordTimings.Add(wt);
        }
    }

    private bool IsWordPunctuation(int i)
    {
        if (i < 0 || i >= currentWordTimings.Count)
            return false;

        string w = currentWordTimings[i].Word.Trim();
        return (w.Length == 1 && Char.IsPunctuation(w[0]));
    }

    private void UpdateHighlightedText(float currentAudioTime, bool bHilight = true)
    {
        if (currentWordTimings == null || currentWordTimings.Count == 0)
            return;

        for (int i = currentWordIndex; i < currentWordTimings.Count; i++)
        {
            if (currentWordTimings[i].Time > currentAudioTime)
            {
                currentWordIndex = i;
                break;
            }
        }

        string newForegroundText = "";
        string newBackgroundText = "";
        for (int i = 0; i < currentWordTimings.Count; i++)
        {
            bool isHighlighted = (showHighlight && bHilight && i == currentWordIndex && !IsWordPunctuation(i));
            if (isHighlighted)
            {
                // Highlight the current word in both foreground and background
                newForegroundText += $"<color=#{hilightTextColor}>{currentWordTimings[i].Word}</color>";
                newBackgroundText += $"<mark=#{hilightBackColor}>{currentWordTimings[i].Word}</mark>";
            }
            else
            {
                newForegroundText += currentWordTimings[i].Word;
                newBackgroundText += currentWordTimings[i].Word;
            }
        }

        uiForeground.text = newForegroundText.TrimEnd();
        uiBackground.text = newBackgroundText.TrimEnd();
    }

    public void SetAudioTextHilightColors(string textColor, string backColor)
    {
        hilightTextColor = textColor;
        hilightBackColor = backColor;
    }
}