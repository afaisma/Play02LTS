# Overlay system (OverlayHost + MiniScript) — technical analysis

**Date:** 2026-06-10 (Cowork). Read: `OverlayHost.cs` (1,233 lines), `OverlayDragHandler.cs`,
`Gallery.cs` pass-throughs, the ~17 overlay intrinsics in `PRScript.SetupInterpreter()`, the
`[event NAME TARGET]` dispatch path, and content usage (`Sea_Story_ru`, `TestBook/Animals_Rhymebook`).
Complements `ReadingBuddySuite/ReadingBuddy_ideas_recap.md` §2 (product view); this is the
engineering view.

## Architecture (how it fits together)

```
story .txt script (MiniScript)
  AddOverlayVideo/Sprites/Picture, SetOverlayVideo(prop), Show/Hide/Toggle,
  SetOverlayPosition / AnimateOverlayTo / StopOverlayAnimation,
  Schedule / CancelSchedule, AddVideo+PlayVideo (persistent segment pattern)
        │ (intrinsics, PRScript)
        ▼
StoryStepsUI ──► Gallery (thin pass-throughs) ──► OverlayHost (all logic)
                                                    │ onOverlayEvent(evName, target)
        ◄───────────────────────────────────────────┘
PRScript.DispatchEvent → [event NAME TARGET] block (specific) → [event NAME] (generic)
                          with MiniScript globals: target, nCurrentStep, nSteps
```

- One polymorphic registry: `Dictionary<string, OverlayEntry>` with three subtypes — video
  (RenderTexture + VideoPlayer), sprite-sequence (frames from `manifest.json` + NNN.png), static
  picture. Shared lifecycle: named or `__anon_N` keys, duplicate-name replace, `persistent` flag
  to survive page-change `Clear()` (the Sea_Story AddVideo/PlayVideo pattern).
- Event loop authoring works: `TestBook` butterflies implement a full ambient behavior (5 sprite
  overlays, randomized self-rescheduling `Schedule "flyButterfly"` loops, `onTap` freeze +
  resume) in ~40 lines of MiniScript. The model — C# primitives, MiniScript choreography — is
  sound and exactly what the "ambient packs" roadmap item needs.

## Quality: genuinely good defensive engineering

White-flash prevention (RT cleared to transparent, Images alpha-0 until content arrives);
frame downloads throttled to 8 in-flight (same CloudFront/WAF lesson as library covers);
prepare-race handling (pending segment parked and applied in `prepareCompleted`;
`PauseAfterOneFrame` bails if a segment armed meanwhile); seek robustness (polls `vp.time`,
`seekCompleted` documented unreliable); teardown discipline (DOTween.Kill before destroy,
RenderTexture Release+Destroy, per-frame Texture2D+Sprite disposal, scheduled-callback
cleanup, coroutine null-guards after every yield). This is the most carefully-written
subsystem in the app.

## Findings (ordered by importance to the enrichment roadmap)

### 1. The MiniScript intrinsic leak becomes a real problem at ambient-pack scale
Every `onTap` and every `Schedule` callback runs `PRScript.DispatchEvent` →
`SetupInterpreter()` → ~50 `Intrinsic.Create` into MiniScript's append-only static list
(see `BUG_INTERPRETER_INTRINSIC_LEAK.md`, currently rated low-priority). The TestBook
butterfly pattern fires a scheduled event every ~1–3 s **per overlay** — 5 butterflies ≈
2–5 dispatches/sec ≈ 100–250 leaked intrinsic objects/sec plus a parse+compile each.
A child sitting on an ambient page for 10 minutes ≈ low-hundreds-of-thousands of leaked
objects + constant GC churn on low-end devices. **Re-rate that bug to medium-high and fix
it before shipping ambient packs.** (Fix is already specified in the bug doc.)

### 2. No disk cache for overlay assets
Sprite manifests/frames and videos use raw `UnityWebRequest` — unlike page images and audio,
which go through `DiskCache`. Every page revisit re-downloads every frame (a 5-butterfly page
= ~300 PNGs). Cost: CDN transfer, page-entry latency, offline breakage. Route frame/manifest
fetches through `DiskCache` (same pattern as `PRUtils.DownloadImage`), or adopt the
ship-the-mp4-decode-on-device idea (recap §2.4) which solves size and caching together.

### 3. GPU memory for sprite sequences is unbudgeted
Frames become uncompressed RGBA32 `Texture2D`s at native PNG size. 61 frames × 5 butterflies
at e.g. 512² ≈ ~300 MB *if* frames are large; nothing downscales or compresses. Disposal on
page change is correct, so it's a per-page peak, not a leak — but a single ambitious page can
OOM an older iPad. Needs either a frame-size convention in the SceneForge sprite pipeline
(matte step already does a global crop — add a max-dimension) or downscale-on-load.

### 4. Smaller correctness items
- **Drag vs. animate position model conflict:** `OverlayDragHandler` moves
  `anchoredPosition`; `SetOverlayPosition`/`AnimateOverlayTo` set anchors and leave any drag
  offset in place — a dragged-then-animated overlay ends up displaced by the stale drag
  delta. Fix: zero `anchoredPosition` when setting/tweening anchors.
- **`OverlayHost` has no `OnDestroy`** (prior audit, still true): scene unload with live
  overlays orphans runtime RenderTextures/Textures until Unity's next unused-asset sweep. Low.
- **`SetOverlayVideo` is misnamed** — it's the generic property setter for all overlay types
  (the C# side is correctly named `SetOverlayProperty`). Cosmetic, but it confuses authoring;
  consider an alias intrinsic `SetOverlayProperty` and keep the old name for compat.
- **Update() loop** iterates the registry every frame even with zero video overlays — trivial
  today; revisit only if hosts multiply.

### 5. Adoption gap (confirms recap §2)
Production content uses essentially none of this: in `stories_02`, only `Sea_Story_ru` (the
AddVideo/PlayVideo segment pattern). The butterfly showcase lives only in
`FileServer/uploads/stories/TestBook/`. The engine is built and hardened; the content isn't
using it. The recap's "ambient packs" pilot is the right unlock — gated on findings 1–3.

### 6. Documentation drift
`CLAUDE.md`'s intrinsic list omits the entire overlay API (AddOverlay*, SetOverlayVideo
properties incl. draggable/tappable/tapPlayback/persistent, Show/Hide/Toggle, position/
animate, Schedule/CancelSchedule, LoopAudio/StopAudio/IsAudioPlaying) and the
`[event NAME TARGET]` two-level dispatch convention. TestBook's inline comments are currently
the best (only) authoring reference. Worth a doc pass before non-engineers author content.

## Recommended order (pre-requisites for the ambient-pack pilot)
1. Fix the intrinsic leak (existing bug doc; re-rated by finding 1).
2. DiskCache for sprite frames + manifests (finding 2) — or decide the mp4 delivery question.
3. Frame-size convention in the sprite pipeline + one on-device memory test of a 5-overlay page (finding 3).
4. One-line fixes: drag/animate offset zeroing; CLAUDE.md intrinsic catalog update.
5. Then ship the ambient-pack pilot on 2–3 books and measure (CloudFront logs) whether kids linger on enriched pages.
```
