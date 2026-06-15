# Play6.3 source audit — significant-bug pass (2026-06-08)

Scope: read PRScript, AudioAndTextPlayer, AudioPlayer, Globals, PRLibrary, StoryStepsUI,
Gallery, Navigation, OverlayHost, PRUtils, and the MiniScript intrinsic layer; traced the
book-open flow against a real script (`TheTaleOfPeterRabbit`).

## Headline
**No significant bug found** — nothing in the crash / data-corruption / broken-core-flow
class. The code is clean and visibly hardened (disk+memory caches, timeouts, per-row CSV
isolation, overlay teardown, scheduled-callback cleanup, null-after-await guards). The items
below are the only things worth a decision; none are emergencies.

## Worth a decision / fix

### 1. Reopening a book does NOT resume — and discards the saved page position (verify intent)
On open, the script preamble runs `GoTo("Next")` (the standard idiom — 227 occurrences across
content). That calls `NextStep()` → `SetCurrentStep(1)`, which immediately does
`g_prbook.SetAndSaveCurrentPage(1)` (`PRScript.cs:917`). Consequences:
- The reader always lands on the first content page, never the page the child left off on.
- The previously-saved `currentPage` (e.g. 10) is **overwritten with 1** on every open, so the
  stored furthest-page is effectively meaningless. The "in-progress" dot in `BookViewItem`
  (`currentPage != 0`) still shows, so this is invisible in the library UI.
- `nCurrentStep` is never initialized from `g_prbook.currentPage` anywhere in `_Story`.

This is likely *by design* (kids re-read from the start), in which case it's not a bug — but
the saved page value is then dead weight. **If resume is ever wanted, this is the blocker.**
Decision needed before building any "continue where you left off" feature.

### 2. `GotoStep(label)` is a stub — labeled `GoTo` silently does nothing (latent footgun)
`PRScript.cs:975`:
```csharp
private void GotoStep(string label)
{
    SetUIAccordingToCurrentStep();   // ignores `label` entirely
}
```
`GoTo("next")` / `GoTo("prev")` work (handled earlier in the intrinsic), but any *named-label*
jump does nothing: it neither changes `nCurrentStep` nor renders a page. **No live impact**
today — all 227 content uses are `GoTo("Next")`; none use a label. But it's a trap: the first
author who writes `GoTo("page5")` will get a silent no-op with no error. Either implement
label→index lookup (scriptlets already carry names via `Scriptlet.GetName()`) or make it log a
warning so the failure is visible.

## Minor (already noted in BUG_INTERPRETER_INTRINSIC_LEAK.md or low value)
- MiniScript intrinsic re-registration leak — see `BUG_INTERPRETER_INTRINSIC_LEAK.md` (low–med, not urgent).
- Redundant double `SetCurrentStep` per navigation → duplicate PlayerPrefs writes per page turn.
- `StoryStepsUI.ScrollToIndex` can produce NaN when content is short (denominator → 0).
- `PRScript.NormalizeUrl` collapses all `//` (fine for current CDN paths; fragile if URLs ever carry encoded `//`).
- `OverlayHost` has no `OnDestroy`; per-instance `RenderTexture`s are released on `Clear()`
  (page change) but a scene unload while a video overlay is live leaks those RTs until GC. Low.

## Checked and OK (not bugs)
- Book-open skipping chunk_0: intentional convention (chunk_0 = cover, `GoTo("Next")` lands on chunk_1).
- `OverlayHost.Update` foreach over `_overlayVideos` — no re-entrant dictionary mutation (taps come from UI events, not Update).
- Audio cache / disk cache / fragment-clip freeing in `AudioAndTextPlayer` — correctly handled (H4/C3 fixes present).
- CSV parse — per-row try/catch isolates malformed rows (C2 fix present).
- Known Globals-launch regressions already captured in `BUG_message_*.md` / `BUG_story_jumps_to_library.md`.
