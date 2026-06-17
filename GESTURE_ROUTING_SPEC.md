# Claude Code hand-off — one gesture owner: tap = listen, swipe = page

**Problem:** word-tap and page-turn are decided by two independent input systems — `WordTapHandler`
(EventSystem `IPointerClickHandler`) and `SwipeDetector` (raw `Input.GetTouch` polled in `Update`).
They can't coordinate (`eventData.Use()` doesn't reach the raw-Input swipe path), leaving a fragile
seam. **Fix:** make `SwipeDetector` the single owner that classifies each gesture once — small move →
**tap** (play the word), large horizontal move → **swipe** (turn the page) — and delete
`WordTapHandler`.

## Behavior after
- A finger press is classified exactly once on release:
  - `dist <= TapMaxDist` (≈20px) → **tap**: play the tapped word from the word bank (if any), nothing else.
  - `dist > minSwipeDist` (50px) and horizontal → **swipe**: page (unchanged).
  - in between → ignore.
- Paging stays on swipe + the on-screen ◀ ▶ buttons (explicit EventSystem buttons, untouched).
- Tap on a non-word / unmapped word → nothing.

## 1. `AudioAndTextPlayer` — screen-pos entry point (owns the foreground TMP)
Add a public method that does the TMP hit-test on its OWN `uiForeground` (so the word logic stays
with the component that has the reference), reusing the existing `TryPlayWord`:
```csharp
public bool TryPlayWordAtScreenPos(Vector2 screenPos)
{
    if (!wordTapEnabled || uiForeground == null) return false;
    Camera cam = null;
    Canvas c = uiForeground.canvas;
    if (c != null && c.renderMode != RenderMode.ScreenSpaceOverlay) cam = c.worldCamera;
    int wi = TMP_TextUtilities.FindIntersectingWord(uiForeground, screenPos, cam);
    if (wi < 0) return false;
    return TryPlayWord(uiForeground.textInfo.wordInfo[wi].GetWord());
}
```

## 2. `SwipeDetector` — classify tap vs swipe in `TouchPhase.Ended`
Add `private const float TapMaxDist = 20f;`. In `Ended`, after the existing
`_startedOnDraggableOverlay` early-out and `swipeDist` computation:
```csharp
if (swipeDist <= TapMaxDist)
{
    // TAP → word listening. Raycast at the touch and hand off to the AudioAndTextPlayer
    // under it (if any). No-op when the tap isn't on the reading text.
    var ped = new PointerEventData(EventSystem.current) { position = touch.position };
    var hits = new List<RaycastResult>();
    EventSystem.current.RaycastAll(ped, hits);
    foreach (var h in hits)
    {
        var player = h.gameObject != null ? h.gameObject.GetComponentInParent<AudioAndTextPlayer>() : null;
        if (player != null) { player.TryPlayWordAtScreenPos(touch.position); break; }
    }
    break; // handled this gesture as a tap; never also treat as a swipe
}
// else: existing swipe handling (swipeDist > minSwipeDist && horizontal → Left/RightSwipe)
```
Keep the `_startedOnDraggableOverlay` suppression for the swipe branch only (a tap on an overlay can
still be a word tap). The foreground TMP must remain **Raycast Target = ON** so the tap raycast finds it.

## 3. Remove `WordTapHandler`
- Delete `Assets/_Story/Players/WordTapHandler.cs` (+ `.meta`).
- In the `_Story` scene, remove the now-missing `WordTapHandler` component from the foreground TMP
  GameObject (it leaves a "missing script" slot until removed). Save the scene.
- The `_wordTapSource`, the word-bank load, `TryPlayWord`, `PlaySlice`, `NormalizeWord`, and the
  `wordTapEnabled` gate all stay on `AudioAndTextPlayer`.

## Tests / verify (play mode)
- Tap a word → it plays; the page does NOT turn.
- Swipe left/right → the page turns; no word plays.
- Tap an empty area / the image → nothing plays and no page turn (paging via swipe or ◀ ▶).
- A small jittery tap (<20px) still reads as a tap, not a swipe.

## Safety
Net simplification: one input owner, mutually-exclusive tap/swipe outcomes, no EventSystem↔raw-Input
coordination. Reverts by restoring `WordTapHandler` and removing the tap branch.
