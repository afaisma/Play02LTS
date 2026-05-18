# ReadingBuddy — Bug Findings (Round 3)

*Third audit pass, covering the small/medium files that weren't reviewed in the previous two rounds. Most of what's left in the codebase is short and self-contained, so this round produced a fewer-but-more-concrete batch of findings.*

Files newly audited:
- `Assets/_Story/Story/AutoplayToggle.cs`
- `Assets/_Story/Story/ButtonController.cs`
- `Assets/_Story/Story/ButtonSelectionController.cs`
- `Assets/_Story/Story/RateTheApp.cs`
- `Assets/_Story/Story/RandomMessageGenerator.cs`
- `Assets/_Story/Settings/SettingsScene.cs`
- `Assets/_Story/Utils/Alerts/AlertDialogManager.cs`
- `Assets/_Story/Utils/NetworkStatus.cs`
- `Assets/_Story/Utils/AudioClipUtilities.cs`

---

## High

### H-R3-1. `AutoplayToggle`: "checked" and "unchecked" colors are visually identical

**File:** `Assets/_Story/Story/AutoplayToggle.cs:13-14`

```csharp
public Color uncheckedColor = new Color(255, 255, 255, 128);
public Color checkedColor   = new Color(100, 255, 100, 128);
```

`UnityEngine.Color` takes float components in the **0.0–1.0** range, not 0–255. Values greater than 1 are stored as-is but rendered clamped to 1. The result:

- `uncheckedColor` renders as **opaque white** (1, 1, 1, 1).
- `checkedColor` renders as **opaque white** (1, 1, 1, 1).

Both states look the same. The "Autopage On / Off" *label* changes (line 38), so users get textual feedback, but the visual color cue does nothing. The cyan-tinted button image (`buttonNext.image.color`, line 34) is also affected.

This very plausibly matches the "I couldn't find the auto-page-turn toggle" complaint in the Android review history — the toggle is visually flat between states.

**Fix:** Use `Color32` (which takes byte 0–255) or normalize to floats.

```csharp
public Color uncheckedColor = new Color32(255, 255, 255, 128);
public Color checkedColor   = new Color32(100, 255, 100, 128);
```

Trivial diff, real user-visible improvement.

### H-R3-2. `NetworkStatus`: condition is commented out, so a request fires every 5 seconds forever

**File:** `Assets/_Story/Utils/NetworkStatus.cs:53-64`

```csharp
private IEnumerator CheckInternetConnection()
{
    while (true)
    {
        //if (lastReachability != Application.internetReachability)
        {
            StartCoroutine(TryAgain());
        }

        yield return new WaitForSeconds(checkFrequency);
    }
}
```

The condition is commented out — what's left is an unconditional code block that fires `TryAgain()` **every 5 seconds for the lifetime of the GameObject**. `TryAgain()` performs a `UnityWebRequest.Get("http://www.google.com")` to test connectivity.

Compounding issue: `StartCoroutine(TryAgain())` is called each time without waiting. If the previous probe hasn't finished (slow network), a new one fires alongside it. On a flaky connection, you can have a dozen concurrent probes piling up.

Also: the probe URL is **HTTP** (cleartext), which means on Android 9+ (any modern emulator or device) it will fail regardless of actual connectivity — making the connectivity-indicator permanently misleading.

**Fix:**

1. Restore the condition: `if (lastReachability != Application.internetReachability)`, and also gate by elapsed time / a "currently probing" flag to prevent concurrent probes.
2. Change the probe URL to `https://www.google.com` (or, better, to your own CDN's connectivity endpoint).

### H-R3-3. `ButtonSelectionController` logs current selection BEFORE setting it — NRE risk on first call

**File:** `Assets/_Story/Story/ButtonSelectionController.cs:57-58`

```csharp
private void OnButtonClicked(Button button, string buttonName)
{
    Debug.Log("Button clicked: " + button.name);
    OnSomethingClicked?.Invoke(buttonName);

    // if (_currentlySelected == button) ...
    Debug.Log("***2222 currently selected button: " + _currentlySelected.name);   // ← NRE if null
    _currentlySelected = button;
    ...
}
```

`_currentlySelected.name` is read before `_currentlySelected = button` is assigned. On the very first invocation, `_currentlySelected` is `null`, and `.name` NREs.

In normal flow, the first call to `OnButtonClicked` comes from `ButtonOptions` (line 106), which sets `_currentlySelected = buttons[firstVisibleButton]` immediately before calling `OnButtonClicked` (line 105–106). So the field is non-null by the time we hit line 57 — *if* the only entry point is `ButtonOptions`.

But the user can also click a button directly (the `onClick.AddListener` in `Start`, line 28). If a user manages to click a voice-mode button BEFORE `VoiceOptions(...)` has been called from `PRScript.Start` — for instance, if a script error in `PRScript` prevents `VoiceOptions` from firing — the first direct click triggers an NRE in the Debug.Log call.

This is a latent bug, but easy to fix:

```csharp
Debug.Log("***2222 currently selected button: " + (_currentlySelected != null ? _currentlySelected.name : "<none>"));
```

Or, more honestly, drop the debug log — it doesn't carry useful information once selection is established.

### H-R3-4. `ButtonController.DisableButtonsForTime` allows overlapping enable timers

**File:** `Assets/_Story/Story/ButtonController.cs:22-34`

```csharp
public void DisableButtonsForTime(float timeInSec)
{
    StartCoroutine(DisableButtonsCoroutine(timeInSec));
}

private IEnumerator DisableButtonsCoroutine(float timeInSec)
{
    DisableButtons();
    yield return new WaitForSeconds(timeInSec);
    EnableButtons();
}
```

`PRScript.NextStep` / `PrevStep` / `ReplayCurrenStep` each call `DisableButtonsForTime(1f)` to prevent rapid re-tapping. But this method has no de-duplication — each call starts a fresh coroutine. If something does trigger two calls within a second (e.g., two SwipeableObjects hit by a swipe — which I fixed in H-R2-1, or any future code path that double-fires), then:

- Coroutine A starts at t=0, disables buttons, waits 1s, re-enables at t=1.
- Coroutine B starts at t=0.3, disables buttons (already disabled), waits 1s, re-enables at t=1.3.
- BUT coroutine A's re-enable at t=1 already runs first, so buttons are re-enabled at t=1, before B's intended 1.3 deadline.

Worse case: if `EnableButtons()` and `DisableButtons()` interleave through multiple coroutines, the final state is non-deterministic depending on coroutine ordering.

**Fix:** track an `_endTime` and only re-enable when `Time.time >= _endTime`, or stop the prior coroutine before starting a new one:

```csharp
private Coroutine _disableCoroutine;

public void DisableButtonsForTime(float timeInSec)
{
    if (_disableCoroutine != null) StopCoroutine(_disableCoroutine);
    _disableCoroutine = StartCoroutine(DisableButtonsCoroutine(timeInSec));
}

private IEnumerator DisableButtonsCoroutine(float timeInSec)
{
    DisableButtons();
    yield return new WaitForSeconds(timeInSec);
    EnableButtons();
    _disableCoroutine = null;
}
```

---

## Medium

### M-R3-1. `RateTheApp.RateNow` for low ratings just dismisses the panel — no feedback flow

**File:** `Assets/_Story/Story/RateTheApp.cs:60-64`

```csharp
else
{
    movingRatingsOptionsPanel.MoveOut();
    // panelEmailUs.SetActive(true);
}
```

If a user gives 1–3 stars and taps Rate Now, the panel closes silently. The `panelEmailUs.SetActive(true)` line — which would route a dissatisfied user to a feedback form — is commented out.

The "low ratings get a feedback form, high ratings go to the store" pattern is standard for app rating prompts because it filters bad reviews away from the store. Right now, the negative-feedback path is dead.

**Fix:** Uncomment the line, or implement the actual "what didn't work?" UI you intended.

### M-R3-2. `AudioClipUtilities.MakeSubclip` rejects `stop == clip.length`

**File:** `Assets/_Story/Utils/AudioClipUtilities.cs:13`

```csharp
if (start < 0 || start >= clip.length || stop < 0 || stop >= clip.length || start >= stop)
{
    Debug.LogError("Invalid start or stop time.");
    return null;
}
```

The check `stop >= clip.length` rejects the case where the caller wants to include the very last sample. A subclip from 0 to clip.length is a legitimate "play the entire clip via the subclip API" request, and right now it errors out.

**Fix:** `stop > clip.length` instead of `>=`.

### M-R3-3. `AudioClipUtilities.MakeSubclip` allocates the full clip into memory

**File:** `Assets/_Story/Utils/AudioClipUtilities.cs:23-27`

```csharp
float[] originalData = new float[clip.samples * clip.channels];
clip.GetData(originalData, 0);

float[] subclipData = new float[samplesLength * clip.channels];
System.Array.Copy(originalData, samplesStart * clip.channels, subclipData, 0, samplesLength * clip.channels);
```

To extract a 1-second window from a 60-second 44 kHz stereo clip, the code allocates **5.3 MB** for `originalData` just to copy 88 KB out of it. The allocation happens every call. Over a long reading session, this is real GC pressure.

`AudioClip.GetData` accepts an offset — read just the window directly:

```csharp
float[] subclipData = new float[samplesLength * clip.channels];
clip.GetData(subclipData, samplesStart);
```

Two lines instead of four, no large allocation.

### M-R3-4. `NetworkStatus.OnTryAgainClickede` has a typo in the method name

**File:** `Assets/_Story/Utils/NetworkStatus.cs:81`

```csharp
public void OnTryAgainClickede()
{
    ShowDialog(false);
    StartCoroutine(TryAgain());
}
```

"Clickede" instead of "Clicked." The method is presumably wired in the Inspector's `OnClick` event, so renaming would break the wiring without an extra step. Either rename and re-wire, or leave it and document.

### M-R3-5. `SettingsScene` writes PlayerPrefs without `PlayerPrefs.Save()`

**File:** `Assets/_Story/Settings/SettingsScene.cs:94, 104, 110`

```csharp
PlayerPrefs.SetString("g_Rate", rate);
PlayerPrefs.SetInt("g_bSetReadingSpeedByBooksAgeGroup", 1);
PlayerPrefs.SetInt("g_bSetReadingSpeedByBooksAgeGroup", 0);
```

Unity persists `PlayerPrefs` opportunistically — on app pause, scene change, or quit. Without an explicit `PlayerPrefs.Save()`, settings tweaked just before a crash (or a force-kill from the recents switcher on Android) are lost. After the H6 fix from round 1, `OnApplicationPause(true)` does call `UpdateGameStatistics` which writes a few keys but doesn't call `Save()` either — so the same vulnerability applies.

**Fix:** Call `PlayerPrefs.Save()` at the end of each handler that mutates settings, OR add a single `PlayerPrefs.Save()` call to `Globals.OnApplicationPause`. The latter is one line and protects everything.

### M-R3-6. `AlertDialogManager` singleton drops `Instance` to a destroyed reference between scenes

**File:** `Assets/_Story/Utils/Alerts/AlertDialogManager.cs:11-22`

```csharp
private void Awake()
{
    if (Instance == null)
    {
        Instance = this;
        //DontDestroyOnLoad(gameObject);   ← commented out
    }
    else
    {
        //Destroy(gameObject);             ← also commented out
    }
}
```

With both lines commented out, the manager is **not** persistent. When a scene unloads, the GameObject is destroyed but the static `Instance` field still references it. Unity's overloaded `==` makes `Instance == null` *appear* true for destroyed objects, but other access patterns (`Instance.ShowAlertDialog(...)` without checking) will work or fail unpredictably depending on whether the new scene's `Awake` has fired first.

Typical pattern: scripts that fire alerts call `AlertDialogManager.Instance.ShowAlertDialog(...)` from their `Start` or later — by which point the new scene's `AlertDialogManager.Awake` has run. So in practice this is OK. But the singleton story is fragile.

**Fix:** Either uncomment the `DontDestroyOnLoad(gameObject)` line (and adjust the duplicate-destroy logic to match), or document explicitly that each scene needs its own `AlertDialogManager` instance.

---

## Low

### L-R3-1. `AutoplayToggle.cs` has a duplicate `using UnityEngine;`

**File:** `Assets/_Story/Story/AutoplayToggle.cs:3-4`

```csharp
using UnityEngine;
using UnityEngine;
```

Harmless. Remove one.

### L-R3-2. `SettingsScene` doesn't handle `Update` (empty stub)

**File:** `Assets/_Story/Settings/SettingsScene.cs:59-62`

Empty `Update()`. Costs a per-frame native→managed call. Delete it.

### L-R3-3. `RateTheApp.RateApplication` toggles all stars on/off per click — fine, but the star sub-objects are addressed via `transform.GetEnumerator()`

**File:** `Assets/_Story/Story/RateTheApp.cs:28-44`

```csharp
for (int i = 0; i < rateValue; i++)
{
    foreach (Transform t in starButton[i].transform)
    {
        t.gameObject.SetActive(true);
    }
}
```

This iterates **all children** of each star button. If the button has any extra children that aren't the "filled" star icon (e.g., a TextMeshProUGUI label), it'll get SetActive(true) along with the star image. Likely fine given the prefab structure, but it's an implicit dependency on the prefab having only the star-icon child.

### L-R3-4. `NetworkStatus.checkFrequency` is publicly editable but not validated

**File:** `Assets/_Story/Utils/NetworkStatus.cs:9`

If someone sets `checkFrequency = 0` in the inspector, `WaitForSeconds(0)` runs once per frame — 60+ probes per second. Not a real risk since no one would set 0 intentionally, but a `Mathf.Max(0.5f, checkFrequency)` guard would be cheap insurance.

### L-R3-5. `RandomMessageGenerator` arrays are `private` but commented as "editable in the Unity Inspector"

**File:** `Assets/_Story/Story/RandomMessageGenerator.cs:11`

```csharp
// Arrays to hold the strings, now editable in the Unity Inspector with default values
private string[] purposeArray = new string[]
```

Comment says editable in inspector, but the field is `private` without `[SerializeField]` — so Unity won't show it. Either drop the comment or add `[SerializeField]`.

### L-R3-6. `ButtonSelectionController.OnDestroy` may NRE if `buttons` is null

**File:** `Assets/_Story/Story/ButtonSelectionController.cs:33-39`

```csharp
private void OnDestroy()
{
    foreach (Button button in buttons)   // ← NRE if buttons is null
    {
        button.onClick.RemoveAllListeners();
    }
}
```

Start has the null guard at line 19; OnDestroy doesn't. If a GameObject with this script is destroyed before its inspector was wired up, OnDestroy NREs. Edge case but free fix: `if (buttons != null)`.

---

## Confirmed non-bugs (things that looked suspicious but check out)

- **`SettingsScene` switches on `slider.value` (a float)** — C# 8 constant patterns support this, and Unity's slider with `wholeNumbers = true` always gives integer-valued floats. Compiles and works correctly.

- **`AlertDialogManager.DestroyDialogInstance` calls `Destroy` on possibly-already-destroyed objects** — Unity's overloaded `==` operator makes the `if (alertDialogInstance != null)` check correctly treat destroyed objects as null, so this is fine.

- **`PuzzleImage._shuffleSeed`** — was a bug in round 2, now fixed.

---

## Where the next pass should go

Files still unaudited that might have impactful bugs:
- **`Assets/_Story/VAPI/*`** — visual animation layer used by Map scene. ~2K lines across 11 files. If Map starts misbehaving, audit here.
- **`Assets/_Story/Filters/MovingRatingsOptionsPanel.cs`, `MovingVoiceOptionsPanel.cs`** — the slide-in panels for rating and voice selection. Small, would take 10 minutes.
- **`Assets/_Story/Story/Art/`, `Assets/_Story/GUI/`** — Art and GUI helper components (`BlinkingOutlineHint`, `ButtonRangeGroup`, `ConvenientButton`, etc.). Some are deeply buried.
- **`Assets/_Story/Players/AnimatedCircle.cs`, `VUMeter.cs`, `AspectRatioFitter.cs`** — small UI animation helpers.

Each is small enough that a dedicated 30-minute pass would clear it.

---

## Suggested fix order

| Priority | Item | Effort |
|---|---|---|
| 1 | **H-R3-1** — `AutoplayToggle` `Color32` fix | 2 min |
| 2 | **H-R3-2** — `NetworkStatus` restore the condition + dedupe | 10 min |
| 3 | **H-R3-3** — `ButtonSelectionController` null guard on debug log | 1 min |
| 4 | **M-R3-1** — `RateTheApp` low-rating feedback path (uncomment or delete) | 2 min |
| 5 | **M-R3-2** — `AudioClipUtilities` `stop > clip.length` | 1 min |
| 6 | **M-R3-3** — `AudioClipUtilities` avoid full-clip allocation | 5 min |
| 7 | **H-R3-4** — `ButtonController` deduplicate disable coroutines | 10 min |
| 8 | **M-R3-5** — `Globals.OnApplicationPause` add `PlayerPrefs.Save()` | 1 min |
| 9 | **L-R3-1, L-R3-2, L-R3-6** — tiny cleanups | 5 min combined |

Items 1, 3, 4, 5, 8, plus the L items are sub-5-line diffs with no plausible regression — the same "simple and safe" tier as the previous rounds. Want me to apply them?
