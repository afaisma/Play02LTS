# ReadingBuddy — Bug Findings (Round 5, 3.1.0 release QA)

*Audit of the runtime path a child actually uses (Home → Library → Story, narration, word tap, read-along, image/audio caches), done while shipping iOS 3.1.0. Rule for this round: nothing is listed as a bug unless it was reproduced in the Editor or proven from data; suspicions that did not survive are listed at the end as false alarms.*

Fixed in build 311 (shipped): F-1, F-2, F-3. Everything else is post-release, one at a time, tests + Simulator smoke each.

---

## Fixed in 3.1.0 (build 311)

### F-1. Puzzle mode silently dead since the lean image decode

**File:** `Assets/_Story/GUI/PuzzleImage.cs` (RefreshVisuals)

Since `339f159` page textures load with `markNonReadable = true`, and `RefreshVisuals` had an `if (!src.texture.isReadable) { …show as normal image; return; }` gate. Tapping the puzzle button after narration set `IsPuzzled = true` and built 0 pieces. The pieces are `Sprite.Create(src.texture, subRect, …)`, which needs no CPU pixels, so the gate was removed. Verified: Good People, 2732² non-readable texture → 9 pieces, screenshot checked.

### F-2. Word-tap slice became runaway playback of the whole word bank

**File:** `Assets/_Story/Players/AudioAndTextPlayer.cs`

`Play()`, `PlayExt()`, `StopAudio()` and `StopPageAudioForReadAlong()` call `StopAllCoroutines()`, which killed `PlayWordTapSlice` — the only thing that ever stopped `_wordTapSource` at the slice end. Tap a word, then turn the page / open the picker within ~0.5 s → the entire `wordbank.mp3` (82 s on Good People, up to 7.5 min) kept playing over the next page. Reproduced: source still playing at t = 12 s after `StopAudio()`. Fix: `StopWordTap()` (stop source, hide flash, restore volume) at those four sites; the slice fade now targets the authored volume (`_wordTapVolume`, captured in `Start`) instead of the current one, which also closes a latent ratchet (a tap during a fade captured the half-faded volume as the new target). 10 published books ship a word bank.

### F-3. Two loads of the same picture at once orphaned a full-size texture

**File:** `Assets/_Story/Utils/PRUtils.cs` (LoadImageSprite, network path)

`SpriteLruCache.Add` on an existing key replaces the entry *without* destroying the old sprite ("the caller owns it"). Two callers that both missed the cache both downloaded and both added → one texture (up to 29 MB) neither in the cache nor destroyed, until the next scene load. Two everyday producers: `Gallery.addGalleryItem` calls `DisplayCurrentItem()` per `AddGalleryImage`, so a page with N gallery images requests item 0 N times (LittleAngelLoveScience: 18 such pages, 2–5 images each); and a page turn while `PrefetchNextPage` is mid-download of that page (`Runnable.Stop` does not abort the nested request). Reproduced: 4 concurrent loads → 3 orphaned 1024² textures. Fix: after the download, if the url is now cached, destroy the fresh texture and hand out the cached sprite. Verified: 4 loads → 1 texture, all four Images share it.

---

## Open — verified, post-release

### O-1. Stale download overwrites the current page's picture (fast page turn on a cold cache)

**File:** `Assets/_Story/Story/Gallery.cs:181`, `Assets/_Story/Utils/PRUtils.cs:297-301`

`DownloadImage` only checks `image == null` after the wait, never whether the Image still wants that url; `clearUpGalleryItems()` does not stop the in-flight coroutine. Page 1's picture still downloading → Next → page 2 (cached) shows → page 1's download lands on `imgMain`. Reproduced with two loads on one Image (slow first, cached second): the Image ended up showing the slow one. Same for `StoryStepsUI.cs:146` (background). Proposed fix: Gallery keeps the `Coroutine` handle of the main-image load and `StopCoroutine`s it in `clearUpGalleryItems` / before starting the next one (smallest change; the abandoned `UnityWebRequest` is released by its finalizer).

### O-2. Manual Next during read-along can auto-turn a second page

**File:** `Assets/_Story/Story/PRScript.cs:1283,1296`, `Assets/_Story/Players/ReadAlongService.cs:210-219`

`NextStep`/`PrevStep` disarm SpeechListenService but never touch ReadAlongService. The service's completion check keeps running with the old page's cursor until the *new* page's timings are parsed (`ReadAlongPageReady → Begin`), which on a first read is a network fetch. Child reads ≥90 % of page N, taps Next before the 1.2 s auto-turn, timings for N+1 take longer than the remaining silence → `Complete()` → `OnPageReadComplete()` → `NextStep()` again → lands on N+2. Verified by reading (needs a real microphone to reproduce). Proposed fix: a `ReadAlongService.PageChanging()` that sets `_completed = true` and `Stop()`s, called from `NextStep`/`PrevStep`; `Begin` already resets the flag.

### O-3. Reading-mode picker leaves the read-along microphone live

**File:** `Assets/_Story/Story/UnifiedReadingModePicker.cs:646-665`

`OpenPicker()` only calls `_player?.StopAudio()`. With "I read it myself" on, speech behind the modal still advances the highlight and a lenient completion turns the page while the picker is up (mic indicator stays on). Proposed fix: `_readAlong?.Stop()` in `OpenPicker`; `ClosePicker → ReplayCurrenStep` already restarts it through `Play → ReadAlongPageReady`.

### O-4. A door with a bad Nav address leaves the NavCover on screen forever

**File:** `Assets/_Story/Home/TapFeedback.cs:85-92`

If `nav()` no-ops (e.g. `home_doors.json` door `"filter": "story?book=Typo"` — addresses skip the catalog check), the 6 s watchdog resets `_armed` but never destroys the full-screen `NavCover` that blocks raycasts: Home is a blank page-coloured screen until the app is killed. Content-triggered only; the live `home_doors.json` uses plain filter tokens, all present in `PRLibrary.bookCategories`. Proposed fix: destroy `cover.gameObject` in the watchdog branch (2 lines).

### O-5. `DownloadImage` stamps the last-read book's `?v=` onto unrelated urls

**File:** `Assets/_Story/Utils/PRUtils.cs:285,324`, `Assets/_Story/Story/Globals.cs:669`

`Globals.g_prbook` is never cleared after a book, so Home door art (`ResolveDoorImageUrl`, no `?`), Bookstore covers and CSV-catalog covers get `?v=<last book's rev>` — a different memory/disk key after every different book. Online: a redundant re-download; offline after reading a book: door art misses the disk cache and every door falls back to the glyph tile. Proposed fix: only append the rev when the url belongs to the open book's folder (`url.StartsWith(g_prbook.bookFolderUrl)`), or clear `g_prbook` in `Navigation.GoToHome`.

### O-6. Library "finished" dot does not update in-session

**File:** `Assets/_Story/LIbrary/PRLibrary.cs:486-489`, `LIbrary/BookViewItem.cs:49`

`SetBookDone` writes the pref but not `prBook.book_done`, which `BookViewItem.SetBookProperties` reads and which is only filled at catalog parse. Finish a book → back to the Library → still the in-progress shade until the next launch (Home's rail reads prefs directly and is right). One-line fix: set the field in `SetBookDone`.

### O-7. Word-bank clip leaks when the child leaves the book before it finishes loading

**File:** `Assets/_Story/Players/AudioAndTextPlayer.cs:838-917`

`LoadWordBankAssets` runs on `Runnable` (survives the scene) and completes on the destroyed player: `_wordBankClip = clip` on a dead instance, never destroyed until the next scene load (up to ~80 MB decoded for the duration of the next book). Also a decoded clip is dropped without `Destroy` when `wordbank.json` is missing/malformed. Fix: `if (this == null || key != _wordBankLoadedRev) { Destroy(clip); yield break; }` before assignment, and `Destroy(clip)` on the two early exits.

### O-8. DiskCache writes are not atomic; a corrupt timings file is served forever

**File:** `Assets/_Story/Utils/DiskCache.cs:87,102`, `Assets/_Story/Players/AudioAndTextPlayer.cs:404-411`

`File.WriteAllBytes/Text` straight to the final path. A kill or full disk mid-write leaves a partial file that `TryReadText` returns as a hit; `JSON.Parse` on it throws inside `LoadAudioAndTimings`, so that page never narrates on any later visit (nothing deletes a bad entry; images alone fall through to network). Fix: write to `path + ".tmp"` then `File.Move`/replace; wrap the two `JSON.Parse` calls in try/catch that deletes the cached file and falls through to network.

### O-9. Narration MP3 disk hits never touch last-access → audio eviction is FIFO

**File:** `Assets/_Story/Players/AudioAndTextPlayer.cs:349-361,850-860`

Both audio readers go `PathFor + File.Exists + file://` and skip `TryReadBytes`, so `SetLastAccessTimeUtc` never runs; `TrimSubdirToBudget` then evicts the most re-read book's narration first once the 150 MB tier is full. Fix: touch the access time in that branch (one line).

### O-10. Catalog download caches before it parses

**File:** `Assets/_Story/Story/Globals.cs:930-937`, `ParseJSON` 768

A 200 response whose body is not the catalog (a broken publish; captive portals are ruled out because the catalog url is HTTPS) is written to the disk cache first, then `JSON.Parse` throws or yields an empty list: the Welcome button sticks on "Loading Library Catalog…" / Home loops on "Loading…", and the previously good offline copy is overwritten until a real online launch. `rb_publish`'s lint gate makes this unlikely; fix is parse-then-cache with `Count > 0`.

### Latent / low

- `FilterContainer.OnFilterChanged` only routes tokens that exist in `PRLibrary.bookCategories` (+ `levelN`, age ranges); a future `home_doors.json` door with a new genre token would open an unfiltered "everything" shelf under that door's label. All current tokens are in the list.
- `TextLoader` (Parents letter) resolves book links through `PRLibrary.prbooks`, which is only set when the Library scene has run; from Home → For grown-ups the links are dead. Should read `Globals.g_listPRBooks` and go through `GotoPrBook`.
- The Parents letter is network-only (no DiskCache) → blank offline.
- `AudioPlayer.LoadAudioClip` appends a new decoded clip on every (re)execution of a chunk with `AddAudio`; nothing removes or destroys them within a book (bounded by the scene reload).
- `OverlayHost` sprite-sequence frames download readable (`GetTexture(url)` without `nonReadable`), doubling their memory; not a leak.
- Read-next "Let's go!" / Library row double-tap issues two `LoadSceneAsync` (visible double start, no crash).

---

## False alarms (checked, cleared)

- **Runnable coroutines dying on scene load** (would have left `_trimScheduled` stuck and the image cache unbounded): the Runnable GameObject is `HideAndDontSave`, which includes DontSave → not destroyed on scene load. Fine.
- **`protectNewest` no longer protecting the newest entries** once in-use entries are moved to the tail: true in theory, but in the Story scene the in-use set is 1–3 pages (≤ 90 MB) so the walk always reaches budget before the tail; on the shelves everything displayed is in-use-protected anyway.
- **Captive-portal poisoning of the catalog cache**: the catalog url is HTTPS, so a portal produces a TLS/connection error and the cached copy is used.
- **Store build reads `…/stories-qa/stories.json`**: same url the released 3.0.0 used (`898e56a`); "qa" is just the tree's name. By design.
- **Hidden pooled library rows keeping covers alive / grey squares**: `inUseProvider` scans inactive Images too, and `BooksScrollView.AddBook` re-fetches a destroyed cover.
- **Static-event double subscription** (`Application.lowMemory`, `sceneLoaded`, `activeSceneChanged`): all one-shot guarded.
- **PlayerPrefs key drift**: every key is written and read under the same literal.
- **`MemFootprint.mm` `long long` vs C# `long`**: both 64-bit; plugin meta is iOS-only.
- **Sweep "learn" action opens the retired `_LearnToRead` ladder**: the sweep's own navigation, not reachable from the app.

---

## Housekeeping noticed

- `readingbuddy-aws/tools/regen_chunk_azure.py` has an uncommitted July change (Azure word timings via a file sink, with validation + retries) — commit or discard.
- `readingbuddy-aws/.claude/settings.local.json` is untracked; add `.claude/` to `.gitignore`.
- `Build/_smoke/` scratch from this round: `story_src.tgz`, `stories.json`, `home_doors.json`, `puzzle_fix.png`, `_device_export_status.txt` (all ignored by git).
