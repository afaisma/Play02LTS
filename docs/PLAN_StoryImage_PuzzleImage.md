# Plan: StoryImage and PuzzleImage (Image Subclasses)

## 1. Goal

Introduce two Unity component types for story/puzzle display:

- **StoryImage** — a subclass of `UnityEngine.UI.Image` used as the base type for story illustrations.
- **PuzzleImage** — a subclass of **StoryImage** that can operate in two modes:
  - **Unpuzzled:** behaves like a normal `Image` (shows the full sprite; no puzzle logic).
  - **Puzzled:** behaves like a puzzle (e.g. jigsaw pieces from the same sprite; draggable pieces, win state).

No code changes are made yet; this document is planning only.

---

## 2. Class Hierarchy

```
UnityEngine.UI.Image
        │
        ▼
   StoryImage          (subclass of Image)
        │
        ▼
   PuzzleImage         (subclass of StoryImage)
```

- **StoryImage** is the direct subclass of `Image`. It keeps full compatibility with existing code that expects an `Image` (e.g. `Gallery.imgMain`, `PRUtils.DownloadImage(url, image)`), because it *is* an Image.
- **PuzzleImage** is a subclass of StoryImage, so anywhere a StoryImage or Image is required (e.g. `imgMain`), a PuzzleImage can be used. When in unpuzzled mode it should be indistinguishable from a plain Image for display and API purposes.

---

## 3. StoryImage

### 3.1 Role

- **Type marker** for “story” images so the project can consistently use StoryImage (and, where needed, PuzzleImage) instead of raw `Image` for book illustrations.
- **Future extension point** for story-specific behavior (e.g. optional metadata like source URL, safe-area handling, or analytics) without touching Unity’s Image.
- **Default behavior:** identical to `UnityEngine.UI.Image`. No overrides required for basic display; the base `Image` handles `sprite`, `preserveAspect`, `color`, raycasting, and layout.

### 3.2 What to add (minimal for now)

- Optional fields only if needed later (e.g. `string sourceUrl` for debugging or puzzle collection). For the initial plan, StoryImage can be an empty subclass so that existing pipelines (Gallery, PRUtils.DownloadImage) work unchanged when `imgMain` is typed as `Image` and assigned a StoryImage or PuzzleImage in the editor.

### 3.3 Compatibility

- **Gallery.imgMain:** Can remain declared as `Image`; assigning a GameObject with a StoryImage or PuzzleImage component is valid (Liskov substitution).
- **PRUtils.DownloadImage(string url, Image image, bool bPreserveAspect):** Accepts any `Image`; setting `image.sprite` works on StoryImage and PuzzleImage.
- **Canvas, layout, RectTransform:** Unchanged; StoryImage and PuzzleImage are still UI.Image under the hood.

---

## 4. PuzzleImage

### 4.1 Modes

- **Unpuzzled:** The component displays the current sprite as a **single full image**, exactly like the parent `Image` / StoryImage. No pieces, no drag, no puzzle logic. Use this in the **story scene** when showing the same art that might later be used in a puzzle (e.g. last page with “Play puzzle”).
- **Puzzled:** The component uses the **same sprite** (or its texture) to present a **puzzle** (e.g. jigsaw grid). Behavior includes: splitting into pieces, shuffling, accepting drag/drop or tap-to-move, and detecting completion.

Mode can be represented by an enum, e.g. `PuzzleMode { Unpuzzled, Puzzled }`, or a bool `IsPuzzled`, set in code or in the Inspector.

### 4.2 Unpuzzled behavior (design)

- **Visual:** Same as `Image`: one sprite filling the component’s rect (with `preserveAspect` if desired). No extra rendering.
- **Implementation approach:**  
  - Do not draw puzzle pieces; do not enable piece interaction.  
  - Option A: Use base `Image` rendering as-is when Unpuzzled (no override of `OnPopulateMesh` or similar unless necessary).  
  - Option B: If PuzzleImage always builds internal “pieces” for the Puzzled state, in Unpuzzled mode simply hide the piece UI and show the full-image view (e.g. one child Image or the component’s own sprite).  
- **API:** When Unpuzzled, `sprite` get/set should behave like on Image (e.g. setting `sprite` from `PRUtils.DownloadImage` updates what the user sees as one image). Any internal puzzle state (e.g. piece sprites) can be derived from that sprite when switching to Puzzled.

### 4.3 Puzzled behavior (design)

- **Visual:** The same artwork is shown as a puzzle (e.g. jigsaw grid). Pieces can be child UI elements (Image with sliced sprite, or raw quads) or a custom mesh; layout and interaction are managed by PuzzleImage.
- **Data:** Source is the component’s current `sprite` (or its texture). When entering Puzzled mode, generate piece data from that sprite (e.g. grid cells, slice texture into tiles).
- **Interaction:** Touch/drag to move pieces; optional snap-to-grid when near correct position; optional “snap” feedback (e.g. particle or sound). No keyboard dependency.
- **State:** Track placed vs not placed; when all pieces are correctly placed, fire a “puzzle complete” event or set a flag so the scene can show completion UI (e.g. “Well done!” and “Another puzzle” / “Back to story” as in the existing puzzle plan).

### 4.4 Mode switching

- **When:** Unpuzzled is used in the **story** flow (e.g. Gallery’s main image). Puzzled is used when the app enters the **puzzle** flow (e.g. _Puzzle scene or puzzle overlay).
- **How:** Either:
  - **Same component, switch mode:** One PuzzleImage on a GameObject; in story it’s `SetPuzzleMode(Unpuzzled)` and receives sprite from `DownloadImage`; when entering puzzle, set sprite (or pass same texture/sprite), then `SetPuzzleMode(Puzzled)` and build pieces from current sprite.  
  - **Separate instances:** Story scene uses a StoryImage (or PuzzleImage in Unpuzzled mode) for Gallery; puzzle scene uses a different PuzzleImage instance in Puzzled mode and assigns it the same sprite/URL.  
- **Recommendation:** Support both: allow one PuzzleImage to switch mode (Unpuzzled ↔ Puzzled) so the same component can be reused; also allow a dedicated PuzzleImage in the puzzle scene that is only ever used in Puzzled mode.

---

## 5. Where These Classes Plug In

| Location | Current type | After plan (no code yet) |
|----------|--------------|---------------------------|
| **Gallery.imgMain** | `Image` | Can stay `Image`; in Editor, assign a GameObject with **StoryImage** or **PuzzleImage**. No API change required. |
| **PRUtils.DownloadImage** | `Image image` | No change; StoryImage and PuzzleImage are subclasses of Image. |
| **Story scene** | Single Image for page art | Use **StoryImage** or **PuzzleImage (Unpuzzled)** so that later the same prefab/scene can use PuzzleImage in Puzzled mode if desired. |
| **_Puzzle scene** (from PLAN_Puzzle_End_Of_Book) | TBD | Use **PuzzleImage** in **Puzzled** mode; assign it the sprite (or load from same URL) for the chosen illustration. |

---

## 6. Implementation Hooks (for later)

When implementing, consider:

1. **StoryImage**
   - New script `StoryImage.cs`, subclass of `Image`; empty or with optional `sourceUrl` / metadata.
   - No change to `OnPopulateMesh` or drawing unless future features need it.

2. **PuzzleImage**
   - New script `PuzzleImage.cs`, subclass of `StoryImage`.
   - Field or property: `PuzzleMode Mode` (or `bool IsPuzzled`).
   - When **Unpuzzled:** ensure a single-image view (base Image behavior or one full-image child).
   - When **Puzzled:** on mode set (or when sprite is set and mode is Puzzled), build puzzle from current sprite (e.g. grid size 2×2, 3×3, 4×4); create piece GameObjects or meshes; handle input and snap/win logic.
   - Optional: `SetPuzzleMode(PuzzleMode mode)` and `RebuildPuzzleFromCurrentSprite()` to switch and refresh.

3. **Compatibility**
   - Keep `Gallery.imgMain` as `Image` (or optionally change to `StoryImage` for clarity). No change to `PRUtils.DownloadImage` signature.
   - Prefab/scene: replace the component on the main story image GameObject from `Image` to `StoryImage` or `PuzzleImage`; set default mode to Unpuzzled for story use.

---

## 7. Summary

- **StoryImage:** Subclass of `UnityEngine.UI.Image`; behaves like Image; serves as the base type for story illustrations and as a hook for future story-specific behavior.
- **PuzzleImage:** Subclass of StoryImage with two modes:
  - **Unpuzzled:** Same behavior as parent Image (one full sprite; no puzzle).
  - **Puzzled:** Same sprite shown as a puzzle (e.g. jigsaw); pieces, drag, and completion.
- Existing code (Gallery, PRUtils) continues to work with `Image`; StoryImage and PuzzleImage are drop-in compatible. No code changes are made in this step; this document is the plan only.
