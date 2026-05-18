# Prompt: Plan StoryImage and PuzzleImage (No Code)

Use this prompt with an AI assistant (e.g. Cursor, ChatGPT) to get a **planning document** for the StoryImage/PuzzleImage design. Do not ask the AI to write code—only a plan.

---

## Copy-paste prompt (self-contained)

**Task: Planning only — no code.**

We have a Unity project where book illustrations are displayed using **UnityEngine.UI.Image**: a single `Image` component shows a sprite (loaded from a URL and assigned to `Image.sprite`). We want to introduce two new component types that subclass `Image` and support a future “puzzle” feature.

**Please produce a planning document that covers:**

1. **StoryImage**  
   - A subclass of `UnityEngine.UI.Image`.  
   - It should behave exactly like the base `Image` (same display, same API: `sprite`, `preserveAspect`, etc.).  
   - Its purpose is to be the base type for story illustrations so we can later use or swap in more specialized image types (e.g. puzzle) where needed.  
   - The plan should note how it stays compatible with existing code that expects an `Image` (e.g. methods that take `Image` and set `image.sprite`).

2. **PuzzleImage**  
   - A subclass of **StoryImage** (so the hierarchy is: `Image` → `StoryImage` → `PuzzleImage`).  
   - It has **two modes**:
     - **Unpuzzled mode:** Behaves like the parent: shows the full sprite as one image, no puzzle logic, no pieces. Same visual and API as a normal `Image` / `StoryImage`.
     - **Puzzled mode:** Uses the same sprite (or its texture) to display a **puzzle** (e.g. jigsaw: split into pieces, shuffle, drag to reassemble, detect when complete).  
   - The plan should describe:
     - What “unpuzzled” means concretely (single full image; no puzzle UI).
     - What “puzzled” means concretely (e.g. grid of pieces, interaction, win state).
     - How mode could be represented (e.g. enum or bool) and when each mode is used (e.g. unpuzzled in the story screen, puzzled in a puzzle screen).
     - How switching between modes could work (same component switching mode vs. separate instances).

3. **Integration**  
   - Where these components would plug in: e.g. a “gallery” that currently has a single `Image` for the main illustration—that reference could point to a `StoryImage` or a `PuzzleImage` in unpuzzled mode without changing the existing API (they are still `Image`).  
   - No changes to existing method signatures that take `Image` should be required.

4. **Deliverable**  
   - A structured plan (sections, bullet points, optional class diagram or table).  
   - **Do not write any C# or Unity code**—only the design/plan.  
   - The plan should be clear enough for a developer (or an AI in a follow-up step) to implement `StoryImage` and `PuzzleImage` later.

**Context (optional for the AI):** The app is a children’s reading app. Story scripts set the current page image via something like `DisplayMainImage(url)`; that eventually downloads the image and sets it on an `Image` component. We want the same image to be usable later as a jigsaw puzzle at the end of the book, hence the need for a component that can act as a normal image (unpuzzled) or as a puzzle (puzzled).

---

## Shorter version (minimal prompt)

**Planning only — no code.**

Plan two Unity UI component types:

1. **StoryImage** — subclass of `UnityEngine.UI.Image`; behaves identically to `Image`; used as the base type for story illustrations.  
2. **PuzzleImage** — subclass of **StoryImage** with two modes:  
   - **Unpuzzled:** same as a normal Image (one full sprite, no puzzle).  
   - **Puzzled:** same sprite shown as a puzzle (e.g. jigsaw: pieces, drag, completion).

Describe: class hierarchy; what each mode does; how mode is represented and when each is used; how this fits into existing code that uses `Image` (e.g. a gallery with one main image). Do not write any code—only a structured plan a developer can use to implement later.
