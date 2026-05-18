# ReadingBuddy — Bug Findings (Round 2)

*Second-round audit, focused on files that weren't reviewed in `READINGBUDDY_BUG_FINDINGS.md`. The first audit covered `AudioAndTextPlayer`, `PRScript`, `Globals`, `PRUtils`, `Gallery`, `StoryStepsUI`, `PRLibrary`, and `AcceptAllCertificatesHandler`. This pass covers everything else of consequence.*

Files newly audited:
- `Assets/_Story/GUI/PuzzleImage.cs` (~1068 lines)
- `Assets/_Story/GUI/ParentalGate.cs`
- `Assets/_Story/Players/AudioPlayer.cs`
- `Assets/_Story/Players/PRVideoPlayer.cs`
- `Assets/_Story/Players/TTS/MicrosoftTextToSpeech.cs`
- `Assets/_Story/Players/StreamingMic.cs`
- `Assets/_Story/Utils/SwipeDetector.cs`
- `Assets/_Story/LIbrary/BooksScrollView.cs`
- `Assets/_Story/Bookstore/PRBookstore.cs`

---

## Critical

### C-R2-1. `ParentalGate.CheckAnswerCounting` crashes the app on non-numeric input

**File:** `Assets/_Story/GUI/ParentalGate.cs:66`

```csharp
public void CheckAnswerCounting()
{
    int playerAnswer = int.Parse(answerInputField.text);  // ← FormatException on bad input
    ...
}
```

A child can type any keyboard character into the input field. The instant they press the check button after typing a letter (or leaving the field blank), `int.Parse` throws `FormatException` and bubbles up uncaught. Depending on Unity's exception handling settings the app either logs and ignores the click (best case) or crashes (worst case on builds with "Use Strict Exception Handling" set).

The **text-answer variant** (`CheckAnswerText`) uses string comparison and isn't vulnerable. So the impact depends on which check method is wired to the inspector's `checkOrNextButton.onClick` — if a story script ever switches the gate to the counting variant, you have a child-facing crash one keystroke away.

**Fix:**

```csharp
if (!int.TryParse(answerInputField.text, out int playerAnswer))
{
    answerInputField.text = "";
    if (checkOrNextButton != null)
        checkOrNextButton.GetComponentInChildren<TextMeshProUGUI>().text = "Try Again";
    return;
}
```

Same one-liner pattern as the C2 (Level 1) fix we did for the CSV parser — wrap risky parse in a try/catch or TryParse.

### C-R2-2. `ParentalGate.correctAnswer` is uninitialized in the counting variant

**File:** `Assets/_Story/GUI/ParentalGate.cs:23, 25-29, 56-62`

```csharp
private int correctAnswer;   // ← defaults to 0

void Start()
{
    GenerateQuestionText();   // ← does NOT call GenerateQuestionCounting()
    ...
}
```

If the active flow is the counting variant (the inspector wires `checkOrNextButton` to `CheckAnswerCounting` instead of `CheckAnswerText`), `correctAnswer` stays at `0` until the user clicks check — and `GenerateQuestionCounting` is only called *after* the first wrong check (line 81). So **before the first check, the correct answer is 0**, and typing `0` succeeds.

A child who tries `0` first — perfectly plausible for a small child mashing keys — bypasses the gate immediately.

**Fix:** Call `GenerateQuestionCounting()` from `Start()` if that's the active flow. Or, more cleanly: have one `GenerateQuestion()` method that picks based on an inspector enum, called from Start.

---

## High

### H-R2-1. `SwipeDetector` triggers a page turn for **every** SwipeableObject under the swipe

**File:** `Assets/_Story/Utils/SwipeDetector.cs:42-55`

```csharp
foreach (RaycastResult result in results)
{
    SwipeableObject swipeable = result.gameObject.GetComponent<SwipeableObject>();
    if (swipeable != null)
    {
        if (swipeDirection.x > 0)
            prScript.RightSwipe(swipeable);
        else
            prScript.LeftSwipe(swipeable);
    }
}
```

`EventSystem.RaycastAll` returns **all** UI hits under the pointer, including stacked elements. If the swipe lands on a spot where both the `textforeground` and the `gallery` are under the pointer (which can happen near layout boundaries, or if either is set up as full-screen), `PRScript.LeftSwipe` fires twice — once for each — and the user advances **two pages** with a single swipe.

This is especially likely to bite after the H8 fix that I shipped earlier: now the gallery's edge-swipe also calls `NextStep()`, so a swipe that hits both `gallery` and `textforeground` produces two `NextStep()` calls.

**Fix:** Break after the first match.

```csharp
foreach (RaycastResult result in results)
{
    SwipeableObject swipeable = result.gameObject.GetComponent<SwipeableObject>();
    if (swipeable == null) continue;

    if (swipeDirection.x > 0)
        prScript.RightSwipe(swipeable);
    else
        prScript.LeftSwipe(swipeable);
    break;   // ← only honor the topmost swipe target
}
```

### H-R2-2. `MicrosoftTextToSpeech` leaks UnityWebRequest, AudioClip, and AudioSource — every `Speak()` call adds three native handles

**File:** `Assets/_Story/Players/TTS/MicrosoftTextToSpeech.cs:36-43, 49, 17`

Three independent leaks in this one file:

```csharp
// Line 36-43 — request never disposed
UnityWebRequest www = new UnityWebRequest(apiUrl, "POST");
www.uploadHandler = new UploadHandlerRaw(postDataBytes);
www.downloadHandler = new DownloadHandlerBuffer();
...
yield return www.SendWebRequest();
// no Dispose, no using block — same H3-class issue we fixed in PRUtils

// Line 49 — AudioClip overwritten without destroying the prior one
audioClip = AudioClip.Create("TTS_AudioClip", audioData.Length / 2, 1, sampleRate, false);

// Line 17 — AudioSource added every Start(), not reused
audioSource = gameObject.AddComponent<AudioSource>();
```

The Audio Clip leak is the worst — each TTS utterance creates an AudioClip with `audioData.Length / 2 * 4` bytes of float samples retained until Unity's GC reclaims it (often never on iOS until app restart).

This file is only reached via the `Speak()` MiniScript intrinsic in `PRScript.cs:301`, so the impact depends on whether any active books call `Speak()`. Grep:

```bash
grep -l '\bSpeak(' /Users/alexanderfaisman/dev/FileServer/uploads/stories/*/
```

If any do, this leaks per page.

**Fix:** Wrap the request in `using`, destroy `audioClip` before reassigning (same pattern as H4), and gate the `AddComponent` with `GetComponent` first.

### H-R2-3. `MicrosoftTextToSpeech` injects untrusted text into SSML XML

**File:** `Assets/_Story/Players/TTS/MicrosoftTextToSpeech.cs:33`

```csharp
string postData = $"<speak version='1.0' xmlns='...'><voice name='{v}'><prosody rate='{r}'><prosody pitch='{p}'> "+ text + "</prosody></prosody></voice></speak>";
```

`text` is interpolated raw. If the story script's `Speak("Tom & Jerry")` call contains an `&` (or `<`, `>`, `"`), the SSML is malformed and Azure rejects the request — silently, with the user hearing nothing.

The current catalog is unlikely to hit this — books rarely have `&` in titles — but any future content with `&` in the displayed text will break TTS without warning.

**Fix:**

```csharp
string xmlText = System.Security.SecurityElement.Escape(text);
string postData = $"<speak ...> {xmlText} </prosody>...";
```

### H-R2-4. `AudioPlayer` uses deprecated `WWW` API and leaks subclips

**File:** `Assets/_Story/Players/AudioPlayer.cs:37, 100`

Two issues:

```csharp
// Line 37 — WWW class deprecated since Unity 2018
using (var www = new WWW(audioURL))

// Line 100 — MakeSubclip allocates new AudioClip on every play, never freed
AudioClip ac = AudioClipUtilities.MakeSubclip(audioClipStruct.audioClip, dFrom, dTo);
audioSource.clip = ac;
```

The `WWW` deprecation isn't broken today but Unity periodically removes deprecated APIs in major releases — this is a ticking compatibility issue. The subclip leak is the same shape as H4 from the previous audit (which we fixed in `AudioAndTextPlayer`), just in a parallel code path that wasn't touched.

**Fix:**
- Replace `new WWW(audioURL)` with `UnityWebRequestMultimedia.GetAudioClip(audioURL, AudioType.MPEG)` (already used elsewhere in this codebase, see `AudioAndTextPlayer.cs:294`).
- Before assigning `audioSource.clip = ac`, destroy the previous subclip if it was one (same `name.StartsWith("Fragment_")`-style guard, applied here to "subclip"-named clips).

### H-R2-5. `AudioPlayer` adds duplicates to the cache forever

**File:** `Assets/_Story/Players/AudioPlayer.cs:49`

```csharp
audioClipStructs.Add(audioClipStruct);   // always Add, never check for existing key
```

`LoadAudioClip` is called via the `AddAudio` MiniScript intrinsic. If a story script (or the same page replayed) calls `AddAudio` with the same name+URL, a duplicate entry is added each time. `PlayAudio` uses `.Find(x => x.audioClipName == name)` which returns the first match — so behavior is correct, but memory usage grows unbounded over a long reading session that revisits pages.

**Fix:**

```csharp
// In LoadAudioClip, before adding:
int existing = audioClipStructs.FindIndex(x => x.audioClipName == audioClipName);
if (existing >= 0)
    audioClipStructs[existing] = audioClipStruct;
else
    audioClipStructs.Add(audioClipStruct);
```

### H-R2-6. `StreamingMic.RecordingHandler` busy-waits on the main thread

**File:** `Assets/_Story/Players/StreamingMic.cs:97-99`

```csharp
m_acRecording = Microphone.Start(...);
while (!(Microphone.GetPosition(null) > 0))
{
    // ← empty body, no yield — main thread is blocked
}
yield return null;
```

If `StreamingMic.Start()` is ever called on a device where the mic permission hasn't been granted, or where the mic takes more than a few hundred ms to come up, this freezes the entire Unity main thread (UI unresponsive, audio glitches, no rendering). On Android in particular, the OS may kill the app for being unresponsive (ANR).

**Fix:**

```csharp
while (Microphone.GetPosition(null) <= 0)
    yield return null;   // or WaitForFixedUpdate
```

### H-R2-7. `StreamingMic.GetData(samples, 0)` always reads from sample 0

**File:** `Assets/_Story/Players/StreamingMic.cs:128`

```csharp
int diff = pos - lastSample;
...
samples = new float[nsamplesarray];
m_acRecording.GetData(samples, 0);    // ← always sample 0, ignoring lastSample
//m_acRecording.GetData(samples, lastSample);   // ← the correct version, commented out
```

The commented-out line is the right one. As written, the code allocates `diff`-sized buffer (where `diff` is "new samples since last read") but then reads from offset 0 of the circular buffer — which is the **oldest** data, not the newest. The whole vocalization-detection pipeline downstream is operating on stale samples.

This is signal-processing-broken, not Unity-broken. The mic code probably isn't used in the production reading app (it's likely leftover from a speech-therapy app), but if anyone re-enables it, the level/RMS readings will be wrong.

### H-R2-8. `StreamingMic` double-assigns `rmsLevel` with a different divisor

**File:** `Assets/_Story/Players/StreamingMic.cs:139-140`

```csharp
for (int i=2; i<nsamplesarray; i++){sum += filtered[i]*filtered[i];}
rmsLevel = Mathf.Sqrt(sum/(nsamplesarray-2));    // first version, divisor matches loop range
rmsLevel = Mathf.Sqrt(sum/nsamplesarray);        // ← overwrites with wrong divisor
```

The first assignment is correct (the loop sums from i=2 to nsamplesarray, so the divisor should be `nsamplesarray-2`). The second line overwrites it with the wrong divisor and is what actually gets used. Same caveat as H-R2-7 — only matters if the file is in the live path.

### H-R2-9. `StreamingMic.m_5buff` accumulates forever

**File:** `Assets/_Story/Players/StreamingMic.cs:186, 219`

```csharp
for(int i=NUM_POINTS-5; i<NUM_POINTS; i++){ m_5buff += prevLevels[i]; }
m_5buff /= 5;
```

`m_5buff` is a class field, never reset to 0 before the `+=` loop. So each call to `Threshold()` adds 5 more samples on top of whatever was already there, then divides by 5. The result climbs without bound.

**Fix:** `m_5buff = 0;` before each loop.

### H-R2-10. `BooksScrollView.ClearScrollView` doesn't actually clear

**File:** `Assets/_Story/LIbrary/BooksScrollView.cs:107-115`

```csharp
[Command()]
public void ClearScrollView()
{
    foreach (Transform child in scrollViewContent)
    {
        //Destroy(child.gameObject);   ← commented out
        child.gameObject.SetActive(false);
    }
}
```

It hides the items instead of destroying them. Combined with `AddBook` (line 63-79) which checks `if (prBook.bookViewItem != null)` and reuses the same item — this works as an intentional pool, *as long as the same books are in the catalog every time*. If the catalog is re-downloaded with different books (e.g., remote-config change), stale hidden items accumulate and consume memory forever. Also, ScrollRect layout calculations have to traverse all hidden children every frame.

**Fix:** Destroy the inactive items that aren't in the current prBooks list, or convert to a proper recycler. At minimum, comment-document the intent.

---

## Medium

### M-R2-1. `PuzzleImage._shuffleSeed` inspector field is dead code

**File:** `Assets/_Story/GUI/PuzzleImage.cs:152, 706`

```csharp
[SerializeField] private int _shuffleSeed = 0; // 0 = random each time
...
Shuffle(slots, Environment.TickCount);   // ← always uses TickCount, never _shuffleSeed
```

The inspector lets you set a seed for deterministic shuffling, but the actual call ignores it. If anyone tries to reproduce a specific shuffle for testing or for an Easter egg, the seed input has no effect.

**Fix:** `Shuffle(slots, _shuffleSeed != 0 ? _shuffleSeed : Environment.TickCount);`

### M-R2-2. `PRVideoPlayer.LoadVideo` doesn't handle absolute URLs

**File:** `Assets/_Story/Players/PRVideoPlayer.cs:20`

```csharp
videoPlayer.url = baseURL + url;   // ← always prepends baseURL
```

If a script ever passes `http://example.com/video.mp4` (a full URL), the result is `http://cdn.../http://example.com/video.mp4`, a 404. Compare to `PRScript.NormalizeURL` which checks `if (!url.StartsWith("http")) ...`.

**Fix:** Same conditional prepending as PRScript:

```csharp
videoPlayer.url = url.StartsWith("http") ? url : (baseURL + url);
```

### M-R2-3. `PRVideoPlayer.Stop()` leaves `isPlayingSegment` true

**File:** `Assets/_Story/Players/PRVideoPlayer.cs:90-95`

```csharp
public void Stop()
{
    if (videoPlayer == null) return;
    videoPlayer.Stop();
    // isPlayingSegment is not reset
}
```

After `Stop()`, `Update()` keeps checking `videoPlayer.time >= segmentEndTime`. Since the player is stopped, `time` is 0 and won't reach `segmentEndTime`, so nothing breaks visibly — but the state is logically inconsistent and a future change to the Update loop could surface the bug.

**Fix:**

```csharp
public void Stop()
{
    if (videoPlayer == null) return;
    videoPlayer.Stop();
    isPlayingSegment = false;
}
```

### M-R2-4. `PRBookstore.SetFilter(string)` is an empty stub

**File:** `Assets/_Story/Bookstore/PRBookstore.cs:142-145`

```csharp
public void SetFilter(string filter)
{
    return;
}
```

It's called twice — line 98 (after catalog loads, if a bookstore filter is set) and line 177 (when `GotoCategory` is called in the same scene). Both calls are no-ops. So `Globals.g_bookstoreFilter` is preserved across scenes but never actually does anything.

This is consistent with the audit's earlier finding that `PRLibrary.SetFilter` only changes the background image and doesn't filter the list either — the filter system is more cosmetic than functional. Worth either implementing or documenting the limitation.

### M-R2-5. `PRBookstore.FilterByGenre` repeats the same `Equals` bug from `PRLibrary`

**File:** `Assets/_Story/Bookstore/PRBookstore.cs:117-120`

```csharp
public static List<PRBook> FilterByGenre(string genre)
{
    return prbooks.FindAll(s => s.genre.ToLower().Equals(genre.ToLower()));
}
```

Same bug as in `PRLibrary.FilterByGenre` (which I flagged as M5 in the first audit). The genre field is a colon-separated tag list (e.g., `"Family : Adventure : Fairytales"`); `Equals` against a single tag never matches. Dead code, same fix: `Contains` instead of `Equals`, or delete it.

### M-R2-6. `BooksScrollView.ShowBooks` has a confused null check

**File:** `Assets/_Story/LIbrary/BooksScrollView.cs:98`

```csharp
if (this.filter != null && !filter.Conforms(prBook))
    continue;
```

The `this.filter != null` check guards against `this.filter` being null, but the next operand calls `filter.Conforms(prBook)` on the **parameter** (also named `filter`). The parameter shadows the member. If the parameter were null this would NPE, but the check is for a different `filter`. Either intentional confusion or a real bug — needs to be one consistent reference.

**Fix:** Rename one of them and pick the right check.

### M-R2-7. `StreamingMic.Microphone.devices` string concatenation prints `System.String[]`

**File:** `Assets/_Story/Players/StreamingMic.cs:95`

```csharp
Debug.Log("****StreamingMic devices: " + Microphone.devices);
```

`Microphone.devices` returns `string[]`. C# concatenation of an array with a string calls `Array.ToString()`, which returns `System.String[]` — not the device names. Trivial logging bug.

**Fix:** `string.Join(", ", Microphone.devices)`.

### M-R2-8. `StreamingMic` reinitializes the filter history every buffer

**File:** `Assets/_Story/Players/StreamingMic.cs:133-134`

```csharp
outputHistory[0] = inputHistory[0] = samples[0];
outputHistory[1] = inputHistory[1] = samples[1];
for (int i = 2; i < nsamplesarray; i++)
    filtered[i] = FilterButterworth(samples[i]);
```

A Butterworth IIR filter's history is supposed to *persist* across consecutive sample blocks so the filter's frequency response is continuous. Re-initializing it from `samples[0..1]` at the start of every buffer produces a transient at every buffer boundary — the filter is effectively "warming up" 100 times per second.

Same caveat: only matters if `StreamingMic` is in the live path. Worth checking whether it's wired into any active scene before fixing.

---

## Low

### L-R2-1. `ParentalGate.Navigate` doesn't hide the gate panel

**File:** `Assets/_Story/GUI/ParentalGate.cs:85-97`

```csharp
public void Navigate()
{
    if (sceneNavigateTo != "")
        SceneManager.LoadScene(sceneNavigateTo);
    else
        Application.OpenURL(url);
    //parentalGatePanel.SetActive(false);   ← commented out
}
```

After a successful gate, the panel stays visible. If the URL branch is used (opens browser/store), the user returns to the app to find the gate still up. They could re-submit (with no harm — they passed already) but it's confusing UX.

**Fix:** Uncomment the `SetActive(false)`.

### L-R2-2. `ParentalGate` answer check doesn't tolerate common variants

**File:** `Assets/_Story/GUI/ParentalGate.cs:39`

```csharp
if (playerAnswer.Trim() == answer1 || playerAnswer.Trim().ToLower() == answer2)
```

`answer1 = "25"`, `answer2 = "twenty five"`. A parent typing `"twenty-five"` (hyphenated), `"twentyfive"` (no space), or `"Twenty Five"` (title case, no `.ToLower()` mismatch — wait, the `ToLower()` is applied) — wait, the title-case actually works because of `.ToLower()`. But hyphen and no-space don't.

Minor. Parents reading the prompt usually type what they see.

### L-R2-3. `AudioPlayer.LoadAudioClip` exposed as `[Command]`

**File:** `Assets/_Story/Players/AudioPlayer.cs:30`

`[Command]` from QFSW Quantum Console exposes a method to the runtime debug console. `LoadAudioClip` taking strings is reasonable to expose — but it's also called internally from the MiniScript `AddAudio` intrinsic. Mixing public-debug-API with internal-API is risky if anyone runs `LoadAudioClip "name" "javascript:..."` from the console in a dev build.

### L-R2-4. `SwipeDetector.new PointerEventData(null)` warns on Unity 2022+

**File:** `Assets/_Story/Utils/SwipeDetector.cs:38`

Modern Unity emits a warning when `PointerEventData` is constructed with a `null` `EventSystem`. Should be `new PointerEventData(EventSystem.current)`.

### L-R2-5. `PuzzleImage.Resources.LoadAll` runs synchronously in OnEnable

**File:** `Assets/_Story/GUI/PuzzleImage.cs:336`

```csharp
if (_solvedSoundPool == null)
    _solvedSoundPool = Resources.LoadAll<AudioClip>("PuzzleSounds");
```

`Resources.LoadAll` is synchronous and blocks the main thread until all audio clips in that folder are loaded. Cached after the first call, so impact is one stall on the first puzzle activation. Minor.

---

## What I deliberately didn't audit this round

- `Assets/_Story/VAPI/` — the visual-animation layer (VSprite, SoundManager, World_Mesh, etc.). 11 files, ~2K lines. Used by the Map scene; not part of the core reading flow. Worth a separate dedicated pass if Map starts misbehaving.
- `Assets/_Story/drawing/IRV/bak/` — the `bak` suffix marks them as backup of a discontinued drawing canvas feature. Skipped.
- `Assets/_Story/Players/Runnable.cs` — generic singleton + coroutine runner. Looks like third-party utility code (IBM Watson SDK style).
- All Quantum Console / TextMesh Pro / DoTween / Febucci third-party files — out of scope.

---

## Suggested fix order

| Priority | Item | Effort |
|---|---|---|
| 1 | **C-R2-1** — `ParentalGate.CheckAnswerCounting` TryParse | 5 min |
| 2 | **C-R2-2** — Call `GenerateQuestionCounting` from `Start()` | 5 min |
| 3 | **H-R2-1** — `SwipeDetector` break after first hit | 5 min |
| 4 | **H-R2-2 + H-R2-3** — TTS resource leaks + SSML escape, if `Speak()` is actually used in any book | 30 min |
| 5 | **H-R2-4 + H-R2-5** — `AudioPlayer` modernization (WWW → UnityWebRequest, dedupe cache, destroy subclips) | 1 hr |
| 6 | **M-R2-2** — `PRVideoPlayer.LoadVideo` absolute-URL handling | 5 min |
| 7 | **M-R2-1** — Honor `_shuffleSeed` in `PuzzleImage` | 5 min |
| 8 | **L-R2-1** — `ParentalGate.Navigate` hide panel | 1 min |
| 9 | StreamingMic items (**H-R2-6/7/8/9, M-R2-7, M-R2-8**) — only if you re-enable mic features | half-day | 

`#1, #2, #3, #6, #8` are the same "simple and safe" tier as the fixes I shipped earlier — short diffs, no behavior risk. Worth doing as a single cleanup PR.
