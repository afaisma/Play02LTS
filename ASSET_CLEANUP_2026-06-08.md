# Play6.3 — unused-asset / library cleanup (pre-Recognissimo)

Audit date 2026-06-08. Goal: reclaim space before importing Recognissimo (~266 MB).
Method: build-scene list from `EditorBuildSettings.asset`; code grep for each library's
namespace; GUID cross-reference of each candidate folder's `.meta` GUIDs against all GUIDs
referenced by shipping content (`Assets/_Story`, `Assets/Resources`, `Assets/Settings`).

## IMPORTANT — how to execute safely (read first)
- **Do trims in the Unity editor, not by raw `rm`.** Unity tracks assets by the GUID in each
  `.meta`; moving an asset *inside* the project (Project window) preserves every reference.
- **The "mostly-dead" packs are only *mostly* dead — each has a few entry-point assets that ARE
  used, and those entry assets pull their own dependency closure inside the pack.** So the safe
  move is: right-click the keeper asset(s) → **Select Dependencies** → move that closure into a
  small kept folder → delete the rest of the pack. Do **not** keep only the single file named
  below and delete its siblings — you'll break its texture/mesh deps.
- The GUID scan catches scene/prefab/material references. It does **not** catch
  `Resources.Load("string")` or Addressables. None of the big packs live under a `Resources/`
  folder (checked), so risk is low — but **verify after each delete**: do a full build and watch
  the Console for "missing reference" / a pink (missing-material) object, especially in `_Map`.

---

## Tier 1 — zero-risk, delete now (~15 MB)
Pure vendor demo/example/sample content, none of it in the build, none referenceable by the app:

| Folder | Size |
|---|---|
| `Assets/TextMesh Pro/Examples & Extras` | 6.8 MB |
| `Assets/AssetKits/ParticleImage/Demo` | 4.2 MB |
| `Assets/Plugins/Febucci/Text Animator/Example` | 2.6 MB |
| `Assets/Plugins/QFSW/Quantum Console/Source/Demo Scene` | 768 KB |
| `Assets/Plugins/Demigiant/DOTweenPro Examples` | 292 KB |
| `Assets/_Recovery` (Unity auto-recovery scratch) | 228 KB |
| demo scenes inside Buttons / Simply App Rating / InfinityPBR / PJFX | small |

Deleting a whole demo folder (with its `.meta`s) is safe even via filesystem. Keep the parent
library — only the `Demo`/`Example` subfolder goes.

## Tier 2 — the real wins, dependency-aware trim in Unity (~140 MB)
These packs are 3D/VFX asset libraries that a 2D reading app barely touches. Each is almost
entirely unused; only the listed entry asset(s) are wired into shipping content (likely the
VAPI/`_Map` scene). Keep each entry asset's dependency closure, drop the rest.

| Pack | Size | Entry assets actually referenced | Action |
|---|---|---|---|
| `Assets/InfinityPBR` | **72 MB** | `Magic Spells & Particles/Textures/Materials/lighting Bolt Big.mat` (1 of 485 assets) | keep that material + its texture deps; drop ~71 MB |
| `Assets/PJFX` | **46 MB** | `ShinyItems/Prefabs/Item_Sphere.prefab`, `ShinyItems/Assets/FX_Materials/FX_RoundGlow_Add_10.mat` (2 of 116) | keep those + their deps; drop ~45 MB |
| `Assets/Buttons` | **26 MB** | `PNG/27Button_Long_Red.png`, `PNG/11Button_Midl_Blue.png`, `PNG/26Button_Long_Blue.png` (3 of 39) | keep the 3 PNGs (+ any atlas dep); drop ~25 MB |

Recommended editor recipe per pack: select the entry asset(s) → Select Dependencies → move the
selection into e.g. `Assets/_Story/Art/_kept/<pack>/` → delete the original pack folder → build →
confirm `_Map` and any VFX still render.

## Tier 3 — package manifest (low build impact, cleaner project)
No code references these and the domain (a children's reader) doesn't need them. Remove from
`Packages/manifest.json`, let Unity resolve, recompile:

- `com.unity.netcode.gameobjects` — multiplayer netcode. **High confidence** (no `Unity.Netcode`/`NetworkBehaviour` in code).
- `com.unity.multiplayer.tools` — **High confidence**.
- `com.unity.multiplayer.center` — **High confidence**.
- `com.unity.ai.navigation` — NavMesh surface package. **Verify** (no NavMesh code; the `NavMeshSettings` blocks in scenes are Unity's default empty header, not real usage). Remove and confirm it still compiles/builds.

(These mostly trim editor/import overhead and dependency surface; Unity already strips unused
package code from player builds, so don't expect device-build shrinkage from Tier 3.)

---

## Confirmed USED — do NOT remove
- `MiniScript` — story interpreter (core).
- `DOTween` (Demigiant) — UI animation.
- `Febucci Text Animator` — `RandomMessageGenerator.cs`.
- `TextMesh Pro` (library, not the Examples folder) — all text rendering.
- `QFSW Quantum Console` (library, not the Demo Scene) — `[Command]` debug console used across `_Story` code.
- `AssetKits/ParticleImage` (library, not its Demo) — `VAPI/VPlayParticle.cs`, `VAPI/MapManager.cs` create particles at runtime (0 GUID refs but live code refs — keep).
- Azure TTS (`MicrosoftTextToSpeech.cs`).

## Net
- Tier 1: ~15 MB, do immediately, no risk.
- Tier 2: ~140 MB, the headline — needs ~30 min of careful in-editor dependency trimming + a verification build.
- Tier 3: dependency-surface cleanup; remove the 3 multiplayer/netcode packages with confidence, verify `ai.navigation`.
Combined, well over the ~266 MB Recognissimo adds back, and removes 3D/VFX/multiplayer weight that has nothing to do with the app.
