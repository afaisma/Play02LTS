# BUG: unknown library filter poisons static `currentCategory` to -1 → crash on next Library entry (latent)

**Found:** 2026-06-09 (Cowork source audit, round 2)
**Status:** Diagnosed, fix specified, not yet applied
**Severity:** Low today (no live trigger), but the failure mode is a recurring `ArgumentOutOfRangeException` on every Library entry until app restart. Cheap one-line guard.
**Component:** `Assets/_Story/LIbrary/PRLibrary.cs`
**Owner:** Claude Code (code fix). This doc is the hand-off.

## Root cause
`PRLibrary.SetFilter(filter)` (line ~150) does:
```csharp
currentCategory = bookCategories.FindIndex(c => c.Settings == filter);
```
with no guard. If `filter` is not in `bookCategories`, `FindIndex` returns **-1** and that is stored in the **static** field `currentCategory` (line ~42), which survives scene reloads. The next Library entry runs `Start()` → `GotoCategory()` (line ~248):
```csharp
var (sceneName, categorySettings) = bookCategories[currentCategory];   // [-1] → throws
```
The exception aborts `Start()` (so `ResetScrollPosition` is skipped too) and recurs on every Library entry, since the static never resets.

## Why it's latent (verified 2026-06-09)
- All FilterItems currently in `_Library.unity` ("Family", "Manners", "Nature", "Rhymebooks", "Science", "Special Education", "adventure", "art", "classic", "everything", "fairytales", "math", "sound & speech") map to `bookCategories` entries after `.ToLower()`.
- BUT `FilterContainer.OnFilterChanged` explicitly supports age-range filters ("2-3 years", "3-5 years", "4-7 years", "5-10 years") that are **not** in `bookCategories`, and `FilterContainer.OnToggleValueChanged` calls `prLibrary.SetFilter(...)` unconditionally — re-adding any age FilterItem to the scene arms the crash.
- `MapManager.HandleButtonClick(buttonName)` → `Globals.GotoLibrary(buttonName)` → `SetFilter(g_libraryFilter)` is a second uncontrolled entry point (map button names are not validated against `bookCategories`).

## Fix spec
In `SetFilter`, guard the assignment:
```csharp
int idx = bookCategories.FindIndex(c => c.Settings == filter);
if (idx >= 0)
    currentCategory = idx;
```
Leave `currentCategory` unchanged for unknown filters (keeps Next/Prev category cycling anchored to the last valid category). No other lines change — the rest of `SetFilter` already handles unknown filters via its `else` branch ("filter unknown" log + default background).

## Test plan
- Library opens normally; category Next/Prev cycling unchanged.
- Pick each existing filter from the filter panel; re-enter Library after a book — no exception, filter behavior unchanged.
- (Armed-trigger check, editor only) call `SetFilter("2-3 years")` via the debug console / temporary code, then leave and re-enter Library → must not throw.
