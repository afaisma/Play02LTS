using System.Collections;
using System.Collections.Generic;
using Miniscript;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Runs the dialogues of the open book: collects the Dialogue* commands of each page
/// (DialogueScript), opens the sheet when the page narration ends (DialogueView), plays the
/// question, listens for a touch / a spoken answer / any sound, applies the rules (DialogueFlow)
/// and tells the script (events OnAnswer, OnNoAnswer).
///
/// Fully additive: a book without Dialogue* commands never creates the sheet, never starts the
/// microphone and never holds a page. One instance per story scene, created by PRScript on first
/// use; PRScript calls BeginBook / BeginPage / OnNarrationFinished / OnPageRead.
/// </summary>
public class DialogueController : MonoBehaviour
{
    /// <summary>
    /// PlayerPrefs switch for the microphone in dialogues (the adult's "microphone off"); default on.
    /// Off: spoken answers and "any sound" are not listened for; every dialogue is answered by touch.
    /// </summary>
    public const string MicPrefKey = "dialogue_mic";
    public const string CaptionLookAgain = "Let’s look again.";
    public const string CaptionWait = "It’s OK. I can wait.";

    public readonly DialogueScript Script = new DialogueScript();

    private PRScript _pr;
    private DialogueView _view;
    private DialogueMic _mic;
    private AudioSource _voice;
    private readonly Dictionary<string, AudioClip> _clips = new Dictionary<string, AudioClip>();

    private DialogueSpec _pending;      // DialogueShow ran; waiting for the narration to end
    private DialogueFlow _flow;         // the dialogue on screen
    private SoundTrigger _sound;
    private bool _promptLoading;
    private bool _armedSpeech;
    private float _quiet;               // seconds without an answer while nothing is being spoken
    private bool _fromReadAlong;        // opened by "I read it myself" finishing the page
    private int _playsAtPageStart;      // narration requests before this page's script ran
    private Coroutine _promptCo;

    /// <summary>The sheet is on screen.</summary>
    public bool IsOpen => _flow != null && _flow.step != DialogueFlow.Step.Closed;
    /// <summary>This page has a dialogue that is waiting or open: the page must not turn by itself.</summary>
    public bool HoldsPage => _pending != null || IsOpen;

    public static bool MicAllowed => PlayerPrefs.GetInt(MicPrefKey, 1) == 1;

    public static DialogueController For(PRScript pr)
    {
        var c = pr.GetComponent<DialogueController>();
        if (c == null)
        {
            c = pr.gameObject.AddComponent<DialogueController>();
            c._pr = pr;
            c.Script.Warn = m => Debug.LogWarning("[Dialogue] " + m);
            DialogueCommands.Register();
        }
        return c;
    }

    // ---------------------------------------------------------------- called by PRScript

    public void BeginBook()
    {
        DialogueCommands.Current = this;
        CloseNow();
        Script.BeginBook();
    }

    public void BeginPage()
    {
        DialogueCommands.Current = this;
        CloseNow();
        ForgetClips();
        Script.BeginPage();
        _playsAtPageStart = _pr.audioAndTextPlayer != null ? _pr.audioAndTextPlayer.playRequests : 0;
    }

    /// <summary>
    /// The page script has run. A page that asked for no narration has nothing to wait for, so
    /// its dialogue opens now (otherwise it would wait for a narration end that never comes).
    /// </summary>
    public void EndPage()
    {
        if (_pending == null || _pr.audioAndTextPlayer == null) return;
        if (_pr.audioAndTextPlayer.playRequests == _playsAtPageStart) OpenPending();
    }

    /// <summary>The DialogueShow command.</summary>
    public void Show(string when)
    {
        DialogueSpec spec = Script.Take();
        if (spec == null) return;
        _pending = spec;
        if (_pr.audioAndTextPlayer != null) _pr.audioAndTextPlayer.holdAutoNextStep = true;
        if ((when ?? "").Trim().ToLowerInvariant() == "now") OpenPending();
    }

    /// <summary>The page narration ended.</summary>
    public void OnNarrationFinished()
    {
        // "I read it myself": the (muted) page audio ends long before the child has read the page.
        // The dialogue opens when the reading is complete (OnPageRead).
        if (_pr.audioAndTextPlayer != null && _pr.audioAndTextPlayer.ReadAlongActive) return;
        OpenPending();
    }

    /// <summary>Read-along finished the page. True = a dialogue opened, do not turn the page.</summary>
    public bool OnPageRead()
    {
        OpenPending();
        if (IsOpen) _fromReadAlong = true;
        return IsOpen;
    }

    // ---------------------------------------------------------------- open / close

    private void OpenPending()
    {
        if (_pending == null) return;
        // The reading-mode picker replays the page when it closes; the dialogue comes back then.
        if (UnifiedReadingModePicker.IsOpen) return;

        DialogueSpec spec = _pending;
        _pending = null;
        _flow = new DialogueFlow(spec);
        _quiet = 0f;

        if (_view == null) _view = DialogueView.Create();
        bool voiceOn = spec.VoiceWanted && MicAllowed;
        _view.Show(spec, voiceOn, PictureBottomScreenY(), ResolveUrl,
            id => Answer(id, "touch"), ReplayQuestion, Skip, NextPage, () => Answer("", "touch"));

        Ask();
        Debug.Log("[Dialogue] open: " + spec.question);
    }

    private void CloseNow()
    {
        _pending = null;
        _fromReadAlong = false;
        StopListening();
        StopMic();
        StopPrompt();
        if (_flow != null)
        {
            _flow = null;
            if (_view != null) _view.Hide();
        }
        if (_pr != null && _pr.audioAndTextPlayer != null) _pr.audioAndTextPlayer.holdAutoNextStep = false;
    }

    // Skip, DialogueOnOther "close", DialogueOnSilence "close": the story goes on as it would
    // have without the question. Autopage (or a page the child has just read aloud) turns the
    // page; otherwise the child turns it. The last page ends the book.
    private void CloseAndGoOn()
    {
        bool turn = _fromReadAlong || (_pr.audioAndTextPlayer != null && _pr.audioAndTextPlayer.IsAutoplaying);
        bool last = _pr.IsLastPage;
        CloseNow();
        if (last) EndBook();
        else if (turn) _pr.NextStep();
    }

    // The last page is done: the Read-next sheet. If the page is still being narrated (a dialogue
    // shown with "now"), the end of the narration brings the sheet, as on a page without a dialogue.
    private void EndBook()
    {
        if (_pr.audioAndTextPlayer != null && _pr.audioAndTextPlayer.IsPlaying) return;
        _pr.OnLastStepFinished();
    }

    // The "Next page" / "Done" button of the praise. Guarded: the button stays on screen while
    // the sheet slides away, and a second tap must not turn a second page.
    private void NextPage()
    {
        if (!IsOpen || _flow.step != DialogueFlow.Step.Praise) return;
        bool last = _pr.IsLastPage;
        CloseNow();
        if (last) EndBook();
        else _pr.NextStep();
    }

    private void Skip()
    {
        if (!IsOpen) return;
        _flow.Skip();
        CloseAndGoOn();
    }

    // ---------------------------------------------------------------- asking and answering

    // Speak the question, then listen.
    private void Ask()
    {
        StopListening();
        PlayPrompt(_flow.spec.questionAudio);
        StartListening();
        _quiet = 0f;
    }

    private void ReplayQuestion()
    {
        if (!IsOpen || _flow.step != DialogueFlow.Step.Ask) return;
        Ask();
    }

    private void Answer(string id, string how)
    {
        if (!IsOpen) return;
        DialogueFlow flow = _flow;
        DialogueFlow.Reaction reaction = flow.Answer(id);
        if (reaction == DialogueFlow.Reaction.None) return;
        // For the book: was it THE answer (also true when the dialogue has none, or any sound counts).
        bool right = flow.spec.IsSound || flow.spec.answer.Length == 0 || id == flow.spec.answer;
        Debug.Log($"[Dialogue] answer '{id}' by {how}: {reaction} (attempt {flow.attempts})");

        switch (reaction)
        {
            case DialogueFlow.Reaction.Correct:
                StopListening();
                StopMic();
                string praise = flow.spec.praiseText.Length > 0 ? flow.spec.praiseText : DialogueSpec.DefaultPraise;
                _view.ShowPraise(flow, praise, _pr.IsLastPage ? DialogueView.TextDone : DialogueView.TextNextPage);
                PlayPrompt(flow.spec.praiseAudio);
                break;
            case DialogueFlow.Reaction.Retry:
                _view.Refresh(flow, CaptionLookAgain);
                Ask();
                break;
        }

        // The book's own reaction comes after the built-in one, while this page is still the
        // current one (so nCurrentStep in the handler is the page of the question).
        _pr.RunStoryEvent("OnAnswer", new Dictionary<string, Value>
        {
            { "answer", new ValString(id) },
            { "isCorrect", right ? ValNumber.one : ValNumber.zero },
            { "how", new ValString(how) },
            { "attempt", new ValNumber(flow.attempts) },
        });

        // Going on comes last. Not if the handler has already turned the page.
        if (reaction == DialogueFlow.Reaction.Close && _flow == flow) CloseAndGoOn();
    }

    private void Silence()
    {
        DialogueFlow flow = _flow;
        DialogueFlow.SilenceReaction reaction = flow.Silence();
        // After the last repeat the dialogue waits quietly: no question, no event.
        if (reaction == DialogueFlow.SilenceReaction.None) return;
        Debug.Log($"[Dialogue] no answer ({flow.silences}): {reaction}");
        if (reaction == DialogueFlow.SilenceReaction.Repeat)
        {
            _view.Refresh(flow, CaptionWait);
            Ask();
        }
        _pr.RunStoryEvent("OnNoAnswer", new Dictionary<string, Value>
        {
            { "attempt", new ValNumber(flow.silences) },
        });
        if (reaction == DialogueFlow.SilenceReaction.Close && _flow == flow) CloseAndGoOn();
    }

    private void Update()
    {
        if (!IsOpen) return;
        // The reading-mode picker came up over the question (it opens by itself on the first page
        // of a book). The picker replays the page when it closes; the question comes back then.
        if (UnifiedReadingModePicker.IsOpen) { CloseNow(); return; }
        bool speaking = IsSpeaking();
        _view.SetSpeaking(_promptLoading || (_voice != null && _voice.isPlaying));
        if (_flow.step != DialogueFlow.Step.Ask) return;

        // One long frame (a system alert, the app coming back) must not count as seconds of silence.
        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);

        if (_sound != null && _mic != null && _mic.HasData)
        {
            if (speaking) _view.SetLevel(0f);
            else
            {
                // The meter is full at four times the trigger level.
                _view.SetLevel(_mic.Level / (_sound.Threshold * 4f));
                if (_sound.Feed(_mic.Level, dt)) { Answer("", "sound"); return; }
            }
        }

        // Silence clock: only while the app itself is quiet.
        if (speaking) { _quiet = 0f; return; }
        _quiet += dt;
        if (_quiet >= _flow.spec.settings.silenceSeconds)
        {
            _quiet = 0f;
            Silence();
        }
    }

    // ---------------------------------------------------------------- listening

    private void StartListening()
    {
        DialogueSpec spec = _flow.spec;
        if (spec.IsSound)
        {
            // The microphone runs for the whole dialogue; a repeated question only measures the
            // room again.
            if (_mic == null) _mic = gameObject.AddComponent<DialogueMic>();
            _sound = new SoundTrigger();
            if (!_mic.Running) StartCoroutine(BeginMic());
            return;
        }
        if (!spec.VoiceWanted || !MicAllowed) return;

        List<DialogueChoice> open = _flow.Open();
        List<string> words = DialogueSpeech.Vocabulary(open);
        if (words.Count == 0) return;
        _armedSpeech = true;
        SpeechListenService.Get().ArmPhrases(words,
            text => DialogueSpeech.Match(text, open),
            id => Answer(id, "voice"),
            IsSpeaking);
    }

    private IEnumerator BeginMic()
    {
        if (!MicAllowed) yield break; // the "Tap to answer" button is the way to answer
        if (!Application.HasUserAuthorization(UserAuthorization.Microphone))
            yield return Application.RequestUserAuthorization(UserAuthorization.Microphone);
        // The dialogue may have closed while the permission question was up.
        if (IsOpen && _flow.spec.IsSound && _flow.step == DialogueFlow.Step.Ask && !_mic.Begin())
            Debug.Log("[Dialogue] no microphone: this dialogue is answered by touch only");
    }

    private void StopListening()
    {
        if (_armedSpeech)
        {
            _armedSpeech = false;
            SpeechListenService.Instance?.Disarm();
        }
        _sound = null;
    }

    private void StopMic()
    {
        if (_mic != null) _mic.End();
    }

    // True while the app itself makes sound: nothing heard then is an answer.
    private bool IsSpeaking()
    {
        if (_promptLoading || (_voice != null && _voice.isPlaying)) return true;
        if (_pr.audioAndTextPlayer != null && _pr.audioAndTextPlayer.IsPlaying) return true;
        return _pr.audioPlayer != null && _pr.audioPlayer.IsPlaying;
    }

    // ---------------------------------------------------------------- question / praise audio

    private void PlayPrompt(string reference)
    {
        StopPrompt();
        if (string.IsNullOrEmpty(reference)) return;
        _promptCo = StartCoroutine(PlayPromptCo(reference));
    }

    private void StopPrompt()
    {
        if (_promptCo != null) { StopCoroutine(_promptCo); _promptCo = null; }
        _promptLoading = false;
        if (_voice != null) _voice.Stop();
    }

    // A missing or unreachable recording is not an error for the child: the question is on screen.
    private IEnumerator PlayPromptCo(string reference)
    {
        if (_voice == null)
        {
            _voice = gameObject.AddComponent<AudioSource>();
            _voice.playOnAwake = false;
        }
        if (!_clips.TryGetValue(reference, out AudioClip clip))
        {
            _promptLoading = true;
            string url = ResolveUrl(reference + ".mp3");
            using (UnityWebRequest req = UnityWebRequestMultimedia.GetAudioClip(url, AudioType.MPEG))
            {
                req.timeout = 10;
                yield return req.SendWebRequest();
                if (req.result == UnityWebRequest.Result.Success) clip = DownloadHandlerAudioClip.GetContent(req);
                else Debug.Log($"[Dialogue] no recording at {url} ({req.error})");
            }
            _clips[reference] = clip; // also remembers a miss, so a replay does not ask again
            _promptLoading = false;
        }
        if (clip != null)
        {
            _voice.clip = clip;
            _voice.Play();
        }
        _promptCo = null;
    }

    private void ForgetClips()
    {
        foreach (AudioClip clip in _clips.Values)
            if (clip != null) Destroy(clip);
        _clips.Clear();
    }

    // ---------------------------------------------------------------- helpers

    // Book-relative reference ("images//beanstalk.jpg") -> full address, with the book's
    // content revision so a changed file is fetched again.
    private string ResolveUrl(string reference)
    {
        string url = _pr.baseURL + (reference ?? "").Replace("//", "/");
        return Globals.WithContentRev(url, Globals.g_prbook != null ? Globals.g_prbook.contentRev : "");
    }

    // Lower edge of the page picture in screen pixels, so the sheet leaves the picture visible.
    private float PictureBottomScreenY()
    {
        var gallery = _pr.storyStepsUI != null ? _pr.storyStepsUI.gallery : null;
        var rt = gallery != null ? gallery.transform as RectTransform : null;
        if (rt == null) return -1f;
        var corners = new Vector3[4];
        rt.GetWorldCorners(corners);
        var canvas = rt.GetComponentInParent<Canvas>();
        Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        return RectTransformUtility.WorldToScreenPoint(cam, corners[0]).y;
    }

    private void OnDestroy()
    {
        StopListening();
        StopMic();
        ForgetClips();
        if (DialogueCommands.Current == this) DialogueCommands.Current = null;
        if (_view != null) Destroy(_view.gameObject);
    }
}
