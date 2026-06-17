# Claude Code hand-off — catalog-defined navigation tiles ("action" books)

**Author:** Cowork · **For:** Claude Code · **Type:** small, additive. Builds on the `Nav` router.
**Goal:** let a `stories.json` entry render as a Library tile that, when tapped, runs a `Nav`
address instead of opening a story. First use: **Level 1–4 buttons** at the top of "All Books".

## The idea
A catalog entry with a non-empty **`action`** (a `Nav` address string) is a *navigation tile*, not
a book. It has no `script`. Tapping it calls `Nav.Go(action)`. Absence of `action` = a normal book
(today's behavior, unchanged).

Example `stories.json` entries (place them FIRST in the `books` array so they lead the home view):
```json
{ "id": "nav_level1", "name": "Level 1", "cover": "nav/level1.jpg", "action": "library?filter=level1" }
{ "id": "nav_level2", "name": "Level 2", "cover": "nav/level2.jpg", "action": "library?filter=level2" }
{ "id": "nav_level3", "name": "Level 3", "cover": "nav/level3.jpg", "action": "library?filter=level3" }
{ "id": "nav_level4", "name": "Level 4", "cover": "nav/level4.jpg", "action": "library?filter=level4" }
```
`action` can be any address the router handles (`library?filter=science`, `story?book=...`, etc.).
No need to worry about older app builds — they read the old CDN; nav tiles ship only on the new
catalog/new build.

## App changes (all additive; surgical per CLAUDE.md)
1. **`PRBook.action`** — `public string action = "";` (CSV path never sets it → "" → normal book).
2. **`Globals.ParseJSON`** — add `action = b["action"].Value,` alongside the existing
   `level`/`phonics_focus` reads. (SimpleJSON returns "" for a missing key. **Do NOT touch
   `ParseCSV`** — no such column, so CSV catalogs never contain nav tiles → clean rollback.)
3. **`BookViewItem.OnPointerClick`** — keep the null guard, then branch:
   ```csharp
   if (prBook != null)
   {
       if (!string.IsNullOrEmpty(prBook.action)) Nav.Go(prBook.action);
       else                                      Globals.GotoPrBook(prBook);
   }
   ```
4. **`Filter.Conforms`** (in `BooksScrollView.cs`) — nav tiles appear ONLY on the home view. Add at
   the TOP of `Conforms`, before the level/genre checks:
   ```csharp
   // Navigation tiles (entries with an action) show only on the home "All Books" view.
   if (!string.IsNullOrEmpty(prBook.action))
       return level == 0 && (string.IsNullOrEmpty(genre) || genre == "everything");
   ```
   Effect: on the home view (genre "", level 0) the Level buttons show; tapping "Level 2" filters
   to `level2` and the buttons disappear (they don't match a level/genre filter). Genre/age filters
   exclude them too.

**Ordering needs no code change** — put the four entries first in `stories.json`; the home view
iterates catalog order, so they lead. (The learn-to-read shelf's `(level, number)` sort is
unaffected: nav tiles are `level==0` but excluded there by `Conforms`.)

## Catalog/content side (NOT this task — note for the content hand-off)
- `book.json` gains optional `action` (string); a nav-tile `book.json` has `action` set and no
  `script`/audio. `readingbuddy-aws/tools/catalog_gen.py` must: emit `action`, and **skip the
  script/audio/content_rev validation for action-only entries** (they aren't books). Author four
  `nav_levelN` entries + their cover art (or generated level-band badges).

## Tests (EditMode, pure logic)
- `ParseJSON`: an entry with `action` set → `PRBook.action` populated; without → "".
- `Filter.Conforms`: a nav-tile `PRBook` (action set) conforms when `genre==""`/`"everything"` &
  `level==0`, and does NOT conform under `level1`/a genre filter; a normal book is unaffected
  (no regression to existing FilterConforms tests).

## Acceptance / verify (play mode)
- Home "All Books": the four Level tiles lead the grid; tapping one filters the Library to that level
  and the tiles vanish; a normal book tile still opens its story.
- `Nav.Go` from a tile works exactly like the console `NavGo` we already verified.
- EditMode suite stays green (existing + new tests).

## Safety
Additive: one optional field + ~3 tiny branches; default-empty preserves current behavior. Reuses
the shipped `Nav` router. `ParseCSV`/rollback untouched. No progress/script concerns (nav tiles have
no `script`; the tap branches before `GotoPrBook`).
