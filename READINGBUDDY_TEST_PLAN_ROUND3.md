# ReadingBuddy — Test Plan for Round-3 Fixes (7 changes)

*Companion to `READINGBUDDY_BUG_FINDINGS_ROUND3.md`. Each fix has a code check, a manual test, and a sign-off line.*

---

## Summary of what changed

| ID | Bug | File | Diff |
|---|---|---|---|
| **H-R3-1** | Autopage toggle "on"/"off" colors rendered identically (white) | `AutoplayToggle.cs` | `Color(...)` → `Color32(...)` |
| **L-R3-1** | Duplicate `using UnityEngine;` line | `AutoplayToggle.cs` | removed |
| **H-R3-3** | NRE on Debug.Log of `_currentlySelected.name` before it's set | `ButtonSelectionController.cs` | ternary null-guard |
| **L-R3-6** | `OnDestroy` NRE if `buttons` is null | `ButtonSelectionController.cs` | early-return + per-element null check |
| **M-R3-2** | `MakeSubclip` rejected `stop == clip.length` | `AudioClipUtilities.cs` | `>` instead of `>=` |
| **M-R3-3** | `MakeSubclip` allocated entire clip into memory per call | `AudioClipUtilities.cs` | direct `GetData` at offset |
| **M-R3-5** | `PlayerPrefs.Save()` never called → settings can be lost on Android force-kill | `Globals.cs` | added in `OnApplicationPause` |
| **L-R3-2** | Empty `Update()` cost a per-frame native→managed call | `SettingsScene.cs` | removed |

7 fixes (M-R3-2 and M-R3-3 share a file), 5 files touched, all sub-15-line diffs.

---

## Pre-flight

1. Open the project in Unity 6.
2. Confirm it compiles. Console should be clean.

---

## Per-fix tests

### H-R3-1 — Autopage toggle visually distinguishes on/off

**Code check:**
```
grep -n "Color32" Assets/_Story/Story/AutoplayToggle.cs
```
Expected: two hits (`uncheckedColor` and `checkedColor`).

**Manual test:**

Before testing in the running app, check the Inspector for the `AutoplayToggle` component (find the GameObject in the `_Story` scene that hosts it):

- **If `Unchecked Color` and `Checked Color` in the Inspector are already at sensible normalized values** (e.g. `(1, 1, 1, 0.5)` and `(0.39, 1, 0.39, 0.5)`) — the Inspector overrides the code default. The fix doesn't change runtime appearance; it just corrects what `Right-Click → Reset` produces.
- **If they're at `(255, 255, 255, 128)` / `(100, 255, 100, 128)`** — the buggy defaults are in use, the bug is live, and the fix will visibly correct the toggle.

To force the code defaults: right-click each Color field → Reset.

Now run the app:

1. Open any book.
2. Tap the Autopage toggle off and on a few times.

**Expected after H-R3-1:**
- Off state: white-ish translucent background.
- On state: soft green translucent background.

The label still flips between "Autopage Off" and "Autopage On" (unchanged).

**Before H-R3-1:** both states rendered as opaque white (`Color` clamped values >1 to 1, so 255 → 1). Only the label changed.

**Sign-off:** the two toggle states have distinguishable background colors.

---

### L-R3-1 — Duplicate using directive removed

**Code check:**
```
grep -c "^using UnityEngine;" Assets/_Story/Story/AutoplayToggle.cs
```
Expected: 1 (was 2).

**Manual test:** none — purely cosmetic. Project should still compile.

**Sign-off:** project compiles, no behavior change.

---

### H-R3-3 — `OnButtonClicked` no longer NREs on the debug log

**Code check:**
```
grep -n "_currentlySelected != null ?" Assets/_Story/Story/ButtonSelectionController.cs
```
Expected: one hit on the Debug.Log line.

**Manual test:** the bug only manifests if you can click a voice-mode button *before* `VoiceOptions(...)` is called (which happens in `PRScript.Start`). Normally this doesn't occur. To verify the fix indirectly: open the Console, run a book, switch voice modes a few times. No NRE in the log. The debug log line still prints something (now `<none>` on the first call, instead of crashing).

**Sign-off:** voice-mode switching produces no NRE in the Console.

---

### L-R3-6 — `OnDestroy` guards against null `buttons`

**Code check:**
```
grep -n "if (buttons == null)" Assets/_Story/Story/ButtonSelectionController.cs
```
Expected: one hit at the top of `OnDestroy`.

**Manual test:** if you can construct a scenario where a `ButtonSelectionController` is added at runtime without its `buttons` array wired (unusual), destroying it would have NREd before. Now it returns cleanly. In normal scenes wired in the Inspector this code path isn't reached.

**Sign-off:** scene unload (Home button from a story) produces no NRE.

---

### M-R3-2 + M-R3-3 — `AudioClipUtilities.MakeSubclip` correctness + memory

**Code check:**
```
grep -nE "stop > clip.length|clip\.GetData\(subclipData, samplesStart\)" Assets/_Story/Utils/AudioClipUtilities.cs
```
Expected: two hits — the `stop > clip.length` bound, and the direct `clip.GetData(subclipData, samplesStart)` (without an intermediate full-clip allocation).

**Manual test — full-clip subclip case (M-R3-2):**

Hard to surface from end-user flow because nothing in the app currently asks for `stop == clip.length`. To verify by code:

```csharp
// In a temporary editor menu or debug script:
AudioClip src = ...;
AudioClip whole = AudioClipUtilities.MakeSubclip(src, 0f, src.length);
Debug.Log(whole != null ? "OK" : "REJECTED");
```

Before M-R3-2: "REJECTED" (the check rejected `stop == clip.length`).
After: "OK".

**Manual test — memory usage (M-R3-3):**

1. Open the **Tale of Peter Rabbit** book (the one that uses `PlayAudioAndShowText` with `fromS`/`toS` — the path that goes through subclipping). Other fragment-using books work too.
2. Open Unity Profiler → Memory module.
3. Page through 20 pages.

**Expected after M-R3-3:** per-page memory allocation is dramatically lower. The GC alloc on each `MakeSubclip` call drops from `~clip.length × frequency × channels × 4 bytes` to just the subclip window size. For a 60-second 44 kHz stereo clip with a 2-second window, this is ~88 KB vs ~5.3 MB per call.

**Sign-off:** subclipping still produces correct audio (no audible glitches), and per-frame GC allocations are lower in the Profiler.

---

### M-R3-5 — Settings/progress survive Android force-kill

**Code check:**
```
grep -n "PlayerPrefs.Save" Assets/_Story/Story/Globals.cs
```
Expected: one hit inside `OnApplicationPause`.

**Manual test (Android device or emulator):**

1. Install the app.
2. Open the Settings screen. Move the reading-speed slider to a noticeable position (e.g., Beginner if it's not).
3. **Press the device's Home button.** (This triggers `OnApplicationPause(true)`.)
4. From the recent-apps switcher, **swipe the app away** to force-kill it.
5. Re-launch the app, open Settings.

**Expected after M-R3-5:** the slider is at the position you set in step 2.

**Before M-R3-5:** behavior was Unity's default — `PlayerPrefs` writes were persisted on graceful quit but could be lost on a recents-swipe kill that happened before Unity's opportunistic flush.

This is the same kind of test as round 1's H6 (which added the `OnApplicationPause` hook in the first place); M-R3-5 just adds the explicit `PlayerPrefs.Save()` call so the writes hit disk *now*, not whenever Unity feels like.

**Sign-off:** settings survive a recents-swipe kill on Android.

---

### L-R3-2 — `SettingsScene.Update` empty stub removed

**Code check:**
```
grep -c "void Update" Assets/_Story/Settings/SettingsScene.cs
```
Expected: 0 (was 1).

**Manual test:** none — the removed method had no behavior. Project should still compile and Settings scene should function identically.

**Sign-off:** Settings scene opens, slider/toggle work, version text shows.

---

## Cross-cutting smoke test

After applying all 7 fixes, run a 5-minute sanity check:

1. **Cold launch** → Library loads with covers.
2. **Open a book** → first page appears, audio plays.
3. **Tap the Autopage toggle** → background color visibly changes between on/off states (this is the main user-visible improvement).
4. **Switch voice modes** (human / computer / no-voice) → Console shows no NREs.
5. **Open Settings**, move the reading-speed slider, close the app, swipe-kill, re-open → slider position preserved.
6. **Page through a fragment-using book** (e.g., Peter Rabbit) — Unity Profiler shows lower allocation per page if you're checking.

If all six steps work, the round-3 batch is good.

---

## Rollback

| ID | Revert location |
|---|---|
| H-R3-1, L-R3-1 | `Assets/_Story/Story/AutoplayToggle.cs` (Color32 fields + the duplicate using) |
| H-R3-3, L-R3-6 | `Assets/_Story/Story/ButtonSelectionController.cs` (Debug.Log ternary + OnDestroy null guard) |
| M-R3-2, M-R3-3 | `Assets/_Story/Utils/AudioClipUtilities.cs` (`>` vs `>=`, direct `GetData` call) |
| M-R3-5 | `Assets/_Story/Story/Globals.cs` (the `PlayerPrefs.Save()` call) |
| L-R3-2 | `Assets/_Story/Settings/SettingsScene.cs` (just re-add the empty `Update()` if you really want) |

Each fix is independent.

---

## When ready to commit

```bash
cd ~/dev/Play6.3
git add \
    Assets/_Story/Story/AutoplayToggle.cs \
    Assets/_Story/Story/ButtonSelectionController.cs \
    Assets/_Story/Story/Globals.cs \
    Assets/_Story/Settings/SettingsScene.cs \
    Assets/_Story/Utils/AudioClipUtilities.cs \
    READINGBUDDY_BUG_FINDINGS_ROUND3.md \
    READINGBUDDY_TEST_PLAN_ROUND3.md

git commit -m "fix(round-3): autopage toggle colors, debug log NRE, subclip memory, prefs durability

- AutoplayToggle: use Color32 (byte 0-255) so the on/off background colors
  actually differ. The original 'new Color(255, 255, 255, 128)' silently
  clamped to opaque white because UnityEngine.Color takes float 0-1, not
  bytes — both states rendered identically.
- ButtonSelectionController: null-guard the debug log on _currentlySelected
  (was read before assignment on first call) and guard against a null
  buttons array in OnDestroy.
- AudioClipUtilities.MakeSubclip: allow stop == clip.length and read only
  the needed sample window directly (was allocating the whole clip into
  a temporary float[] per call — ~5MB for a 60s 44kHz stereo source).
- Globals.OnApplicationPause: explicit PlayerPrefs.Save() after stats
  update, so settings tweaks survive an Android recents-swipe force-kill.
- SettingsScene: removed empty Update() stub.
- AutoplayToggle: removed duplicate 'using UnityEngine;' line."

git push origin develop
```
