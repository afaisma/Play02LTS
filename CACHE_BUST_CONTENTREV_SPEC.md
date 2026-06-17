# Claude Code hand-off — bust the media cache with `content_rev`

**Type:** small, additive, surgical (per CLAUDE.md §"Surgical Changes").
**Problem:** images/audio/timings are cached by URL only (`PRUtils.cacheImages` in-memory +
`DiskCache` on disk). `content_rev` is parsed but explicitly *not consumed* (Globals.cs comment
"not-yet-consumed fields (content_rev, read_to_me)"). When a book is updated, the media files
change but their URLs don't — so the app keeps serving stale cached images/audio until the cache
is manually cleared. (Hit live: a re-rendered book showed old illustrations.)
**Fix:** fold the book's `content_rev` into media URLs as a `?v=<rev>` query param. A changed
`content_rev` ⇒ different URL ⇒ cache miss ⇒ fresh fetch; unchanged ⇒ same URL ⇒ cache still hits.

## 1. Store `content_rev` on the book
- `PRBook` (in `Assets/_Story/LIbrary/PRLibrary.cs`): add `public string contentRev = "";`.
- `Globals.ParseJSON` (the JSON book-construction block): set `contentRev = b["content_rev"].Value,`
  (SimpleJSON returns "" when absent → safe for older catalogs / CSV path). **Do not touch
  `ParseCSV`** (no such column → contentRev stays "" → behaves exactly as today = rollback-safe).

## 2. One idempotent helper (in `Globals`)
```csharp
// Appends ?v=<rev> for cache-busting. Idempotent and safe: no-op when rev is empty,
// when url isn't http(s), or when the url already carries a query string.
public static string WithContentRev(string url, string rev)
{
    if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(rev)) return url;
    if (!url.StartsWith("http", System.StringComparison.OrdinalIgnoreCase)) return url;
    if (url.Contains("?")) return url;            // already busted / has query → leave it
    return url + "?v=" + rev;
}
```

## 3. Apply it at the media URL/key sites (use the owning book's `contentRev`)
The cache key is the URL string, so appending `?v=` busts both the in-memory and disk caches.

- **Story images** — in `AudioAndTextPlayer`/`PRScript`/`StoryStepsUI` wherever a story image URL is
  finalized (the `DisplayMainImage`, `AddGalleryImage`, `DisplayBackgroundImage` intrinsics in
  `PRScript.SetupInterpreter`, and any cover/character image). Wrap the absolute URL with
  `Globals.WithContentRev(absUrl, Globals.g_prbook?.contentRev)`. The simplest single chokepoint is
  `PRUtils.DownloadImage(url, …)`: at the top do
  `url = Globals.WithContentRev(url, Globals.g_prbook != null ? Globals.g_prbook.contentRev : "");`
  (in the Library `g_prbook` is null → covers handled in the next bullet).
- **Audio + timings** — in `AudioAndTextPlayer.Play(...)`, after building `audioURL` and
  `jsonTimingsURL` (the `{chunk}_{rate}{voice}.mp3` / `_timings.json` strings), append the rev to
  those *relative* strings (they're reused as the disk-cache keys at
  `DiskCache.PathFor(audioURL,"audio",…)` / `TryReadText(textURL,"timings",…)`), e.g.
  `string rev = Globals.g_prbook != null ? Globals.g_prbook.contentRev : "";`
  `if (!string.IsNullOrEmpty(rev)) { audioURL += "?v="+rev; jsonTimingsURL += "?v="+rev; }`
  so the busted suffix flows into both the fetch URL (`baseURL+audioURL`) and the cache key.
- **Library covers** — where `BookViewItem`/the cover grid downloads `bookImageUrl`, wrap with
  `Globals.WithContentRev(coverUrl, thisItem.book.contentRev)` (the item knows its own book).
  The `WithContentRev` idempotency guard prevents a double `?v=` if `DownloadImage` also wraps.

## 4. Tests / verify
- EditMode: `WithContentRev` — empty rev → unchanged; non-http → unchanged; url with existing `?`
  → unchanged; normal http + rev → `…?v=<rev>`.
- Play: update a deployed book (new `content_rev` in stories.json) → app shows the NEW image/audio
  on next open **without** manually clearing `~/Library/Application Support/Imagiration/ReadingBuddy/cache`.

## Safety
Additive; default-empty `contentRev` reproduces today's behavior exactly. `ParseCSV` untouched.
The helper is idempotent and only rewrites http media URLs.
