using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Downloads every book of the chosen level to the phone, so the books also work without
/// internet (Story Comprehension: the edition says downloadAllBooks). Started from the Home
/// screen; runs in the background, one file after another, and skips what is on the phone.
///
/// The files go into the same DiskCache the reader uses, under the same addresses the reader
/// asks for (so the reader finds them): the script, the cover, every picture the script names,
/// the narration at the current reading speed with its word timings, and the question
/// recordings. The word bank (tap a word) is not downloaded here; it comes when it is used.
/// </summary>
public class BookPrefetch : MonoBehaviour
{
    public static BookPrefetch Instance { get; private set; }

    public int Total { get; private set; }
    public int Done { get; private set; }
    public bool Running => _co != null;
    /// <summary>True when the last run finished with every file on the phone.</summary>
    public bool Complete { get; private set; }

    private Coroutine _co;
    private string _listKey = "";
    private int _failed;

    /// <summary>The line for the Home screen, or "" when there is nothing to say.</summary>
    public static string StatusText
    {
        get
        {
            if (Instance == null || Instance.Total == 0) return "";
            if (Instance.Running) return "Downloading books for offline use: " + (Instance.Done + 1) + " of " + Instance.Total + "…";
            if (Instance.Complete) return "All " + Instance.Total + " books are on this phone.";
            return "Some books could not be downloaded. They download the next time you are online.";
        }
    }

    /// <summary>Download these books (the current level). A list already running is left alone.</summary>
    public static void StartFor(List<PRBook> books)
    {
        if (books == null || books.Count == 0) return;
        if (Instance == null)
        {
            var go = new GameObject("BookPrefetch");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<BookPrefetch>();
        }
        var keys = new List<string>();
        foreach (PRBook b in books) if (b != null && string.IsNullOrEmpty(b.action)) keys.Add(b.bookUrl + "@" + b.contentRev);
        string key = string.Join("|", keys);
        if (key == Instance._listKey && (Instance.Running || Instance.Complete)) return;
        Instance._listKey = key;
        if (Instance._co != null) Instance.StopCoroutine(Instance._co);
        Instance._co = Instance.StartCoroutine(Instance.Run(books));
    }

    private IEnumerator Run(List<PRBook> books)
    {
        Total = 0; Done = 0; _failed = 0; Complete = false;
        var list = new List<PRBook>();
        foreach (PRBook b in books) if (b != null && string.IsNullOrEmpty(b.action)) list.Add(b);
        Total = list.Count;
        foreach (PRBook book in list)
        {
            yield return FetchBook(book);
            Done++;
        }
        Complete = _failed == 0;
        Debug.Log("[Prefetch] done: " + Done + " books, " + _failed + " file(s) failed");
        _co = null;
    }

    private IEnumerator FetchBook(PRBook book)
    {
        string script = null;
        yield return PRUtils.DownloadFile(book.bookFullUrl, t => script = t); // also keeps the script
        if (string.IsNullOrEmpty(script)) { _failed++; yield break; }

        string rev = book.contentRev;
        string baseUrl = PRUtils.RemoveFileNameFromUrl(book.bookFullUrl);
        if (!string.IsNullOrEmpty(book.bookImageUrl))
            yield return Fetch(Globals.WithContentRev(Globals.baseURL + book.bookImageUrl, rev), "images", ".png", DiskCache.ImageBudgetBytes);

        // The voice and speed the reader will use for this book (AudioAndTextPlayer.BuildTtsUrls).
        Match voice = Regex.Match(script, "SetCurrentVoice\\s*\\(?\\s*\"([^\"]+)\"");
        string postfix = voice.Success ? "_" + voice.Groups[1].Value : "";
        string rate = PlayerPrefs.GetInt("g_bSetReadingSpeedByBooksAgeGroup", 1) == 1
            ? "" + Globals.defaultAudioRateFromPRBook(book) : PlayerPrefs.GetString("g_Rate", "0");

        var seen = new HashSet<string>();
        foreach (string line in script.Split('\n'))
        {
            if (line.TrimStart().StartsWith("//")) continue;
            foreach (Match m in Regex.Matches(line, "\"(images//[^\"]+\\.(?:jpg|jpeg|png))\"", RegexOptions.IgnoreCase))
            {
                string url = Globals.WithContentRev(baseUrl + Normalize(m.Groups[1].Value), rev);
                if (seen.Add(url)) yield return Fetch(url, "images", ".png", DiskCache.ImageBudgetBytes);
            }
            Match chunk = Regex.Match(line, "PlayAudioAndText\\s*\\(?\\s*\"([^\"]+)\"");
            if (chunk.Success)
            {
                var tts = AudioAndTextPlayer.BuildTtsUrls(Normalize(chunk.Groups[1].Value), rate, postfix, rev);
                if (seen.Add(tts.audio)) yield return Fetch(baseUrl + tts.audio, "audio", ".mp3", DiskCache.AudioBudgetBytes);
                if (seen.Add(tts.timings)) yield return Fetch(baseUrl + tts.timings, "timings", ".json", DiskCache.TimingsBudgetBytes);
            }
            Match q = Regex.Match(line, "Dialogue(?:Question|Praise)\\s*\\(?\\s*\"(?:[^\"]|\"\")*\"\\s*,\\s*\"([^\"]+)\"");
            if (q.Success)
            {
                string url = Globals.WithContentRev(baseUrl + Normalize(q.Groups[1].Value) + ".mp3", rev);
                if (seen.Add(url)) yield return Fetch(url, "audio", ".mp3", DiskCache.AudioBudgetBytes);
            }
        }
    }

    // As PRScript.NormalizeUrl: "images//x.jpg" -> "images/x.jpg".
    private static string Normalize(string reference) => reference.Replace("//", "/");

    private IEnumerator Fetch(string url, string subdir, string ext, long budget)
    {
        if (System.IO.File.Exists(DiskCache.PathFor(url, subdir, ext))) yield break;
        using (UnityWebRequest req = UnityWebRequest.Get(url))
        {
            req.timeout = 60;
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success)
            {
                _failed++;
                Debug.Log("[Prefetch] failed: " + url + " (" + req.error + ")");
                yield break;
            }
            if (ext == ".json") DiskCache.WriteText(url, subdir, ext, req.downloadHandler.text, budget);
            else DiskCache.WriteBytes(url, subdir, ext, req.downloadHandler.data, budget);
        }
    }
}
