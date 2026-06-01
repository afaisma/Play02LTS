# Bug: app jumps from a story back to the Library while reading

**Status:** Diagnosed, fix not yet applied
**Date:** 2026-05-30
**Severity:** High — interrupts core reading flow; intermittent, so easy to ship unnoticed
**Component:** `Assets/_Story/Story/Globals.cs` (startup catalog-load navigation)

> Diagnosis only. The fix is a C# change and belongs in Claude Code.

---

## Symptom

Open a book and turn a page; a second or two later the app suddenly returns to the Library, destroying the `_Story` scene mid-read.

## Root cause

The navigation is fired by the **startup CSV/catalog-load coroutine in `Globals`**, not by anything in the story. The page turn is a timing coincidence.

Sequence:

1. At app launch, `Globals.Start()` (Globals.cs:174) calls `PreLoadBooks()`, which starts an **async CSV download** (`StartDownloadCSV`) from CloudFront. `Globals` is `DontDestroyOnLoad` and now starts at app launch (BeforeSceneLoad via Bootstrap).
2. The user reaches the Library and opens a book before that download finishes, entering `_Story`.
3. The CSV download completes. Its callback (in `PreLoadBooks`) starts `WaitAndNavigate(targetScene, delay)`.
4. `WaitAndNavigate` captures `sceneAtStart`, waits `delay` seconds, and if the active scene is unchanged calls `LoadTargetScene()` → `Navigation.GoToScene(targetScene)`, where `targetScene` is the Library.
5. Because the download finished **after** the user was already in `_Story`, `sceneAtStart` is captured as `_Story`, `delay` computes to ~0 (elapsed already exceeds `minTimeInScene`), the scene hasn't changed during the (zero) wait, so the guard passes and the Library is force-loaded.

The existing guard in `WaitAndNavigate`:

```csharp
string sceneAtStart = SceneManager.GetActiveScene().name;
yield return new WaitForSeconds(delay);
if (SceneManager.GetActiveScene().name != sceneAtStart) yield break;
LoadTargetScene();
```

only protects against the user navigating **during** the wait. It does not cover the case where the catalog load completes after the user has already entered a book. The in-code comment already flags the regression class:

> *Post-Step-3, `Globals.Start` runs at app launch (BeforeSceneLoad via Bootstrap), so the coroutine outlives multiple scene loads and the user is the one driving navigation.*

## Evidence (from the captured log)

- `Globals:Start()` → `PreLoadBooks` → `Downloading CSV from: https://d1lgnf093kp9w0.cloudfront.net/uploads/stories_02/stories.csv` at startup.
- User reads `TimmyAndHisFamily_v2/TimmyAndHisFamily01.txt`: step set to 0 (cover), then NextStep to step 1, chunk_2 executes (`PlayAudioAndShowText "TimmyAndHisFamily.mp3", 4.2, 7.3, ...`).
- Immediately after: `OnDestroy PRScript` → `PRLibrary Start`.
- The book has 24 chunks, so this is **not** an end-of-book overflow. `NextStep`/`SetCurrentStep` (PRScript.cs) return false past the end and never navigate — confirming the Library load comes from elsewhere (Globals).

## Why it's intermittent

It's a race between CSV download latency and how fast the user opens a book. Fast open or slow network → download lands mid-story → user is yanked out. Waiting on the splash until the catalog finishes avoids it.

## Suggested fix (for Claude Code)

The post-catalog navigation should become a no-op once the user has moved past the loading/bootstrap scene. Options, in order of preference:

1. **Only navigate from the bootstrap/loading scene.** In `WaitAndNavigate` / `LoadTargetScene`, bail unless the active scene is the splash/loading scene the navigation was intended to advance *from* — not merely "unchanged since the coroutine started."
2. **Clear the pending target when the user opens a book.** Set `targetScene = ""` (and stop `waitAndNavigateCoroutine`) in `Globals.GotoPrBook` so opening a book cancels any queued catalog-load navigation.

Apply the same protection to the early-return branch of `PreLoadBooks`:

```csharp
if (g_listPRBooks != null)
{
    if (!string.IsNullOrEmpty(targetScene))
        waitAndNavigateCoroutine = StartCoroutine(WaitAndNavigate(targetScene, minTimeInScene));
    return;
}
```

It schedules the same navigation and needs the same guard.

## Files / lines

- `Assets/_Story/Story/Globals.cs`
  - `Start` — line ~174
  - `PreLoadBooks` (incl. the early-return branch and the download callback) — lines ~253–283
  - `WaitAndNavigate` — lines ~285–305
  - `LoadTargetScene` — lines ~307–322
- `Globals.GotoPrBook` — lines ~441–452 (candidate for clearing `targetScene`)

## Verification after fix

- Throttle the network (or point `csvUrl` at a slow/large response) so the CSV download reliably finishes *after* a book is opened; confirm the story is no longer interrupted.
- Confirm the normal cold-start path still lands on the Library when no book is opened during loading.
