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

    [Header("Stall hint")]
    [Tooltip("Seconds with no cursor advance (page not complete) before nudging the reader toward Next.")]
    [SerializeField] private float stallHintSec = 5f;

    [Header("Sentence-tail flush")]
    [Tooltip("Pause (s) with no new recognition before flushing a sentence's late-committed tail.")]
    [SerializeField] private float sentenceFlushDelay = 0.35f;
    [Tooltip("Only flush when the cursor is within this many words of the next sentence end.")]
    [SerializeField] private int sentenceTailWords = 2;

    // ---- validated matcher: stopword_safe + stall re-sync (lifted from the harness) ----
    [SerializeField] private int stallK = 3;    // stuck-count that arms the wide re-sync
    [SerializeField] private int wideLook = 6;  // re-sync scans look = 3..wideLook
    private int _stuck;                          // consecutive recognized words with no advance

    // ---- voice-activity gate: only accept recognized words while the mic actually hears voice ----
    // (the page-restricted grammar otherwise maps silence/breath/noise onto page words and advances).
    [SerializeField] private float voiceRmsThreshold = 0.012f;  // mic RMS above this = real voice
    [SerializeField] private float voiceHoldSec      = 0.35f;   // treat voice as active this long after energy
    private float _lastVoiceTime = -999f;
    private bool VoiceActive => (Time.realtimeSinceStartup - _lastVoiceTime) <= voiceHoldSec;
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
    private bool _recognizedThisPage; // true once a genuine word recognition advanced the cursor on this page
    private bool _stallHintRaised;    // one-shot: StallHint fired for the current stall (cleared on advance/page change)
    private float _lastAdvanceTime;
    private bool _completed;
    private readonly List<int> _sentenceEndIdx = new(); // ascending word indices that end a sentence
    private string _lastPartialText = "";                // last partial seen in OnPartial
    private int _partialLenAtAdvance;                    // partial length captured at the last advance
    private List<string> _utteranceWords = new();        // positional utterance tokens (incl. "unk" holders)

    // Tentative advance held until the next word (or the utterance's final) confirms it — see MatchWord.
    // Dup: an advance onto a word identical to the just-consumed one (partial jitter / a re-read).
    // Skip: a wide stall re-sync jump, committed only when the following word matches _expected[idx+1].
    private enum PendingKind { None, Dup, Skip }
    private PendingKind _pendingKind = PendingKind.None;
    private int _pendingIdx;

    // ---- activation / wiring ----
    private bool _active;
    private PRScript _prScript;
    private float _nextScan;

    // Raised when the recognizer can't run (mic init/permission failure) AFTER read-along was
    // active, so a listener (the picker) can fall back to narration instead of a silent page.
    public event System.Action Unavailable;

    // Stall hint: raised once when an active page hasn't advanced for stallHintSec (and isn't complete)
    // so the reader can gently nudge toward the Next arrow; StallHintCleared fires on the next advance,
    // page change, completion, or when read-along turns off.
    public event System.Action StallHint;
    public event System.Action StallHintCleared;

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
                && _recognizedThisPage // never flush before the child has actually read a word
                && end - _cursor <= sentenceTailWords
                && (Time.realtimeSinceStartup - _lastAdvanceTime) >= sentenceFlushDelay
                && _lastPartialText.Length <= _partialLenAtAdvance)
            {
                int before = _cursor;
                _cursor = end + 1; // flush this sentence's tail; never past the sentence end
                Debug.Log($"[ReadAlong][SENT-FLUSH] cursor {before}->{_cursor}");
                if (_page != null) _page.SetReadProgress(_cursor);
                _lastAdvanceTime = Time.realtimeSinceStartup;
                ResetStallHint(); // a flush is an advance — drop any nudge

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
            if (_recognizedThisPage && // a page with no recognized words can never auto-complete
                ((_cursor >= threshold && _reachedLastRegion && silence >= trailingSilenceSec) ||
                 (_cursor >= threshold && silence >= hardCompletionSilenceSec)))
            {
                Complete();
            }
        }

        // Stall hint (one-shot): an active page that hasn't advanced for stallHintSec and hasn't
        // completed — nudge the reader toward the Next arrow. Completion fires well before stallHintSec,
        // so a normally-read page never reaches here; cleared on the next advance / page change.
        if (_active && _recognizing && !_completed && _expected.Count > 0 && !_stallHintRaised
            && (Time.realtimeSinceStartup - _lastAdvanceTime) >= stallHintSec)
        {
            _stallHintRaised = true;
            StallHint?.Invoke();
        }
    }

    // Clear a raised stall hint (cursor advanced / page changed / completed / read-along off) so the
    // reader stops nudging. No-op when no hint is currently up.
    private void ResetStallHint()
    {
        if (!_stallHintRaised) return;
        _stallHintRaised = false;
        StallHintCleared?.Invoke();
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
            ResetStallHint(); // read-along off — drop any nudge
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
        // Read-along-local normalization IN PLACE: strip edge quotes the shared NormalizeWord keeps
        // (see ReadAlongNormalize). Length/order are preserved — _sentenceEndIdx and SetReadProgress
        // are index-aligned with the page's words — so a token may become "" and simply never match.
        for (int i = 0; i < _expected.Count; i++) _expected[i] = ReadAlongNormalize(_expected[i]);
        _sentenceEndIdx.Clear();
        if (page.ExpectedSentenceEndIndices != null) _sentenceEndIdx.AddRange(page.ExpectedSentenceEndIndices);
        _cursor = 0;
        _stuck = 0;
        _reachedLastRegion = false;
        _recognizedThisPage = false;
        _completed = false;
        ResetStallHint(); // new page — drop any nudge carried over from the previous page
        _lastAdvanceTime = Time.realtimeSinceStartup;
        _lastPartialText = "";
        _partialLenAtAdvance = 0;
        _utteranceWords.Clear();
        _pendingKind = PendingKind.None; // new page — drop any tentative advance from the previous page

        EnsureStack();

        var vocab = _expected.Where(w => w.Length > 0).Distinct().ToList(); // empties (stripped tokens) never match
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
        _pendingKind = PendingKind.None;   // <-- add: StopProcessing() is async, so a straggler
                                           // partial can still arrive and commit a stale pending
                                           // advance (Complete() -> Stop() is the live path).
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
        _micSource.SamplesReady += OnMicSamples; // drive the voice-activity gate from the existing mic stream

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

    // Read-along-local normalization: the shared NormalizeWord keeps apostrophes at word EDGES, so
    // quoted dialogue ("'Row, toads, row!'") yields expected words like 'row / row!' that are
    // out-of-vocabulary in the Vosk grammar (discarded) and can never match — the highlight stalls at
    // every quoted line. Strip edge quotes and re-trim exposed punctuation until stable ('row → row,
    // row!' → row). Read-along only: the shared NormalizeWord (and the wordbank keys that depend on it)
    // is deliberately left untouched.
    private static string ReadAlongNormalize(string s)
    {
        string w = AudioAndTextPlayer.NormalizeWord(s);
        while (true)
        {
            string w2 = w.Trim('\'');
            int a = 0, b = w2.Length - 1;
            while (a <= b && !IsReadAlongWordChar(w2[a])) a++;
            while (b >= a && !IsReadAlongWordChar(w2[b])) b--;
            w2 = a <= b ? w2.Substring(a, b - a + 1) : "";
            if (w2 == w) break;
            w = w2;
        }
        return w;
    }

    private static bool IsReadAlongWordChar(char c)
    {
        return (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '\'';
    }

    // Mic energy meter: stamp _lastVoiceTime whenever the live mic buffer's RMS clears the threshold.
    // Reuses the recognizer's existing MicrophoneSpeechSource stream (no second Microphone started).
    private void OnMicSamples(object sender, Recognissimo.SamplesReadyEventArgs e)
    {
        double sum = 0; int n = e.Length;
        for (int i = 0; i < n; i++) { float s = e.Samples[i]; sum += s * s; }
        float rms = n > 0 ? Mathf.Sqrt((float)(sum / n)) : 0f;
        if (rms >= voiceRmsThreshold) _lastVoiceTime = Time.realtimeSinceStartup;
    }

    private void Feed(string recognized, bool isFinal)
    {
        if (!isFinal && !VoiceActive) return; // gate only partials on mic energy; finals are the recognizer's
                                              // considered decision (its committed last word lands here), and a
                                              // true-silence final is empty so it can't advance anyway
        if (_expected.Count == 0) return;

        // Positional token list for THIS utterance. Vosk partials REWRITE in place (not just append —
        // "the ball" → "[unk] spins", "hot" → "hot hot" → "hot"), so position, not append order,
        // identifies an already-fed word. Keep "unk" as a position holder; drop only empties.
        var raw = new List<string>();
        if (!string.IsNullOrEmpty(recognized))
        {
            foreach (string tok in recognized.Split((char[])null, StringSplitOptions.RemoveEmptyEntries))
            {
                string nw = ReadAlongNormalize(tok);
                if (nw.Length == 0) continue; // keep "unk" (a real position); drop only empties
                raw.Add(nw);
            }
        }

        // Content-aware dedupe: feed a token only when the SAME word wasn't already fed at the SAME
        // position this utterance (handles both shrink-regrow jitter and [unk] rewrites).
        for (int i = 0; i < raw.Count; i++)
        {
            string w = raw[i];
            string seen = i < _utteranceWords.Count ? _utteranceWords[i] : null;
            if (w == seen) continue;             // already fed at this position
            if (w != "unk") MatchWord(w, isFinal);
        }

        // Merge: latest words win, but keep the longer old tail as shrink protection.
        if (raw.Count < _utteranceWords.Count)
            raw.AddRange(_utteranceWords.GetRange(raw.Count, _utteranceWords.Count - raw.Count));
        _utteranceWords = raw;

        if (isFinal) UtteranceEnd(); // commit any pending advance, then clear the utterance buffer
    }

    // Utterance boundary (a final result): the recognizer has committed its words, so flush any
    // still-pending tentative advance (both dup and skip commit as cursor = idx + 1), then clear the
    // positional buffer so the next utterance's partials start fresh.
    private void UtteranceEnd()
    {
        if (_pendingKind != PendingKind.None)
        {
            int pidx = _pendingIdx;
            _pendingKind = PendingKind.None;
            _cursor = pidx + 1;
            OnAdvance(pidx);
        }
        _utteranceWords.Clear();
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

        // 0) Resolve a tentative advance held from the PREVIOUS word before matching this one.
        if (_pendingKind != PendingKind.None)
        {
            PendingKind kind = _pendingKind; int pidx = _pendingIdx;
            _pendingKind = PendingKind.None;
            if (kind == PendingKind.Dup)
            {
                // Any next word confirms a duplicate advance — commit it, then match nw from here.
                _cursor = pidx + 1;
                Debug.Log($"[ReadAlong][DUP-COMMIT] exp[{pidx}] cursor {cursorBefore}→{_cursor}");
                OnAdvance(pidx);
            }
            else // Skip: a wide re-sync commits only if THIS word is the one after the skipped word.
            {
                if (pidx + 1 < _expected.Count && _expected[pidx + 1] == nw)
                {
                    _cursor = pidx + 2; // confirmed: consume the skipped word + this one
                    _stuck = 0;
                    Debug.Log($"[ReadAlong][SKIP-CONFIRM] '{nw}' exp[{pidx + 1}] cursor {cursorBefore}→{_cursor}");
                    OnAdvance(pidx + 1);
                    return;
                }
                // Unconfirmed: drop the tentative skip; match nw normally from the old cursor.
            }
        }

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

            // Duplicate-word guard: advancing onto a word identical to the one just consumed is likely
            // partial jitter / a re-read — hold it as a tentative 'dup' until the next word (or final)
            // confirms. Only the normal window is guarded; the look-1/2 skips still commit directly
            // (adding confirmation there stalls noisy pages — validated in the lab).
            if (_cursor > 0 && _expected[_cursor - 1] == nw)
            {
                _pendingKind = PendingKind.Dup; _pendingIdx = idx;
                Debug.Log($"[ReadAlong][DUP-PENDING] '{nw}' look{look} exp[{idx}]");
                advanced = true;
                break;
            }

            _cursor = idx + 1;
            Debug.Log($"[ReadAlong][ADV {(isFinal ? "fin" : "par")}] '{nw}' look{look} cursor {cursorBefore}→{_cursor}");
            OnAdvance(idx);
            advanced = true;
            break;
        }

        // 2) Stall re-sync: after stallK stuck words, scan a wider window and take the first
        // match — ignore stopword suppression here, it's a stall recovery. Tentative: don't commit
        // now; hold as 'skip' and confirm only when the next word matches _expected[idx+1].
        if (!advanced && _stuck >= stallK)
        {
            for (int look = 3; look <= wideLook; look++)
            {
                int idx = _cursor + look;
                if (idx >= _expected.Count) break;
                if (_expected[idx] != nw) continue;
                _pendingKind = PendingKind.Skip; _pendingIdx = idx;
                Debug.Log($"[ReadAlong][STALL-RESYNC-PENDING] '{nw}' look{look} exp[{idx}]");
                advanced = true;
                break;
            }
        }

        _stuck = advanced ? 0 : _stuck + 1;
    }

    private void OnAdvance(int matchedOrdinal)
    {
        _lastAdvanceTime = Time.realtimeSinceStartup;
        _recognizedThisPage = true; // a genuine recognized word advanced the cursor (never set by the flush)
        ResetStallHint(); // a recognized advance clears any active nudge
        _partialLenAtAdvance = _lastPartialText.Length; // for the sentence-tail flush's "not grown" guard

        int lastRegionStart = Mathf.FloorToInt(_expected.Count * (1f - lastRegionFraction));
        if (matchedOrdinal >= lastRegionStart) _reachedLastRegion = true;

        if (_page != null) _page.SetReadProgress(_cursor);
    }

    private void Complete()
    {
        _completed = true;
        ResetStallHint(); // page finished — drop any nudge
        Stop();
        Debug.Log("[ReadAlong] lenient completion → OnPageReadComplete");
        // Either runs the page's [event OnPageRead] reveal (and stays) or auto-advances by default.
        if (_prScript != null) _prScript.OnPageReadComplete();
    }

}
