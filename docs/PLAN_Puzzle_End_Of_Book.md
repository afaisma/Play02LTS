# Plan: End-of-Book Puzzles (1–3 puzzles from book illustrations)

## 1. Goal

Add an optional **puzzle** experience at the end of a book. Children can play **1–3 puzzles** (e.g. jigsaw or similar) built from **illustrations that appeared in that book**. The feature should feel like a reward for finishing the story and reinforce recognition of the book’s art.

---

## 2. User flow (high level)

1. Child reads the book to the **last page** (e.g. “The End” chunk).
2. When on the last page (or when advancing past it), the app offers a **“Play puzzle”** (or “Puzzle”) entry point.
3. Child taps **“Play puzzle”** (or equivalent). App shows **1–3 puzzle options** (e.g. thumbnails of the images that will be used), or a single “Play” if there is only one puzzle.
4. Child selects a puzzle (or the app launches the only one). The **puzzle game** runs (e.g. jigsaw from that image).
5. On **completion**: short celebration, then options such as **“Another puzzle”**, **“Back to story”**, **“Back to library”**.

---

## 3. When to show the puzzle option

- **Trigger:** When the user is on the **last step** of the book (e.g. `nCurrentStep == _scriptlets.Count - 1`).  
  Option A: show a **“Puzzle”** button only on the last page.  
  Option B: after a short delay or after “The End” narration, show a **modal/overlay** (“You finished! Play a puzzle?”) with **Play** / **Maybe later**.
- **Persistence:** Do not block progress. If the user goes **Home** or **Back** without playing, the book remains “done”; the puzzle remains available later (e.g. from a “Replay” or “Puzzle” entry point for that book, if we add it in a second phase).

---

## 4. Where puzzle images come from (data source)

Book illustrations are currently shown via script commands **DisplayMainImage** and **AddGalleryImage** with paths like `"images//1.jpg"` (relative to the book’s `baseURL`). We need **1–3 image URLs per book** to use as puzzle sources.

**Options (choose one for v1):**

| Option | Description | Pros | Cons |
|--------|-------------|------|------|
| **A. Collect during story** | While the script runs, record every image URL passed to DisplayMainImage/AddGalleryImage (deduplicated). At end of book, use that list and pick 1–3 (e.g. first, middle, last, or random). | No script or data format changes; works for all existing books. | Some books may have many images; need a simple rule to pick 1–3. |
| **B. Script-driven** | Add a new script intrinsic, e.g. `AddPuzzleImage "images//5.jpg"`. Authors add 1–3 such lines (e.g. in settings or in chosen chunks). | Explicit control over which illustrations become puzzles. | Requires editing every story script that should have puzzles. |
| **C. Per-book config file** | In each book folder, add a small file (e.g. `puzzle_images.txt`) listing 1–3 image paths. App reads it when entering story or when opening puzzle. | Clear, editable, no script changes. | New asset/convention per book; need a loader. |

**Recommendation for v1:** **Option A** (collect during story). Implement a small “image log” (list of URLs) filled when DisplayMainImage/AddGalleryImage are called; when opening the puzzle flow, take up to 3 (e.g. first, middle, last, or by simple rule). Later, Option B or C can be added to override or define the set explicitly.

---

## 5. Puzzle mechanic (what the child actually plays)

- **Default:** **Jigsaw** – the chosen illustration is split into a grid (e.g. 2×2, 3×3, or 4×4). Pieces are shuffled; the child drags them into place to reassemble the image. Difficulty can be chosen by grid size (or by age if we have it).
- **Alternatives (later):** Simple **slider puzzle** (e.g. 3×3 with one empty), **memory match** (pairs of tiles from the same image), or a **“tap in order”** sequence. For the plan we assume **one primary mechanic (jigsaw)** and optional variants later.

**Requirements:**

- Use the **same image** the child saw in the story (same URL / same texture once loaded).
- **Touch/drag** friendly (no keyboard).
- **Clear win state** and short positive feedback (e.g. “Well done!” + option to play another or go back).
- **Back / Home** available so the child can exit without completing.

---

## 6. Scene and architecture

- **Option 1 – New scene `_Puzzle`:**  
  - Load scene when user chooses “Play puzzle”. Pass context via **Globals** (e.g. current book id/url, list of 1–3 image URLs, which puzzle index is selected).  
  - Puzzle scene: load selected image, build grid, run game, handle completion and “Back to library” / “Back to story”.

- **Option 2 – Canvas/panel inside `_Story`:**  
  - Keep story scene; show a full-screen puzzle canvas on top when “Play puzzle” is tapped. Same image list and selection; when done, hide puzzle canvas and return to last page or home.  
  - Fewer scene loads; reuse story scene’s loading. Slightly more coupling to _Story.

**Recommendation:** **New scene `_Puzzle`** for clearer separation, easier testing, and simpler flow (story → puzzle → library). Use **Globals** (and optionally a small **PuzzleContext** or **PuzzleSession** struct) to pass book id, baseURL, and the 1–3 image URLs.

---

## 7. Implementation phases (no code yet)

### Phase 1: Data and entry point

- **1.1** Decide and implement **source of puzzle images** (recommended: collect DisplayMainImage/AddGalleryImage URLs during story run; cap at 3 with a simple selection rule).
- **1.2** Store “puzzle image list” for the current book somewhere reachable (e.g. on Globals or on a small **BookPuzzleData** / **PuzzleSession** that _Story sets and _Puzzle reads).
- **1.3** On the **last page** of the book, show a **“Puzzle”** button (or equivalent). Tapping it sets the puzzle context (book, image list) and loads `_Puzzle` (or opens puzzle overlay if Option 2 is chosen).

### Phase 2: Puzzle scene and selection

- **2.1** Create **scene `_Puzzle`** with: camera, UI (title, “Back”), and a placeholder area for “puzzle selection” (e.g. 1–3 thumbnails or buttons).
- **2.2** **Puzzle scene controller:** On start, read from Globals (or PuzzleSession) the list of 1–3 image URLs. Display thumbnails (or “Puzzle 1”, “Puzzle 2”, “Puzzle 3”) and let the user pick one. If only one image, skip selection and go straight to game.
- **2.3** When user selects a puzzle, load the full image (reuse **PRUtils.DownloadImage** or same pipeline), then pass the loaded texture/sprite to the puzzle game logic.

### Phase 3: Jigsaw game logic

- **3.1** **Puzzle grid:** Slice the chosen image into N×M pieces (e.g. 2×2 for easiest, 3×3 default). Generate sprites or UI Images for each piece; store correct position.
- **3.2** **Shuffle:** Randomize piece positions (or swap order) so the puzzle is solvable. Display pieces in a “board” area (e.g. on a Canvas).
- **3.3** **Drag and drop:** Implement touch/drag to move one piece at a time. Optional: snap-to-grid when a piece is near its correct slot. Detect when all pieces are in the correct place.
- **3.4** **Win state:** On solve, show a short celebration (e.g. particle or message) and buttons: **“Another puzzle”** (if more images available), **“Back to story”**, **“Back to library”**.

### Phase 4: Polish and edge cases

- **4.1** **Back button:** Always available; returns to library (or to story if we want “back to last page”).
- **4.2** **No images:** If the book had no DisplayMainImage/AddGalleryImage calls, do not show “Puzzle” or show a message (“No puzzles for this book”).
- **4.3** **Difficulty:** Optional: let user (or parent) choose grid size (e.g. 2×2 vs 3×3); or derive from book age if available.
- **4.4** **Audio:** Optional: reuse existing audio patterns (e.g. success sound on complete).

---

## 8. Files and components to add (conceptual)

- **Scenes:** `_Puzzle.unity` (if using dedicated scene).
- **Scripts (new):**  
  - **PuzzleSession** or **Globals** extension: hold current book id, baseURL, list of 1–3 image URLs.  
  - **PuzzleSceneController:** entry point for _Puzzle scene; load context, show selection, start game.  
  - **JigsawPuzzle** (or **BookPuzzleGame**): grid build, shuffle, drag-and-drop, win detection.  
  - **PuzzlePiece:** single piece behaviour (drag, snap, correct position).
- **Scripts (modify):**  
  - **PRScript** (or **StoryStepsUI** / **Gallery**): when DisplayMainImage or AddGalleryImage is called, append URL to a “puzzle image list” (with dedup and max 3).  
  - **PRScript** (or **StoryStepsUI**): on last step, show “Puzzle” button; on click, set PuzzleSession and load _Puzzle.  
  - **Globals:** optional static fields or API for “current puzzle image list” and “current book” for puzzle scene.

---

## 9. Out of scope for this plan

- **Authoring tool** for puzzle selection (script or config) can be added later.
- **Multiple puzzle types** (slider, memory) – design for one (jigsaw) first.
- **Progress/save** of “puzzle completed” per book (optional later).
- **Offline:** assume same network/loading as current story images (e.g. cache if already loaded in story).

---

## 10. Summary

| Item | Choice |
|------|--------|
| **Trigger** | “Puzzle” button (or modal) on last page of book. |
| **Image source** | Collect up to 3 image URLs from DisplayMainImage/AddGalleryImage during story (Option A). |
| **Puzzle type** | Jigsaw: slice image into grid, shuffle, drag to reassemble. |
| **Scene** | New scene `_Puzzle`; context via Globals / PuzzleSession. |
| **Flow** | Last page → Puzzle → (optional) select 1 of 1–3 → play jigsaw → celebrate → “Another” / “Back to story” / “Back to library”. |

No code changes are made in this step; this document is the plan only. Implementation can follow Phases 1–4 in order.
