# ReadingBuddy — Test Plan for Round-2 Fixes (8 changes)

*Companion to `READINGBUDDY_BUG_FINDINGS_ROUND2.md`. Each fix has a code check (5-second grep), a manual test, and a sign-off line.*

---

## Summary of what changed

| ID | Bug | File | Diff size |
|---|---|---|---|
| **C-R2-1** | `ParentalGate.CheckAnswerCounting` crashed on non-numeric input | `ParentalGate.cs` | TryParse + shared `ShowTryAgain` helper |
| **C-R2-2** | Default `correctAnswer = 0` made "0" a bypass on first try | `ParentalGate.cs` | 1 line in `Start()` |
| **L-R2-1** | Gate panel stayed visible after successful Navigate | `ParentalGate.cs` | uncommented `SetActive(false)` |
| **H-R2-1** | Swipe over overlapping swipeables advanced the page twice | `SwipeDetector.cs` | `break;` after first hit |
| **L-R2-4** | `new PointerEventData(null)` warning on modern Unity | `SwipeDetector.cs` | pass `EventSystem.current` |
| **M-R2-1** | `_shuffleSeed` inspector field was dead code | `PuzzleImage.cs` | use field when non-zero |
| **M-R2-2** | `LoadVideo` double-prefixed absolute URLs | `PRVideoPlayer.cs` | scheme check |
| **M-R2-3** | `Stop()` left `isPlayingSegment = true` | `PRVideoPlayer.cs` | reset flag |

All 8 are in your working tree under `Assets/_Story/`.

---

## Pre-flight

1. Open the project in Unity 6 (6000.3.9f1).
2. Confirm it compiles. Console should be clean.
3. If you're on the ParentalGate / Bookstore flow, make sure you know which scene's `checkOrNextButton` is wired to which method (`CheckAnswerText` vs `CheckAnswerCounting`) so you can test the right variant.

---

## Per-fix tests

### C-R2-1 — Counting-variant parental gate no longer crashes on letters

**Code check:**
```
grep -n "TryParse" Assets/_Story/GUI/ParentalGate.cs
```
Expected: one hit inside `CheckAnswerCounting`.

**Manual test — counting variant required.** In the inspector for whatever GameObject hosts `ParentalGate`, the `checkOrNextButton`'s `OnClick` needs to be wired to `CheckAnswerCounting` (not `CheckAnswerText`). If your project uses the text variant exclusively, this test isn't applicable — skip to the next fix.

1. Trigger the gate (e.g. tap a Bookstore Amazon link, or whatever in the project invokes the gate).
2. Type **`abc`** into the input field and tap the check button.

**Expected after C-R2-1:** the button label changes to "Try Again", the input clears, a new "What is X * Y?" question appears, no crash.

**Before C-R2-1:** Unity Console showed `FormatException: Input string was not in a correct format` and the gate locked up; on builds with strict exception handling, the app crashed entirely.

**Regression check:** type a valid number that's not the answer (e.g. `99`). The "Try Again" path still fires correctly with a new question.

**Sign-off:** non-numeric input never crashes; the gate stays usable.

---

### C-R2-2 — Typing "0" can't bypass the counting gate before the first question

**Code check:**
```
grep -n "correctAnswer = -1" Assets/_Story/GUI/ParentalGate.cs
```
Expected: one hit in `Start()`.

**Manual test — counting variant:**
1. Trigger the gate.
2. **As the very first action**, type `0` and tap the check button.

**Expected after C-R2-2:** wrong-answer flow — "Try Again", new question appears.

**Before C-R2-2:** `Navigate()` fired immediately because `correctAnswer` was still `0` (its default value) and the user typed `0`. A child could bypass the gate with a single keystroke.

**Regression check:** after the first wrong attempt, the gate generates a real question (e.g. "What is 3 * 7?"). Type the right answer (`21`). Navigate fires normally.

**Sign-off:** the bypass-with-0 path no longer succeeds; the legitimate answer path still works.

---

### L-R2-1 — Gate panel hides after a successful answer

**Code check:**
```
grep -n "parentalGatePanel.SetActive" Assets/_Story/GUI/ParentalGate.cs
```
Expected: two hits — one in `Cancel`, one in `Navigate` (no longer commented out).

**Manual test:**
1. Trigger the gate.
2. Answer correctly.
3. If `Navigate()` opens an external URL (the bookstore Amazon link path): the browser opens, then come back to the app.

**Expected after L-R2-1:** when you return to the app, the gate panel is **gone**. The Bookstore (or wherever you came from) is visible.

**Before L-R2-1:** the gate panel was still showing on top of the underlying scene. Cosmetic but confusing.

**Regression check — scene navigation variant:** if `sceneNavigateTo != ""`, `SceneManager.LoadScene` runs first, which destroys the gate anyway. The new `SetActive(false)` call is a no-op in that path but does no harm.

**Sign-off:** gate disappears after success in both navigation modes.

---

### H-R2-1 — Swipe over overlapping swipeables advances the page only once

**Code check:**
```
grep -n "break;" Assets/_Story/Utils/SwipeDetector.cs
```
Expected: one hit inside the `foreach (RaycastResult result in results)` loop.

**Manual test — needs a page where `gallery` and `textforeground` overlap.** Look at the `_Story` scene in Unity: are the Gallery and Textforeground RectTransforms positioned so their bounds overlap? If yes, find such a region (often the seam between text area and image area).

1. Open any book with multiple pages, advance to page 3 (any non-edge page).
2. Note the page number / step indicator.
3. Swipe left **directly across the seam where Gallery and Textforeground overlap**.

**Expected after H-R2-1:** the page advances by exactly one (to page 4).

**Before H-R2-1:** the swipe fired `PRScript.LeftSwipe` for each overlapping swipeable, advancing two pages (to page 5).

If you don't have an overlap in the current scene layout, this test is academic — the fix is still a correctness improvement for any future scene that does have overlap, and the `break` doesn't change behavior in the no-overlap case.

**Regression check — single swipeable area:**
- Swipe left on a `textforeground`-only region → page advances by one.
- Swipe left on a `gallery`-only region (single-image page) → page advances by one.
- Swipe within a multi-image gallery (not at the edge) → gallery cycles, page doesn't change. (This exercises H8 from round 1.)

**Sign-off:** a single swipe gesture always advances by exactly one page in single-image scenes, and exactly one image-or-page step in multi-image scenes.

---

### L-R2-4 — No more "PointerEventData with null EventSystem" warning

**Code check:**
```
grep -n "new PointerEventData" Assets/_Story/Utils/SwipeDetector.cs
```
Expected: one hit, with `EventSystem.current` (not `null`) as the argument.

**Manual test:** open the Unity Console (or `adb logcat | grep -i pointerevent` on device), then swipe a few times in the story.

**Expected after L-R2-4:** no warning entries. (You may see other Unity warnings unrelated to this fix.)

**Before L-R2-4:** on Unity 2022+, the Console showed warnings like:
```
PointerEventData was created without specifying the EventSystem; pass EventSystem.current.
```

**Sign-off:** clean Console when swiping.

---

### M-R2-1 — `PuzzleImage._shuffleSeed` now honored

**Code check:**
```
grep -n "_shuffleSeed != 0" Assets/_Story/GUI/PuzzleImage.cs
```
Expected: one hit inside `AssignInitialPieceSlots`.

**Manual test:**
1. In the Unity Editor, pick any prefab or scene object that has a `PuzzleImage` component.
2. Set its **Shuffle Seed** field in the inspector to a non-zero value, e.g. `12345`.
3. Enter Play mode, trigger the puzzle (turn it puzzled). Note which pieces land in which slots.
4. Exit Play mode, re-enter Play mode, trigger the puzzle again.

**Expected after M-R2-1:** with seed=12345, the same piece-to-slot assignment appears every time. Useful for reproducing bug reports.

**Regression check:** set seed back to `0`. The shuffle becomes random each time (uses `Environment.TickCount`). Same as the old behavior.

**Sign-off:** non-zero seed gives reproducible shuffles; `0` keeps the random behavior.

---

### M-R2-2 — `PRVideoPlayer.LoadVideo` no longer double-prefixes absolute URLs

**Code check:**
```
grep -n "url.StartsWith(\"http\")" Assets/_Story/Players/PRVideoPlayer.cs
```
Expected: one hit inside `LoadVideo`.

**Manual test:** none of the current books use absolute video URLs, so this is a guard for future content. To verify the logic:

1. Open the Quantum Console in a running story (tilde key).
2. (If there's a way to invoke `PRVideoPlayer.LoadVideo` directly from a script or test scene — otherwise, this is editor-only.)

Easier: read the diff and confirm the conditional. The fix is a single ternary; nothing in production exercises it yet.

**Regression check — relative URL:** any book that uses `AddVideo("videos/clip.mp4")` (relative) should still play. `baseURL + url` is still applied when the URL doesn't start with `http`.

**Sign-off:** relative URLs work as before (no regression). Absolute URLs would not be doubly-prefixed (no easy way to test until content uses them).

---

### M-R2-3 — `PRVideoPlayer.Stop()` resets `isPlayingSegment`

**Code check:**
```
grep -n "isPlayingSegment = false" Assets/_Story/Players/PRVideoPlayer.cs
```
Expected: three hits — in `PlaySegment` (the existing logic), in `InterruptPlaySegment`, in `Update`, and now also in `Stop`.

**Manual test:** this fix has no user-visible effect today (the bug it prevents is dormant). The verification is that nothing breaks.

1. Find a story that uses `PlayVideo` (rare — most books don't). If none, skip.
2. Trigger the video. Mid-segment, navigate away (Home button).
3. Re-open the same book / re-trigger the video.

**Expected after M-R2-3:** video plays normally on the second attempt. Same as before, but the internal state is now consistent.

**Sign-off:** no regression on video playback.

---

## Cross-cutting smoke test

Run this after applying all 8 fixes:

1. **Cold launch.** Library loads.
2. **Open a book**, read 3 pages, navigate Home. Open again — picks up where you left off. (Unchanged behavior.)
3. **Swipe through a multi-image gallery** to its last image, then swipe past it. Page advances (H8 from round 1 still works).
4. **Swipe at the gallery/text seam** if your layout has one. Page advances by exactly one. (H-R2-1)
5. **Open the Bookstore.** Tap an Amazon link. The parental gate appears.
6. **Type `abc`** → "Try Again". (C-R2-1)
7. **Type `0`** → "Try Again". (C-R2-2)
8. **Type a number that's not the answer** → "Try Again". (Existing behavior preserved.)
9. **Type the correct answer.** External browser opens. Return to the app. The gate panel is gone, not still visible. (L-R2-1)
10. **Open Unity Console.** No new warnings from swiping. (L-R2-4)

If all 10 steps behave as expected, this batch is ready to merge.

---

## Rollback

Each fix is a self-contained edit. Roll back per fix:

| ID | Revert by reverting |
|---|---|
| C-R2-1 | `Assets/_Story/GUI/ParentalGate.cs:CheckAnswerCounting` + `ShowTryAgain` helper |
| C-R2-2 | `Assets/_Story/GUI/ParentalGate.cs:Start` — the `correctAnswer = -1` line |
| L-R2-1 | `Assets/_Story/GUI/ParentalGate.cs:Navigate` — the new `SetActive(false)` block |
| H-R2-1 | `Assets/_Story/Utils/SwipeDetector.cs` — the `break;` and the early-`continue` |
| L-R2-4 | `Assets/_Story/Utils/SwipeDetector.cs` — `new PointerEventData(EventSystem.current)` |
| M-R2-1 | `Assets/_Story/GUI/PuzzleImage.cs:AssignInitialPieceSlots` — the seed conditional |
| M-R2-2 | `Assets/_Story/Players/PRVideoPlayer.cs:LoadVideo` — the URL conditional |
| M-R2-3 | `Assets/_Story/Players/PRVideoPlayer.cs:Stop` — the `isPlayingSegment = false` line |

All are independent; reverting any one doesn't affect the others.

---

## When ready to commit

Same approach as round 1 — single focused commit:

```bash
cd ~/dev/Play6.3
git add \
    Assets/_Story/GUI/ParentalGate.cs \
    Assets/_Story/Utils/SwipeDetector.cs \
    Assets/_Story/GUI/PuzzleImage.cs \
    Assets/_Story/Players/PRVideoPlayer.cs
git commit -m "fix(round-2): parental gate, swipe, puzzle seed, video URL handling

- ParentalGate.CheckAnswerCounting: TryParse + shared ShowTryAgain helper
  (was: int.Parse crashed the gate on non-numeric input).
- ParentalGate.Start: initialize correctAnswer = -1
  (was: default 0 let a child bypass the gate by typing '0' as first answer).
- ParentalGate.Navigate: hide gate panel after successful answer
  (was: panel stayed visible behind the external URL).
- SwipeDetector: break after first SwipeableObject hit
  (was: overlapping swipeables advanced the page twice in one gesture).
- SwipeDetector: pass EventSystem.current to PointerEventData
  (silences a warning on Unity 2022+).
- PuzzleImage: honor _shuffleSeed inspector field
  (was: dead code — Environment.TickCount always used).
- PRVideoPlayer.LoadVideo: only prepend baseURL for relative URLs.
- PRVideoPlayer.Stop: also reset isPlayingSegment for state consistency."
git push origin develop
```
