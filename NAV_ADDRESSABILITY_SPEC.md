# Claude Code hand-off — Navigation addressability (Phase 1, no page hook)

**Author:** Cowork · **For:** Claude Code · **Scope:** additive, low-risk.
**Goal:** one consistent, string-addressable way to navigate to a scene *with state* — usable from
any scene's button, code, or (optionally) a story script. Reuse the existing
"set state on `Globals` → destination reads it in `Start()`" pattern; don't rewrite it.

## EXPLICITLY OUT OF SCOPE (postponed — do NOT implement now)
- The **Story start-page hook / resume** (`g_startPage`, opening a book at page N, the
  preamble `GoTo("Next")` / page-overwrite reconciliation). The `page` address param is
  PARSED and ACCEPTED but **ignored** for now (reserved for a later phase).
- Any change to `PRScript`'s open/parse sequence, `SetCurrentStep`, or the `GotoStep` stub.

## Working rules
- Follow `Play6.3/CLAUDE.md` §3 "Surgical Changes" — touch only what's needed; match existing style.
- Keep every existing entry point working (`Globals.GotoPrBook`, `Globals.GotoLibrary`,
  `Navigation.GoToX`, scene button onClicks). The new layer **delegates to these**, it does not
  replace them.
- Fail soft: a bad/unknown address or an unresolvable book is a logged no-op, never a crash and
  never a scene load with null state.
- Run the EditMode test suite after; report a diff summary.

---

## 1. New file: `Assets/_Story/Story/Nav.cs`

A small static router. Address grammar: `"<scene>[?key=value&key=value]"` (case-insensitive
scene; `+` and `%20` → space in values).

Public API:
```csharp
public static class Nav
{
    public static void Go(string address);                 // "story?book=The Big Pig", "library?filter=level1"
    public static void GoToLibrary(string filter = "everything");
    public static void GoToBook(PRBook book);
    public static void GoToBookByName(string name);
    public static void GoToBookById(string id);

    // testable helpers (make them public so EditMode tests can call them):
    public static (string scene, System.Collections.Generic.Dictionary<string,string> args) Parse(string address);
    public static PRBook ResolveBook(System.Collections.Generic.Dictionary<string,string> args);
}
```

Behavior of `Go(address)` (parse, then dispatch — delegate to existing methods):
- `library`  → `GoToLibrary(args.GetValueOrDefault("filter","everything"))`
- `story`    → `var b = ResolveBook(args); if (b==null) { Debug.LogWarning(...); return; } GoToBook(b);`
              (the `page` arg, if present, is ignored this phase)
- `bookstore`→ `Globals.g_bookstoreFilter = args.GetValueOrDefault("filter","everything"); Navigation.GoToBookstore();`
- `settings|parents|map|message|start` → the matching `Navigation.GoToX()`
- anything else → `Debug.LogWarning("Nav: unknown address …"); return;`

Thin wrappers (these are what most app code should call — compile-time safe):
- `GoToLibrary(filter)` → `Globals.GotoLibrary(filter)` (existing: sets `g_libraryFilter`, loads `_Library`).
- `GoToBook(book)`      → `Globals.GotoPrBook(book)`   (existing: sets script/book, loads `_Story`).
- `GoToBookByName/ById` → resolve via `ResolveBook` then `GoToBook`.

`ResolveBook(args)` (uses `Globals.g_listPRBooks`; returns null if catalog not loaded or no match):
1. if `args["id"]`   → `Find(b => b.id == id)`
2. else if `args["book"]` → `Find(b => string.Equals(b.bookName, name, OrdinalIgnoreCase))`
3. else if `args["url"]`  → `Find(b => b.bookUrl == url)`
4. else null.

`Parse`: split on the FIRST `'?'`; scene = left, lower-cased, trimmed; args = right split on `'&'`
then first `'='`; value decode = replace `'+'`→space and `%20`→space (no full URL-decode needed).
Empty/blank address → scene "" + empty args.

## 2. Level filter: `Assets/_Story/LIbrary/BooksScrollView.cs`

Make `levelN` an addressable filter (so `filter=level1` … `level4` work) without disturbing genre
filters. The filter string already flows to `Filter.SetFilter(ageFrom, ageTo, genre)` via
`FilterContainer.OnFilterChanged` (it passes the token as `genre`). Interpret it in `Filter`:
- Add `int level = 0;` to `Filter`.
- In `Filter.SetFilter` (or a tiny helper): if `genre` matches `^level([1-4])$` (case-insensitive),
  set `level = N` and treat genre as empty for the substring test.
- In `Filter.Conforms(prBook)`: if `level > 0` → `return prBook.level == level;` (keep any age test
  if present). Otherwise the existing genre/age logic unchanged.
- (Optional) In `BooksScrollView.ShowBooks`, also sort `level > 0` shelves by `(level, number)` like
  the existing `"learn to read"` special-case.

Do NOT change `FilterContainer.OnFilterChanged` or `PRLibrary.SetFilter` matching — `level1` will
fall through their default branch (default title/background is acceptable for this phase; a
`"Level 1"` title case in `PRLibrary.SetFilter` is a nice-to-have, optional).

## 3. (Optional, include if quick) Story-script link intrinsic: `Assets/_Story/Story/PRScript.cs`

In `SetupInterpreter()`, following the existing intrinsic pattern, add ONE intrinsic so content can
link anywhere:
```csharp
f = Intrinsic.Create("OpenAddress");
f.AddParam("address", "");
f.code = (context, partialResult) => {
    Nav.Go(context.GetVar("address").ToString());
    return new Intrinsic.Result(ValNumber.one);
};
```
(`OpenAddress("library?filter=science")`, `OpenAddress("story?book=The Big Pig")`.) This rides the
existing intrinsic system; the known MiniScript intrinsic-registration leak is separate and out of
scope here (`BUG_INTERPRETER_INTRINSIC_LEAK.md`).

## 4. Tests (`Assets/Tests/Editor/`) — EditMode, pure logic
- New `NavParseTests.cs`:
  - `Parse("story?book=The Big Pig&page=3")` → scene `"story"`, args `book=="The Big Pig"`, `page=="3"`.
  - `Parse("library?filter=level1")` → scene `"library"`, `filter=="level1"`.
  - `Parse("map")` / `Parse("")` → no args / empty.
  - `ResolveBook`: seed `Globals.g_listPRBooks` with a couple of `PRBook`s; resolve by id, by name
    (case-insensitive), and a miss → null. (Don't call `Go`/scene loads in EditMode.)
- Extend `FilterConformsTests.cs`: a `PRBook{ level=1 }` conforms to `"level1"`, not `"level2"`;
  a genre filter still conforms as before (no regression).

## Acceptance
- `Nav.GoToLibrary("science")`, `Nav.Go("library?filter=level1")`, `Nav.Go("story?book=The Big Pig")`
  all navigate with correct state, from any scene.
- All existing nav still works unchanged; EditMode suite green (existing + new tests).
- No change to the Story open/parse sequence (page addressing remains a no-op this phase).
