#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Encoder;
using UnityEditor.Recorder.Input;
using UnityEngine;

// Editor-only helper that records GameView+audio clips of books reading themselves aloud.
//
// SAFETY: inert unless explicitly armed for the current play session. Add it to a scene, call
// ClipRecorder.Arm(), then Play — it records and exits play. A leftover/forgotten instance on a
// NORMAL Play is not armed, so it removes itself and does nothing. Arming is consumed at Awake.
//
// Modes:
//   - single: set `bookName` + `outputPathNoExt`
//   - batch:  set `bookNames` (+ `outputDir`); each book is recorded in ONE play session to
//             outputDir/<slug>.mp4. (slug = lowercase, non-alnum -> '_')
//
// Visual changes (hiding chrome, warming the camera clear) are tracked and restored per book.
public class ClipRecorder : MonoBehaviour
{
    public string bookName = "The Snow Queen";
    public string[] bookNames;                 // batch mode (takes priority if non-empty)
    public string outputDir = "";              // batch output folder
    public float maxSeconds = 240f;            // per-book safety cap
    public float endHoldSeconds = 3f;          // linger on the last page
    public int outW = 1080, outH = 1920;
    public string outputPathNoExt =
        "/Users/alexanderfaisman/Library/Application Support/Claude/local-agent-mode-sessions/f86a9f4d-b203-4775-a506-2ca322c8258b/c932ea7c-d449-4066-ad03-18d94cf2a38e/local_7447ecb1-0034-4683-8795-ab7eba33fd40/outputs/rb_video/reader_clip";

    private const string ArmKey = "ClipRecorder.Armed";
    private bool _armed;
    private PRScript _curPr;                    // reader instance recorded in the last RecordOne
    private readonly List<GameObject> _hidden = new();
    private Camera _bgCam; private Color _bgColor; private bool _bgChanged;

    public static void Arm() => SessionState.SetBool(ArmKey, true);

    private void Awake()
    {
        _armed = SessionState.GetBool(ArmKey, false);
        SessionState.SetBool(ArmKey, false);
        if (!_armed) { Debug.Log("[ClipRecorder] not armed; self-removing (no-op)."); Destroy(gameObject); return; }
        Application.runInBackground = true;
        DontDestroyOnLoad(gameObject);
    }

    private void Start() { if (_armed) StartCoroutine(Run()); }

    private IEnumerator Run()
    {
        // Wait for the catalog once.
        float t0 = Time.time;
        while ((Globals.g_listPRBooks == null || Globals.g_listPRBooks.Count == 0) && Time.time - t0 < 25f)
            yield return new WaitForSeconds(0.25f);
        Debug.Log("[ClipRecorder] catalog books = " + (Globals.g_listPRBooks?.Count ?? -1));

        var names = (bookNames != null && bookNames.Length > 0) ? bookNames : new[] { bookName };
        string dir = !string.IsNullOrEmpty(outputDir)
            ? outputDir : System.IO.Path.GetDirectoryName(outputPathNoExt);

        PRScript prev = null;
        for (int i = 0; i < names.Length; i++)
        {
            string outNoExt = (bookNames != null && bookNames.Length > 0)
                ? System.IO.Path.Combine(dir, Slug(names[i]))
                : outputPathNoExt;
            Debug.Log($"[ClipRecorder] ({i + 1}/{names.Length}) {names[i]} -> {outNoExt}.mp4");
            yield return StartCoroutine(RecordOne(names[i], outNoExt, prev));
            prev = _curPr;                       // next book must wait for a different reader instance
            Time.timeScale = 1f;                 // ensure time runs before the next book loads
            yield return new WaitForSeconds(1f);
        }

        Debug.Log("[ClipRecorder] BATCH DONE (" + names.Length + " books)");
        EditorApplication.isPlaying = false;
    }

    private IEnumerator RecordOne(string nm, string outNoExt, PRScript oldPr)
    {
        var settings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
        settings.SetRecordModeToManual();
        settings.FrameRate = 30; settings.CapFrameRate = false;

        var movie = ScriptableObject.CreateInstance<MovieRecorderSettings>();
        movie.name = "ReaderClip"; movie.Enabled = true;
        movie.EncoderSettings = new CoreEncoderSettings
        { Codec = CoreEncoderSettings.OutputCodec.MP4, EncodingQuality = CoreEncoderSettings.VideoEncodingQuality.High };
        movie.ImageInputSettings = new GameViewInputSettings { OutputWidth = outW, OutputHeight = outH };
        movie.AudioInputSettings.PreserveAudio = true; movie.CaptureAudio = true;
        movie.OutputFile = outNoExt;
        settings.AddRecorderSettings(movie);

        var controller = new RecorderController(settings);
        controller.PrepareRecording();
        controller.StartRecording(); // start immediately — PrepareRecording freezes time until this

        Nav.GoToBookByName(nm);

        // Wait for the NEW reader instance (the scene reloads on each book) so we never read the
        // previous book's state — this is what kept timings correct across a batch.
        PRScript pr = null; float f0 = Time.time;
        while (Time.time - f0 < 25f)
        {
            var c = FindFirstObjectByType<PRScript>();
            if (c != null && c != oldPr) { pr = c; break; }
            yield return new WaitForSeconds(0.2f);
        }
        _curPr = pr;
        int nSteps = 0;
        var fi = typeof(PRScript).GetField("_scriptlets",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        float s0 = Time.time;
        while (pr != null && nSteps == 0 && Time.time - s0 < 25f)
        {
            if (fi != null && fi.GetValue(pr) is System.Collections.ICollection list) nSteps = list.Count;
            if (nSteps == 0) yield return new WaitForSeconds(0.25f);
        }

        HideChrome();

        float cap = Time.time + maxSeconds;
        if (pr != null && nSteps > 0)
            while (pr.nCurrentStep < nSteps - 1 && Time.time < cap) yield return null;
        else
            while (Time.time < cap) yield return null;
        yield return new WaitForSeconds(endHoldSeconds);

        controller.StopRecording();
        RestoreChrome();
        Debug.Log("[ClipRecorder] saved -> " + outNoExt + ".mp4");
    }

    private void HideChrome()
    {
        var names = new HashSet<string>
        { "Toolbar", "txtTitle", "btnPuzzle", "btnLink", "btnDevView", "btnReload" };
        foreach (var t in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (names.Contains(t.name) && t.gameObject.activeSelf) { t.gameObject.SetActive(false); _hidden.Add(t.gameObject); }
        foreach (var cam in FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (cam.clearFlags == CameraClearFlags.SolidColor)
            { _bgCam = cam; _bgColor = cam.backgroundColor; _bgChanged = true; cam.backgroundColor = UiTheme.Bg; break; }
    }

    private void RestoreChrome()
    {
        foreach (var go in _hidden) if (go != null) go.SetActive(true);
        _hidden.Clear();
        if (_bgChanged && _bgCam != null) _bgCam.backgroundColor = _bgColor;
        _bgChanged = false;
    }

    private void OnDisable() => RestoreChrome();

    private static string Slug(string s)
    {
        s = s.ToLowerInvariant();
        var sb = new System.Text.StringBuilder();
        bool us = false;
        foreach (char c in s)
        {
            if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9')) { sb.Append(c); us = false; }
            else if (!us) { sb.Append('_'); us = true; }
        }
        return sb.ToString().Trim('_');
    }
}
#endif
