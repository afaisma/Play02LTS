# Automated tests for Play6.3 — hand-off plan

**Written:** 2026-06-09 (Cowork)
**Owner:** Claude Code (implementation). This doc is the hand-off.
**Scope rule:** Phase 1 must NOT modify any runtime/shipped code. Tests + test assembly only.
The one allowed exception is noted in "Assembly problem" below, and needs explicit approval first.

## Why now
The 2026-06-09 audit found bug classes that plain EditMode tests would have caught (CSV parsing
edge cases, `FindIndex` -1 poisoning, mono-only subclip math, negative caching). Cheap insurance
before the Recognissimo/read-to-me work starts churning the codebase.

## Phase 1 — EditMode tests over pure logic (do this now)

### Assembly problem — resolve FIRST
All game code is in the predefined `Assembly-CSharp` (no asmdef). A test asmdef **cannot**
reference predefined assemblies. Options, in order of preference:
1. Put EditMode tests in an `Editor` folder (e.g. `Assets/Tests/Editor/`) so they compile into
   `Assembly-CSharp-Editor`, which references `Assembly-CSharp`. Verify NUnit (`nunit.framework`)
   resolves there in this Unity 6 setup (Test Framework package is auto-referenced in most
   configs). Zero impact on runtime code. **Try this first.**
2. If (1) fails: a test asmdef + adding an asmdef to game code. This changes compilation for the
   whole project — **do not do this without asking first.**
Confirm the Unity Test Framework package (`com.unity.test-framework`) is in `Packages/manifest.json`;
add it if missing (editor-only package, no runtime impact).

### Test targets (≈40–60 tests, all pure or editor-safe)

**`Globals.ParseCSV` (static, string → List<PRBook>)** — highest value:
- well-formed 9-column and 11-column rows (both shapes exist in production, verified 2026-06-09);
- malformed rows are skipped without aborting the rest (C2 behavior); `number` stays contiguous;
- empty lines skipped; header skipped;
- relative `bookUrl` gets `baseURL` prefix, absolute `http...` does not;
- **documented hazard:** a comma inside a field silently shifts columns — write a test that
  *documents* current behavior (asserts the shift/skip as-is), with a comment that this is a
  known limitation, not an endorsement.
- Note: reads `Globals.baseURL` (static) and PlayerPrefs — set/reset both in [SetUp]/[TearDown].

**`Filter.Conforms` (BooksScrollView.cs)** — "everything", genre substring match
(multi-genre strings like "rhymebooks : family : special education"), age-range branch,
empty filter.

**`Scriptlet.ParseTitleString` / `GetName` (PRScript.cs)** — `////////[chunk name=page1]` →
"page1"; missing name → ""; extra key=value pairs.

**`PRLibrary.SetFilter` regression (2026-06-09 fix)** — pure part only: assert
`bookCategories.FindIndex` returns -1 for "2-3 years" (documents why the guard exists). Full
SetFilter needs scene objects — skip; the guard itself is covered by inspection + PlayMode later.

**`PRUtils`** — `RemoveFileNameFromUrl` (normal URL, trailing-file URL, garbage → returned
unchanged via catch), `UrlUp` (n steps, exhausted slashes → ""), `StringToColor` /
`StringToColor1` (hex, R,G,B, invalid), `SplitStringIntoLines` (\n, \r\n, \r),
`CapitalizeFirstLetter`, `Convert` (number words), `AlmostEqual`.

**`AudioPlayer.GuessAudioTypeFromUrl`** — .wav/.ogg/.mp3/.aiff, query strings, no extension,
empty/null → MPEG.

**`AudioClipUtilities.MakeSubclip`** — create mono AND stereo clips via `AudioClip.Create` +
`SetData` with a recognizable ramp; assert subclip length, channels, frequency, and sample
content for both; whole-clip subclip (stop == length, the M-R3-2 case); invalid ranges → null.
*(This is the test that would have caught the `PRUtils.MakeSubclip` mono bug class.)*

**`DiskCache`** — WriteBytes/TryReadBytes round-trip, WriteText/TryReadText round-trip, miss →
null, `TrimSubdir` evicts oldest-accessed beyond cap. Uses `Application.persistentDataPath` —
use a dedicated subdir name like `"testcache_<guid>"` is NOT possible (subdir is the cache class
param — fine, use e.g. "test_images") and delete it in [TearDown].

**`Globals` small statics** — `ageGroupLabelFromPRBook`, `defaultAudioRateFromPRBook`
(2/3/4/5 + the out-of-range → -30 wart: document current behavior with a comment),
`Prefs_BookUrl_To_Page_Key` / `_BookDone_Key`.

### Conventions
- NUnit, one test class per target class, `Assets/Tests/Editor/`.
- Tests that *document* known warts (CSV comma shift, ageFrom=6 → -30) must say so in a comment
  so a future fix knows to flip the assertion.
- Every test must pass before hand-back. Run via Test Runner window or
  `Unity -batchmode -runTests -testPlatform EditMode`.

## Phase 2 — PlayMode fixture-book smoke test (later, separate hand-off)
Goal: load `_Story` with a fixture script served via the existing `resources:` scheme
(`PRUtils.DownloadFile` already supports it — no network needed), step through every page, assert
no exceptions, correct `nCurrentStep` progression, and (reload twice) no stale-plate exceptions —
the regression test for BUG_STORYSTEPS_RELOAD_STALE_PLATES. Needs: fixture script + images in
`Resources/`, scene-load test setup, `Globals.g_scriptName = "resources:..."`. Do NOT start this
without a separate go-ahead; it likely needs small testability seams and those need discussion.

## Phase 3 — CI (someday/maybe)
game-ci GitHub Actions + Unity license activation. Skip until phase 1/2 prove their worth;
running EditMode tests locally before commits is enough for now.
