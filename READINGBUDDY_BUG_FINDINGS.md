# ReadingBuddy — Bug Findings

*Result of a line-by-line audit of the core scripts in `Assets/_Story/`. Every bug here is grounded in a specific file:line reference. Severity reflects user impact and likelihood, not necessarily fix effort.*

Files audited:
- `Assets/_Story/Players/AudioAndTextPlayer.cs` (568 lines)
- `Assets/_Story/Story/PRScript.cs` (816 lines)
- `Assets/_Story/Story/Globals.cs` (482 lines)
- `Assets/_Story/Story/StoryStepsUI.cs` (282 lines)
- `Assets/_Story/Story/Gallery.cs` (179 lines)
- `Assets/_Story/Utils/PRUtils.cs` (385 lines)
- `Assets/_Story/Utils/AcceptAllCertificatesHandler.cs` (12 lines)
- `Assets/_Story/LIbrary/PRLibrary.cs` (305 lines)

---

## Critical

### C1. `AcceptAllCertificatesHandler` disables all TLS verification

**File:** `Assets/_Story/Utils/AcceptAllCertificatesHandler.cs:7-11`

```csharp
protected override bool ValidateCertificate(byte[] certificateData)
{
    // Always return true to accept all certificates, including self-signed ones
    return true;
}
```

This handler is attached to every web request in the project — `PRUtils.DownloadFile()` (line 149) and `PRUtils.DownloadImage()` (line 217). Combined effect: the app accepts **any** certificate, including expired, self-signed, or MITM-forged ones. On a hostile WiFi network, an attacker could serve children any content they want — substituted images, audio, or scripts — and the app will display it. Production CDN traffic is currently HTTP and bypasses TLS entirely, but the attack surface here is large and the handler should never have been left on.

**Fix:** Remove the handler entirely. If a specific endpoint needs an exception (e.g., a self-signed dev server), gate it on `Application.isEditor` or a debug build flag.

---

### C2. CSV parser uses naïve `Split(',')` — a single comma in any field corrupts the catalog

**File:** `Assets/_Story/Story/Globals.cs:383`

```csharp
string[] values = line.Split(',');
```

There is no quote handling, no escape handling. If any book name, author, genre, or notes-for-parents field ever contains a comma (e.g., a future book titled *"Apples, Oranges, and More"*), that book's row shifts every subsequent column and either throws `int.Parse` (on age columns now landing on text) or silently misreads URLs and ids. The current catalog happens to avoid commas in any field, so this is latent — but it will bite on the first comma added.

**Related:** lines 391–392 call `int.Parse(values[4])` and `int.Parse(values[5])` without try/catch. A single malformed row aborts the entire `ParseCSV` and leaves `g_listPRBooks` null. `PRLibrary.LoadBooksWithRetry` (line 75) will then sit in its retry loop and time out, presenting the user with a silent empty library.

**Fix:** Use a proper CSV reader (e.g., a small inline parser that handles `"…"` quoting), and wrap the per-row parse in a try/catch that logs the bad row and continues.

---

### C3. Cache "LRU" is actually FIFO — hot entries can be evicted before cold ones

**Files:**
- `Assets/_Story/Players/AudioAndTextPlayer.cs:278-281, 478-487`
- `Assets/_Story/Utils/PRUtils.cs:210-214, 236-243`

Both caches use `OrderedDictionary` with this pattern:

```csharp
// hit:
if (cache.Contains(url)) return cache[url] as T;          // ← does NOT reorder
// miss:
if (cache.Count >= max) cache.RemoveAt(0);                // remove "oldest"
cache[url] = value;                                       // insert at end
```

The comment in `AddToCache` says "Remove the oldest entry" implying LRU, but a cache hit does not move the entry to the end. Effect: a cover image that's accessed on every Library scroll can be evicted by 30 unrelated reads. Not catastrophic (worst case: re-download), but defeats the point of the cache, and on flaky networks visibly stalls page turns.

**Fix:** On a hit, remove and re-insert the key to move it to the end:

```csharp
if (cache.Contains(url)) {
    var v = cache[url];
    cache.Remove(url);
    cache[url] = v;
    return v as T;
}
```

---

## High

### H1. `PRUtils.DownloadFile` only recognizes `http:` — `https:` URLs silently fall into the `resources:` branch

**File:** `Assets/_Story/Utils/PRUtils.cs:132-146`

```csharp
if (!url.StartsWith("http:"))
{
    // Load from Resources
    string resourcePath = url.Replace("resources:", "").TrimStart('/');
    TextAsset asset = Resources.Load<TextAsset>(resourcePath);
    ...
}
```

`"https://example.com/foo.txt".StartsWith("http:")` is **false**. The function will try `Resources.Load<TextAsset>("https://example.com/foo.txt")`, get null, log a not-found, and **never call `onComplete`** (see H2). Any switch to HTTPS — required by iOS App Transport Security for new submissions unless an exception is configured — will silently break every story download.

**Fix:** `url.StartsWith("http:") || url.StartsWith("https:")` — or just `url.Contains("://")` for any scheme.

---

### H2. `PRUtils.DownloadFile` resource-not-found path never fires the callback — coroutines hang

**File:** `Assets/_Story/Utils/PRUtils.cs:137-145`

```csharp
if (asset != null) {
    onComplete?.Invoke(asset.text);
} else {
    Debug.Log($"Error: Could not find local resource at {resourcePath}");
}
yield break;
```

When the resource is missing, the callback is never invoked. `PRScript.Reload()` (line 210) waits for this callback to call `parse()`. The user sees an empty `_Story` scene with no error message. Combined with H1, an accidental `https:` URL produces this exact hang.

**Fix:** Either call `onComplete?.Invoke("")` with an error marker, or surface a UI alert via `AlertDialogManager.Instance.ShowAlertDialog(...)`.

---

### H3. Memory leak: `UnityWebRequest` not disposed in `DownloadFile` and `DownloadImage`

**Files:**
- `Assets/_Story/Utils/PRUtils.cs:148` (DownloadFile)
- `Assets/_Story/Utils/PRUtils.cs:216` (DownloadImage)

```csharp
UnityWebRequest request = UnityWebRequest.Get(url);
request.certificateHandler = new AcceptAllCertificatesHandler();
yield return request.SendWebRequest();
// ... no using {} block, no request.Dispose()
```

Compare to `AudioAndTextPlayer.LoadAudioAndTimings` which correctly wraps requests in `using` (line 294, 321). The pattern in `PRUtils` leaks the request object, its download handler, and on `DownloadImage` the underlying Texture2D. Over hundreds of page turns on a phone, this is real memory pressure.

**Fix:** Wrap in `using (UnityWebRequest request = ...) { ... }`.

---

### H4. Audio fragment clips are created per-call and never destroyed — memory leak per page

**File:** `Assets/_Story/Players/AudioAndTextPlayer.cs:389-407`

```csharp
if (startTime > 0 || endTime < originalClip.length)
{
    ...
    AudioClip fragmentClip = AudioClip.Create("Fragment_" + originalClip.name, ...);
    fragmentClip.SetData(samples, 0);
    audioSource.clip = fragmentClip;          // ← assigned but never freed
}
```

`AudioClip.Create()` returns a new asset each time `PlayExt` is invoked with non-default `startTime`/`endTime`. When the next page assigns a new clip, the old fragment becomes garbage but is never explicitly destroyed via `AudioClip.DestroyImmediate` / `Object.Destroy`. Unity does eventually GC these but lazily, and on iOS native audio buffers can stay resident. For books that use `PlayAudioAndShowText` (i.e., `PRScript.cs:486` → `PlayExt`) with fragments, every page turn leaks one clip.

**Fix:** Before reassigning `audioSource.clip`, `if (audioSource.clip != null && audioSource.clip.name.StartsWith("Fragment_")) Destroy(audioSource.clip);`.

---

### H5. `ParseTimings` overwrites cached timings when `pageNum != -1`

**File:** `Assets/_Story/Players/AudioAndTextPlayer.cs:490-502`

```csharp
private void ParseTimings(AudioAndTextStruct audioAndTextStruct, int pageNum)
{
    if (pageNum != -1)
    {
        ...
        audioAndTextStruct.jsonNodeTimings = singleChunk;   // ← clobbers cache entry
    }
    ...
}
```

`ParseTimings` is called on **every** play, including cache hits. When the cached struct came from a previous `PlayExt(audioURL, ..., pageNum=2)` and the next call is `PlayExt(audioURL, ..., pageNum=5)`, the cached struct's `jsonNodeTimings` is rewritten with the new page's content. If a third call comes in later expecting page 2's content, it gets page 5's. The cache becomes order-dependent and effectively poisoned for any book that uses static-text mode with multiple pages sharing one audio file.

**Fix:** Only mutate a copy — clone the struct before overwriting, or key the cache by `(audioURL, pageNum)` instead of `audioURL`.

---

### H6. iOS pause/kill loses session stats — only `OnApplicationQuit` saves

**File:** `Assets/_Story/Story/Globals.cs:100-103`

```csharp
void OnApplicationQuit()
{
    UpdateGameStatistics();
}
```

On Android (and modern iOS), `OnApplicationQuit` is unreliable when the user kills the app via the recent-apps switcher. The session minutes computed in `UpdateGameStatistics` (line 132 — `(Time.time - gameStartTime) / 60f`) are simply lost. Also affects the "ask to rate" timing in `PRLibrary.LoadBooksWithRetry:100` since `g_openedStoriesCount` *is* saved but session-minutes aren't.

**Fix:** Also call `UpdateGameStatistics()` from `OnApplicationPause(true)` and `OnDisable()`.

---

### H7. `PastelLavender` color uses `alpha` as the blue channel — produces a muddy yellow, not lavender

**File:** `Assets/_Story/Utils/PRUtils.cs:42`

```csharp
{"Pastel Lavender", new Color(0.9019f, 0.7451f, alpha, alpha)}
//                                              ^^^^^ — should be the blue value (~0.94)
```

`alpha` is `0.35f` (line 32), so this color is `RGB(0.90, 0.74, 0.35)` — a dirty sand/yellow. Every other pastel passes explicit RGB. Wherever "Pastel Lavender" is selected (it's in `pastelColors` and reachable via `MapStringToPastelColor` hash-fallback), the user sees the wrong color.

**Fix:** `new Color(0.9019f, 0.7451f, 0.9412f, alpha)` (or your designer-chosen lavender blue value).

---

### H8. Multi-image gallery swipe traps the user on the last/first image

**File:** `Assets/_Story/Story/PRScript.cs:776-810`

```csharp
public void LeftSwipe(SwipeableObject swipeable) {
    if (swipeable.name.ToLower() == "gallery") {
        if (gallery._currentGalleryItemIndex == gallery._galleryItems.Count - 1)
            return;                                  // ← swallows the swipe
        if (gallery._galleryItems.Count > 1)
            gallery.DisplayNextItem();
        else
            NextStep();
    }
    ...
}
```

When the user has scrolled to the **last** image of a multi-image gallery and swipes left, the early-return swallows the input. The natural expectation is "swipe past the last image → advance to the next page." Same problem mirrored in `RightSwipe` on the first image. The user must reach for the Next/Prev button. The intended chain "swipe through gallery, then swipe to next page" is broken.

**Fix:** When at the edge, treat the swipe as a page turn instead of returning:

```csharp
if (gallery._currentGalleryItemIndex == gallery._galleryItems.Count - 1) {
    NextStep();                                      // fall through to page turn
    return;
}
```

---

### H9. Android auto-page-turn may hang forever — already documented

**File:** `Assets/_Story/Players/AudioAndTextPlayer.cs:429-433`

`while (audioSource.isPlaying)` has no escape if Android audio focus is lost (Unity's `AudioSource.isPlaying` is known to occasionally stay `true` after focus loss). Reliably matches the public Android review complaint about auto-page-turn not working. Detailed earlier — fix is to add an `expectedEnd = Time.time + clip.length + 0.5f` guard.

---

### H10. `NextStep` / `PrevStep` save book progress twice per page turn

**File:** `Assets/_Story/Story/PRScript.cs:669-690, 599-615, 648-667`

```csharp
public void NextStep() {
    SetCurrentStep(nCurrentStep + 1);   // ← saves to PlayerPrefs here
    ...
    ExecuteStep(nCurrentStep);          // ← calls SetCurrentStep(nCurrentStep) again
}
public void ExecuteStep(int index) {
    bool bStepChanged = SetCurrentStep(index);   // ← second save
    ...
}
public bool SetCurrentStep(int index) {
    ...
    Globals.g_prbook?.SetAndSaveCurrentPage(index);   // PlayerPrefs.SetInt
    if (index == _scriptlets.Count - 1)
        Globals.g_prbook.SetBookDone(1);              // PlayerPrefs.SetInt
}
```

So every page turn writes two `PlayerPrefs.SetInt`s for the page key (same value), and on the last page writes two `book_done` keys. `PlayerPrefs.SetInt` is cheap but the redundancy is a code smell pointing at a confused control flow. The variable `bStepChanged` is misnamed — `SetCurrentStep` returns `true` for any *valid* index, even when the index didn't change. The naming hides the double-call.

**Fix:** Have `ExecuteStep` accept the trust that `nCurrentStep` was already set, or have `SetCurrentStep` early-return when `index == nCurrentStep`.

---

## Medium

### M1. `WaitAndNavigate` ignores its `targetScene` parameter

**File:** `Assets/_Story/Story/Globals.cs:184-188`

```csharp
private IEnumerator WaitAndNavigate(string targetScene, float delay) {
    yield return new WaitForSeconds(delay);
    LoadTargetScene();           // ← reads this.targetScene, not the param
}
```

Currently harmless because `targetScene` is only ever passed in matching `this.targetScene`. But if someone later calls `WaitAndNavigate("_Bookstore", 0)` from elsewhere they get the wrong scene with no indication.

**Fix:** Remove the unused parameter or actually use it.

---

### M2. Two URL normalizers with names differing only by case

**File:** `Assets/_Story/Story/PRScript.cs:757-774`

`NormalizeURL(url)` prepends `baseURL` if not absolute. `NormalizeUrl(url)` (lowercase "rl") collapses `//`. The functions do completely different things. They're called interchangeably across `AddCharacter`, `CreateButton`, `AddAudio`, etc., and in `AddCharacter` (lines 250-257) `NormalizeURL(url)` is called **twice on the same value** because the author thought one of the calls was the other.

**Fix:** Rename to `ResolveAbsoluteURL` / `CollapseDoubleSlashes` and audit every call site.

---

### M3. `Globals.GotoPrBook` / `GotoBook` have identical if/else branches

**File:** `Assets/_Story/Story/Globals.cs:331-363`

```csharp
if (IsTablet())
    SceneManager.LoadScene("_Story");
else
    SceneManager.LoadScene("_Story");
```

Both branches load `_Story`. Either dead code or a TODO that was never completed (separate tablet/phone story scenes were probably planned). Either way, the conditional is meaningless and misleading to a maintainer.

**Fix:** Remove the conditional.

---

### M4. `Singleton` race: `Globals.Instance` can create a dummy GameObject

**File:** `Assets/_Story/Story/Globals.cs:49-74`

If any script's `Awake()` calls `Globals.Instance` **before** the real `Globals` GameObject's `Awake()` fires (Unity has no guarantee of order without `[DefaultExecutionOrder]`), the static getter creates a brand-new GameObject with default field values:

```csharp
GameObject go = new GameObject("Globals");
instance = go.AddComponent<Globals>();   // ← default csvUrl, no inspector values
```

Then the real `Globals`'s `Awake` fires, sees `instance != null`, and **destroys itself**. The "shadow" instance with no inspector values becomes the singleton. From that point, `csvUrl`, `targetScene`, `buttonLoadingRetryContinue`, etc. are all defaults/null. Catalog download still works because `csvUrl` has a field initializer, but `buttonLoadingRetryContinue` is null and `targetScene` is "_Library" (the field initializer), so it's mostly recoverable — but it's a fragile pattern that will break the first time someone relies on an Inspector-set field.

**Fix:** Either (a) make `Awake` *replace* `instance` rather than destroy `gameObject`, copying inspector fields over; or (b) use `[DefaultExecutionOrder(-100)]` on `Globals` to guarantee its `Awake` runs first; or (c) drop the auto-creating getter — require a Globals to exist in the scene.

---

### M5. `PRLibrary.FilterByGenre` uses `Equals` on a multi-tag string — never matches

**File:** `Assets/_Story/LIbrary/PRLibrary.cs:128-131`

```csharp
public static List<PRBook> FilterByGenre(string genre)
{
    return prbooks.FindAll(s => s.genre.ToLower().Equals(genre.ToLower()));
}
```

A book's `genre` is a colon-separated tag list like `"Rhymebooks : Family : Special Education : Manners"`. `Equals("family")` is always false. The function returns an empty list for every realistic input. The good news is `SetFilter()` (lines 159-237) doesn't call this — it only swaps the background image. So this is dead code, but its existence next to a working SetFilter is misleading.

**Fix:** `s.genre.ToLower().Contains(genre.ToLower())` — or delete the function.

---

### M6. `PRLibrary.LoadBooksWithRetry` doesn't actually retry the download

**File:** `Assets/_Story/LIbrary/PRLibrary.cs:68-88`

```csharp
while (Globals.g_listPRBooks == null && retryCount < maxRetries)
{
    yield return new WaitForSeconds(waitTime);
    retryCount++;
}
```

It only **polls** for `Globals.g_listPRBooks` to become non-null — it doesn't re-trigger `Globals.PreLoadBooks()`. If the original download from `_StartScene` failed, no further download is attempted; the user just gets a generic warning logged to console and a silent empty Library. The retry button in `Globals.DownloadCSV`'s error path is only visible on the *Start* scene, which is gone by the time `PRLibrary` runs.

**Fix:** On exhaustion, either call `Globals.Instance.PreLoadBooks()` to actually retry, or surface a UI message with a manual retry button.

---

### M7. `PRScript.SetupInterpreter()` re-creates the interpreter on every script execution

**File:** `Assets/_Story/Story/PRScript.cs:587-597`

```csharp
[Command]
void RunScript(string script)
{
    SetupInterpreter();        // ← every page turn, re-registers ~35 intrinsics
    _interpreter.Reset(script);
    _interpreter.Compile();
    ...
}
```

`SetupInterpreter()` (line 213) constructs a new `Interpreter` and calls `Intrinsic.Create("…")` for every intrinsic. Even if MiniScript is fine with re-registering, this is wasted work on every page turn and on the `OnExecuteStep` event (which fires once *per page* on top of the chunk itself — so 2× per page turn). The original interpreter is left to GC.

**Fix:** Build the interpreter once in `Start()` and just `Reset(script)` per execution.

---

### M8. Gallery navigation: `DisplayNextItem` wraps, `DisplayPreviousItem` clamps

**File:** `Assets/_Story/Story/Gallery.cs:135-151`

```csharp
public void DisplayNextItem() {
    _currentGalleryItemIndex++;
    if (_currentGalleryItemIndex > _galleryItems.Count - 1)
        _currentGalleryItemIndex = 0;            // wrap
    ...
}
public void DisplayPreviousItem() {
    _currentGalleryItemIndex--;
    if (_currentGalleryItemIndex < 0)
        _currentGalleryItemIndex = 0;            // clamp (different!)
    ...
}
```

Inconsistent. The PRScript swipe handlers (H8) guard against this by checking edges before calling, so the discrepancy is masked — but anyone wiring these to a different UI element will get asymmetric behavior.

**Fix:** Pick one. Wrap is more natural for a circular gallery; clamp is more natural for a linear one.

---

### M9. `RateUs()` parses iOS version assuming exactly two `.`-separated parts and US locale

**File:** `Assets/_Story/Utils/PRUtils.cs:346-373`

```csharp
float version = float.Parse(systemVersion.Split('.')[0] + "." +
                            UnityEngine.iOS.Device.systemVersion.Split('.')[1]);
```

Two problems:
1. If the iOS version string is ever single-part (`"19"`), `Split('.')[1]` throws `IndexOutOfRangeException`. No try/catch.
2. `float.Parse` with no `IFormatProvider` is culture-sensitive. In a locale where the decimal separator is `,`, the resulting `"14.5"` is rejected. The catch isn't there, so the whole `RateUs` flow crashes.

**Fix:** `float.Parse(..., System.Globalization.CultureInfo.InvariantCulture)` and a `Split(...)[i]` guard, or just do `int.TryParse(systemVersion.Split('.')[0], out var major) && major >= 11`.

---

## Low

### L1. `LoadAudioAndTimings` doesn't surface audio download errors to UI

`AudioAndTextPlayer.cs:298-302` only logs `Debug.LogError` on failed downloads. The user sees silence with the highlight running over text that wasn't supposed to play yet (because `audioSource.time` is 0 forever). Worth an `AlertDialogManager` or onscreen toast.

### L2. `ParseCSV`'s `int.Parse` is locale-sensitive

`Globals.cs:391-392` parses ages without an invariant culture. Currently safe because the values are simple integers, but the same issue as M9.

### L3. `SetCurrentStep` marks book "done" but never marks it "undone"

`PRScript.cs:656-660` calls `SetBookDone(1)` when the user reaches the last page, but if the user re-reads the same book from page 1, `book_done` stays at 1. The UI probably uses `book_done` to show a "✓" badge — once earned, never lost. Probably intentional but worth confirming.

### L4. `g_libraryFilter` not re-applied on app cold start

`Globals.cs:28` defaults to `"everything"`. On warm restart (foreground/background), the previous filter is preserved. On cold start, the filter resets even though `PlayerPrefs` could persist it. Small UX paper-cut.

### L5. `ConfigOutput()` is called from `SetupInterpreter()` but the alert dialog references `_interpreter.source`

`PRScript.cs:631-640`. If an error happens during `Compile()` (before `_interpreter.source` is set?), the dialog shows whatever was last set, which could be stale or empty. Edge case.

### L6. `DownloadImage` doesn't pass through `bPreserveAspect` to the fallback `NoImage` sprite

`PRUtils.cs:224` sets `image.sprite = Resources.Load<Sprite>("NoImage")` after the error, but `image.preserveAspect` was set per the parameter. If `NoImage` has a different aspect than the original it's stretched. Cosmetic.

### L7. `Globals.csvUrl` field has six "convenience" siblings — Inspector clutter

Eight strings (`csvUrl`, `CSVURL`, six `convinience*` variants) hold variations of the same URL. None used at runtime except `csvUrl`. Plus the misspelling "convinience". (Already noted in the improvements report — repeating because it confuses any new developer reading `Globals.cs`.)

### L8. Static `_alStoryPlates` is `ArrayList` instead of `List<StoryStepPlate>`

`StoryStepsUI.cs:34`. Loses type safety, forces casts at every access (line 76, 103). Trivial refactor.

### L9. `LeftSwipe` / `RightSwipe` log "LeftSwipe" identically — RightSwipe's log message is wrong

`PRScript.cs:794-796`:

```csharp
public void RightSwipe(SwipeableObject swipeable)
{
    Debug.Log("LeftSwipe " + swipeable.name);   // ← says LeftSwipe
```

Cosmetic but actively misleading when reading logs.

---

## Already documented elsewhere (for completeness)

The earlier overview / improvements report covered these:
- Duplicate IDs in `stories.csv` (id=38 and id=67 each appear twice).
- 23 orphan story folders being served by CloudFront.
- FileServer `WebSecurityConfig` fully commented out — credentials inert, upload form posts to wrong path.
- Stale `AudioAndTextPlayer_bak.cs` next to the live class.
- Unused Unity packages in `Packages/manifest.json` (Netcode, multiplayer, cloth, vehicles, terrain).

---

## Suggested fix order

| Priority | Item | Effort |
|---|---|---|
| 1 | **C1** — Remove `AcceptAllCertificatesHandler`. | 5 minutes |
| 2 | **H1 + H2** — Fix `DownloadFile` to accept `https:` and always invoke `onComplete`. | 15 minutes |
| 3 | **H9** — Add the Android-safe end-time guard to `AudioAndTextPlayer`'s while-loop. | 15 minutes |
| 4 | **H3 + H4** — Wrap web requests in `using`, destroy fragment audio clips. | 30 minutes |
| 5 | **H7** — Color bug in `PastelLavender`. | 1 minute |
| 6 | **H8** — Edge-of-gallery swipe should advance the page. | 15 minutes |
| 7 | **C2** — Quoted-field CSV parser + per-row try/catch. | 1 hour |
| 8 | **C3 + H5** — Fix cache LRU semantics and key-corruption in `ParseTimings`. | 30 minutes |
| 9 | **H10** — Stop double-saving page progress on every page turn. | 30 minutes |
| 10 | **H6** — Save stats on `OnApplicationPause(true)`. | 5 minutes |
| 11 | Tier-M items as a single cleanup PR. | half a day |

That sequence ships the security fix first, then the user-visible correctness fixes (Android auto-advance, gallery swipe, pastel color), then the data-integrity and reliability work.
