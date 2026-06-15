# Claude Code hand-off — `FX` effects library (visual + audio, cross-scene, isolated)

**Author:** Cowork · **For:** Claude Code · **Type:** new, self-contained subsystem.
**Goal:** a reusable library of named **effects** — stars/sparks/confetti bursts, persistent
attached decorations (e.g. a "Lab bubble" on a book tile), button highlights, and sounds —
callable from C# and the Unity Editor, usable in **any scene** (Library, Story, Map, Message…).

## HARD REQUIREMENTS (these define success — do not compromise)
1. **One self-contained top-level folder: `Assets/FX/`.** All code, an `Editor/` subfolder, and a
   `Resources/FX/` subfolder for the assets live here. Removing this one folder removes the whole
   system.
2. **One-way dependency, by convention — NOT by asmdef.** The game code lives in the predefined
   `Assembly-CSharp` (the project has no game-code asmdefs), and Unity forbids a custom asmdef
   from referencing `Assembly-CSharp`. So **FX must NOT have an asmdef** — its scripts live in the
   predefined assemblies (`Assembly-CSharp` for runtime, `Assembly-CSharp-Editor` for `Editor/` and
   tests), where they can freely reference `Globals`/`PuzzleImage`/`PRBook` and the package asmdefs
   (`ParticleImage`, `DOTween`). The one-way rule is a discipline: **do not edit any existing `.cs`
   file to call FX.** All wiring is done from inside FX, by subscribing to events/state the app
   already exposes (see §Hooks). This still guarantees rollback-by-delete: since nothing in the app
   references an FX type, deleting `Assets/FX/` cannot break the app's compilation.
3. **Simple rollback, two levels:** (a) *disable* — a kill-switch (`bool enabled` on the library
   asset, or a `FX_DISABLED` define) makes the bootstrap a no-op; (b) *remove* — delete
   `Assets/FX/`. Because of the one-way dependency, deleting the folder cannot break the app's
   compilation. The only residue is cosmetic "missing script" refs on any scene object that had an
   `FXTrigger` added in the Inspector (cleared via Unity's "Remove Missing Scripts").
4. **Fail-soft:** a missing/unloadable effect logs a warning and renders nothing — never throws,
   never blocks gameplay.

## Files (all under `Assets/FX/`)
| File | Role |
|---|---|
| `FXEffect.cs` | ScriptableObject — one effect: `id`, `kind` (Visual\|Audio\|Composite), backend ref + params, optional `AudioClip`. |
| `FXLibrary.cs` | ScriptableObject catalog — list of `FXEffect`, lookup by id; `enabled` kill-switch; the per-book decoration map (§Lab). |
| `FX.cs` | Static facade: `Play`, `Decorate`, `PlaySound`, `Stop`. Resolves ids, routes to backends. |
| `FXRuntime.cs` | The persistent manager MonoBehaviour: owns the overlay Canvas + `AudioSource`(s), pooling, handle bookkeeping. |
| `FXBootstrap.cs` | `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]` — instantiates the runtime from `Resources/FX/`, wires per-scene hooks on `SceneManager.sceneLoaded`. |
| `FXTrigger.cs` | Optional Inspector component: pick effect id + trigger mode (OnEnable\|OnButtonClick\|Manual `Fire()`) + target (self\|RectTransform\|fullscreen). |
| `Editor/FXTriggerEditor.cs` | Optional — effect-id dropdown from the library (a plain string field works without it). |
| `Resources/FX/FXLibrary.asset`, `*.asset` | Editor-authored catalog + per-effect assets. |
| `Resources/FX/FXRuntime.prefab` | The persistent Canvas + AudioSource prefab the bootstrap instantiates. |
| `Resources/FX/…` particle prefabs / sprite art | Effect assets (reuse Map's ParticleImage prefabs where possible). |

**No asmdef** — FX scripts compile into the predefined `Assembly-CSharp` (runtime) and
`Assembly-CSharp-Editor` (`Editor/` + `Tests/Editor/`), the same assemblies the existing game code
and the 111 existing EditMode tests use. (A custom asmdef cannot reference `Assembly-CSharp`, so an
asmdef would break the FX compile.)

## Facade API
```csharp
FXHandle FX.Play(string id, RectTransform target);   // burst at a UI element
FXHandle FX.Play(string id, FXTarget.FullScreen);    // fullscreen burst
FXHandle FX.Decorate(string id, RectTransform target); // persistent, parented under target
void     FX.PlaySound(string id);                    // standalone sound
void     FX.Stop(FXHandle handle);                   // stop a decoration / loop
```
- **Burst** = one-shot, rendered on the persistent top Canvas at the target's screen position, auto-cleaned when finished.
- **Decorate** = persistent/looping, **parented under `target`** (so it scrolls + clips with the
  element, e.g. a library tile), removed via `Stop`.
- A `kind == Composite` entry can fire a visual + its `AudioClip` together.

## Backends (route by `kind`/backend type)
- **Particle** (stars/sparks/confetti) — instantiate a `AssetKits.ParticleImage` prefab at the
  target, `.Play()`, destroy on completion. (Reuse the `VPlayParticle`/Map pattern.)
- **SpriteOverlay** (art-directed: fairy dust, a `luma`-matte sparkle, a mascot) — reuse
  `OverlayHost.AddOverlaySprites` as a one-shot (play once, not loop) or looped (for decorations),
  then remove. Driving OverlayHost from C# **sidesteps the MiniScript intrinsic leak** (that leak
  is in `PRScript.SetupInterpreter`, not OverlayHost).
- **Tween** (button highlight/pulse/glow) — DOTween scale-pulse + alpha on the target; loop until `Stop`.
- **Audio** — `FXRuntime`'s own `AudioSource`: `PlayOneShot` for one-shots, a dedicated source for
  loops. Coexists with the existing `AudioPlayer`/`ButtonSound`; does not replace them.

## Cross-scene bootstrap + hooks (the zero-edit wiring)
`FXBootstrap` creates the persistent runtime once, then on each `sceneLoaded` re-subscribes:
- **Puzzle solved** → subscribe (in code) to `PuzzleImage.PuzzleSolvedEvent` on found `PuzzleImage`
  instances → `FX.Play("stars", puzzleRect)`. (No edit to `PuzzleImage` — the event exists.)
- **Correct piece** (optional) → `PuzzleImage` `_onPiecePlaced` / its event (has `isCorrect`).
- **Book done** → in `_Story`, watch the existing `book_done` flip (`Globals.Prefs_Get_Book_Done`
  / `g_prbook.book_done`) and fire once when it becomes 1 → `FX.Play("book_done", FullScreen)`.
  (No edit to `PRScript`.)
- **Library "Lab" decoration** (see below) → on `_Library` load.

## Lab-book decoration (cross-scene decoration, the pilot use case)
Config lives in `FXLibrary.asset` as a small map: `{ bookId (or bookName) → effectId, placement }`.
On `_Library` scene loaded, the bootstrap runs a coroutine that waits for `Globals.g_listPRBooks`
and the target book's `bookViewItem` to be non-null (the grid populates async), then
`FX.Decorate(effectId, labBook.bookViewItem.<RectTransform>)`.
- The tile↔book link is public both ways (`BookViewItem.prBook`, `PRBook.bookViewItem`) — read-only,
  preserving the one-way dependency. No edit to `BookViewItem`/`PRLibrary`.
- The grid is NOT virtualized (tiles cached on `prBook.bookViewItem`; `ClearScrollView`'s `Destroy`
  is commented out → filtering deactivates/reactivates), so a child decoration survives filter
  changes. **Re-attach on each `_Library` entry** (tiles are rebuilt on scene reload).
- First decoration = a "Lab"/pilot bubble on the first voice-engine book; the map makes adding
  more trivial and keeps "which book is special" inside the FX config (no catalog/`PRBook` edit).

## The two spots to get right (test in play mode)
1. The persistent overlay **Canvas + `sortingOrder`** — bursts must draw above everything in every
   scene; decorations parent under their target (NOT the top canvas) so they scroll/clip with it.
2. **Positioning at a target `RectTransform`** across canvas render modes — use
   `RectTransformUtility.WorldToScreenPoint` / `ScreenPointToLocalPointInRectangle`.

## Phasing (ship an MVP, grow it)
- **MVP:** `FXEffect` + `FXLibrary` + `FX` + `FXRuntime` + `FXBootstrap`, **Particle + Audio
  backends only**, two effects (`stars`, `book_done` with a chime), the puzzle-solved + book-done
  hooks. ~5 code files, **zero edits to existing code**.
- **v2:** `Decorate` + the Lab-book map (Library bubble); SpriteOverlay + Tween backends; `FXTrigger`
  + the Editor dropdown; pooling.

## Acceptance
- All FX code/assets under `Assets/FX/`; **no asmdef** (FX in the predefined assemblies); no
  existing `.cs` references an FX type.
- Deleting `Assets/FX/` leaves the project compiling (verify), with at most cosmetic missing-script
  refs where `FXTrigger` was hand-placed.
- Kill-switch disables everything without deletion.
- `FX.Play("stars", rect)` bursts at a UI element in any scene; `FX.Decorate(...)` attaches a
  looping effect to a library tile that scrolls/clips with it; `FX.PlaySound("chime")` plays in any
  scene. Puzzle-solved and book-done fire automatically with no edits to `PuzzleImage`/`PRScript`.
- EditMode suite stays green; FX logic that is pure (id parsing, library lookup, target-rect math)
  gets a couple of EditMode tests.
