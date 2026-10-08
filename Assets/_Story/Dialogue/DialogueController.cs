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
    /// <summary>Set when the microphone was switched off because the system refused the permission.</summary>
    public const string MicRefusedPrefKey = "dialogue_mic_refused";
    public const string CaptionLookAgain = "Let’s look again.";
    public const string CaptionWait = "It’s OK. I can wait.";

    public readonly DialogueScript Script = new DialogueScript();
    private readonly StarRow _stars = new StarRow();

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
    private bool _narrationOver;        // this page's narration has ended (or the page has none)
    private float _pageStartedAt;
    private Coroutine _promptCo;

    /// <summary>The sheet is on screen.</summary>
    public bool IsOpen => _flow != null && _flow.step != DialogueFlow.Step.Closed;
    /// <summary>This page has a dialogue that is waiting or open: the page must not turn by itself.</summary>
    public bool HoldsPage => _pending != null || IsOpen;

    public static bool MicAllowed
    {
        get
        {
            if (PlayerPrefs.GetInt(MicPrefKey, 1) == 1) return true;
            // Off only because the system said no: on again as soon as the grown-up has allowed
            // the microphone in the system settings. ("Tap only" stays off.)
            if (PlayerPrefs.GetInt(MicRefusedPrefKey, 0) != 1) return false;
            if (!DialogueMicPermission.Granted) return false;
            PlayerPrefs.SetInt(MicPrefKey, 1);
            PlayerPrefs.DeleteKey(MicRefusedPrefKey);
            PlayerPrefs.Save();
            return true;
        }
    }

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

    /// <param name="preamble">The script lines before the first page.</param>
    /// <param name="pages">The script text of every page, in order.</param>
    public void BeginBook(string preamble, IList<string> pages)
    {
        DialogueCommands.Current = this;
        CloseNow();
        Script.BeginBook();

        // One place in the row of stars for every question that earns one - from the page the
        // book opens at (a book resumed at page 6 does not show the earlier questions as missed).
        var starPages = new List<int>();
        for (int i = Mathf.Max(0, _pr.nCurrentStep + 1); i < pages.Count; i++)
            if (DialogueScript.PageEarnsStar(preamble, pages[i])) starPages.Add(i);
        _stars.Reset(starPages);
        HideSummary();
        // Made now, not at the first answer (building it there could cost a frame of the animation).
        if (starPages.Count > 0) DialogueRewards.Chime(DialogueRewards.Level == RewardLevel.Lively);
    }

    public void BeginPage()
    {
        DialogueCommands.Current = this;
        CloseNow();
        ForgetClips();
        Script.BeginPage();
        _playsAtPageStart = _pr.audioAndTextPlayer != null ? _pr.audioAndTextPlayer.playRequests : 0;
        _narrationOver = false;
        _pageStartedAt = Time.realtimeSinceStartup;
    }

    /// <summary>
    /// The page script has run. A page that asked for no narration has nothing to wait for, so
    /// its dialogue opens now (otherwise it would wait for a narration end that never comes).
    /// </summary>
    public void EndPage()
    {
        if (_pr.audioAndTextPlayer == null) return;
        if (_pr.audioAndTextPlayer.playRequests != _playsAtPageStart) return;
        _narrationOver = true;
        if (_pending != null) OpenPending();
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
        _narrationOver = true;
        // "I read it myself": the (muted) page audio ends long before the child has read the page.
        // The dialogue opens when the reading is complete (OnPageRead).
        if (_pr.audioAndTextPlayer != null && _pr.audioAndTextPlayer.ReadAlongActive) return;
        OpenPending();
    }

    /// <summary>
    /// Read-along finished the page. True = a dialogue is open, do not turn the page.
    /// staysOnPage: the book has its own OnPageRead handler, which keeps the page; closing the
    /// question then must not turn it either.
    /// </summary>
    public bool OnPageRead(bool staysOnPage)
    {
        OpenPending();
        if (IsOpen) _fromReadAlong = !staysOnPage;
        return IsOpen;
    }

    /// <summary>
    /// The book has ended (the Read-next sheet is about to come): show the collected stars.
    /// Nothing for a book without questions, or when none was answered.
    /// </summary>
    public void ShowSummary()
    {
        if (_view == null || _stars.Earned == 0) return;
        _view.ShowSummary(DialogueRewards.SummaryText(_stars.Earned), _stars.States());
    }

    /// <summary>The page changed (or the book is opened again): the summary goes.</summary>
    public void HideSummary()
    {
        if (_view != null) _view.HideSummary();
    }

    /// <summary>
    /// The reader asks for the next page (the Next arrow or a swipe). In "I read it myself" the
    /// question opens only when the page has been read aloud, so a child who cannot read the page
    /// would never see it: there the question opens now, and the page turns after it.
    /// True = a question opened; do not turn the page. (When the app reads, the Next arrow during
    /// the narration keeps its meaning: go on, without the question.)
    /// </summary>
    public bool OpenBeforePageTurn()
    {
        if (_pending == null || _pr.audioAndTextPlayer == null || !_pr.audioAndTextPlayer.ReadAlongActive) return false;
        if (UnifiedReadingModePicker.IsOpen) return false;
        // As when the page has been read: read-along stops listening, the question listens now.
        var readAlong = FindObjectOfType<ReadAlongService>();
        if (readAlong != null) readAlong.Stop();
        OpenPending();
        if (IsOpen) _fromReadAlong = true; // Skip then turns the page, which is what was asked for
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
        // The "Say it or tap it" sign only when something can really be heard (picture choices
        // without a word have nothing to listen for).
        bool voiceOn = spec.VoiceWanted && MicAllowed && DialogueSpeech.Vocabulary(spec.choices).Count > 0;
        _view.Show(spec, voiceOn, PictureBottomScreenY(), ResolveUrl,
            id => Answer(id, "touch"), ReplayQuestion, Skip, NextPage, () => Answer("", "touch"));

        Ask();
        Debug.Log("[Dialogue] open: " + spec.question);
        if (!PlayerPrefs.HasKey(DialogueRewards.UsedPrefKey))
        {
            PlayerPrefs.SetInt(DialogueRewards.UsedPrefKey, 1); // Settings now offers the reward level
            PlayerPrefs.Save();
        }
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
        // A question shown with "now" can be closed while the page is still being read: then the
        // end of the narration turns the page (or brings the puzzle button), as without a question.
        else if (NarrationAudible) { }
        else if (turn) _pr.NextStep();
        else _pr.OnDialogueClosedOnPage();
    }

    private bool NarrationAudible => _pr.audioAndTextPlayer != null && _pr.audioAndTextPlayer.IsAudible;

    // The last page is done: the Read-next sheet. If the page is still being narrated (a dialogue
    // shown with "now"), the end of the narration brings the sheet, as on a page without a dialogue.
    private void EndBook()
    {
        if (NarrationAudible) return;
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
                Reward(flow.spec, how);
                break;
            case DialogueFlow.Reaction.Retry:
                _view.Refresh(flow, CaptionLookAgain);
                Ask();
                break;
        }

        // DialogueOnOther "close": the child did answer, so the star is earned (shown at the end).
        // Before the book's handler, which may turn the page.
        if (reaction == DialogueFlow.Reaction.Close && flow.spec.settings.reward) _stars.Earn(_pr.nCurrentStep);

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

    // The reward for an answered question: a star for every answer (also on a second try, also
    // by "any sound"), a little more for an answer given by voice or sound, then the praise.
    private void Reward(DialogueSpec spec, string how)
    {
        RewardLevel level = DialogueRewards.Level;
        if (spec.settings.reward)
        {
            bool spoken = how != "touch";
            int place = _stars.Earn(_pr.nCurrentStep);
            _view.ShowReward(_stars.States(), place, spoken, spoken ? DialogueRewards.TextHeard : "",
                level == RewardLevel.Lively);
        }
        StopPrompt();
        _promptCo = StartCoroutine(RewardThenPraiseCo(spec, level));
    }

    private IEnumerator RewardThenPraiseCo(DialogueSpec spec, RewardLevel level)
    {
        if (spec.settings.reward && level != RewardLevel.Quiet)
        {
            // The book's own sound for this question, or the chime.
            AudioClip clip = null;
            if (spec.settings.rewardSound.Length > 0)
            {
                yield return LoadClipCo(spec.settings.rewardSound);
                _clips.TryGetValue(spec.settings.rewardSound, out clip);
            }
            if (clip == null) clip = DialogueRewards.Chime(level == RewardLevel.Lively);
            EnsureVoice();
            _voice.clip = clip;
            _voice.Play();
            yield return new WaitForSecondsRealtime(Mathf.Min(clip.length, 3f));
            _voice.Stop();
        }
        yield return PlayPromptCo(spec.praiseAudio);
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
            (text, final) => DialogueSpeech.Match(text, open, final),
            id => Answer(id, "voice"),
            IsSpeaking);
    }

    private IEnumerator BeginMic()
    {
        if (!MicAllowed) yield break; // the "Tap to answer" button is the way to answer
        if (!DialogueMicPermission.Granted) yield return DialogueMicPermission.Request();
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
        if (NarrationAudible) return true;
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

    private void EnsureVoice()
    {
        if (_voice != null) return;
        _voice = gameObject.AddComponent<AudioSource>();
        _voice.playOnAwake = false;
    }

    private IEnumerator PlayPromptCo(string reference)
    {
        if (!string.IsNullOrEmpty(reference))
        {
            // A question shown with "now" opens while the page is still being read: its recording
            // waits for the narration, so that two voices never speak at once. (The narration
            // starts a moment after the page script, hence the short wait at the page start.)
            while (!_narrationOver && (NarrationAudible || Time.realtimeSinceStartup - _pageStartedAt < 1.5f))
                yield return null;

            yield return LoadClipCo(reference);
            if (_clips.TryGetValue(reference, out AudioClip clip) && clip != null)
            {
                EnsureVoice();
                _voice.clip = clip;
                _voice.Play();
            }
        }
        _promptCo = null;
    }

    // A missing or unreachable recording is not an error for the child: the question is on screen.
    private IEnumerator LoadClipCo(string reference)
    {
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
