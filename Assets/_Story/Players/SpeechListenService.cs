using System;
using System.Collections;
using System.Collections.Generic;
using Recognissimo;
using Recognissimo.Components;
using UnityEngine;

// ============================================================================================
// Mode B — speech "summon": listen for ONE keyword on a page and react. The EASY case: a single-
// word grammar ({word, "[unk]"}), no reading cursor, no matcher, none of the read-along machinery.
//
// Driven entirely by the ListenFor intrinsic (PRScript). A page chunk calls
//   ListenFor "octopus", hintAfter: 4
// which Arms this service; on hearing the word the OnHeard story event fires; off-script speech
// fires OnNotUnderstood; silence for hintAfter seconds fires the hint (re-prompt).
//
// Fully additive + opt-in: nothing here runs unless a script calls ListenFor (so books without it
// are byte-identical). Self-bootstrapped + kept warm, like ReadAlongService, with the SAME async
// Stop→Start grammar lifecycle (a synchronous restart drops the new grammar). Mode B and read-along
// never share a page, so this owns its own recognizer and never touches ReadAlongService.
// Rollback = delete this file + the ListenFor intrinsic + the two event hooks in PRScript.
// ============================================================================================
public class SpeechListenService : MonoBehaviour
{
    public static SpeechListenService Instance { get; private set; }

    // ---- Recognissimo stack (lazy, created once, kept warm across pages) ----
    private SpeechRecognizer _recognizer;
    private MicrophoneSpeechSource _micSource;
    private bool _stackReady;
    private bool _recognizing;
    private Coroutine _restartCo; // pending deferred restart (waits for the async StopProcessing)

    [Tooltip("Min seconds between OnNotUnderstood reprompts (prevents babble/noise spam).")]
    [SerializeField] private float notUnderstoodCooldown = 2f;
    [Tooltip("Max silence re-prompts before falling silent (keeps listening).")]
    [SerializeField] private int maxHints = 3;

    // ---- per-arm state ----
    private bool _armed;
    private bool _listening;             // recognizer genuinely started (after the prompt went quiet)
    private string _target = "";        // normalized keyword to listen for
    private float _hintAfterSec;
    private float _armTime;              // set when listening ACTUALLY starts (after the prompt is quiet)
    private bool _heardFired;            // onHeard latch (debounce: fire once, then Disarm)
    private bool _heardSpeech;           // any speech detected since arming (suppresses the hint)
    private bool _hintFired;
    private int _hintCount;              // hints fired this page (reset only on a real page/word change)
    private float _lastNotUnderstood;    // last OnNotUnderstood time (cooldown gate)
    private Action _onHeard;
    private Action _onNotUnderstood;
    private Action _onHint;
    private Func<bool> _isSpeaking;      // true while the app's prompt audio is playing
    private Coroutine _armCo;            // pending "wait for prompt to finish, then listen" coroutine

    // Pre-create one warm instance; it stays inert until a script calls ListenFor → Arm().
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;
        new GameObject("SpeechListenService").AddComponent<SpeechListenService>();
    }

    // Lazy accessor so ListenFor works even before the bootstrap callback has run.
    public static SpeechListenService Get()
    {
        if (Instance == null)
            new GameObject("SpeechListenService").AddComponent<SpeechListenService>();
        return Instance;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // ---------------------------------------------------------------- public API

    // Listen for `word` on the current page. Single-word grammar; the model stays loaded across arms.
    // isSpeaking reports whether the app's prompt TTS is playing — listening is DEFERRED until it
    // stops so the mic never hears the app say the keyword (the self-trigger bug).
    public void Arm(string word, float hintAfterSec, Action onHeard, Action onNotUnderstood,
        Action onHint, Func<bool> isSpeaking)
    {
        // A re-arm cancels any pending deferred start.
        if (_armCo != null) { StopCoroutine(_armCo); _armCo = null; }

        // Reset the hint budget only on a real page/word change; same-word re-arms (the hint re-prompt
        // path) keep counting toward maxHints so re-prompts can't loop forever.
        string newTarget = AudioAndTextPlayer.NormalizeWord(word);
        if (newTarget != _target) _hintCount = 0;

        _target = newTarget;
        _hintAfterSec = hintAfterSec;
        _onHeard = onHeard;
        _onNotUnderstood = onNotUnderstood;
        _onHint = onHint;
        _isSpeaking = isSpeaking;

        _armed = true;
        _listening = false; // not yet — ArmWhenQuiet starts the recognizer after the prompt is quiet
        _heardFired = false;
        _heardSpeech = false;
        _hintFired = false;

        if (string.IsNullOrEmpty(_target))
        {
            Debug.LogWarning("[Listen] ListenFor called with an empty/unmappable word; not arming.");
            _armed = false;
            return;
        }

        EnsureStack();
        _recognizer.Vocabulary = new List<string> { _target, "[unk]" };
        _armCo = StartCoroutine(ArmWhenQuiet());
        Debug.Log($"[Listen] arm '{_target}' hintAfter={hintAfterSec} (waiting for prompt to finish)");
    }

    // Hold the recognizer off until the prompt audio has stopped (+ a short tail/echo settle), so the
    // prompt is never fed into the recognizer's stream. Only then start listening and start the hint clock.
    private IEnumerator ArmWhenQuiet()
    {
        while (_armed && _isSpeaking != null && _isSpeaking())
            yield return null;
        yield return new WaitForSeconds(0.15f); // let the speaker tail / echo decay

        _armCo = null;
        if (!_armed) yield break;

        _armTime = Time.realtimeSinceStartup; // hint clock starts now (after the prompt)
        StartListening();
        _listening = true; // recognizer is genuinely listening → the hint clock may run
    }

    public void Disarm()
    {
        _armed = false;
        _listening = false;
        _onHeard = _onNotUnderstood = _onHint = null;
        if (_armCo != null) { StopCoroutine(_armCo); _armCo = null; }
        if (_restartCo != null) { StopCoroutine(_restartCo); _restartCo = null; }
        if (_recognizer != null && _recognizing) _recognizer.StopProcessing();
    }

    // ---------------------------------------------------------------- recognition lifecycle

    // Async Stop→Start (same fix as ReadAlongService): StopProcessing() is async — the recognizer
    // finishes on a later frame (Finished sets _recognizing=false). A synchronous restart would run
    // Setup while the previous session is tearing down and the new grammar would never install.
    private void StartListening()
    {
        if (_recognizer == null) return;
        if (_restartCo != null) { StopCoroutine(_restartCo); _restartCo = null; }

        if (!_recognizing)
        {
            _recognizer.SpeechSource = _micSource;
            _recognizer.StartProcessing();
            return;
        }

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
        _recognizer.StartProcessing(); // installs the Vocabulary set by Arm() for this keyword
    }

    // Build the Recognissimo stack on an INACTIVE child, configure it, then activate (so each
    // OnEnable sees configured fields). Kept warm; never disposed between arms.
    private void EnsureStack()
    {
        if (_stackReady) return;

        var sttGO = new GameObject("SpeechListenRecognissimo");
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
        _recognizer.InitializationFailed.AddListener(e => { _recognizing = false; Debug.LogError("[Listen] init failed: " + e); });
        _recognizer.RuntimeFailed.AddListener(e => { _recognizing = false; Debug.LogError("[Listen] runtime failed: " + e); });

        sttGO.SetActive(true);
        _stackReady = true;
    }

    // ---------------------------------------------------------------- results

    private void OnPartial(PartialResult p) => Handle(p.partial, false);
    private void OnResult(Result r) => Handle(r.text, true);

    // Single-word match test (no cursor): the keyword present → onHeard ONCE (latch, then Disarm).
    // Any recognized token marks speech (so the hint timer won't fire mid-utterance). A FINAL result
    // with speech but not the keyword (off-script / only [unk]) → onNotUnderstood.
    private void Handle(string recognized, bool isFinal)
    {
        if (!_armed) return;
        // Ignore anything heard while the app is speaking (covers onNotUnderstood/onHint re-prompt
        // overlap and any straggler results between StopProcessing and the prompt actually ending).
        if (_isSpeaking != null && _isSpeaking()) return;

        bool sawTarget = false;
        bool sawSpeech = false;
        if (!string.IsNullOrEmpty(recognized))
        {
            foreach (string raw in recognized.Split((char[])null, StringSplitOptions.RemoveEmptyEntries))
            {
                string nw = AudioAndTextPlayer.NormalizeWord(raw);
                if (nw.Length == 0) continue; // (NormalizeWord("[unk]") == "unk" → still counts as speech)
                sawSpeech = true;
                if (nw == _target) sawTarget = true;
            }
        }

        if (sawSpeech) _heardSpeech = true;

        if (sawTarget && !_heardFired)
        {
            _heardFired = true;
            Debug.Log($"[Listen] heard '{_target}'");
            var cb = _onHeard;
            Disarm();        // debounce: latch + stop before reacting
            cb?.Invoke();
            return;
        }

        if (isFinal && !_heardFired && sawSpeech
            && Time.realtimeSinceStartup - _lastNotUnderstood >= notUnderstoodCooldown)
        {
            _lastNotUnderstood = Time.realtimeSinceStartup;
            Debug.Log("[Listen] not understood (off-script speech)");
            _onNotUnderstood?.Invoke();
        }
    }

    // Hint timer: no speech for hintAfterSec → fire the hint (re-prompt), then keep listening.
    // Runs only once the recognizer is genuinely listening (_listening), so the pre-prompt wait with
    // a stale _armTime can't fire a hint instantly and loop.
    private void Update()
    {
        if (!_armed || !_listening || _heardFired || _hintFired || _heardSpeech) return;
        // While the app is speaking (e.g. mid-prompt), don't count it as silence — rebase the clock.
        if (_isSpeaking != null && _isSpeaking())
        {
            _armTime = Time.realtimeSinceStartup;
            return;
        }
        if (Time.realtimeSinceStartup - _armTime >= _hintAfterSec)
        {
            _hintFired = true;
            if (_hintCount < maxHints)
            {
                _hintCount++;
                Debug.Log($"[Listen] hint (no speech) {_hintCount}/{maxHints}");
                _onHint?.Invoke();
            }
            else
            {
                Debug.Log("[Listen] hint budget exhausted — listening silently");
            }
        }
    }
}
