# BUG: `StoryStepsUI.Cleanup()` never clears `_alStoryPlates` → second `Reload()` leaves stale destroyed references (latent)

**Found:** 2026-06-09 (Cowork source audit, round 2)
**Status:** Diagnosed, fix specified, not yet applied
**Severity:** Low today — the only second-Reload trigger (`btnReload` in `_Story.unity`, wired to `PRScript.Reload`) is **inactive** (`m_IsActive: 0`). Same latent-footgun class as the `GotoStep` label stub. Failure mode if armed: `MissingReferenceException` in `SetStep` / `ScrollToIndex` and wrong plate indices, breaking page navigation.
**Component:** `Assets/_Story/Story/StoryStepsUI.cs`
**Owner:** Claude Code (code fix). This doc is the hand-off.

## Root cause
- `PRScript.Reload()` → `storyStepsUI.Cleanup()` → `CleanupStorySteps()` destroys all `StoryStepUI` **GameObjects** (via `FindObjectsOfType`), but the bookkeeping list `_alStoryPlates` (line ~34) is never cleared.
- `parse()` then calls `AddStoryStep(content, i)` for the new scriptlets, which **appends** — after a second load the list holds N stale entries (destroyed GameObjects) followed by N fresh ones.
- `SetStep(index)` / `ScrollToIndex(index)` index from 0, so they hit the destroyed entries: `GetComponent` on a destroyed GameObject → `MissingReferenceException`, thrown inside the page-turn path (`NextStep` → `SetStep`).

First load is unaffected (`Start()` → `Reload()` runs once on a fresh list) — which is why this has never been seen live.

## Fix spec
One line in `Cleanup()` (or at the top of `CleanupStorySteps()`):
```csharp
_alStoryPlates.Clear();
```
Indices realign by construction since `parse()` repopulates 0..N-1 immediately after.

## Out of scope (pre-existing quirks — do NOT bundle)
- `nCurrentStep` is not reset by `Reload()`, so a mid-book reload resumes from `nCurrentStep + 1` rather than the cover. Separate decision if btnReload is ever re-enabled.
- `SetStep` never un-highlights the previous plate: `PRScript.NextStep()` updates `nCurrentStep` *before* calling `SetStep`, so the `prScript.nCurrentStep != index` check is always false. Cosmetic (debug panel); fix only if asked.
- `prStageCharacters` / character buttons are also not cleaned on Reload — same latent class, separate item.

## Test plan
- Normal book open + full read-through (Next/Prev/swipe) — unchanged.
- Editor: activate `btnReload` temporarily, open a book, tap Reload, then page through — no exceptions, pages render and highlight correctly. Deactivate the button again (do not ship it enabled).
