# Fix: restore `_Message → _Library` auto-advance, and stop the debug `_Story` jump

**Status:** Diagnosed, fix specified, not yet applied
**Date:** 2026-05-31
**Severity:** Medium — launch flow no longer auto-advances; debug starts get yanked
**Component:** `Assets/_Story/Story/Globals.cs` (`WaitAndNavigate`)
**Decision:** Old behavior for both — `_Message` auto-advances to `_Library`; `_Story` (debug start) stays put

> Diagnosis + exact fix. The change is C# and belongs in Claude Code.

---

## Symptom

Two regressions from the same mechanism:

1. **Start from `_StartScene`** → goes to `_Message` as expected, but **no longer auto-advances to `_Library`** (it used to).
2. **Debug-start directly in `_Story`** → a few seconds later the app **jumps to `_Library`** unexpectedly.

## Root cause

The startup auto-advance is `PreLoadBooks` → (on CSV load) `WaitAndNavigate(targetScene, …)` → `LoadTargetScene()` → `Navigation.GoToScene(targetScene)`. On the `Globals` prefab, `targetScene = "_Library"`, `minTimeInScene = 5`.

`WaitAndNavigate`'s current guard decides whether to fire by comparing the active scene before vs. after the wait:

```csharp
string sceneAtStart = SceneManager.GetActiveScene().name;
yield return new WaitForSeconds(delay);
if (SceneManager.GetActiveScene().name != sceneAtStart) yield break;
LoadTargetScene();
```

Because `Globals` now boots at app launch (BeforeSceneLoad, DontDestroyOnLoad) and the localhost CSV loads almost instantly:

- **`_StartScene` case:** the download finishes while `_StartScene` is still active, so the coroutine captures `sceneAtStart = _StartScene`; then `StartScene.Start` switches to `_Message` during the wait → `sceneAtStart` mismatch → `yield break`. Auto-advance lost.
- **`_Story` debug case:** you sit in `_Story` with no scene change, so the guard passes and it navigates to `_Library`. And since you didn't open a book via `GotoPrBook`, `CancelPendingNavigation` never cleared `targetScene`. Unwanted jump.

So the brittle "did the scene change?" guard fires in exactly the wrong situations.

## Fix (simple, safe, one spot)

Replace the relative guard with a positive check: **auto-advance only from the launch/loading screen.** Add a serialized loading-scene name and gate on it.

Field (next to `targetScene` / `minTimeInScene`, ~Globals.cs:30):

```csharp
[SerializeField] private string loadingSceneName = "_Message";
```

`WaitAndNavigate` (~Globals.cs:322):

```csharp
private IEnumerator WaitAndNavigate(string targetScene, float delay)
{
    yield return new WaitForSeconds(delay);
    // Auto-advance only from the launch/loading screen. Never from _Story
    // (debug start) or wherever else the user happens to be sitting.
    if (SceneManager.GetActiveScene().name != loadingSceneName) yield break;
    LoadTargetScene();
}
```

(The `sceneAtStart` capture is removed.)

### Resulting behavior

- `_StartScene → _Message`: after `minTimeInScene`, active scene is `_Message` → fires → `_Library`. ✅ restored.
- Debug-start in `_Story`: active scene is `_Story ≠ _Message` → bails → stays in `_Story`. ✅ old behavior.

## Why it's safe

- The new condition is strictly **more restrictive** than today's — it can only ever navigate *from* `_Message`. It cannot reintroduce the "reader yanked out of `_Story` while reading" bug; it reinforces that fix.
- Independent of, and compatible with, the existing `CancelPendingNavigation()` / `targetScene` clearing on `GotoPrBook` (that path still works; this just adds a second, scene-based safeguard).
- Both `WaitAndNavigate` start sites (`PreLoadBooks` early-return branch ~line 300 and the download-success callback ~line 317) route through the same coroutine, so the single change covers all paths.

## Files / lines

- `Assets/_Story/Story/Globals.cs`
  - new field `loadingSceneName` — near lines 30–31
  - `WaitAndNavigate` — ~lines 322–334 (replace guard)
- `Assets/Resources/Globals.prefab` — optionally serialize `loadingSceneName: _Message` (defaults in code anyway; confirm the prefab keeps `targetScene: _Library`, `minTimeInScene: 5`)

## Verification

- `_StartScene` (with `startSceneName = _Message`): lands on `_Message`, then auto-advances to `_Library` after ~`minTimeInScene` seconds. Test both cold (CSV downloads) and warm (catalog cached) starts.
- Debug-start in `_Story`: stays in `_Story`; no jump to `_Library`.
- Normal play: open a book from the Library → read and turn pages → not interrupted (the earlier jump-to-Library fix still holds).
- Offline start: `_Message` shows the retry button; once connectivity returns and the catalog loads, it auto-advances.
