# Change brief: read the catalog from stories.json (CSV stays as rollback path)

**Written:** 2026-06-10 (Cowork). App-side hand-off for Claude Code.
**Companion doc (content side):** `~/dev/readingbuddy-aws/CATALOG_JSON_SPEC.md` — schema,
generator, invariants. Read it first; the `script`/`bookUrl` invariant defined there is the
one thing this change must not get wrong.
**Status:** Specified, not yet applied. Do NOT start until the generator exists and
`stories.json` is published on the QA path (`stories-qa/stories.json`).

## Scope (surgical)
1. **New `Globals.ParseJSON(string json) → List<PRBook>`** beside `ParseCSV` in
   `Assets/_Story/Story/Globals.cs`. Use the vendored **SimpleJSON**
   (`Assets/_Story/Utils/SimpleJSON.cs` — already used by AudioAndTextPlayer), not
   JsonUtility (top-level object + array + per-book objects with optional fields is
   exactly what JsonUtility is bad at).
   - Map fields per the spec. `genres` array → join to the legacy `" : "` string into
     `PRBook.genre`, so `Filter.Conforms` / `FilterByGenre` and the library UI need **zero**
     changes. `PRBook` itself is unchanged in this step (`level` / `read_to_me` are parsed
     but may be dropped until a consumer exists — or stored if trivial; author's call).
   - `bookUrl` = the `script` string **verbatim** — it keys PlayerPrefs progress
     (`Prefs_Get_Book_Page(values...)` equivalent path must behave identically).
   - Per-book try/catch like ParseCSV's C2 row isolation: one malformed book entry is
     skipped with a warning, the rest of the catalog loads.
   - Ignore unknown JSON fields everywhere; tolerate missing optional fields with the same
     defaults the CSV path produces ("" / 0). Check `schema_version` and log (don't reject)
     if it's newer than known.
2. **Dispatch by extension** at the single call site (`PreLoadBooks`'s download callback):
   URL ends with `.json` → `ParseJSON`, else → `ParseCSV`. `ParseCSV` is NOT modified.
3. **No other changes.** Base-URL derivation (`PRUtils.RemoveFileNameFromUrl`), download
   path, retry button, `IsDownloading` flow all stay as-is — they're format-agnostic.
   The actual cutover is an Inspector edit of the Globals **prefab** URL (separate,
   reversible step — not part of this code change).

## Tests (extend the existing EditMode suite in Assets/Tests/Editor/)
- Happy path: full catalog JSON → same count/fields as the equivalent CSV (use a fixture
  pair; the generator's `--diff-csv` guarantees production equivalence, the test guards
  the parser itself).
- `bookUrl` verbatim preservation (progress-keys invariant) — assert exact string equality
  including any double-slash or spacing quirks present in the fixture.
- Malformed book entry skipped, remainder loads (C2 parity).
- Unknown extra fields ignored; missing optional fields → CSV-path defaults.
- genres array → legacy `" : "` joined string.
- Extension dispatch: `.json` vs `.csv` URL routes to the right parser.

## Rollback
Flip the Globals prefab URL back to `stories.csv` (still generated and published). No code
revert needed. This is why ParseCSV must remain untouched and reachable.

## QA before cutover (manual, dev build on stories-qa)
- Library populates fully; filters and backgrounds behave identically.
- A book with pre-existing CSV-era progress shows the same in-progress/done dot and state.
- Kill network → retry button flow unchanged.
