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
    public bool staticText = false;

    public string nextPlayUseVoice = "";
    public bool showHighlight = true;
    public bool audioNameGenerated = true;

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

    // True while a pre-generated prompt/narration clip is playing. Used by Mode B
    // (SpeechListenService) to gate the recognizer so the mic never hears the app's own prompt.
    public bool IsPlaying => audioSource != null && audioSource.isPlaying;

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

    // The reading-mode picker (UnifiedReadingModePicker) stages the next playback voice
    // ("human"/"computer"/"novoice"); PreparePlayVoiceSettings reads it each Play. The picker is
    // the single source of the playback voice.
    public void SetReadingVoice(string voiceName) { nextPlayUseVoice = (voiceName ?? "").ToLower(); }

    // Picker-driven Autopage: auto-advance to the next step when narration ends (drives the same
    // triggerNextStep the old autopage toggle did via nextStepToggle).
    public void SetAutopage(bool on) { triggerNextStep = on; }

    void PreparePlayVoiceSettings()
    {
        // Voice comes from the picker via SetReadingVoice (stored in nextPlayUseVoice). Empty →
        // the computer branch below, the same default as before when no voice was selected.
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

        // Computer/TTS names + the ?v={content_rev} cache-bust come from the shared TtsUrls
        // helper so the page-load path and the word-tap path can never drift (see word-tap region).
        var ttsUrls = TtsUrls(chunkname, currentVoicePostfix);
        string audioURL = ttsUrls.audio;
        string jsonTimingsURL = ttsUrls.timings;

        // If not generated by TTS or if we want a plain chunk
        if (!audioNameGenerated)
            audioURL = AppendContentRev($"{chunkname}.mp3");

        // If static text is true, ignore external JSON timings
        if (staticText)
            jsonTimingsURL = AppendContentRev($"{chunkname}.json");

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

        // ---- word-tap ---- load the per-book word bank on the side; a tapped word plays ONLY
        // from this bank (no per-page fallback). Lazy, once per book, never throws.
        MaybeLoadWordBank();
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

        // Instant text on page entry (esp. slow connections): paint the script-provided
        // page text immediately from `content`, before the audio/timings download. The real
        // word-timings replace this list as soon as they're parsed (ParseTimings on cache
        // hit, or after download), so the highlight upgrade needs no extra code. Resetting
        // currentWordTimings here also fixes a latent stale-text bug: if the timings fetch
        // fails, ParseTimings early-returns and the PREVIOUS page's words would otherwise
        // remain on screen — now the current page's text is always what shows.
        if (!string.IsNullOrEmpty(content))
        {
            currentWordTimings = new List<WordTiming> { new WordTiming { Word = content, Time = 0f } };
            currentWordIndex = 0;
            UpdateHighlightedText(0, false); // render plain, unhighlighted, no audio
        }

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
            if (!string.IsNullOrEmpty(audioURL) && !ReadAlongActive)
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
                                    uwr.downloadHandler.data, DiskCache.AudioBudgetBytes);
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
                                content, DiskCache.TimingsBudgetBytes);
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
            // Skip caching only on a real audio-fetch failure (non-empty URL but no
            // clip) so the download is retried on the next visit instead of being
            // negatively cached. The empty-audioURL static-text case still caches.
            if (string.IsNullOrEmpty(audioURL) || audioAndTextStruct.audioClip != null)
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

            // ---- read-along ---- When read-along owns this page, the child reads it aloud: skip
            // page narration AND the audio-driven highlight loop entirely, then hand off to the
            // ReadAlongService. Inert when ReadAlongActive is false (mode OFF) → the path below is
            // byte-identical. (Single guarded block; rest of the read-along code lives in the marked
            // region at the end of this file.)
            if (ReadAlongActive)
            {
                if (audioSource != null && audioSource.isPlaying) audioSource.Stop();
                OnReadAlongPageParsed();
                yield break;
            }

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
            else if (!string.IsNullOrEmpty(audioURL))
            {
                // Audio fetch failed for this page: clear any stale clip so we play
                // silence instead of replaying the previous page's narration.
                if (audioSource.clip != null && audioSource.clip.name.StartsWith("Fragment_"))
                {
                    Destroy(audioSource.clip);
                }
                audioSource.clip = null;
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
        if (string.IsNullOrEmpty(content)) return "";
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

    // ====================================================================================
    // ---- word-tap ----
    // Tap a word on a story page to hear just that word — played ONLY from the per-book word
    // bank (wordbank.mp3 + wordbank.json). If there is no bank, or the tapped word isn't in it,
    // nothing plays and the tap falls through to navigation. This region is fully additive: it
    // never reads or writes audioSource, currentWordTimings, currentWordIndex, or the highlight
    // loop — the tapped word plays on the dedicated _wordTapSource IN PARALLEL with the page
    // narration. Remove this region +
    // the MaybeLoadWordBank call in Play() + the SwipeDetector tap branch to fully revert.
    // ====================================================================================

    public bool wordTapEnabled = true;

    const float WordTapFadeInMs = 18f;   // fade up from silence so the exact-onset seek doesn't click
    const float WordTapFadeOutMs = 45f;  // fade before Stop to avoid a click

    [SerializeField] private AudioSource _wordTapSource; // dedicated source, separate from audioSource
    private Coroutine _wordTapStopCo;

    // ---- tap highlight (purely visual, decoupled from UpdateHighlightedText) ----
    // A brief flash behind the tapped word on its OWN overlay Image — never the TMP text/mesh, so
    // it can't be clobbered by the per-frame highlight string rebuild during parallel playback.
    [SerializeField] private float tapHighlightHoldSec = 0.35f;    // full-alpha hold before fading
    [SerializeField] private float tapHighlightFadeOutSec = 0.15f; // fade full→0 after the hold
    // Own color so a tapped word reads as distinct from the playback mark (hilightBackColor).
    // Soft warm yellow; alpha ~0.65 to match the mark's translucency.
    [SerializeField] private Color tapHighlightColor = new Color(0.96f, 0.88f, 0.55f, 0.65f);
    private Image _wordTapHighlight;        // lazily created, sibling just behind uiForeground
    private float _wordTapHighlightFullAlpha; // base alpha captured from tapHighlightColor
    private Coroutine _wordTapHighlightCo;

    // ---- word bank (OPTIONAL, per-book) ----
    // A book may ship a clean isolated-word recording (wordbank.mp3) + a word→[start,end] map
    // (wordbank.json). A tapped word plays its isolated slice from this bank — and nothing else.
    // Entirely optional: a book with no bank (or a word not in the bank) gets a silent tap that
    // passes through to navigation. Never shared across books — guarded per book by
    // _wordBankLoadedRev and cached by the FULL book URL (see MaybeLoadWordBank / section 2a).
    private AudioClip _wordBankClip;
    private Dictionary<string, (float start, float end)> _wordBankMap; // normalized word -> ms span
    private bool _wordBankReady;        // true only after both files loaded & parsed
    private string _wordBankLoadedRev;  // baseURL+"|"+contentRev we loaded, to load once per book

    // Computer-voice (TTS) relative URLs for a chunk, with the SAME rate/voice/cache-bust that
    // Play() needs. Play() calls this too so the two paths can't drift.
    public (string audio, string timings) TtsUrls(string chunkname, string voicePostfix)
    {
        string rev = Globals.g_prbook != null ? Globals.g_prbook.contentRev : "";
        return BuildTtsUrls(chunkname, Globals.getReadingRate(), voicePostfix, rev);
    }

    // Pure naming logic, factored out so EditMode tests can pin it down without Unity statics.
    public static (string audio, string timings) BuildTtsUrls(string chunkname, string rate,
        string voicePostfix, string rev)
    {
        string a = $"{chunkname}_{rate}{voicePostfix}.mp3";
        string t = $"{chunkname}_{rate}_timings{voicePostfix}.json";
        if (!string.IsNullOrEmpty(rev))
        {
            a += "?v=" + rev;
            t += "?v=" + rev;
        }
        return (a, t);
    }

    // Appends the owning book's content_rev as ?v= (matches the cache-bust Play() applies to the
    // human/static URL overrides). Empty rev → no-op.
    private static string AppendContentRev(string url)
    {
        string rev = Globals.g_prbook != null ? Globals.g_prbook.contentRev : "";
        if (!string.IsNullOrEmpty(rev))
            return url + "?v=" + rev;
        return url;
    }

    // ---- word bank load (OPTIONAL, once per book / content_rev) ----

    // Loads the per-book word bank lazily the first time a book (baseURL) / content_rev is seen.
    // Setting _wordBankLoadedRev up-front means a missing bank is fetched at most once — a 404 is
    // NOT retried on every tap. Any failure leaves _wordBankReady=false so taps stay silent and
    // pass through to navigation. Never throws.
    private void MaybeLoadWordBank()
    {
        string rev = Globals.g_prbook != null ? Globals.g_prbook.contentRev : "";
        string key = baseURL + "|" + rev;
        if (key == _wordBankLoadedRev) return;

        // Mark this book as "attempted" before the async load so a missing file isn't re-fetched
        // on every subsequent tap, and clear any previous book's bank so it can't be reused.
        _wordBankLoadedRev = key;
        _wordBankClip = null;
        _wordBankMap = null;
        _wordBankReady = false;

        if (string.IsNullOrEmpty(baseURL)) return;

        // Cache + fetch by the FULL book URL so two books (or two revs) can never share a cache
        // entry even though the filename is identical across books (section 2a).
        string suffix = string.IsNullOrEmpty(rev) ? "" : "?v=" + rev;
        Runnable.Run(LoadWordBankAssets(
            baseURL + "wordbank.mp3" + suffix,
            baseURL + "wordbank.json" + suffix));
    }

    // Fetches wordbank.mp3 + wordbank.json via the existing DiskCache → UnityWebRequest path,
    // keyed by the full URL. Any miss/404/parse failure returns quietly with _wordBankReady=false.
    private IEnumerator LoadWordBankAssets(string audioURL, string jsonURL)
    {
        AudioClip clip = null;

        // --- Audio: disk cache first, then network ---
        string diskAudioPath = DiskCache.PathFor(audioURL, "audio", ".mp3");
        if (System.IO.File.Exists(diskAudioPath))
        {
            using (UnityWebRequest diskReq = UnityWebRequestMultimedia.GetAudioClip(
                "file://" + diskAudioPath, AudioType.MPEG))
            {
                yield return diskReq.SendWebRequest();
                if (diskReq.result == UnityWebRequest.Result.Success)
                    clip = DownloadHandlerAudioClip.GetContent(diskReq);
            }
        }

        if (clip == null)
        {
            using (UnityWebRequest uwr = UnityWebRequestMultimedia.GetAudioClip(audioURL, AudioType.MPEG))
            {
                uwr.timeout = 60;
                yield return uwr.SendWebRequest();
                if (uwr.result == UnityWebRequest.Result.Success)
                {
                    clip = DownloadHandlerAudioClip.GetContent(uwr);
                    if (clip != null)
                        DiskCache.WriteBytes(audioURL, "audio", ".mp3",
                            uwr.downloadHandler.data, DiskCache.AudioBudgetBytes);
                }
            }
        }

        if (clip == null)
        {
            // No bank audio for this book — expected for every book we haven't regenerated.
            Debug.Log($"[WB] no audio (url={audioURL})"); // TEMP [WB]
            yield break; // _wordBankReady stays false
        }

        // --- JSON: disk cache first, then network ---
        string text = DiskCache.TryReadText(jsonURL, "timings", ".json");
        if (text == null)
        {
            using (UnityWebRequest www = UnityWebRequest.Get(jsonURL))
            {
                www.timeout = 20;
                yield return www.SendWebRequest();
                if (www.result == UnityWebRequest.Result.Success)
                {
                    text = www.downloadHandler.text;
                    DiskCache.WriteText(jsonURL, "timings", ".json", text, DiskCache.TimingsBudgetBytes);
                }
            }
        }
        if (string.IsNullOrEmpty(text))
        {
            Debug.Log($"[WB] no json (url={jsonURL})"); // TEMP [WB]
            yield break; // audio without a map → no bank, taps stay silent
        }

        var map = ParseWordBank(text);
        if (map == null || map.Count == 0)
        {
            Debug.Log($"[WB] json failed/empty (url={jsonURL})"); // TEMP [WB]
            yield break; // malformed/empty → no bank
        }

        _wordBankClip = clip;
        _wordBankMap = map;
        _wordBankReady = true;
        Debug.Log($"[WB] READY words={map.Count}"); // TEMP [WB]
    }

    // Parses wordbank.json into a normalized word → (start,end) ms map. Tolerant of either an
    // array of {word,start,end} objects or a {word:{start,end}} / {word:[start,end]} object. Returns
    // null on any malformed input — callers treat null as "no bank". Never throws.
    public static Dictionary<string, (float start, float end)> ParseWordBank(string json)
    {
        if (string.IsNullOrEmpty(json)) return null;
        try
        {
            JSONNode root = JSON.Parse(json);
            if (root == null) return null;

            // Entries may be wrapped under a "words" key alongside schema/audio metadata; select
            // that node so we iterate the word entries and not the wrapper's metadata keys.
            JSONNode src = (root.IsObject && root.HasKey("words")) ? root["words"] : root;

            var map = new Dictionary<string, (float start, float end)>();

            if (src.IsArray)
            {
                foreach (JSONNode n in src)
                {
                    string w = NormalizeWord(n["word"].Value);
                    if (w.Length == 0) continue;
                    float start = n.HasKey("start") ? n["start"].AsFloat : n["time"].AsFloat;
                    float end = n.HasKey("end") ? n["end"].AsFloat : -1f;
                    map[w] = (start, end);
                }
            }
            else if (src.IsObject)
            {
                foreach (KeyValuePair<string, JSONNode> kv in src.Linq)
                {
                    string w = NormalizeWord(kv.Key);
                    if (w.Length == 0) continue;
                    JSONNode v = kv.Value;
                    float start, end;
                    if (v.IsArray)
                    {
                        start = v.Count > 0 ? v[0].AsFloat : 0f;
                        end = v.Count > 1 ? v[1].AsFloat : -1f;
                    }
                    else
                    {
                        start = v.HasKey("start") ? v["start"].AsFloat : v["time"].AsFloat;
                        end = v.HasKey("end") ? v["end"].AsFloat : -1f;
                    }
                    map[w] = (start, end);
                }
            }

            return map.Count > 0 ? map : null;
        }
        catch
        {
            return null; // malformed → no bank, never throw
        }
    }

    // Screen-pos tap entry point. Hit-tests the OWN foreground TMP (the layer this component owns)
    // at the given screen position and, on a hit, plays that word via TryPlayWord. Returns false
    // when word-tap is off, there's no foreground, or no word is under the point.
    public bool TryPlayWordAtScreenPos(Vector2 screenPos)
    {
        // Read-along (Mode A) has no echo-gate: a wordbank slice played here would be heard by the
        // live mic and wrongly advance the matcher cursor — so word-tap is off while it owns the page.
        if (!wordTapEnabled || ReadAlongActive || uiForeground == null) return false;
        // Overlay canvases hit-test with a null camera; others use their world/event camera.
        Camera cam = null;
        Canvas c = uiForeground.canvas;
        if (c != null && c.renderMode != RenderMode.ScreenSpaceOverlay) cam = c.worldCamera;
        int wi = TMP_TextUtilities.FindIntersectingWord(uiForeground, screenPos, cam);
        if (wi < 0) return false;
        // Flash the tapped word independently of TryPlayWord, so even a word not in the bank flashes.
        FlashTapHighlight(wi);
        return TryPlayWord(uiForeground.textInfo.wordInfo[wi].GetWord());
    }

    // Tap entry point. Plays the tapped word ONLY from the per-book word bank: returns true (so the
    // caller consumes the tap) when the bank is loaded and contains the normalized word; otherwise
    // returns false and plays nothing, letting navigation handle the tap.
    public bool TryPlayWord(string wordText)
    {
        if (!wordTapEnabled || !_wordBankReady || _wordBankClip == null || _wordBankMap == null)
            return false;

        string nw = NormalizeWord(wordText);
        Debug.Log($"[WB] tap '{wordText}' -> '{nw}' ready={_wordBankReady} inBank={(nw.Length > 0 && _wordBankMap.ContainsKey(nw))}"); // TEMP [WB]

        if (nw.Length == 0 || !_wordBankMap.TryGetValue(nw, out var span)) return false;

        // Guard a degenerate map entry: an end not after start runs to the clip end.
        float endMs = span.end > span.start ? span.end : _wordBankClip.length * 1000f;
        PlaySlice(_wordBankClip, span.start, endMs);
        return true; // played → caller consumes the tap
    }

    // Word-bank slice playback: seek the dedicated _wordTapSource to the clamped start, then run the
    // fade-in → play-to-end → fade-out → restore-volume coroutine. Plays IN PARALLEL with the page
    // narration (it never touches audioSource). No-op on a null clip/source.
    private void PlaySlice(AudioClip clip, float startMs, float endMs)
    {
        if (clip == null || _wordTapSource == null) return;

        float clipLen = clip.length;

        if (_wordTapStopCo != null) StopCoroutine(_wordTapStopCo);
        _wordTapSource.clip = clip;
        // Seek to the exact onset (clamped). No lead-in — fades at start/stop replace it,
        // killing the seek/Stop clicks without pulling in neighbouring audio.
        _wordTapSource.time = Mathf.Clamp(startMs / 1000f, 0f, Mathf.Max(0f, clipLen - 0.001f));
        _wordTapStopCo = StartCoroutine(PlayWordTapSlice(endMs / 1000f));
    }

    // Plays the already-seeked _wordTapSource up to endSec, fading the volume up from silence at
    // the start and back down before Stop(), then RESTORING the captured target volume so the next
    // tap isn't left muted.
    private IEnumerator PlayWordTapSlice(float endSec)
    {
        if (_wordTapSource == null) { _wordTapStopCo = null; yield break; }

        float targetVolume = _wordTapSource.volume;

        // Fade in from silence so the exact-onset seek doesn't pop.
        _wordTapSource.volume = 0f;
        _wordTapSource.Play();
        float fadeInSec = WordTapFadeInMs / 1000f;
        for (float t = 0f; t < fadeInSec && _wordTapSource != null; t += Time.unscaledDeltaTime)
        {
            _wordTapSource.volume = Mathf.Lerp(0f, targetVolume, t / fadeInSec);
            yield return null;
        }
        if (_wordTapSource != null) _wordTapSource.volume = targetVolume;

        // Play out to the slice end.
        while (_wordTapSource != null && _wordTapSource.isPlaying && _wordTapSource.time < endSec)
            yield return null;

        if (_wordTapSource != null)
        {
            // Fade to 0 before Stop to avoid a click, then restore the target volume.
            float fadeOutSec = WordTapFadeOutMs / 1000f;
            for (float t = 0f; t < fadeOutSec && _wordTapSource != null; t += Time.unscaledDeltaTime)
            {
                _wordTapSource.volume = Mathf.Lerp(targetVolume, 0f, t / fadeOutSec);
                yield return null;
            }
            if (_wordTapSource != null)
            {
                _wordTapSource.Stop();
                _wordTapSource.volume = targetVolume;
            }
        }
        _wordTapStopCo = null;
    }

    // Lazily create the single reusable overlay quad behind the readable text. It is a SIBLING of
    // uiForeground inserted just BEHIND it (lower sibling index → drawn first), with raycastTarget
    // off so it never steals taps, and disabled by default. Its base color (incl. alpha) comes from
    // hilightBackColor; that base alpha is captured as the "full" alpha for the flash.
    private void EnsureTapHighlight()
    {
        if (_wordTapHighlight != null || uiForeground == null) return;

        var parent = uiForeground.rectTransform.parent as RectTransform;
        if (parent == null) return;

        var go = new GameObject("WordTapHighlight", typeof(RectTransform), typeof(Image));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.SetSiblingIndex(uiForeground.rectTransform.GetSiblingIndex()); // just behind the text
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);

        var img = go.GetComponent<Image>();
        img.raycastTarget = false;
        _wordTapHighlightFullAlpha = tapHighlightColor.a; // captured so the fade restores full alpha
        img.color = tapHighlightColor;
        img.enabled = false;

        _wordTapHighlight = img;
    }

    // Flash a highlight behind word `wordIndex`: size/position the overlay to the word's local-space
    // bounds (computed from its visible character verts — a word stays on one line), snap it to full
    // alpha, and (re)start the single fade coroutine. Purely visual; never touches the TMP strings.
    private void FlashTapHighlight(int wordIndex)
    {
        if (uiForeground == null) return;
        var ti = uiForeground.textInfo;
        if (ti == null || wordIndex < 0 || wordIndex >= ti.wordCount) return;

        var wInfo = ti.wordInfo[wordIndex];
        int first = wInfo.firstCharacterIndex;
        int last = wInfo.lastCharacterIndex;
        if (first < 0 || last < 0 || last >= ti.characterCount) return;

        // Word bounds in uiForeground's local space: min bottomLeft / max topRight over the chars.
        float minX = float.MaxValue, minY = float.MaxValue;
        float maxX = float.MinValue, maxY = float.MinValue;
        bool any = false;
        for (int i = first; i <= last; i++)
        {
            var ci = ti.characterInfo[i];
            if (!ci.isVisible) continue;
            minX = Mathf.Min(minX, ci.bottomLeft.x);
            minY = Mathf.Min(minY, ci.bottomLeft.y);
            maxX = Mathf.Max(maxX, ci.topRight.x);
            maxY = Mathf.Max(maxY, ci.topRight.y);
            any = true;
        }
        if (!any) return;

        EnsureTapHighlight();
        if (_wordTapHighlight == null) return;

        // The verts are in uiForeground's local space. The overlay anchors to its parent's centre,
        // so place it by world position (TransformPoint maps foreground-local → world) and size it
        // to the word box (sizeDelta == size since anchorMin == anchorMax).
        var rt = _wordTapHighlight.rectTransform;
        rt.sizeDelta = new Vector2(maxX - minX, maxY - minY);
        Vector3 centerLocal = new Vector3((minX + maxX) * 0.5f, (minY + maxY) * 0.5f, 0f);
        rt.position = uiForeground.rectTransform.TransformPoint(centerLocal);

        // Snap to full alpha and re-arm the single coroutine so rapid taps restart rather than stack.
        Color c = _wordTapHighlight.color;
        c.a = _wordTapHighlightFullAlpha;
        _wordTapHighlight.color = c;
        _wordTapHighlight.enabled = true;

        if (_wordTapHighlightCo != null) StopCoroutine(_wordTapHighlightCo);
        _wordTapHighlightCo = StartCoroutine(FadeTapHighlight());
    }

    // Hold the flash at full alpha, fade it out, then disable the overlay and RESTORE full alpha so
    // the next tap starts solid. Uses unscaledDeltaTime to match the audio fades.
    private IEnumerator FadeTapHighlight()
    {
        for (float t = 0f; t < tapHighlightHoldSec; t += Time.unscaledDeltaTime)
            yield return null;

        for (float t = 0f; t < tapHighlightFadeOutSec && _wordTapHighlight != null; t += Time.unscaledDeltaTime)
        {
            Color c = _wordTapHighlight.color;
            c.a = Mathf.Lerp(_wordTapHighlightFullAlpha, 0f, t / tapHighlightFadeOutSec);
            _wordTapHighlight.color = c;
            yield return null;
        }

        if (_wordTapHighlight != null)
        {
            _wordTapHighlight.enabled = false;
            Color c = _wordTapHighlight.color;
            c.a = _wordTapHighlightFullAlpha;
            _wordTapHighlight.color = c;
        }
        _wordTapHighlightCo = null;
    }

    // Normalize a token to a word-bank key. MUST match SceneForge: lowercase, then trim leading and
    // trailing characters that are not [a-z0-9'] (apostrophes inside a word are kept). Punctuation /
    // whitespace-only / empty input → "".
    public static string NormalizeWord(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        s = s.ToLowerInvariant();
        int a = 0, b = s.Length - 1;
        while (a <= b && !IsWordBankChar(s[a])) a++;
        while (b >= a && !IsWordBankChar(s[b])) b--;
        return a <= b ? s.Substring(a, b - a + 1) : "";
    }

    private static bool IsWordBankChar(char c)
    {
        return (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '\'';
    }

    // ====================================================================================
    // ---- read-along ----
    // Mode A read-along hooks. The child reads the page aloud; ReadAlongService (the single owner)
    // runs Recognissimo + the validated matcher and calls SetReadProgress(cursor) to advance the
    // highlight WITH NO PAGE AUDIO PLAYING. Fully additive + opt-in: every member here is inert
    // unless ReadAlongActive is set true by the service, and the audio-driven highlight path
    // (UpdateHighlightedText) is never modified. Rollback = delete this region + the one guarded
    // block in LoadAudioAndTimings + ReadAlongService + the toggle.
    // ====================================================================================

    // True only while the read-along service owns the current page. Gates every hook below and the
    // one guarded branch in LoadAudioAndTimings. False (default) → the app behaves exactly as today.
    public bool ReadAlongActive { get; set; }

    // Fired (only when ReadAlongActive) once a page's timings are parsed and its words are ready, so
    // the service can (re)start recognition with this page's grammar. Page audio is NOT played.
    public event Action<AudioAndTextPlayer> ReadAlongPageReady;

    // The current page's normalized words (reading order) + the map from each word ordinal to its
    // token index in currentWordTimings (which also holds the space/punctuation tokens). Rebuilt
    // whenever currentWordTimings is replaced (i.e. per page).
    private List<string> _expectedWords;
    private List<int> _expectedWordTokenIndex;
    private List<int> _expectedSentenceEnd;   // ascending expected-word indices that end a sentence
    private List<WordTiming> _expectedWordsBuiltFrom;
    private static readonly char[] ReadAlongSentenceEnders = { '.', '!', '?' };

    private void EnsureExpectedWords()
    {
        if (ReferenceEquals(_expectedWordsBuiltFrom, currentWordTimings) && _expectedWords != null)
            return;

        _expectedWords = new List<string>();
        _expectedWordTokenIndex = new List<int>();
        _expectedSentenceEnd = new List<int>();
        if (currentWordTimings != null)
        {
            for (int i = 0; i < currentWordTimings.Count; i++)
            {
                string nw = NormalizeWord(currentWordTimings[i].Word);
                if (nw.Length > 0)
                {
                    _expectedWords.Add(nw);
                    _expectedWordTokenIndex.Add(i);
                }
            }

            // An expected word ends a sentence if any raw token from it up to (but not including) the
            // next expected word contains [.!?] — punctuation may be attached to the word or a
            // separate token. Derived from the raw token text (the normalized words drop punctuation).
            for (int k = 0; k < _expectedWordTokenIndex.Count; k++)
            {
                int ti = _expectedWordTokenIndex[k];
                int nextTi = (k + 1 < _expectedWordTokenIndex.Count)
                    ? _expectedWordTokenIndex[k + 1] : currentWordTimings.Count;
                for (int t = ti; t < nextTi; t++)
                {
                    string w = currentWordTimings[t].Word;
                    if (!string.IsNullOrEmpty(w) && w.IndexOfAny(ReadAlongSentenceEnders) >= 0)
                    {
                        _expectedSentenceEnd.Add(k);
                        break;
                    }
                }
            }
        }
        _expectedWordsBuiltFrom = currentWordTimings;
    }

    // The current page's normalized words, in reading order. Empty until a page is loaded.
    public IReadOnlyList<string> ExpectedWords
    {
        get { EnsureExpectedWords(); return _expectedWords; }
    }

    // Ascending expected-word indices immediately followed by sentence-ending punctuation in the raw
    // page text. Used by ReadAlongService's sentence-tail flush. Empty until a page is loaded.
    public IReadOnlyList<int> ExpectedSentenceEndIndices
    {
        get { EnsureExpectedWords(); return _expectedSentenceEnd; }
    }

    // Read-along progress: wordIndex = number of words read so far (the matcher cursor). Highlights
    // the word at that ordinal (clamped) via the SAME rich-text render the audio path uses, but
    // driven by this index instead of audio time. No-op unless ReadAlongActive; never touches audio.
    public void SetReadProgress(int wordIndex)
    {
        if (!ReadAlongActive) return;
        EnsureExpectedWords();
        if (_expectedWordTokenIndex == null || _expectedWordTokenIndex.Count == 0) return;

        int ordinal = Mathf.Clamp(wordIndex, 0, _expectedWordTokenIndex.Count - 1);
        currentWordIndex = _expectedWordTokenIndex[ordinal];
        RenderReadAlongHighlight();
    }

    // Stop any page narration + the audio-driven highlight coroutine so they can't fight the
    // recognition-driven highlight. Used when read-along is switched on for an already-loaded page.
    public void StopPageAudioForReadAlong()
    {
        if (!ReadAlongActive) return;
        StopAllCoroutines();
        if (audioSource != null && audioSource.isPlaying) audioSource.Stop();
    }

    // Index-driven highlight render. Mirrors UpdateHighlightedText's render block (the audio path is
    // left untouched) but does NO audio-time search, so it renders with no page audio playing.
    private void RenderReadAlongHighlight()
    {
        if (currentWordTimings == null || currentWordTimings.Count == 0) return;

        string newForegroundText = "";
        string newBackgroundText = "";
        for (int i = 0; i < currentWordTimings.Count; i++)
        {
            // Read-along ALWAYS shows the highlight: drop the showHighlight gate (set by the audio
            // voice mode) — this method only runs when ReadAlongActive.
            bool isHighlighted = (i == currentWordIndex && !IsWordPunctuation(i));
            if (isHighlighted)
            {
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

    // Called from the one guarded block in LoadAudioAndTimings (read-along only): render the page
    // text (highlight reset to the first word) and notify the service its words are ready.
    private void OnReadAlongPageParsed()
    {
        currentWordIndex = 0;
        RenderReadAlongHighlight();
        ReadAlongPageReady?.Invoke(this);
    }
}