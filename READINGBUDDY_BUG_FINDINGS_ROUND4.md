# ReadingBuddy — Bug Findings (Round 4)

*Fourth audit pass, covering the smaller UI/item components and a couple of unused-but-present files. Diminishing returns by file count, but two of the findings are real user-facing bugs worth knowing about.*

Files newly audited:
- `Assets/_Story/Bookstore/BookstoreScrollView.cs`
- `Assets/_Story/Bookstore/BookstoreViewItem.cs`
- `Assets/_Story/LIbrary/BookViewItem.cs`
- `Assets/_Story/Filters/MovingRatingsOptionsPanel.cs`
- `Assets/_Story/Filters/MovingVoiceOptionsPanel.cs`
- `Assets/_Story/Filters/FilterContainer.cs`
- `Assets/_Story/Story/SoundBar.cs`
- `Assets/_Story/Story/TitlePage.cs`
- `Assets/_Story/Settings/TextLoader.cs`
- `Assets/_Story/Players/AnimatedCircle.cs`
- `Assets/_Story/Players/VUMeter.cs`
- `Assets/_Story/Rooms/CombinedButton.cs`

---

## High

### H-R4-1. `CombinedButton` double-fires Button onClick

**File:** `Assets/_Story/Rooms/CombinedButton.cs:14-24`

```csharp
public void OnPointerDown(PointerEventData eventData)
{
    Debug.Log("Button Pressed!");
    if (button != null)
    {
        button.onClick.Invoke();   // ← fires onClick on press
    }
}
```

Unity's `Button` already fires `onClick` on **pointer up** (the standard click event). This script adds a `OnPointerDown` handler that *also* invokes `onClick` — on **press**. Every tap fires the click handler **twice**: once when the finger goes down, once when it comes up.

Effect depends on what the button does. If it's "Next page," the user advances two pages per tap. If it's "Settings → open scene," the scene loads twice (the second `LoadScene` cancels and reloads, so cosmetically OK but wasted work).

`CombinedButton` is in the `Rooms/` folder — used in `_Map` and `_Message` scenes per the project layout. If any of the navigation buttons there have this component attached, every Map button-press counts twice.

**Fix:** delete the `CombinedButton.cs` file entirely (the standard Unity Button already does what's needed), OR remove the `button.onClick.Invoke()` line, OR change the script to call something different on press vs release.

**Quick check:** grep for `<CombinedButton>` in scene files to find where it's wired:

```bash
grep -rln "CombinedButton" /Users/alexanderfaisman/dev/Play6.3/Assets/_Story/Rooms/
```

### H-R4-2. `VUMeter` busy-waits the main thread on mic startup

**File:** `Assets/_Story/Players/VUMeter.cs:34`

```csharp
while (!(Microphone.GetPosition(microphoneDevice) > 0)) { }
monitoringSource.Play();
```

Same shape as the `StreamingMic` H-R2-6 bug from round 2 — empty body, no yield. If the microphone takes more than a few hundred ms to produce its first sample (slow device, denied permission, no mic), this freezes the entire Unity main thread. On Android, the OS may issue an ANR (Application Not Responding) and kill the app.

`VUMeter` requires mic permission. If the user hasn't granted it, `Microphone.GetPosition` will keep returning `0` indefinitely → permanent hang.

The standard Unity reading-app flow doesn't seem to use VUMeter (likely a leftover from speech-therapy apps in the developer's portfolio), but the file is in the project. If anyone ever drops it into a scene, this hangs the build.

**Fix:** make `Start` a coroutine and yield each iteration:

```csharp
IEnumerator Start()
{
    ...
    while (!(Microphone.GetPosition(microphoneDevice) > 0))
        yield return null;
    monitoringSource.Play();
}
```

Or check `Microphone.IsRecording` plus a timeout to bail if mic isn't actually working.

### H-R4-3. `FilterContainer.OnToggleValueChanged` ignores the `isOn` parameter

**File:** `Assets/_Story/Filters/FilterContainer.cs:22-41`

```csharp
public void OnToggleValueChanged(bool isOn, Toggle toggle)
{
    FilterItem filterItem = toggle.GetComponent<FilterItem>();
    if (filterItem != null)
    {
        //if (isOn && currentFilter != filterItem.filter)
        if (currentFilter != filterItem.filter.ToLower())
        {
            ...
            prLibrary.SetFilter(filterItem.filter.ToLower());
        }
    }
    MoveOut();
}
```

The commented-out original logic checked **both** `isOn && currentFilter != filterItem.filter`. The replacement only checks the filter-difference part. Effect:

- Turning a toggle **on** → applies that filter. ✓
- Turning a toggle **off** → also applies that filter (instead of clearing). ✗

If your filter UI is a `ToggleGroup` where exactly one toggle is on at a time, this isn't visible (turning one off only happens when another comes on, which has its own ON handler). But on any UI flow that allows free toggle-off without toggle-on, the filter stays applied to whatever the user just deactivated.

**Fix:** restore the `isOn &&` check, or rephrase to match your actual ToggleGroup setup. If `requireAtLeastOne = true` is set on the toggle group, the bug doesn't manifest in practice; but the code is misleading.

---

## Medium

### M-R4-1. `BookViewItem.OnPointerClick` NREs on uninitialized item

**File:** `Assets/_Story/LIbrary/BookViewItem.cs:37-40`

```csharp
public void OnPointerClick(PointerEventData eventData)
{
    Globals.GotoPrBook(prBook);   // ← no null check
}
```

`Globals.GotoPrBook(null)` dereferences `prBook.bookFullUrl` immediately and NREs. If a `BookViewItem` GameObject is shown before `SetBookProperties` has been called (race during catalog load), and the user taps it, the app crashes.

In practice the catalog loads in `Start`, items are populated immediately, and the user can't tap before the layout settles — so this is unlikely to bite today. But on a slow Android device with a janky first-frame, it's possible.

**Fix:** `if (prBook != null) Globals.GotoPrBook(prBook);` — same pattern `BookstoreViewItem.OnPointerClick` already uses.

### M-R4-2. `BookViewItem` has a typo: `imageBaclground` (should be `imageBackground`)

**File:** `Assets/_Story/LIbrary/BookViewItem.cs:13`

```csharp
[SerializeField] public Image imageBaclground;
```

`[SerializeField] public Image imageBaclground;` — "Baclground" → "Background." Public Inspector-wired field. Renaming requires `[FormerlySerializedAs("imageBaclground")]` to preserve existing scene wiring.

Cosmetic but it's a permanent eyesore in the inspector.

### M-R4-3. `SoundBar` uses deprecated WWW API and has the same FIFO-not-LRU cache bug

**File:** `Assets/_Story/Story/SoundBar.cs:40, 32-35`

```csharp
using (var www = new WWW(audioURL))   // deprecated since Unity 2018
...
if (cacheAcudioStructs.Contains(audioURL))
{
    acudioStruct = cacheAcudioStructs[audioURL] as AudioStruct;
    // ← does NOT move to end, so cache is FIFO not LRU
}
```

Same issues we identified and fixed in `AudioAndTextPlayer.cs` / `PRUtils.cs` in round 1 (C3) and in `AudioPlayer.cs` in round 2 (H-R2-4). `SoundBar` was missed both rounds. The fix is the same shape:

```csharp
// LRU: re-insert on hit
acudioStruct = cacheAcudioStructs[audioURL] as AudioStruct;
cacheAcudioStructs.Remove(audioURL);
cacheAcudioStructs[audioURL] = acudioStruct;
```

And `using (var uwr = UnityWebRequestMultimedia.GetAudioClip(...))` instead of `new WWW(...)`.

### M-R4-4. `MovingRatingsOptionsPanel.MoveOut` uses string-based `Invoke`

**File:** `Assets/_Story/Filters/MovingRatingsOptionsPanel.cs:40` (also `MovingVoiceOptionsPanel.cs:47`, `FilterContainer.cs:92`)

```csharp
public void MoveOut()
{
    Invoke("_MoveOut", 0.25f);
}
```

`MonoBehaviour.Invoke(string, float)` uses reflection. If `_MoveOut` is renamed or removed, the call fails silently at runtime. (Unity's compile-time refactoring tools can't track this dependency.)

Use a lambda or direct method reference if possible. Or at minimum, mark the target with `[ContextMenu]` or document that it's reflection-called so a future refactor doesn't break it silently.

### M-R4-5. `TextLoader.LoadTextFromUrl` retries with zero delay

**File:** `Assets/_Story/Settings/TextLoader.cs:35-58`

```csharp
while (attempts < maxAttempts)
{
    using (UnityWebRequest webRequest = UnityWebRequest.Get(textUrl))
    {
        yield return webRequest.SendWebRequest();
        if (request.result == Success) ...
        else attempts++;   // ← no delay before next retry
    }
}
```

On a quick DNS failure or connection refused, all three attempts complete in well under a second — burning the retry budget on the same transient error. Add a small back-off:

```csharp
attempts++;
yield return new WaitForSeconds(Mathf.Pow(2, attempts));   // 2s, 4s, 8s
```

### M-R4-6. `TitlePage.Update` and `Start` are empty stubs

**File:** `Assets/_Story/Story/TitlePage.cs:14-23`

```csharp
void Start() { }
void Update() { }
```

Same L-R3-2-class issue from round 3. Empty `Update()` costs a per-frame managed call. Delete.

### M-R4-7. `BookstoreViewItem` brace indentation is misaligned

**File:** `Assets/_Story/Bookstore/BookstoreViewItem.cs:40-52, 85-91`

Two regions with closing braces at wrong indent levels:

```csharp
        if (btnPrinted != null)
        {
            ...
            if (hasPrintedUrl)
            {
                btnPrinted.onClick.AddListener(...);
            }
    }                                   // ← outer if closes here, looks misaligned
```

```csharp
    public void OnPointerClick(PointerEventData eventData)
    {
        if (prBook != null)
        {
        Globals.GotoPrBook(prBook);     // ← inner block dedented
    }
    }
```

Syntactically valid — the braces match up correctly — but the indentation is misleading and makes the code review painful. Rider's "Reformat Code" would fix it in one keystroke.

---

## Low

### L-R4-1. `AnimatedCircle.cs` contains a class named `VectorDrawing`

**File:** `Assets/_Story/Players/AnimatedCircle.cs:3`

```csharp
public class VectorDrawing : MonoBehaviour
```

Filename says `AnimatedCircle`, class says `VectorDrawing`. Unity allows this but it's confusing. Either rename the class to `AnimatedCircle` or rename the file. This script also has hardcoded `numberOfLines = 500`, `lineMinLength`, `lineMaxLength` parameters with no inspector exposure — looks like a debug visualization that someone left in.

### L-R4-2. `BookstoreScrollView` has the same `this.filter != null` confusion as `BooksScrollView`

**File:** `Assets/_Story/Bookstore/BookstoreScrollView.cs:67`

```csharp
if (this.filter != null && !filter.Conforms(prBook))
```

Identical to the issue I called out in round 2 (M-R2-6) — the `this.filter != null` check guards the field but the next operand calls `filter.Conforms(prBook)` on the parameter (also named `filter`). Cosmetic but confusing.

### L-R4-3. `BookViewItem.txtBookName.color` hardcoded to a specific RGBA

**File:** `Assets/_Story/LIbrary/BookViewItem.cs:32`

```csharp
txtBookName.color = new Color(0.4f, 0.15f, 0.15f, 1f);
```

The pastel-background color is computed dynamically (line 29: `PRUtils.GetNthPastelColor(prBook.number)`), but the text color is a fixed brown regardless of background. For some backgrounds this may have poor contrast (e.g., pastel pink with brown text is OK, but pastel mint with brown text is muddy). The commented-out line 31 (`PRUtils.DarkenColorByPercentage(opppositeColor, 0.4f)`) was the intended dynamic version.

Either re-enable the dynamic text-color line or pick a more universally-readable text color. Accessibility consideration for the special-education audience the app targets.

### L-R4-4. `BookViewItem.imageBaclground` typo (same as M-R4-2)

See M-R4-2. Tier-promoting because field is `public` and Inspector-bound.

### L-R4-5. `TitlePage.SetTitlePage` doesn't null-check `parentalGate`

**File:** `Assets/_Story/Story/TitlePage.cs:34`

```csharp
parentalGate.url = link;   // NRE if parentalGate is null
```

If the inspector field isn't wired, NRE on every title-page setup. Pre-existing pattern across the codebase — many SerializeFields are dereferenced without null guard, all dependent on the scene being correctly configured.

### L-R4-6. `MovingRatingsOptionsPanel.OnSomethingClickedHandler` is empty + dead `OnDestroy`

**File:** `Assets/_Story/Filters/MovingRatingsOptionsPanel.cs:20-27`

```csharp
private void OnSomethingClickedHandler(string selectedValue)
{
    //MoveOut();
}

private void OnDestroy()
{
}
```

Empty method body — but unlike `MovingVoiceOptionsPanel`, this version is never registered as a listener anywhere. Dead code. Delete.

---

## What I deliberately didn't audit this round

- **`Assets/_Story/VAPI/*`** — still untouched. Used by the Map/Rooms scenes. Would need a dedicated pass.
- **`Assets/_Story/GUI/BlinkingOutlineHint.cs`, `ButtonRangeGroup.cs`, `ButtonRangedItem.cs`, `ConvenientButton.cs`, `PRCharButton.cs`, `PRImage.cs`, `PRTitle.cs`, `ToggleView.cs`** — mostly look like small UI helpers, no obvious red flags but not exhaustively read.
- **`Assets/_Story/drawing/`** — the IRV/bak files are deprecated per the directory name.
- **`Assets/_Story/Utils/SimpleJSON.cs`** — third-party JSON library.

---

## Confirmed non-bugs I noticed

- **`MovingVoiceOptionsPanel.OnDestroy` correctly removes its listener** (line 31-37). Compare to `MovingRatingsOptionsPanel` which has an empty OnDestroy because it never registered one. Both are correct relative to their own setup.

- **`BookstoreViewItem` brace misalignment** (M-R4-7) is genuinely just indentation — the code compiles and runs correctly. I flagged it because it's confusing during code review, not because it's wrong.

---

## Suggested fix order

| Priority | Item | Effort |
|---|---|---|
| 1 | **H-R4-1** — `CombinedButton` double-fire | check usage, then either delete file or remove `Invoke` | 5 min |
| 2 | **M-R4-1** — `BookViewItem.OnPointerClick` null guard | 1 line | 1 min |
| 3 | **M-R4-7** — `BookstoreViewItem` reformat (Rider "Reformat Code") | 30 sec |
| 4 | **M-R4-6** — `TitlePage` remove empty Start/Update | 2 min |
| 5 | **H-R4-3** — `FilterContainer.OnToggleValueChanged` restore `isOn` check | 1 min, but verify against your actual toggle setup first |
| 6 | **M-R4-3** — `SoundBar` LRU fix (modeled on round 1 C3) | 5 min |
| 7 | **L-R4-6** — `MovingRatingsOptionsPanel` delete dead empty methods | 1 min |
| 8 | **L-R4-3** — `BookViewItem` text-color readability | design call |
| 9 | **L-R4-1, L-R4-4** — rename class / fix typo with `[FormerlySerializedAs]` | 5 min combined |
| 10 | **H-R4-2** — `VUMeter` busy-wait → coroutine yield | only if you ever use VUMeter |

Items 1, 2, 4, 5, 7 are the "simple and safe" tier this round. Combined diff under 30 lines across 5 files.
