# URP Render Graph crash on page turn — diagnosis & fix

**Symptom:** turning a story page intermittently throws and freezes the reader:
`Render Graph Execution error` → `IndexOutOfRangeException` in
`PostProcessPassRenderGraph.RenderUberPost` → `RenderGraphResourceRegistry.GetTextureResource`.

## Diagnosis (confirmed — NOT a project misconfiguration)
Investigation ruled out every local cause:
- `_Story` has a **single** camera (`Main Camera`, Renderer2D); **Post Processing is OFF**; camera
  **stack is empty**. No leftover/DontDestroyOnLoad/runtime cameras in code.
- The `DefaultVolumeProfile` overrides are all **neutral** (saturation 0, contrast 0, filter white)
  — Unity's stock profile, no visible effect.

This matches a **known, open Unity bug** in URP's Render Graph (URP 17.x, Unity 6.0–6.3, all
platforms): intermittent `IndexOutOfRange`/`already released` in the UberPost pass. A Unity
engineer attributed it to *"the HDR UI overlay texture in the post-process stack."* It is not
fixed as of late-2025 forum reports. Refs:
- https://discussions.unity.com/t/render-graph-execution-error-indexoutofrangeexception-in-urp/1639786
- https://discussions.unity.com/t/render-graph-execution-error-in-ui-post-processing/1700907

**Compatibility Mode is NOT an option here** — it's hidden in this Unity version and only reachable
via an unsupported `URP_COMPATIBILITY_MODE` Player define (per the Graphics settings warning).

## Tried and ruled out
- **HDR off** (`m_SupportsHDR: 0` on `UniversalRP.asset`) — did NOT stop the crash. (Harmless for
  a 2D book; can stay off or be reverted.)
- **Global `m_PostProcessData` removal on `Renderer2D`** — NOT safe: `_Map` and `_Message` each
  use a post-processing Volume (shared profile `eb66803a…`), and there is a single shared renderer
  (`Renderer2D`, the only entry in `UniversalRP.asset` m_RendererDataList). Stripping it would kill
  their post-processing too.

## Fix — give `_Story` its own renderer with NO post-process data (targeted, deterministic)
The reading view uses no post-processing (camera PP already off), so removing the post-process pass
*for `_Story` only* eliminates UberPost there without touching `_Map`/`_Message`.
1. Duplicate `Assets/Settings/Renderer2D.asset` → e.g. `Renderer2D_NoPost.asset`. On the copy, set
   **Post Process Data = None** (clears `m_PostProcessData`).
2. Add the new renderer to `Assets/Settings/UniversalRP.asset` → **Renderer List** (so it has an
   index, e.g. 1).
3. On the **`_Story` scene `Main Camera`** → Rendering ▸ **Renderer** dropdown → select
   `Renderer2D_NoPost`. (Leave `_Map`/`_Message` cameras on the original.)
4. **Verify:** page the whole pigeon book (Title → The End), forward and back, repeatedly — expect
   no `Render Graph Execution error`. Confirm `_Map`/`_Message` still look correct.

## Otherwise
It's an open Unity bug — alternatively defer (the book reads correctly end-to-end; the crash is an
intermittent engine glitch) and pick up a Unity/URP patch when one ships.

## Secondary (only if the above is insufficient)
- Update the Unity Editor + URP package to a newer 6000.x once Unity ships a Render Graph fix for
  this (watch the threads above / the URP changelog).
- Keep the `OverlayHost` end-of-frame RenderTexture-release hardening in mind for *video* overlays
  (defer `rtex.Release()/Destroy()` to end of frame) — not this crash's trigger (the pigeon book
  has no video), but a defensible general safeguard.

## Scope
Settings only (HDR flag). Do **not** change the story interpreter, catalog, or FX logic. Fully
reversible (re-tick HDR). Note the change in the project so HDR isn't silently re-enabled.
