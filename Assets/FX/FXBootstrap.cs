using System.Collections;
using System.Collections.Generic;
using ReadingBuddy.UI;   // PuzzleImage (from Assembly-CSharp, one-way reference)
using UnityEngine;
using UnityEngine.SceneManagement;

// The zero-edit wiring. Runs after the first scene loads, stands up the persistent
// runtime, then subscribes to events/state the app ALREADY exposes — no existing
// .cs file is touched. Everything is guarded so a hook failure never blocks gameplay.
public static class FXBootstrap
{
    private static bool _started;
    private static readonly HashSet<int> _subscribedPuzzles = new HashSet<int>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
#if FX_DISABLED
        Debug.Log("[FX] FX_DISABLED is defined — FX bootstrap is a no-op.");
        return;
#else
        if (_started) return;

        // Kill-switch: a disabled (or absent-then-default-disabled) library means no-op.
        var lib = FX.Library;
        if (lib == null || !lib.enabled)
        {
            Debug.Log("[FX] Library disabled — bootstrap is a no-op.");
            return;
        }

        _started = true;
        var runtime = FX.EnsureRuntime();

        // Continuously catch PuzzleImages — they are created dynamically during
        // Story play, not present at scene-load time.
        runtime.StartCoroutine(PuzzleWatch());

        SceneManager.sceneLoaded += OnSceneLoaded;
        // Handle the scene that is already active when we boot.
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
#endif
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        var runtime = FX.Runtime;
        if (runtime == null) return;

        try
        {
            if (scene.name == "_Story")
                runtime.StartCoroutine(BookDoneWatch());
            else if (scene.name == "_Library")
                runtime.StartCoroutine(LabDecorate());
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[FX] OnSceneLoaded hook failed (ignored): {e.Message}");
        }
    }

    // ---- puzzle-solved hook (no edit to PuzzleImage; its event already exists) ----

    private static IEnumerator PuzzleWatch()
    {
        var wait = new WaitForSeconds(0.75f);
        while (true)
        {
            var puzzles = Object.FindObjectsByType<PuzzleImage>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var pz in puzzles)
            {
                int id = pz.GetInstanceID();
                if (_subscribedPuzzles.Contains(id)) continue;
                _subscribedPuzzles.Add(id);
                pz.PuzzleSolvedEvent += OnPuzzleSolved;
            }
            yield return wait;
        }
    }

    private static void OnPuzzleSolved(PuzzleImage owner)
    {
        if (owner == null) return;
        FX.Play("stars", (RectTransform)owner.transform);
    }

    // ---- book-done hook (watch the existing PlayerPrefs flip; no edit to PRScript) ----

    private static IEnumerator BookDoneWatch()
    {
        // Wait briefly for the Story scene to settle and g_prbook to be set.
        float deadline = Time.realtimeSinceStartup + 5f;
        while (Globals.g_prbook == null && Time.realtimeSinceStartup < deadline)
            yield return null;

        var book = Globals.g_prbook;
        if (book == null) yield break;

        string url = book.bookUrl;
        // Baseline at entry: if the book is already done we never fire (we only
        // react to the 0 → non-zero transition during this Story session).
        int baseline = Globals.Prefs_Get_Book_Done(url);

        while (SceneManager.GetActiveScene().name == "_Story")
        {
            int now = Globals.Prefs_Get_Book_Done(url);
            if (baseline == 0 && now != 0)
            {
                FX.Play("book_done", FXTarget.FullScreen);
                yield break;
            }
            yield return null;
        }
    }

    // ---- Library "Lab" decoration (the cross-scene pilot use case) ----

    private static IEnumerator LabDecorate()
    {
        var lib = FX.Library;
        var runtime = FX.Runtime;
        if (lib == null || runtime == null || lib.labMap == null) yield break;

        // Grid populates async — wait for the catalog.
        float deadline = Time.realtimeSinceStartup + 10f;
        while (Globals.g_listPRBooks == null && Time.realtimeSinceStartup < deadline)
            yield return null;
        var books = Globals.g_listPRBooks;
        if (books == null) yield break;

        foreach (var entry in lib.labMap)
        {
            var book = FXLibrary.ResolveBook(books, entry.bookId, entry.bookName);
            if (book == null)
            {
                Debug.LogWarning($"[FX] Lab map: no book matched (id='{entry.bookId}', name='{entry.bookName}').");
                continue;
            }
            var effect = lib.Find(entry.effectId);
            if (effect == null)
            {
                Debug.LogWarning($"[FX] Lab map: unknown effect '{entry.effectId}'.");
                continue;
            }

            // Tile is built async when the grid populates/filters — wait for it.
            float tileDeadline = Time.realtimeSinceStartup + 10f;
            while (book.bookViewItem == null && Time.realtimeSinceStartup < tileDeadline)
                yield return null;
            if (book.bookViewItem == null)
            {
                Debug.LogWarning($"[FX] Lab map: tile for '{book.bookName}' never appeared.");
                continue;
            }

            var rt = (RectTransform)book.bookViewItem.transform;
            runtime.Decorate(effect, rt, entry.placement);
        }
    }
}
