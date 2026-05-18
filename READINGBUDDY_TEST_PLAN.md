# ReadingBuddy — Test Plan for the 9 Applied Fixes

*How to verify the fixes that just landed. Each section gives a precise reproduction, the expected behavior before and after, and where applicable a quick way to confirm without a full app run.*

---

## Summary of what changed

| ID | Bug | File(s) edited | Diff size |
|---|---|---|---|
| **C1** | TLS cert verification fully bypassed | `PRUtils.cs:148-160, 216-233` | 4 lines removed (the `certificateHandler =` assignments) + comment |
| **C2** *(Level 1)* | One bad CSV row aborted the entire catalog load | `Globals.cs:371-413` | +12 lines (per-row try/catch + summary log) |
| **C3** | Caches were FIFO, not LRU | `PRUtils.cs:210-218`, `AudioAndTextPlayer.cs:278-284` | +6 lines |
| **H1** | `DownloadFile` ignored `https://` | `PRUtils.cs:132` | 1-line condition change |
| **H3** | `UnityWebRequest` not disposed | `PRUtils.cs:148-160, 216-233` | wrapped in `using` |
| **H4** | Fragment audio clips leaked | `AudioAndTextPlayer.cs:387-394` | +4 lines |
| **H6** | Session stats lost on Android background-kill | `Globals.cs:105-112, 130-141` | +10 lines (new pause hook + idempotency reset) |
| **H7** | "Pastel Lavender" was actually muddy yellow | `PRUtils.cs:42` | 1 number changed |
| **H8** | Multi-image gallery trapped the user on edges | `PRScript.cs:776-815` | rewritten swipe handlers + fixed misleading log |

All changes are in the user's working tree (`/Users/alexanderfaisman/dev/Play6.3/Assets/_Story/`) ready to open in Unity.

---

## Pre-flight (do once before any of the tests below)

1. Open the project in Unity 6 (`6000.3.9f1`).
2. Confirm the project compiles. Window → General → Console should show **no errors** after the asset-import scan. The fixes only change method bodies — no API or signature changes — so any compile error is a sign something went wrong.
3. With `_StartScene` open, set `Globals.csvUrl` in the Inspector to your usual dev backend (the local FileServer, the QA bucket, or production).
4. Run on **two** targets if you can: the Editor (for fast iteration) and **a real Android device** (the only place H6 and the H9 follow-up can be fully observed).

---

## Per-fix tests

### C1 — TLS verification is back on

**What to verify:** the app no longer accepts arbitrary HTTPS certificates.

**Quick code check (no app run needed):**
```
grep -rn "certificateHandler" Assets/_Story/
```
Expected: only the C1 explanatory comments, no actual assignment of `request.certificateHandler = ...`. (`AcceptAllCertificatesHandler.cs` still exists as a dead class — leave it for a follow-up cleanup PR.)

**Manual test 1 — production still works:** With `Globals.csvUrl` pointing at the CloudFront URL (`http://d5wtw8f0w3ire.cloudfront.net/...`), launch the app. The Library should populate exactly as before, with all 67 book covers visible. The production CDN is plain HTTP, so cert verification is irrelevant — this confirms nothing else broke.

**Manual test 2 — bad cert is now rejected:**
1. Start the local FileServer with `gradlew bootRun` but flip its SSL config on: in `application.properties`, uncomment the `server.ssl.*` lines so it serves HTTPS with `keystore.p12` (a self-signed cert).
2. Point `Globals.csvUrl` at `https://localhost:8443/api/files/download/stories/stories.csv`.
3. Run.

Expected: the catalog download fails. The retry button in `_StartScene` appears with text "Connect to the Internet and Retry," and the Console logs a TLS error. (Before C1, the bad cert would have been accepted and the download would have succeeded — that's the regression we wanted.)

**Sign-off criteria:** production loads normally; a self-signed cert is correctly rejected.

---

### C2 — One bad CSV row no longer kills the catalog

**What to verify:** if `stories.csv` contains a row that fails to parse (missing field, non-numeric age, etc.), that row is skipped with a warning, and every other row still loads.

**Quick code check:**
```
grep -n "ParseCSV: skipping malformed row" Assets/_Story/Story/Globals.cs
```
Expected: one hit inside the new catch block.

**Manual test — inject a bad row:**

The easiest way to reproduce is against the local FileServer:

1. Make a temporary copy of `stories.csv`:
   ```
   cp /Users/alexanderfaisman/dev/FileServer/uploads/stories/stories.csv \
      /Users/alexanderfaisman/dev/FileServer/uploads/stories/stories.csv.bak
   ```
2. Edit `stories.csv` and insert one malformed row **before** any valid row. Three flavors of "bad" worth testing one at a time:
   - **Too few columns:** `Bad Book,Some Author` (only 2 fields).
   - **Non-numeric age:** `Bad Book,Some Author,thumb.jpg,foo.txt,three,8,Family,notes,99` (ageFrom = "three").
   - **Empty age:** `Bad Book,Some Author,thumb.jpg,foo.txt,,8,Family,notes,99` (empty ageFrom).
3. Start the FileServer, point `Globals.csvUrl` at the local URL, run the app.

**Expected after C2:**
- The Library populates with **all 67 good books** (catalog count is the same as before — the bad row is not displayed).
- The Console shows one `ParseCSV: skipping malformed row #1 (FormatException: ...). Row content: "Bad Book,Some Author,..."` warning.
- At the end of the parse, a summary line: `ParseCSV: loaded 67 books, skipped 1 malformed row(s).`

**Before C2:** the first bad row would throw `FormatException` or `IndexOutOfRangeException` out of `ParseCSV`, leaving `g_listPRBooks` null. `PRLibrary.LoadBooksWithRetry` would then poll three times (6 seconds total), log "Failed to load books," and the Library would render empty with no books and no error message to the user. The whole catalog is lost because of a single bad row.

**Regression check — clean CSV still works:** restore `stories.csv` from the `.bak`, re-run. All 67 books load with no warnings. (This confirms the try/catch didn't change behavior on good data.)
```
mv /Users/alexanderfaisman/dev/FileServer/uploads/stories/stories.csv.bak \
   /Users/alexanderfaisman/dev/FileServer/uploads/stories/stories.csv
```

**Numbering check:** the `PRBook.number` field is used as a display index. With the bad row at position 1 (the first data row), the first *good* book should still get `number = 0` — i.e. numbering is contiguous from 0 regardless of skipped rows. Verify by reading any code that displays `book.number`, or in the Editor inspector on a `BookViewItem`.

**Important caveat:** Level 1 does **not** fix the underlying CSV-parsing weakness (`Split(',')` still doesn't handle quoted commas). It only ensures the failure mode is "skip one book" instead of "lose the whole catalog." A future row that legitimately needs a comma inside a quoted field will *still* fail to parse — but it will now fail visibly in one row instead of silently killing all 67.

**Sign-off criteria:** with a bad row injected, the Library still shows the other 66 books and the Console contains a clear warning naming the bad row.

---

### C3 — Cache is now LRU, not FIFO

**What to verify:** repeatedly accessed entries survive cache eviction; old unused entries are the ones evicted.

**Best way to test:** the cache size is hard-coded to 30 entries. A focused stress test in the Quantum Console:

1. Open a book with 30+ pages (e.g., **Alphabet Rhymebook** has 27 chunks — close enough; or **Cinderella**).
2. Open the in-game console (tilde key) and run:
   ```
   SetStep 0
   ```
3. Tap "Next" through to the last page so every page's audio + timings get cached.
4. Open a **second** book and read 5 pages — this evicts 5 entries from the audio cache.
5. Go back to book 1 and re-open page 0 (the one you accessed first, the "oldest" by insertion order).

**Expected after C3:** page 0's audio plays from cache *with no perceptible delay* (the half-second start delay only, no network spinner). Without C3, page 0 would have been the first evicted (FIFO) and would re-download.

**Verifying via logs:** Add a temporary `Debug.Log("CACHE HIT for " + audioURL)` inside the `if (CacheAudioAndTimingsStructs.Contains(audioURL))` block in `AudioAndTextPlayer.cs`. Page 0's open should log a CACHE HIT after step 5. Remove the log before merging.

**Image-cache version:** same idea — scroll the Library, drag a few cover images into view (each is cached), then scroll back to the first cover. Before C3 they'd re-download once 30 images had been seen; after, the early ones stay cached.

**Sign-off criteria:** previously-accessed audio/images replay/re-show with no network round trip even after 30 newer items have been seen.

---

### H1 — `https://` URLs route through the network path

**What to verify:** any URL with a scheme (http, https, file) is treated as a network URL; only schemeless paths and `resources:foo` go to local Resources.

**Code check:**
```
grep -n "Contains(\"://\")" Assets/_Story/Utils/PRUtils.cs
```
Expected: one hit on line ~134 (`if (!url.Contains("://"))`).

**Manual test — HTTPS catalog:**
1. Set `Globals.csvUrl` to any `https://` URL that serves a valid CSV. The simplest reproducible one: enable HTTPS on the local FileServer per C1's manual test 2 (with a valid cert this time — or use any HTTPS-served test CSV).
2. Run the app.

**Expected after H1:** the catalog downloads and the Library populates. **Before H1**, the URL would have been passed to `Resources.Load<TextAsset>("https://...")`, returned null, and the coroutine would have hung silently with no log beyond "Could not find local resource."

**Regression check — `resources:` still works:** in a story script, use `resources:NoImage` as an image URL. The fallback NoImage sprite from `Resources/` should still load. This confirms we didn't break the local-resource path.

**Sign-off criteria:** an HTTPS CSV loads; the `resources:` pseudo-scheme still resolves to a `Resources.Load` call.

---

### H3 — `UnityWebRequest` is disposed

**What to verify:** no native handle leaks on repeated page turns / book opens.

**Quick code check:**
```
grep -n "using (UnityWebRequest" Assets/_Story/Utils/PRUtils.cs
```
Expected: two `using` blocks (one in `DownloadFile`, one in `DownloadImage`).

**Soak test (the only real way to verify a leak):**
1. Build a Development build with **Autoconnect Profiler** enabled.
2. Connect the Unity Profiler → Memory module → switch to "Detailed" mode.
3. Open a book and tap Next through every page back-to-back at the fastest pace (or use auto-page-turn for a hands-off run).
4. After ~100 page turns, take a memory snapshot.
5. Compare against a snapshot taken at the same point on the pre-fix build.

**Expected after H3:** the "Native → UnityWebRequest" allocation count plateaus instead of climbing linearly. The "DownloadHandlerTexture" / "DownloadHandlerBuffer" counts plateau similarly.

**A cheaper smoke test:** open and close 10 books in a row (Library → tap book → wait → Home → tap next book). Watch the Unity Editor's bottom-right stats display for "Total Reserved" memory. Before H3 it should creep up by a few MB per book; after H3 it should stay roughly flat (with normal GC churn).

**Sign-off criteria:** repeated reading doesn't grow native memory unboundedly.

---

### H4 — Fragment audio clips are destroyed before the next is created

**What to verify:** the fragment-clip leak inside `LoadAudioAndTimings` is closed.

**Quick code check:**
```
grep -n "StartsWith(\"Fragment_\")" Assets/_Story/Players/AudioAndTextPlayer.cs
```
Expected: one hit, around line 393, inside the clip-assignment block.

**Manual test — `PlayAudioAndShowText` exercises the fragment path:**
The only books that hit this path use `PlayAudioAndShowText` with non-default `fromS`/`toS`. Grep the catalog:
```
grep -rln "PlayAudioAndShowText" /Users/alexanderfaisman/dev/FileServer/uploads/stories/
```
Open one of those books (in my last audit, **The Tale of Peter Rabbit** uses static-text + audio fragments) and tap through all pages twice. Use the Unity Profiler → Audio module to count live AudioClips.

**Expected after H4:** the AudioClip count grows during the first read-through, then plateaus. Each page turn that creates a `Fragment_*` clip destroys the prior one. Before H4, the count climbed by one per page indefinitely.

**Editor-only spot-check:** add a temporary `Debug.Log("audioSource.clip = " + audioSource.clip?.name)` after the `audioSource.clip = ...` assignments. On a multi-page fragment book, every page should log a fresh `Fragment_*` name; the previous one should not be reachable. (Run the editor with "Stop on Error" off; this is informational only.)

**Sign-off criteria:** repeated reading of a fragment-using book doesn't grow live AudioClip count.

---

### H6 — Session minutes survive a recent-apps-swipe kill on Android

**What to verify:** `PlayerPrefs["TotalMinutesInGame"]` is correctly updated when the user backgrounds the app, even if they then kill it from recents.

**Reproduction (Android device required):**
1. Install a Development build on an Android device.
2. Cold-launch the app. Read one book for **2 minutes** (use a real timer). Don't quit yet.
3. Press the **home button** to background the app. (This is the path that fires `OnApplicationPause(true)` but **not** `OnApplicationQuit`.)
4. Swipe the app out of recents.
5. Re-launch the app.
6. In the Unity Editor with the same device connected via `adb logcat` or via a Development build's logging, dump `PlayerPrefs.GetFloat("TotalMinutesInGame", 0f)` — easiest is to add a one-shot `Debug.Log` at the top of `InitializeGameStatistics()`.

**Expected after H6:** the logged value is ~2.0 (give or take a few seconds for app start/shutdown). Before H6, it would be 0.0 because only `OnApplicationQuit` saved stats and that hook doesn't fire on a recents-swipe kill.

**Double-counting regression check:** if you instead do home → app comes back → home → kill, the value should be the *actual* elapsed time, not double. The fix to `UpdateGameStatistics` resets `gameStartTime` after each save specifically to prevent double-counting. Verify by reading once, backgrounding, foregrounding, reading one more minute, then killing — expect ~3.0, not ~5.0.

**iOS equivalent:** `OnApplicationPause(true)` fires on iOS when the app moves to background, so the same flow works on iOS too. Worth running once on iOS as a sanity check.

**Sign-off criteria:** session minutes are durable across a background-kill on Android, with no double-counting on background-resume cycles.

---

### H7 — Pastel Lavender is actually lavender

**What to verify:** the `Pastel Lavender` entry in `PRUtils.pastelColors` produces a soft lavender, not the previous yellow-brown.

**Easiest test — Unity Editor inspector:** there's no UI surface that names "Pastel Lavender" directly, but the color is used by `MapStringToPastelColor` as one of the eight hash-bucket fallback colors. Anything that calls `PRUtils.textToColor("some-string-that-hashes-to-lavender")` will produce a different color now.

**Quickest verification path:** open a C# script test/Editor menu and run:
```csharp
[MenuItem("Dev/Print Pastel Lavender")]
static void PrintLavender() {
    Color c = PRUtils.pastelColors["Pastel Lavender"];
    Debug.Log($"Pastel Lavender = {c} (RGB hex {(int)(c.r*255):X2}{(int)(c.g*255):X2}{(int)(c.b*255):X2})");
}
```

**Expected after H7:** prints `RGBA(0.902, 0.745, 0.941, 0.350)` → hex `~E6BEF0` (a pale lavender). Before, blue was 0.350 and the color was `~E6BE59` (a sandy yellow).

**Visual sanity:** if any in-app element uses a pastel-colored chip (e.g., a category background, a debug tint), and any of them happened to be Pastel Lavender, that element will now look lavender instead of muddy. Since the hash bucket depends on string input, the assignment of which UI elements get which color may shift if multiple things share the bucket.

**Sign-off criteria:** the printed color reads `(~0.90, ~0.74, ~0.94, ~0.35)`.

---

### H8 — Multi-image gallery swipe never traps the user

**What to verify:** swiping left on the last gallery image advances the page; swiping right on the first gallery image goes back to the previous page; swipes in the middle of a multi-image gallery still cycle through images; single-image pages still page-turn on any swipe.

**Find a multi-image page:** in `stories.csv`, browse to any book with multiple `AddGalleryImage` calls in a single chunk. **Cinderella**, **Goldilocks and the Three Bears**, and **Knights of Camelot** have galleries; the simplest is anything in `LittleAngelLoveScience/`. Confirmed via:
```
grep -l "AddGalleryImage" /Users/alexanderfaisman/dev/FileServer/uploads/stories/*/
```
You want a chunk that calls `AddGalleryImage` two or more times.

**Manual scenarios:**

| # | Setup | Action | Expected (after H8) |
|---|---|---|---|
| 1 | Multi-image gallery, on image 1 of 3 | swipe left on the gallery | gallery advances to image 2 of 3 |
| 2 | Multi-image gallery, on image 3 of 3 (last) | swipe left on the gallery | **page advances to the next chunk** (before H8: nothing happened) |
| 3 | Multi-image gallery, on image 1 of 3 (first) | swipe right on the gallery | **page goes back to the previous chunk** (before H8: nothing happened) |
| 4 | Multi-image gallery, on image 2 of 3 | swipe right on the gallery | gallery retreats to image 1 of 3 |
| 5 | Single-image page | swipe left or right on the gallery | page advances / retreats (unchanged behavior) |
| 6 | Any page | swipe left/right on the **text area** (`textforeground`) | page advances / retreats (unchanged behavior) |

**Bonus check (log clarity):** swipe right on any page. The Console log should now read `"RightSwipe Gallery"` (or similar), not the misleading `"LeftSwipe Gallery"` from before. This is the L9 fix that piggy-backed on H8.

**Sign-off criteria:** all 6 scenarios above behave as listed, with no swipe ever silently swallowed.

---

## Cross-cutting smoke test

Run this as the final pass before declaring "ready to merge":

1. **Cold launch** (kill app first) → Library loads → tap any book → reads page 1.
2. **Swipe left** on the gallery (if multi-image) → goes through gallery → keep swiping → page advances → reads page 2.
3. **Swipe right** → goes back through gallery → page goes back.
4. Tap **Auto-page-turn** toggle on → audio plays → page advances on its own (this exercises the *unmodified* auto-advance path; H9 is **not** in this batch).
5. **Press Home** → wait 5 s → re-launch.
6. Open the **same book again** → progress was saved, and the same audio plays from cache (no network spinner).
7. Open a **different book** → reads it for 1 minute.
8. **Kill from recents.** Re-launch. Open Settings (or whatever shows stats) → verify `TotalMinutesInGame` increased by ~1 minute (not 0, not 2).
9. Open **Inspect → Network** (in Editor) and confirm there are no 30+ duplicate audio downloads as you flip through 30 pages of any rhymebook (C3 — cache effective).

Any deviation here points back to one of the fixes regressing.

---

## What's intentionally NOT covered

These were also in the bug findings but were *not* fixed in this batch (per the earlier discussion of "simple and safe"):
- **C2 (Levels 2 & 3)** — full RFC 4180 CSV parser, or schema migration to JSON. Level 1 (per-row try/catch) **was** applied and is tested above; the deeper parser work is deferred.
- **H2** — silent-resource-not-found callback (one-liner but has UX implications, deferred).
- **H5** — `ParseTimings` cache key corruption (needs design pass).
- **H9** — Android auto-page-turn timer (instrument first, fix after).
- **H10** — double-save on page turn (naive fix breaks ReplayCurrentStep).

If a test in this plan accidentally exposes any of those, log it but don't conflate it with a regression in the 8 fixes that just landed.

---

## Rollback

Each fix is a self-contained `Edit` to one of four files. If a regression appears, the cleanest rollback is per-fix:

| ID | Revert by reverting changes in |
|---|---|
| C1, H1, H3 | `Assets/_Story/Utils/PRUtils.cs` (DownloadFile + DownloadImage methods) |
| C2 | `Assets/_Story/Story/Globals.cs` (the `try { ... } catch` wrap inside `ParseCSV`'s while loop, plus the summary `Debug.LogWarning` after it) |
| C3 | `Assets/_Story/Utils/PRUtils.cs` (DownloadImage cache-hit block) and `Assets/_Story/Players/AudioAndTextPlayer.cs` (cache-hit block) |
| H4 | `Assets/_Story/Players/AudioAndTextPlayer.cs` (the `if (audioSource.clip != null && ... StartsWith("Fragment_")) Destroy(...)` block) |
| H6 | `Assets/_Story/Story/Globals.cs` (the new `OnApplicationPause` method and the `gameStartTime = Time.time;` line in `UpdateGameStatistics`) |
| H7 | `Assets/_Story/Utils/PRUtils.cs:42` (the single Color constant) |
| H8 | `Assets/_Story/Story/PRScript.cs` (the `LeftSwipe` and `RightSwipe` methods) |

Each is git-reversible without touching the others.
