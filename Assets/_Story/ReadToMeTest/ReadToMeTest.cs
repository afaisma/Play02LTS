using System.Collections.Generic;
using System.Linq;
using System.Text;
using Recognissimo;
using Recognissimo.Components;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// ============================================================================================
// Read-to-Me (Recognissimo) feasibility harness — SELF-CONTAINED, fully reversible.
//
// Drop this single component on one GameObject in an otherwise-empty scene. It builds the entire
// UI and the Recognissimo speech stack at runtime (no Inspector wiring needed). Press Start, read
// the page aloud, and watch words turn green as they are recognized — the actual test of whether
// child-read speech can drive page progress, and how much the page-restricted vocabulary helps.
//
// Touches nothing else: no PRScript / AudioAndTextPlayer / Globals / existing-scene references.
// Revert by deleting the Assets/_Story/ReadToMeTest/ folder and removing the scene from Build
// Settings. The Vosk model under StreamingAssets/LanguageModels/en-US is used read-only.
// ============================================================================================
public class ReadToMeTest : MonoBehaviour
{
    [TextArea(6, 20)]
    [Tooltip("The text the child is expected to read. Editable in the Inspector.")]
    public string expectedText =
        "The storm turned the sky dark. The horses are still out, cried Mark.\nHe ran hard across the yard. The barn door was stuck fast.\nMark pulled and pulled until it turned. One by one, the horses went in.\nYou are safe now, he said softly.";

    [Tooltip("Start with the recognizer restricted to the page's words (vs. free recognition).")]
    public bool restrictVocabularyToPage = true;

    [Tooltip("Optional pre-recorded read (e.g. ElevenLabs the_sun_is_up.mp3) for an identical, " +
             "repeatable run. Used only when the 'Use clip' toggle is on. Must be MONO.")]
    [SerializeField] private AudioClip testClip;

    // Strong green wrap for words that have been read (high contrast on the light panel).
    private const string ReadColorHex = "#1A7F37";

    // Smart matcher (validated on real recorded audio + the app's Vosk model: 46/46 on the clean
    // read, the repetitions read, and a missed-content-word stall). Forward-only, lookahead 2 with
    // stopword-safe skips, plus a WIDE re-sync once we've been stuck for a few words (real Vosk drops/
    // inserts words, so confirmation-by-next-word fails — a stall recovery is what works). Gated by
    // the "Smart matcher" toggle (off = plain lookahead: first look 0..2 match wins, no suppression).
    private bool smartMatcher = true;
    private int _stuck;                              // consecutive recognized words with no advance
    [SerializeField] private int stallK = 3;         // stuck-count that arms the wide re-sync
    [SerializeField] private int wideLook = 6;       // re-sync scans look = 3..wideLook

    // Common words a look>0 skip must not leapfrog onto (a re-read "the" shouldn't jump the cursor to
    // a later "the"); ignored by the stall re-sync, which is a recovery path.
    private static readonly HashSet<string> STOPWORDS = new()
    {
        "the", "a", "an", "is", "are", "was", "were", "and", "or", "of", "to", "in", "on", "it",
        "he", "she", "you", "we", "they", "i", "up", "by", "at", "as", "be", "his", "her",
        "that", "this", "one", "now"
    };

    // ---- expected-text model ----
    private struct Token
    {
        public string raw;     // verbatim slice (word run or whitespace run) for re-rendering
        public int wordIndex;  // index into _expectedNorm, or -1 if this token is not a counted word
    }

    private readonly List<Token> _tokens = new();
    private readonly List<string> _expectedNorm = new(); // ordered normalized words to be read
    private int _cursor;                                 // # of words read so far (forward-only)

    // ---- per-line dynamic window (A/B vs full-page grammar) ----
    // Each normalized word is mapped to a sentence/line number; the active grammar is just the
    // current line's words + the first windowSlack words of the next line + "[unk]" (kept small).
    // The recognizer is cheaply re-windowed at each line change (Stop → set Vocabulary → Start), with
    // the model kept loaded. Behind the "Dynamic window" toggle (OFF = full-page grammar, today).
    [SerializeField] private int windowSlack = 2;
    private bool dynamicWindow = false;
    private readonly List<int> _wordLine = new();                       // word ordinal -> line number
    private readonly List<(int start, int endEx)> _lineRanges = new();  // per-line [start,end) range
    private int _lineCount;
    private int _lastWindowedLine = -1;                                 // guard: re-window on change only

    // ---- sentence-tail flush (additive; does NOT touch Feed) ----
    // Vosk commits each phrase's final word late (it lands only when the NEXT word is recognized), so
    // a sentence's trailing 1-2 words highlight a sentence late and the page's final words never do.
    // On a short pause, flush the cursor past the upcoming sentence end so those tail words light up.
    [SerializeField] private float sentenceFlushDelay = 0.35f;
    [SerializeField] private int sentenceTailWords = 2;
    private readonly List<int> _sentenceEndIdx = new();  // ascending word indices ending a sentence
    private float _lastAdvanceTime;                       // Time.time of the last cursor advance
    private string _lastPartialText = "";                 // last partial seen in OnPartial
    private int _partialLenAtAdvance;                     // partial length captured at the last advance

    // ---- recognition ----
    private SpeechRecognizer _recognizer;
    private MicrophoneSpeechSource _micSource;
    private AudioListenerSpeechSource _listenerSource; // real-time clip capture (built if testClip set)
    private AudioSource _clipAudioSource;              // plays testClip at normal speed for capture
    private bool _pendingClipPlay;                     // play the clip once the recognizer is listening
    private bool _running;
    private bool _useClip;
    private string _latestRecognized = "";

    // ---- stats (per run) ----
    private int _unkCount;
    private float _startTime;
    private float _stopTime;
    private bool _summaryPrinted;

    // ---- UI ----
    private TMP_Text _pageText;
    private TMP_Text _statusText;
    private TMP_Text _logText;     // append-mode results readout (per-word conf + N-best + summary)
    private TMP_Text _partialText; // single live line for the latest partial
    private TMP_Text _buttonLabel;
    private Toggle _vocabToggle;
    private Toggle _clipToggle;
    private Toggle _smartToggle;
    private Toggle _dynamicToggle;
    private readonly List<string> _logLines = new();
    private const int MaxLogLines = 200;

    private void Start()
    {
        // Ensure the scene has an AudioListener — required so AudioListenerSpeechSource can capture
        // real-time clip playback (and it quiets the "no audio listeners" warning).
        if (FindObjectOfType<AudioListener>() == null)
            gameObject.AddComponent<AudioListener>();

        BuildExpected();
        BuildUI();
        BuildSpeechStack();
        RenderPage();
        RenderStatus();
    }

    // ---------------------------------------------------------------- expected text → tokens

    // Split expectedText into alternating whitespace / word runs. Each word run is normalized; a
    // non-empty normalization becomes the next counted expected word. Pure-punctuation runs stay
    // visible but are never counted or colored.
    private void BuildExpected()
    {
        _tokens.Clear();
        _expectedNorm.Clear();
        _wordLine.Clear();
        _lineRanges.Clear();
        _sentenceEndIdx.Clear();
        _cursor = 0;

        string t = expectedText ?? "";
        int i = 0;
        int lineNo = 0;
        while (i < t.Length)
        {
            int j = i;
            bool ws = char.IsWhiteSpace(t[i]);
            while (j < t.Length && char.IsWhiteSpace(t[j]) == ws) j++;
            string raw = t.Substring(i, j - i);

            int wordIndex = -1;
            if (!ws)
            {
                bool endsSentence = raw.IndexOfAny(SentenceEnders) >= 0;
                string norm = NormalizeWord(raw);
                if (norm.Length > 0)
                {
                    wordIndex = _expectedNorm.Count;
                    _expectedNorm.Add(norm);
                    _wordLine.Add(lineNo); // this word belongs to the current sentence/line
                    // This word is immediately followed by sentence-ending punctuation in the raw text.
                    if (endsSentence) _sentenceEndIdx.Add(wordIndex);
                }
                // A sentence boundary [.!?] inside this token starts a new line for the NEXT word.
                if (endsSentence) lineNo++;
            }
            _tokens.Add(new Token { raw = raw, wordIndex = wordIndex });
            i = j;
        }

        // Contiguous per-line word ranges (lines are monotonic; an empty line yields an empty range).
        _lineCount = _wordLine.Count > 0 ? _wordLine[_wordLine.Count - 1] + 1 : 0;
        int k = 0;
        for (int ln = 0; ln < _lineCount; ln++)
        {
            int start = k;
            while (k < _wordLine.Count && _wordLine[k] == ln) k++;
            _lineRanges.Add((start, k));
        }
    }

    private static readonly char[] SentenceEnders = { '.', '!', '?' };

    // ---------------------------------------------------------------- matching

    // Feed recognized text (partial or final) through the forward-only matcher. For each recognized
    // word: (1) normal window — scan exp[cursor+look] for look=0..2; look==0 advances, a look>0 skip
    // advances unless it lands on a stopword (suppressed). (2) If still stuck for stallK words, WIDE
    // re-sync — scan look=3..wideLook and take the first match (ignoring stopword suppression) to
    // recover from a missed content word. [unk]/unk ignored; never moves backward. Smart-matcher OFF
    // = plain lookahead (first look 0..2 match wins, no suppression, no re-sync).
    private void Feed(string recognized, bool isFinal)
    {
        if (string.IsNullOrEmpty(recognized)) return;

        foreach (string raw in recognized.Split((char[])null, System.StringSplitOptions.RemoveEmptyEntries))
        {
            string nw = NormalizeWord(raw);
            if (nw.Length == 0 || nw == "unk") continue; // [unk]/unk ignored

            int cursorBefore = _cursor;
            bool advanced = false;

            // 1) Normal window, lookahead 2 (stopword-safe on skips when smart).
            for (int look = 0; look <= 2; look++)
            {
                int idx = _cursor + look;
                if (idx >= _expectedNorm.Count) break;
                if (_expectedNorm[idx] != nw) continue;

                if (look > 0 && smartMatcher && STOPWORDS.Contains(nw))
                {
                    Log($"[SKIP-SUPPRESSED] '{nw}' look{look} exp[{idx}]");
                    continue; // don't leapfrog onto a later common word; keep scanning
                }
                _cursor = idx + 1;
                Log($"[ADV {(isFinal ? "fin" : "par")}] '{nw}' look{look} exp[{idx}]='{_expectedNorm[idx]}' cursor {cursorBefore}→{_cursor}");
                advanced = true;
                break;
            }

            // 2) Stall re-sync (smart only): after stallK stuck words, scan a wider window and take
            // the first match — ignore stopword suppression here, it's a stall recovery.
            if (!advanced && smartMatcher && _stuck >= stallK)
            {
                for (int look = 3; look <= wideLook; look++)
                {
                    int idx = _cursor + look;
                    if (idx >= _expectedNorm.Count) break;
                    if (_expectedNorm[idx] != nw) continue;
                    _cursor = idx + 1;
                    Log($"[STALL-RESYNC] '{nw}' look{look} exp[{idx}] cursor {cursorBefore}→{_cursor}");
                    advanced = true;
                    break;
                }
            }

            _stuck = advanced ? 0 : _stuck + 1;

            // Only log misses on final results — partial results would spam the same misses.
            if (!advanced && isFinal)
            {
                string w0 = WindowWord(cursorBefore);
                string w1 = WindowWord(cursorBefore + 1);
                string w2 = WindowWord(cursorBefore + 2);
                Log($"[NO fin] '{nw}' @{cursorBefore} window=[{w0},{w1},{w2}]");
            }
        }
    }

    // The expected word at index i, or "-" past the end (for the [NO fin] miss window).
    private string WindowWord(int i)
    {
        return (i >= 0 && i < _expectedNorm.Count) ? _expectedNorm[i] : "-";
    }

    // ---------------------------------------------------------------- rendering

    private void RenderPage()
    {
        if (_pageText == null) return;

        var sb = new StringBuilder(expectedText.Length + 64);
        foreach (var tk in _tokens)
        {
            bool read = tk.wordIndex >= 0 && tk.wordIndex < _cursor;
            if (read)
                sb.Append("<color=").Append(ReadColorHex).Append('>').Append(tk.raw).Append("</color>");
            else
                sb.Append(tk.raw);
        }
        _pageText.text = sb.ToString();
    }

    private void RenderStatus()
    {
        if (_statusText == null) return;

        string mode = (_vocabToggle != null && _vocabToggle.isOn) ? "vocab: page-restricted" : "vocab: free";
        string matcher = smartMatcher ? "matcher: smart" : "matcher: plain";
        string grammar = dynamicWindow ? "grammar: line-window" : "grammar: full-page";
        string state = _running ? "listening…" : "stopped";
        string heard = string.IsNullOrEmpty(_latestRecognized) ? "(nothing yet)" : _latestRecognized;
        _statusText.text = $"heard: {heard}\n\nread {_cursor} / {_expectedNorm.Count} words   [{mode}]   [{matcher}]   [{grammar}]   [{state}]";
    }

    // ---------------------------------------------------------------- recognition control

    // Apply the page-or-free vocabulary based on the toggle. Read at StartProcessing, so we set it
    // each time recognition (re)starts. With the dynamic-window toggle on, the per-line window
    // replaces the full-page grammar (set here without a restart; the caller starts recognition).
    private void ApplyVocabulary()
    {
        if (_recognizer == null) return;

        if (dynamicWindow)
        {
            ApplyWindow(_cursor, restart: false);
            return;
        }

        if (restrictVocabularyToPage)
        {
            var pageWords = _expectedNorm.Distinct().ToList();
            var vocab = new List<string>(pageWords);
            vocab.Add("[unk]"); // allow unknown words through rather than forcing a page word
            _recognizer.Vocabulary = vocab;
            Log($"[GRAMMAR restricted {vocab.Count - 1}] {string.Join(" ", pageWords)} + [unk]");
        }
        else
        {
            _recognizer.Vocabulary = new List<string>(); // empty → free recognition
            Log("[GRAMMAR free]");
        }
    }

    // The sentence/line containing the word at `cursor` (clamped to the last word).
    private int LineOf(int cursor)
    {
        if (_wordLine.Count == 0) return 0;
        return _wordLine[Mathf.Clamp(cursor, 0, _wordLine.Count - 1)];
    }

    // Active-window grammar for `cursor`: unique words of its line + the first windowSlack words of
    // the next line + "[unk]" (kept small, target ≤ ~8 entries).
    private List<string> ComputeWindow(int cursor, out int line)
    {
        line = LineOf(cursor);
        var words = new List<string>();
        AddLineWords(line, int.MaxValue, words);     // whole current line
        AddLineWords(line + 1, windowSlack, words);  // lead-in to the next line
        var window = words.Distinct().ToList();
        window.Add("[unk]");
        return window;
    }

    private void AddLineWords(int line, int maxWords, List<string> into)
    {
        if (line < 0 || line >= _lineRanges.Count) return;
        var r = _lineRanges[line];
        int added = 0;
        for (int i = r.start; i < r.endEx && added < maxWords; i++, added++)
            into.Add(_expectedNorm[i]);
    }

    // Re-window the recognizer for `cursor`'s line. Cheap recognizer reset (model stays loaded):
    // Stop → set Vocabulary → Start. restart=false is the initial set before the caller starts.
    private void ApplyWindow(int cursor, bool restart)
    {
        if (_recognizer == null) return;

        var window = ComputeWindow(cursor, out int line);
        if (restart && _running) _recognizer.StopProcessing();
        _recognizer.Vocabulary = window;
        _lastWindowedLine = line;

        int wordCount = window.Count > 0 ? window.Count - 1 : 0; // minus the trailing [unk]
        Log($"[WINDOW line {line + 1}/{_lineCount}] {string.Join(" ", window.Take(wordCount))} + [unk]");

        if (restart) _recognizer.StartProcessing();
    }

    // Re-window at line changes on the main thread (never re-entrantly inside a result callback).
    private void Update()
    {
        if (!_running || _expectedNorm.Count == 0) return;

        // Dynamic-window re-sync (only when that mode is on).
        if (dynamicWindow && _recognizer != null && LineOf(_cursor) != _lastWindowedLine)
            ApplyWindow(_cursor, restart: true);

        // Sentence-tail flush: when the cursor sits within sentenceTailWords of the next sentence end
        // and nothing new has been recognized for sentenceFlushDelay (partial hasn't grown since the
        // last advance), flush past that sentence end so its late-committed trailing words highlight.
        int end = NextSentenceEnd(_cursor);
        if (end >= 0
            && end - _cursor <= sentenceTailWords
            && Time.time - _lastAdvanceTime >= sentenceFlushDelay
            && _lastPartialText.Length <= _partialLenAtAdvance)
        {
            int before = _cursor;
            _cursor = end + 1; // flush this sentence's tail; do not flush past the sentence end
            Log($"[SENT-FLUSH] cursor {before}->{_cursor}");
            _lastAdvanceTime = Time.time;
            RenderPage();
            RenderStatus();
        }
    }

    // Smallest sentence-end word index >= cursor, or -1 if none remain. (_sentenceEndIdx is ascending.)
    private int NextSentenceEnd(int cursor)
    {
        foreach (int e in _sentenceEndIdx)
            if (e >= cursor) return e;
        return -1;
    }

    private void ToggleRecognition()
    {
        if (_recognizer == null) return;

        if (_running)
        {
            _recognizer.StopProcessing();
            StopClipPlayback();
        }
        else
        {
            // Reset per-run state.
            _cursor = 0;
            _stuck = 0;
            _unkCount = 0;
            _latestRecognized = "";
            _summaryPrinted = false;
            _lastWindowedLine = -1; // force the first window to be applied for this run
            _lastAdvanceTime = Time.time; // arm the sentence-tail flush relative to run start
            _lastPartialText = "";
            _partialLenAtAdvance = 0;
            _startTime = Time.realtimeSinceStartup;
            _stopTime = 0f;

            // Select the speech source for this run. Clip mode uses the AudioListener source (the
            // clip is played in real time once the recognizer is listening — see OnStarted).
            bool useClipNow = _useClip && _listenerSource != null && _clipAudioSource != null;
            if (_useClip && !useClipNow)
                Log("(no testClip assigned — falling back to microphone)");
            SpeechSource src = useClipNow ? _listenerSource : (SpeechSource)_micSource;
            _recognizer.SpeechSource = src;
            _pendingClipPlay = useClipNow;

            ApplyVocabulary();
            string mode = restrictVocabularyToPage ? "page-restricted" : "free";
            string srcName = useClipNow ? "clip(real-time)" : "mic";
            Log($"--- START (vocab: {mode}, source: {srcName}) ---");
            if (_partialText != null) _partialText.text = "";
            RenderPage();
            RenderStatus();
            _recognizer.StartProcessing();
        }
    }

    // Play the clip at normal speed once the recognizer is actually listening, so its head isn't lost
    // to async init. Guarded so a mid-run re-window restart (which re-fires Started) never replays it.
    private void StartClipPlaybackIfPending()
    {
        if (!_pendingClipPlay || _clipAudioSource == null) return;
        _pendingClipPlay = false;
        _clipAudioSource.Play();
    }

    private void StopClipPlayback()
    {
        _pendingClipPlay = false;
        if (_clipAudioSource != null && _clipAudioSource.isPlaying) _clipAudioSource.Stop();
    }

    // ---------------------------------------------------------------- Recognissimo events

    private void OnPartial(PartialResult p)
    {
        // Latest partial on a single live line; do NOT spam the log with partials.
        _latestRecognized = p.partial ?? "";
        if (_partialText != null)
            _partialText.text = "partial: " + (_latestRecognized.Length == 0 ? "…" : _latestRecognized);
        int beforePartial = _cursor;
        Feed(_latestRecognized, false);
        _lastPartialText = _latestRecognized;
        if (_cursor != beforePartial) NoteAdvanceTiming();
        RenderPage();
        RenderStatus();
    }

    // Record when the cursor last advanced + the partial length at that moment, for the sentence-tail
    // flush in Update(). (Kept out of Feed() so the matcher logic stays unchanged.)
    private void NoteAdvanceTiming()
    {
        _lastAdvanceTime = Time.time;
        _partialLenAtAdvance = _lastPartialText.Length;
    }

    private void OnResult(Result r)
    {
        string text = r.text ?? "";
        _latestRecognized = text;

        // Per-word confidences (EnableDetails) and unknown-token tally.
        var words = new StringBuilder();
        if (r.result != null)
        {
            foreach (var w in r.result)
            {
                if (w.word == "[unk]") _unkCount++;
                if (words.Length > 0) words.Append(' ');
                words.Append(w.word).Append("(conf ").Append(w.conf.ToString("0.00")).Append(')');
            }
        }

        // N-best alternatives (Alternatives = 5): "alts: dolphin 7.4 | pig 2.1 | [unk] 0.9".
        var alts = new StringBuilder();
        if (r.alternatives != null)
        {
            int n = 0;
            foreach (var a in r.alternatives)
            {
                if (n++ >= 5) break;
                if (alts.Length > 0) alts.Append(" | ");
                string at = string.IsNullOrEmpty(a.text) ? "(empty)" : a.text;
                alts.Append(at).Append(' ').Append(a.confidence.ToString("0.0"));
            }
        }

        if (text.Length > 0 || words.Length > 0)
        {
            Log("text: " + (text.Length == 0 ? "(empty)" : text));
            if (words.Length > 0) Log("  " + words);
            if (alts.Length > 0) Log("  alts: " + alts);
        }

        int beforeFinal = _cursor;
        Feed(text, true);
        if (_cursor != beforeFinal) NoteAdvanceTiming();
        RenderPage();
        RenderStatus();

        // Auto-summary once the whole page has been read.
        if (_cursor >= _expectedNorm.Count && _expectedNorm.Count > 0)
            PrintSummary();
    }

    private void OnStarted()
    {
        _running = true;
        if (_startTime <= 0f) _startTime = Time.realtimeSinceStartup; // first audio
        if (_buttonLabel != null) _buttonLabel.text = "Stop";
        StartClipPlaybackIfPending(); // recognizer is now listening → play the clip in real time
        RenderStatus();
    }

    private void OnFinished()
    {
        _running = false;
        StopClipPlayback();
        if (_buttonLabel != null) _buttonLabel.text = "Start";
        PrintSummary();
        RenderStatus();
    }

    // Print the per-run summary once (on stop, clip-dry, or page completion).
    private void PrintSummary()
    {
        if (_summaryPrinted) return;
        _summaryPrinted = true;

        if (_stopTime <= 0f) _stopTime = Time.realtimeSinceStartup;
        float elapsed = _startTime > 0f ? Mathf.Max(0f, _stopTime - _startTime) : 0f;
        int total = _expectedNorm.Count;
        int pct = total > 0 ? Mathf.RoundToInt(100f * _cursor / total) : 0;

        string summary = $"READ SUMMARY: matched {_cursor}/{total} ({pct}%) · " +
                         $"elapsed {elapsed:0.0}s · [unk] seen {_unkCount}";
        Log(summary);
        Debug.Log("[ReadToMeTest] " + summary);
    }

    // Append-mode log: keep the last N lines, render newest-at-bottom, and mirror to the Console.
    private void Log(string line)
    {
        _logLines.Add(line);
        if (_logLines.Count > MaxLogLines)
            _logLines.RemoveRange(0, _logLines.Count - MaxLogLines);
        if (_logText != null) _logText.text = string.Join("\n", _logLines);
        Debug.Log("[RTM] " + line);
    }

    private void OnInitFailed(InitializationException e)
    {
        _running = false;
        if (_buttonLabel != null) _buttonLabel.text = "Start";
        if (_statusText != null) _statusText.text = "INIT FAILED: " + e.Message;
        Debug.LogError("[ReadToMeTest] init failed: " + e);
    }

    private void OnRuntimeFailed(RuntimeException e)
    {
        _running = false;
        if (_buttonLabel != null) _buttonLabel.text = "Start";
        if (_statusText != null) _statusText.text = "RUNTIME FAILED: " + e.Message;
        Debug.LogError("[ReadToMeTest] runtime failed: " + e);
    }

    // ---------------------------------------------------------------- build the Recognissimo stack

    // Create the stack on a child GameObject that is INACTIVE while we configure it, then activate
    // it — so each component's OnEnable (which registers its init tasks) sees the configured fields.
    private void BuildSpeechStack()
    {
        var sttGO = new GameObject("Recognissimo");
        sttGO.transform.SetParent(transform, false);
        sttGO.SetActive(false);

        _micSource = sttGO.AddComponent<MicrophoneSpeechSource>();
        _micSource.DeviceName = null; // default microphone

        // Optional repeatable-baseline source from a pre-recorded clip (e.g. ElevenLabs read).
        // Built only when a clip is assigned; selected at Start via the "Use clip" toggle. We capture
        // it in REAL TIME via AudioListenerSpeechSource while an AudioSource plays the clip at normal
        // speed — AudioClipSpeechSource feeds ~100x real-time and drops the tail (32s clip in ~0.3s).
        if (testClip != null)
        {
            _listenerSource = sttGO.AddComponent<AudioListenerSpeechSource>();

            _clipAudioSource = sttGO.AddComponent<AudioSource>();
            _clipAudioSource.clip = testClip;
            _clipAudioSource.playOnAwake = false;
            _clipAudioSource.loop = false;
            _clipAudioSource.spatialBlend = 0f; // 2D → always present in the AudioListener mix
        }

        var provider = sttGO.AddComponent<StreamingAssetsLanguageModelProvider>();
        provider.language = SystemLanguage.English;
        provider.languageModels = new List<StreamingAssetsLanguageModel>
        {
            new StreamingAssetsLanguageModel { language = SystemLanguage.English, path = "LanguageModels/en-US" }
        };

        _recognizer = sttGO.AddComponent<SpeechRecognizer>();
        _recognizer.LanguageModelProvider = provider;
        _recognizer.SpeechSource = _micSource; // default; may switch to clip at Start
        _recognizer.EnableDetails = true;       // per-word Word.conf
        _recognizer.Alternatives = 5;           // N-best Result.alternatives (text + confidence)
        ApplyVocabulary(); // seed vocabulary from the toggle's default before activation

        _recognizer.PartialResultReady.AddListener(OnPartial);
        _recognizer.ResultReady.AddListener(OnResult);
        _recognizer.Started.AddListener(OnStarted);
        _recognizer.Finished.AddListener(OnFinished);
        _recognizer.InitializationFailed.AddListener(OnInitFailed);
        _recognizer.RuntimeFailed.AddListener(OnRuntimeFailed);

        sttGO.SetActive(true);
    }

    // ---------------------------------------------------------------- build the UI

    private void BuildUI()
    {
        if (EventSystem.current == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        var canvasGO = new GameObject("ReadToMeCanvas",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 0.5f;

        // Opaque light-neutral background so text has high contrast (first child = drawn behind).
        var bgGO = new GameObject("Background", typeof(RectTransform), typeof(Image));
        bgGO.transform.SetParent(canvasGO.transform, false);
        var bgRt = bgGO.GetComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = Vector2.zero;
        bgRt.offsetMax = Vector2.zero;
        var bgImg = bgGO.GetComponent<Image>();
        bgImg.color = new Color32(0xF4, 0xF4, 0xF4, 0xFF);
        bgImg.raycastTarget = false;

        var darkText = new Color32(0x22, 0x22, 0x22, 0xFF); // page unread
        var grayText = new Color32(0x33, 0x33, 0x33, 0xFF); // status + log

        var font = TMP_Settings.defaultFontAsset;
        if (font == null)
            Debug.LogWarning("[ReadToMeTest] TMP_Settings.defaultFontAsset is null; text may not render.");

        // Each element gets its OWN vertical band (top→bottom), no overlap.
        _pageText = MakeText("page", canvasGO.transform, font,
            new Vector2(0.03f, 0.55f), new Vector2(0.97f, 0.97f),
            46, TextAlignmentOptions.TopLeft);
        _pageText.richText = true;
        _pageText.color = darkText;

        // Append-mode results readout (per-word conf, N-best, summary). Top-aligned, smaller font,
        // clipped to its rect so it can never overflow onto other elements; last-N cap still applies.
        _logText = MakeText("log", canvasGO.transform, font,
            new Vector2(0.03f, 0.245f), new Vector2(0.97f, 0.52f),
            20, TextAlignmentOptions.TopLeft);
        _logText.color = grayText;
        _logText.enableWordWrapping = true;
        _logText.overflowMode = TextOverflowModes.Truncate;

        // Single live line for the latest partial (kept out of the scrolling log).
        _partialText = MakeText("partial", canvasGO.transform, font,
            new Vector2(0.03f, 0.215f), new Vector2(0.97f, 0.24f),
            22, TextAlignmentOptions.Left);
        _partialText.color = new Color(0.40f, 0.40f, 0.40f, 1f);
        _partialText.enableWordWrapping = false;
        _partialText.overflowMode = TextOverflowModes.Ellipsis;

        // Status line (heard / read X/N / mode / state). Wraps then truncates within its rect.
        _statusText = MakeText("status", canvasGO.transform, font,
            new Vector2(0.03f, 0.135f), new Vector2(0.97f, 0.21f),
            22, TextAlignmentOptions.TopLeft);
        _statusText.color = grayText;
        _statusText.enableWordWrapping = true;
        _statusText.overflowMode = TextOverflowModes.Ellipsis;

        // Controls band (y 0.005–0.13): Start/Stop on the left, the four toggles stacked on the right.
        BuildButton(canvasGO.transform, font);
        _vocabToggle = MakeToggle("VocabToggle", canvasGO.transform, font,
            new Vector2(0.33f, 0.097f), new Vector2(0.99f, 0.125f),
            "Restrict vocabulary to page", restrictVocabularyToPage, OnVocabToggleChanged);
        _smartToggle = MakeToggle("SmartToggle", canvasGO.transform, font,
            new Vector2(0.33f, 0.067f), new Vector2(0.99f, 0.095f),
            "Smart matcher", smartMatcher, OnSmartToggleChanged);
        _dynamicToggle = MakeToggle("DynamicWindowToggle", canvasGO.transform, font,
            new Vector2(0.33f, 0.037f), new Vector2(0.99f, 0.065f),
            "Dynamic window (per-line)", dynamicWindow, OnDynamicToggleChanged);
        _clipToggle = MakeToggle("ClipToggle", canvasGO.transform, font,
            new Vector2(0.33f, 0.007f), new Vector2(0.99f, 0.035f),
            "Use clip (vs mic)", false, OnClipToggleChanged);
        _useClip = _clipToggle.isOn;
    }

    private static TMP_Text MakeText(string name, Transform parent, TMP_FontAsset font,
        Vector2 anchorMin, Vector2 anchorMax, float size, TextAlignmentOptions align)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var tmp = go.AddComponent<TextMeshProUGUI>();
        if (font != null) tmp.font = font;
        tmp.fontSize = size;
        tmp.enableAutoSizing = false;
        tmp.alignment = align;
        tmp.color = Color.black;
        tmp.raycastTarget = false;
        return tmp;
    }

    private void BuildButton(Transform parent, TMP_FontAsset font)
    {
        var go = new GameObject("StartStopButton", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.04f, 0.030f);
        rt.anchorMax = new Vector2(0.30f, 0.120f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var img = go.GetComponent<Image>();
        img.color = new Color(0.10f, 0.50f, 0.21f, 1f); // green to match read-word color

        var btn = go.GetComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(ToggleRecognition);

        _buttonLabel = MakeText("label", go.transform, font,
            Vector2.zero, Vector2.one, 40, TextAlignmentOptions.Center);
        _buttonLabel.text = "Start";
        _buttonLabel.color = Color.white;
    }

    private static Toggle MakeToggle(string name, Transform parent, TMP_FontAsset font,
        Vector2 anchorMin, Vector2 anchorMax, string labelText, bool initialOn,
        UnityEngine.Events.UnityAction<bool> onChanged)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Toggle));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        // Checkbox background (left-aligned square).
        var bgGO = new GameObject("Background", typeof(RectTransform), typeof(Image));
        bgGO.transform.SetParent(go.transform, false);
        var bgRt = bgGO.GetComponent<RectTransform>();
        bgRt.anchorMin = new Vector2(0f, 0.5f);
        bgRt.anchorMax = new Vector2(0f, 0.5f);
        bgRt.pivot = new Vector2(0f, 0.5f);
        bgRt.sizeDelta = new Vector2(44, 44);
        bgRt.anchoredPosition = new Vector2(4, 0);
        var bgImg = bgGO.GetComponent<Image>();
        bgImg.color = Color.white;

        var checkGO = new GameObject("Checkmark", typeof(RectTransform), typeof(Image));
        checkGO.transform.SetParent(bgGO.transform, false);
        var checkRt = checkGO.GetComponent<RectTransform>();
        checkRt.anchorMin = new Vector2(0.15f, 0.15f);
        checkRt.anchorMax = new Vector2(0.85f, 0.85f);
        checkRt.offsetMin = Vector2.zero;
        checkRt.offsetMax = Vector2.zero;
        var checkImg = checkGO.GetComponent<Image>();
        checkImg.color = new Color(0.18f, 0.66f, 0.56f, 1f);

        var label = MakeText("label", go.transform, font,
            new Vector2(0f, 0f), new Vector2(1f, 1f), 26, TextAlignmentOptions.Left);
        label.rectTransform.offsetMin = new Vector2(56, 0);
        label.text = labelText;
        label.color = Color.black;

        var toggle = go.GetComponent<Toggle>();
        toggle.targetGraphic = bgImg;
        toggle.graphic = checkImg;
        toggle.isOn = initialOn;
        toggle.onValueChanged.AddListener(onChanged);
        return toggle;
    }

    private void OnVocabToggleChanged(bool on)
    {
        // Vocabulary is read at StartProcessing, so this takes effect on the next Start. Reflect it
        // in the status line now so the chosen mode is visible before re-starting.
        restrictVocabularyToPage = on;
        RenderStatus();
    }

    private void OnClipToggleChanged(bool on)
    {
        // Speech source is selected at StartProcessing, so this takes effect on the next Start.
        _useClip = on;
        RenderStatus();
    }

    private void OnSmartToggleChanged(bool on)
    {
        // Matcher-only; takes effect immediately on the next recognized word.
        smartMatcher = on;
        RenderStatus();
    }

    private void OnDynamicToggleChanged(bool on)
    {
        // Grammar mode; applied at the next Start and re-windowed per line. If flipped mid-run, the
        // Update line-change check re-windows on the next line (or immediately if mid-line differs).
        dynamicWindow = on;
        RenderStatus();
    }

    // ---------------------------------------------------------------- word normalization

    // Normalize a token to a comparison key. MUST match the app's word-bank rule (AudioAndTextPlayer
    // .NormalizeWord): lowercase, then trim leading/trailing chars not in [a-z0-9'] (internal
    // apostrophes kept). Reimplemented here to keep this harness self-contained.
    private static string NormalizeWord(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        s = s.ToLowerInvariant();
        int a = 0, b = s.Length - 1;
        while (a <= b && !IsWordChar(s[a])) a++;
        while (b >= a && !IsWordChar(s[b])) b--;
        return a <= b ? s.Substring(a, b - a + 1) : "";
    }

    private static bool IsWordChar(char c)
    {
        return (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '\'';
    }
}
