using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Recognissimo;
using Recognissimo.Components;
using UnityEngine;

// ============================================================================================
// Read-Along (Mode A) — the SINGLE owner of the Recognissimo stack + the validated matcher.
//
// The child reads the current story page aloud; recognized words advance the existing page
// highlight (via AudioAndTextPlayer.SetReadProgress) and the page auto-turns on lenient completion.
// Ported straight from the _ReadToMeTest harness (recognizer build, the smart-matcher Feed, and
// NormalizeWord — reused from AudioAndTextPlayer so the rule can't drift). The model is loaded once
// and kept warm across pages; only the grammar + cursor reset per page.
//
// Opt-in: with read-along OFF (the default) nothing here runs. Enabled by the reading-mode picker
// via SetEnabled. Self-bootstraps in the story scene (no scene edits, no per-book script).
// Rollback = delete this file + the // ---- read-along ---- region in AudioAndTextPlayer.
// ============================================================================================
public class ReadAlongService : MonoBehaviour
{
    [Header("Lenient completion (Mode A)")]
    [Tooltip("Auto-turn once the cursor reaches this fraction of the page's words.")]
    [SerializeField] private float completionFraction = 0.9f;
    [Tooltip("At least one recognized word must fall in this trailing fraction (the final line).")]
    [SerializeField] private float lastRegionFraction = 0.2f;
    [Tooltip("Seconds of no further advance, after the above are met, before auto-paging.")]
    [SerializeField] private float trailingSilenceSec = 1.2f;
    [Tooltip("Safety net: once the cursor reaches the threshold, auto-turn after this longer silence " +
             "even if no recognized word landed in the last region (avoids a hang).")]
    [SerializeField] private float hardCompletionSilenceSec = 2.5f;

    [Header("Sentence-tail flush")]
    [Tooltip("Pause (s) with no new recognition before flushing a sentence's late-committed tail.")]
    [SerializeField] private float sentenceFlushDelay = 0.35f;
    [Tooltip("Only flush when the cursor is within this many words of the next sentence end.")]
    [SerializeField] private int sentenceTailWords = 2;

    // ---- validated matcher: stopword_safe + stall re-sync (lifted from the harness) ----
    [SerializeField] private int stallK = 3;    // stuck-count that arms the wide re-sync
    [SerializeField] private int wideLook = 6;  // re-sync scans look = 3..wideLook
    private int _stuck;                          // consecutive recognized words with no advance
    private static readonly HashSet<string> STOPWORDS = new()
    {
        "the", "a", "an", "is", "are", "was", "were", "and", "or", "of", "to", "in", "on", "it",
        "he", "she", "you", "we", "they", "i", "up", "by", "at", "as", "be", "his", "her",
        "that", "this", "one", "now"
    };

    // ---- Recognissimo stack (lazy, created once, kept warm across pages) ----
    private SpeechRecognizer _recognizer;
    private MicrophoneSpeechSource _micSource;
    private bool _stackReady;
    private bool _recognizing;
    private Coroutine _restartCo; // pending deferred restart (waits for the async StopProcessing)

    // ---- per-page matcher + completion state ----
    private AudioAndTextPlayer _page;
    private List<string> _expected = new();
    private int _cursor;
    private bool _reachedLastRegion;
    private float _lastAdvanceTime;
    private bool _completed;
    private readonly List<int> _sentenceEndIdx = new(); // ascending word indices that end a sentence
    private string _lastPartialText = "";                // last partial seen in OnPartial
    private int _partialLenAtAdvance;                    // partial length captured at the last advance
    private List<string> _utteranceWords = new();        // normalized words already matched this utterance

    // ---- activation / wiring ----
    private bool _active;
    private PRScript _prScript;
    private float _nextScan;

    // Raised when the recognizer can't run (mic init/permission failure) AFTER read-along was
    // active, so a listener (the picker) can fall back to narration instead of a silent page.
    public event System.Action Unavailable;

    // Spawn one persistent service automatically; it stays inert until a story scene wires it.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (FindObjectOfType<ReadAlongService>() != null) return;
        new GameObject("ReadAlongService").AddComponent<ReadAlongService>();
    }

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
        // (Re)wire to the story scene's PRScript when present; stop recognition otherwise.
        if (_prScript == null)
        {
            if (Time.unscaledTime < _nextScan) return;
            _nextScan = Time.unscaledTime + 0.5f;

            var found = FindObjectOfType<PRScript>();
            if (found == null)
            {
                // Left the story scene: drop any active recognition so a non-story scene is inert.
                if (_active) { _active = false; Stop(); }
                return;
            }
            WireTo(found);
        }

        // Sentence-tail flush (additive): Vosk commits a phrase's final word late (it lands only when
        // the NEXT word is recognized), so a sentence's trailing 1-2 words highlight a sentence late
        // and the page's final words never do. When the cursor sits within sentenceTailWords of the
        // next sentence end and nothing new has been recognized for sentenceFlushDelay (partial hasn't
        // grown since the last advance), flush past that sentence end so those words light up. Runs
        // BEFORE the completion check, and resets _lastAdvanceTime so the page doesn't auto-turn the
        // instant the tail flushes (the reader gets a beat to see the final words).
        if (_active && _recognizing && !_completed && _expected.Count > 0)
        {
            int end = NextSentenceEnd(_cursor);
            if (end >= 0
                && end - _cursor <= sentenceTailWords
                && (Time.realtimeSinceStartup - _lastAdvanceTime) >= sentenceFlushDelay
                && _lastPartialText.Length <= _partialLenAtAdvance)
            {
                int before = _cursor;
                _cursor = end + 1; // flush this sentence's tail; never past the sentence end
                Debug.Log($"[ReadAlong][SENT-FLUSH] cursor {before}->{_cursor}");
                if (_page != null) _page.SetReadProgress(_cursor);
                _lastAdvanceTime = Time.realtimeSinceStartup;

                // The flush can reach the last region with no recognized word landing there; mark it
                // so lenient completion (which needs _reachedLastRegion) can still fire. Same
                // lastRegionStart as the recognized-word path; the flushed sentence-end word is _cursor-1.
                int lastRegionStart = Mathf.FloorToInt(_expected.Count * (1f - lastRegionFraction));
                if (_cursor - 1 >= lastRegionStart) _reachedLastRegion = true;
            }
        }

        // Lenient completion: cursor ≥ ~90%, a recognized word reached the last region, then a short
        // trailing pause with no further advance → turn the page once. Safety net: once the cursor is
        // at the threshold, a longer silence completes even without _reachedLastRegion (else a page
        // with no tail punctuation and no recognized word in the last region would hang).
        if (_active && _recognizing && !_completed && _expected.Count > 0)
        {
            int threshold = Mathf.CeilToInt(_expected.Count * completionFraction);
            float silence = Time.realtimeSinceStartup - _lastAdvanceTime;
            if ((_cursor >= threshold && _reachedLastRegion && silence >= trailingSilenceSec) ||
                (_cursor >= threshold && silence >= hardCompletionSilenceSec))
            {
                Complete();
            }
        }
    }

    // Smallest sentence-end word index >= cursor, or -1 if none remain. (_sentenceEndIdx is ascending.)
    private int NextSentenceEnd(int cursor)
    {
        foreach (int e in _sentenceEndIdx)
            if (e >= cursor) return e;
        return -1;
    }

    private void WireTo(PRScript prScript)
    {
        _prScript = prScript;
        if (prScript.audioAndTextPlayer != null)
            prScript.audioAndTextPlayer.ReadAlongPageReady += OnPageReady;
    }

    // ---------------------------------------------------------------- activation

    // The reading-mode picker (UnifiedReadingModePicker) enables/disables read-along through here.
    public void SetEnabled(bool on) => SetActive(on);

    private void SetActive(bool on)
    {
        _active = on;
        var player = _prScript != null ? _prScript.audioAndTextPlayer : null;
        if (player != null) player.ReadAlongActive = on;

        if (on)
        {
            // If the page is already loaded (toggled on mid-page), take it over now; otherwise the
            // next page load fires ReadAlongPageReady and we begin then.
            if (player != null && player.ExpectedWords != null && player.ExpectedWords.Count > 0)
            {
                player.StopPageAudioForReadAlong();
                Begin(player);
            }
        }
        else
        {
            Stop();
        }
    }

    // Fired by the player once a freshly-loaded page's words are ready (only when ReadAlongActive).
    private void OnPageReady(AudioAndTextPlayer player)
    {
        if (!_active) return;
        Begin(player);
    }

    // ---------------------------------------------------------------- recognition lifecycle

    // (Re)start recognition for the given page: page-restricted full-page grammar (unique words +
    // [unk]), cursor reset to 0, completion state cleared. Model stays loaded across pages.
    public void Begin(AudioAndTextPlayer page)
    {
        if (page == null) return;

        _page = page;
        _expected = page.ExpectedWords != null ? new List<string>(page.ExpectedWords) : new List<string>();
        _sentenceEndIdx.Clear();
        if (page.ExpectedSentenceEndIndices != null) _sentenceEndIdx.AddRange(page.ExpectedSentenceEndIndices);
        _cursor = 0;
        _stuck = 0;
        _reachedLastRegion = false;
        _completed = false;
        _lastAdvanceTime = Time.realtimeSinceStartup;
        _lastPartialText = "";
        _partialLenAtAdvance = 0;
        _utteranceWords.Clear();

        EnsureStack();

        var vocab = _expected.Distinct().ToList();
        vocab.Add("[unk]"); // page-restricted grammar, but unknown speech is allowed through
        _recognizer.Vocabulary = vocab;

        page.SetReadProgress(0); // render the initial highlight (first word) with no audio
        StartRecognition();

        Debug.Log($"[ReadAlong] begin page — {_expected.Count} words");
    }

    public void Stop()
    {
        // Cancel any pending deferred restart so a Stop (incl. SetActive(false)) can't be resurrected.
        if (_restartCo != null) { StopCoroutine(_restartCo); _restartCo = null; }
        if (_recognizer != null && _recognizing) _recognizer.StopProcessing();
    }

    // Mic/recognizer can't run (init or permission failure). Turn read-along OFF cleanly so the
    // page's next Play narrates instead of staying suppressed, then notify subscribers. Guarded on
    // _active so a stale failure arriving after we've already switched off is a no-op.
    private void OnRecognizerUnavailable(string why)
    {
        _recognizing = false;
        Debug.LogError("[ReadAlong] " + why);
        if (!_active) return;
        _active = false;
        var player = _prScript != null ? _prScript.audioAndTextPlayer : null;
        if (player != null) player.ReadAlongActive = false;
        Stop();
        Unavailable?.Invoke();
    }

    private void StartRecognition()
    {
        if (_recognizer == null) return;

        // Cancel any pending deferred restart (rapid page turns) so only the latest page starts.
        if (_restartCo != null) { StopCoroutine(_restartCo); _restartCo = null; }

        if (!_recognizing)
        {
            // Idle → start now (Begin() has already installed this page's Vocabulary).
            _recognizer.SpeechSource = _micSource;
            _recognizer.StartProcessing();
            return;
        }

        // Busy → StopProcessing() is ASYNC; the recognizer only finishes on a later frame (Finished
        // sets _recognizing=false). Defer StartProcessing until then, else its Setup runs while the
        // previous session is still tearing down and the new page's grammar never installs.
        _recognizer.StopProcessing();
        _restartCo = StartCoroutine(DeferredStart());
    }

    private IEnumerator DeferredStart()
    {
        const float timeoutSec = 1f; // safety: don't wait forever if Finished never arrives
        float deadline = Time.realtimeSinceStartup + timeoutSec;
        while (_recognizing && Time.realtimeSinceStartup < deadline)
            yield return null;

        _restartCo = null;
        if (_recognizer == null) yield break;

        _recognizer.SpeechSource = _micSource;
        _recognizer.StartProcessing(); // installs the Vocabulary set by Begin() for the current page
    }

    // Build the Recognissimo stack on an INACTIVE child, configure it, then activate (so each
    // OnEnable sees configured fields) — the harness ordering. Kept warm; never disposed per page.
    private void EnsureStack()
    {
        if (_stackReady) return;

        var sttGO = new GameObject("ReadAlongRecognissimo");
        sttGO.transform.SetParent(transform, false);
        sttGO.SetActive(false);

        _micSource = sttGO.AddComponent<MicrophoneSpeechSource>();
        _micSource.DeviceName = null; // default microphone

        var provider = sttGO.AddComponent<StreamingAssetsLanguageModelProvider>();
        provider.language = SystemLanguage.English;
        provider.languageModels = new List<StreamingAssetsLanguageModel>
        {
            new StreamingAssetsLanguageModel { language = SystemLanguage.English, path = "LanguageModels/en-US" }
        };

        _recognizer = sttGO.AddComponent<SpeechRecognizer>();
        _recognizer.LanguageModelProvider = provider;
        _recognizer.SpeechSource = _micSource;
        _recognizer.EnableDetails = true;

        _recognizer.PartialResultReady.AddListener(OnPartial);
        _recognizer.ResultReady.AddListener(OnResult);
        _recognizer.Started.AddListener(() => _recognizing = true);
        _recognizer.Finished.AddListener(() => _recognizing = false);
        _recognizer.InitializationFailed.AddListener(e => OnRecognizerUnavailable("init failed: " + e));
        _recognizer.RuntimeFailed.AddListener(e => OnRecognizerUnavailable("runtime failed: " + e));

        sttGO.SetActive(true);
        _stackReady = true;
    }

    // ---------------------------------------------------------------- matcher (validated; feed both)

    private void OnPartial(PartialResult p)
    {
        _lastPartialText = p.partial ?? ""; // set before Feed so OnAdvance captures this length
        Feed(_lastPartialText, false);
    }

    private void OnResult(Result r)
    {
        Feed(r.text, true);
        _utteranceWords.Clear(); // utterance boundary → next utterance's partials start fresh
    }

    // Incremental driver: Vosk partials are CUMULATIVE (the whole growing utterance hypothesis), so
    // feeding the full string each call re-walks already-consumed words — they fail at the cursor,
    // bump _stuck, and eventually arm the wide re-sync, which then teleports on a re-processed common
    // word. Instead, only the NEWLY-APPENDED words past the common prefix are matched.
    private void Feed(string recognized, bool isFinal)
    {
        if (_expected.Count == 0) return;

        // Normalized recognized words (drop empties + [unk]/unk), in order.
        var words = new List<string>();
        if (!string.IsNullOrEmpty(recognized))
        {
            foreach (string raw in recognized.Split((char[])null, StringSplitOptions.RemoveEmptyEntries))
            {
                string nw = AudioAndTextPlayer.NormalizeWord(raw);
                if (nw.Length == 0 || nw == "unk") continue; // [unk]/unk ignored
                words.Add(nw);
            }
        }

        // Common-prefix length with the words already processed for this utterance.
        int l = 0;
        while (l < words.Count && l < _utteranceWords.Count && words[l] == _utteranceWords[l]) l++;

        // Match only the newly-appended tail.
        for (int i = l; i < words.Count; i++)
            MatchWord(words[i], isFinal);

        _utteranceWords = words;
    }

    // Smart matcher for ONE already-normalized word (validated on real recorded audio + the app's Vosk
    // model: 46/46 on the clean read, the repetitions read, and a missed-content-word stall). Forward
    // cursor: (1) normal window look=0..2 — look==0 advances, a look>0 skip advances unless it lands on
    // a stopword (suppressed); (2) if stuck for stallK words, WIDE re-sync look=3..wideLook takes the
    // first match (ignoring suppression) to recover a missed content word. Never moves backward.
    private void MatchWord(string nw, bool isFinal)
    {
        int cursorBefore = _cursor;
        bool advanced = false;

        // 1) Normal window, lookahead 2 (stopword-safe on skips).
        for (int look = 0; look <= 2; look++)
        {
            int idx = _cursor + look;
            if (idx >= _expected.Count) break;
            if (_expected[idx] != nw) continue;

            if (look > 0 && STOPWORDS.Contains(nw))
            {
                Debug.Log($"[ReadAlong][SKIP-SUPPRESSED] '{nw}' look{look} exp[{idx}]");
                continue; // don't leapfrog onto a later common word; keep scanning
            }
            _cursor = idx + 1;
            Debug.Log($"[ReadAlong][ADV {(isFinal ? "fin" : "par")}] '{nw}' look{look} cursor {cursorBefore}→{_cursor}");
            OnAdvance(idx);
            advanced = true;
            break;
        }

        // 2) Stall re-sync: after stallK stuck words, scan a wider window and take the first
        // match — ignore stopword suppression here, it's a stall recovery.
        if (!advanced && _stuck >= stallK)
        {
            for (int look = 3; look <= wideLook; look++)
            {
                int idx = _cursor + look;
                if (idx >= _expected.Count) break;
                if (_expected[idx] != nw) continue;
                _cursor = idx + 1;
                Debug.Log($"[ReadAlong][STALL-RESYNC] '{nw}' look{look} exp[{idx}] cursor {cursorBefore}→{_cursor}");
                OnAdvance(idx);
                advanced = true;
                break;
            }
        }

        _stuck = advanced ? 0 : _stuck + 1;
    }

    private void OnAdvance(int matchedOrdinal)
    {
        _lastAdvanceTime = Time.realtimeSinceStartup;
        _partialLenAtAdvance = _lastPartialText.Length; // for the sentence-tail flush's "not grown" guard

        int lastRegionStart = Mathf.FloorToInt(_expected.Count * (1f - lastRegionFraction));
        if (matchedOrdinal >= lastRegionStart) _reachedLastRegion = true;

        if (_page != null) _page.SetReadProgress(_cursor);
    }

    private void Complete()
    {
        _completed = true;
        Stop();
        Debug.Log("[ReadAlong] lenient completion → OnPageReadComplete");
        // Either runs the page's [event OnPageRead] reveal (and stays) or auto-advances by default.
        if (_prScript != null) _prScript.OnPageReadComplete();
    }

}
