# Learn-to-Read shelf in the Library UI — v1 spec (no new scene)

**Written:** 2026-06-11 (Cowork). Hand-off for Claude Code.
**Context:** the catalog now has a complete 24-book ladder (L1-L4, 6 each), every ladder book
tagged `genres: [learn to read, ...]` + `level` + `phonics_focus` in `stories.json`
(dev FileServer; see `~/dev/readingbuddy-aws/CATALOG_JSON_SPEC.md`). The legacy CSV carries
only the genre tag — so this feature renders fully with the JSON catalog and degrades
gracefully (no badges/sort) on CSV.
**Design decision (already made):** v1 lives entirely inside the existing `_Library` scene
and filter system. No new scene, no locks, no level-strip header — those are v2, after
per-child profiles exist.

## Changes (surgical, in order)

### 1. `PRBook` + `Globals.ParseJSON` — carry the level data (additive)
- Add fields to `PRBook`: `public int level;` (0 = not a ladder book) and
  `public string phonicsFocus;` ("" default).
- In `ParseJSON` (already shipping): map optional `level` (int) and `phonics_focus`
  (string) — both currently parsed-and-ignored. `ParseCSV` is NOT touched; CSV books get
  the defaults (0 / "").
- EditMode tests: level present → mapped; absent → 0; CSV path unaffected (existing
  equivalence test must keep passing — the new fields default on both paths for books
  without them).

### 2. Catalog entry: `bookCategories` + `SetFilter`
- `PRLibrary.bookCategories`: add `("_Library", "learn to read")`.
- `PRLibrary.SetFilter`: add a `"learn to read"` branch — title text "Learn to Read",
  background `Resources/Library/learn_to_read_background` if present, else fall back to
  the default `Library_background` (asset can come later; do NOT block on art).

### 3. The tile in the filter panel (`_Library.unity` scene edit)
- Add one FilterItem toggle to the existing filter grid: `filter = "learn to read"`.
- Tile image: reuse the deployed cover of The Sun Is Up
  (`TheSunIsUp/images/cover.jpg` from the dev FileServer) imported as a sprite, or any
  placeholder sprite — visual polish is a later pass. Match the existing tiles' size and
  label style ("Learn to Read", same green label).

### 4. Level badge on book cards (`BookViewItem.SetBookProperties`)
- If `prBook.level > 0`: `txtBookAgeGroup.text = $"Level {prBook.level}"`.
  Else: unchanged age-range text.
- Optional (cheap, do it): background tint by level band instead of pastel-by-number for
  ladder books — L1 `#F4C0D1` (pink), L2 `#FAC775` (orange), L3 `#C0DD97` (green),
  L4 `#B5D4F4` (blue). Matches the agreed band-color convention.

### 5. Sort by level inside the shelf (`BooksScrollView.ShowBooks`)
- When the active genre filter is `"learn to read"`, order books by `(level, number)`
  instead of catalog order. Smallest possible change: sort a copy of the list inside
  `ShowBooks` when `filter.genre == "learn to read"`; all other filters keep existing
  behavior byte-identical.

## Explicitly OUT of v1 (v2 backlog, do not implement)
- Level-strip header with progress chips, stars, soft locks ("2 books away") — needs
  per-child profiles to be meaningful.
- "Next book" recommendation highlight.
- phonics_focus display ("books about the oa sound") — data is already flowing; UI later.
- The read-to-me mic badge — arrives with the Recognissimo feature itself.

## Test plan
- EditMode: new ParseJSON field tests + all 104 existing tests green.
- In-editor (dev FileServer, stories.json URL): filter panel shows the new tile; tapping
  it shows exactly the 24 ladder books sorted L1→L4 with "Level N" badges and band colors;
  every other filter and "Everything!" unchanged; books open and play normally.
- CSV fallback check: point csvUrl at stories.csv → tile still lists the ladder books
  (genre tag is in the CSV), badges show age ranges, no errors.

## Rollback
All additive: remove the FilterItem from the scene and the `bookCategories` entry and the
UI is byte-identical to today. ParseJSON field mapping is inert without the UI.
