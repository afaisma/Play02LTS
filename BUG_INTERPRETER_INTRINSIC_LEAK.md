# BUG: MiniScript intrinsics re-registered on every page execute / overlay tap (static leak + GC churn)

**Found:** 2026-06-08 (Cowork source audit)
**Severity:** Low–Medium — slow, app-lifecycle-bounded static memory growth. No crash, no incorrect output, no reported symptom (found by code reading). Practical worst case ≈ a few MB in a long single session; mobile backgrounding/kill resets the static list. Worst realistic trigger is a child repeatedly tapping an overlay (`DispatchEvent` re-registers per tap).
**Priority:** Not urgent. Fix opportunistically because the recommended fix is cheap and low-risk — rank it below items that cause visible bugs (Globals-at-launch regressions) or real risk (CDN cutover). Earlier "frame hitch" framing was overstated: page turns/taps are not per-frame hot paths, and registration cost is small next to the per-run parse/compile that happens anyway.
**Owner:** Claude Code (code fix). This doc is the hand-off.

## Summary
`PRScript.SetupInterpreter()` builds a **new** `Interpreter` and re-registers **all ~50 intrinsics** every time it runs. It runs on essentially every script execution, not once. MiniScript stores intrinsics in a process-wide static list that is **append-only**, so every call permanently grows that list and allocates ~50 `Intrinsic`/`Function`/`ValFunction` objects that are never freed.

## Root cause (two files)

1. `Assets/_Story/Story/PRScript.cs`
   - `SetupInterpreter()` (line ~228) does `_interpreter = new Interpreter()` and then ~50× `Intrinsic.Create(...)`.
   - It is called from `RunScript()` (line ~820) and from `DispatchEvent()` (line ~853).
   - `RunScript()` is invoked by `ExecuteScriptlet()` (line ~884), which fires for: the settings preamble (`parse`, line ~185), every `ExecuteStep` page execute (the `OnExecuteStep` handler **and** the page scriptlet — up to 2 per page turn, lines ~875/877), and every `ReplayCurrenStep`.
   - `DispatchEvent()` fires on every overlay `onTap` / `onMediaEnd` / `Schedule` callback.

2. `Assets/MiniScript/Miniscript Source/MiniscriptIntrinsics.cs`
   - `public static List<Intrinsic> all` (line ~64) and `Intrinsic.Create()` (line ~74) which unconditionally does `all.Add(result)` (line ~80). `nameMap[name] = result` (line ~81) overwrites cleanly, so re-registration **does not throw or break behavior** — it just leaks via `all`.

## Impact / math
~2 `RunScript` calls per page turn → ~100 leaked intrinsic objects per page turn, plus the same churn per overlay tap. A ~10-page book ≈ 1,000–1,500 permanently-retained `Intrinsic` entries; a multi-book session pushes `Intrinsic.all` into the tens of thousands. `Globals` is `DontDestroyOnLoad` and `Intrinsic.all` is `static`, so nothing reclaims it until the process dies. The more visible day-to-day symptom is the per-page-turn allocation spike (50 `Intrinsic` + `Function` + `ValFunction` + dictionary writes) causing GC hitches mid-read on low-end phones.

## Suggested fix (do NOT edit MiniScript source per project rule #2 — fix in PRScript)

**Verified prerequisites (checked against the vendored MiniScript in this repo):**
- Intrinsics live in process-wide static storage (`Intrinsic.all` / `nameMap` / `GetByName`), independent of any `Interpreter` instance — so registering them **once** is enough for every interpreter to resolve them by name. (This is the documented intended usage; per-run registration is the misuse.)
- `Interpreter.Reset()` nulls `parser`+`vm`, and `Compile()` builds a fresh VM with a fresh `globalContext`. So both "new Interpreter per run" (today) and "reuse + Reset per run" give **fresh MiniScript globals each run** — the fix does not change run-to-run variable semantics.

### Recommended fix — simple AND safe: register once per PRScript lifetime (in `Start()`)

The reason the current leaky code is *correct* is also why it leaks: each run re-registers, and every closure captures the live `this`, so `nameMap` always points at closures bound to the current `PRScript`. Keep that property; just stop doing it per run.

- Split `SetupInterpreter()` into:
  - `RegisterIntrinsics()` — the ~50 `Intrinsic.Create(...)` blocks. **Verified: none of these blocks reference `_interpreter`** (only `ConfigOutput()` does), so lifting them out is a pure cut/paste. Call this **once** from `Start()`.
  - Per-run interpreter setup — `_interpreter = new Interpreter(); ConfigOutput();` — stays in `RunScript()` / `DispatchEvent()`.
- `RunScript()` / `DispatchEvent()` no longer call the registration; they just `new Interpreter()` + `ConfigOutput()` + `Reset(script)` + `Compile()` + run.

Why this is safe and small:
- The `Create` blocks are independent of `_interpreter`, so moving them is mechanical with no behavior change.
- Closures still capture `this`, so they stay correctly bound to the live `PRScript` — **no `PRScript.Current` rewrite, and no stale-`this` risk on scene reload.**
- Per-run `new Interpreter()` preserves today's exact fresh-globals-per-run semantics (`Reset()`+`Compile()` build a fresh VM/globalContext either way). The only thing removed is the per-page-turn re-registration.

**Residual:** this does not drive the leak to zero — each book-open's `Start()` still adds ~50 entries to MiniScript's static `all` (the prior book's 50 become unreachable-but-retained). That is ~50 per *book opened* vs ~100 per *page turn* today — a 20–50× reduction that also eliminates the per-turn GC churn (the actual user-visible symptom). Accept this residual.

### Do NOT use this variant unless you need zero residual (invasive — higher risk)
Truly-once registration (`static bool` guard, register for the whole app lifetime) eliminates the residual, **but** the once-registered closures would capture the first `PRScript` and call into a **destroyed** instance after a book → library → book reload → NullReference / crash. Making it safe requires routing every one of the ~50 closures through a `static PRScript Current` (set in `Start()`) instead of capturing `this` — a large, error-prone rewrite. Not worth it to reclaim ~50 entries per book. Documented only so a future editor doesn't reach for the `static bool` guard naively.

## Verification after fix (must pass before trusting it)
- Add a temporary `Debug.Log(Intrinsic.all.Count)` (or watch the profiler) and turn ~20 pages + tap several overlays: the count must stay flat after first registration, not climb ~100/turn.
- **Scene-reload smoke test (the risky path):** open a book → Home/Library → open a *different* book, then exercise `GoTo next/prev`, replay, an overlay `onTap`, and a `Schedule` callback. All must run against the new book's PRScript (confirms the `Current` redirection, not a stale `this`).
- Confirm no MiniScript runtime/compile errors appear in the alert dialog during the above.

---

## Other observations from the same audit (lower confidence / lower severity — verify before acting)

- **Redundant double `SetCurrentStep` per navigation.** `NextStep`/`PrevStep` (lines ~945/933) call `SetCurrentStep(...)`, then `ExecuteStep()` (line ~866) calls `SetCurrentStep(index)` again. Net effect: page progress + `book_done` are written to `PlayerPrefs` twice per page turn. Harmless but doubles disk writes on every turn. `bStepChanged` is also a misnomer — `SetCurrentStep` returns true for any in-range index, not only on actual change.

- **`StoryStepsUI.ScrollToIndex` possible NaN.** `normalizedPosition = itemTopPosition / (totalHeight - itemHeight - spacing)` (line ~101). When content is short enough that `totalHeight ≈ itemHeight + spacing`, the denominator approaches 0 → `verticalNormalizedPosition` becomes Inf/NaN for `index > 0`. Guard the denominator (`if (denom <= 0) normalizedPosition = 0;`).

- **`Globals.getReadingRate()` ignores the user's manual rate for some books.** When "set speed by age group" is on (default), it returns `defaultAudioRateFromPRBook(g_prbook)`, which returns `-30` for any `ageFrom` not in {2,3,4,5} (line ~431). Confirm that's intended for age-0/1/6+ books, otherwise those books always request the `-30` audio variant.

- **`PRScript.NormalizeUrl` collapses all `//`.** `url.Replace("//","/")` then restores `http(s)://` (lines ~1030-1038). Any legitimate `//` in a path or query string is also collapsed. Low risk given current CDN paths, but fragile if URLs ever carry encoded `//`.
