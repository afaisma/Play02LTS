# Bug: buttons on the `_Message` scene do nothing ("missing script" warning)

**Status:** Diagnosed, fix not yet applied
**Decision:** Rewire or remove (do NOT restore the deleted script)
**Date:** 2026-05-30
**Severity:** Medium — two buttons in a build-included scene are dead
**Component:** `Assets/_Story/Rooms/_Message.unity` (also affects `_Map.unity`, `_Recovery/0.unity`)

> Diagnosis only. The fix is a scene/asset change and belongs in Claude Code.

---

## Symptom

Editor warning on scene load: *"The referenced script (Unknown) on this Behaviour is missing!"* Two buttons in `_Message` no longer do anything when clicked.

## Root cause

Two buttons have their `OnClick` wired to methods on a component of type **`AVScene`**, but the script `Assets/_Story/drawing/IRV/AVScene.cs` was **deleted in commit `587f204` ("WIP", 2023-07-13)**. The MonoBehaviour reference is now dangling, so Unity reports a missing script and the buttons' `OnClick` calls resolve to nothing.

`AVScene` was an early experimental scene controller (image cache + positioning sprites at screen corners) that predates `PRScript`/VAPI. There is **no current equivalent** of its `RunScript`/`CleanUp` methods, and the GameObject hosting it is inactive scaffolding. These buttons have effectively been dead since 2023.

## Exact references (in `_Message.unity`)

| Item | GameObject | fileID | Wiring |
|---|---|---|---|
| Orphaned component | `VScene` (inactive, `m_IsActive: 0`) | MonoBehaviour `&1944682717` on GO `&1944682716` | missing script, guid `9c4252891488740caa386f9f010ed42d` (`AVScene, Assembly-CSharp`) |
| Button #1 | `RunEmbedded` | `&162837533` | `OnClick` → `AVScene.RunScript` on `&1944682717` (scene line ~655) |
| Button #2 | `ButtonClear` | `&223290756` | `OnClick` → `AVScene.CleanUp` on `&1944682717` (scene line ~788) |

Same dangling guid (`9c42528…`) also appears in:

- `Assets/_Story/Rooms/_Map.unity`
- `Assets/_Recovery/0.unity` (recovery copy — likely safe to ignore)

## Fix (chosen direction: rewire or remove)

**Recommended: remove the dead scaffolding**, since `AVScene` is obsolete and has no live replacement:

1. In `_Message.unity`, delete the orphaned `VScene` GameObject (`&1944682716`) carrying the missing-script component `&1944682717`.
2. On buttons `RunEmbedded` (`&162837533`) and `ButtonClear` (`&223290756`): remove the dead `OnClick` persistent calls that target `&1944682717`. If these buttons have no other purpose in the live `_Message` flow, remove the buttons too.
3. Repeat the same cleanup in `_Map.unity` (same orphaned guid).
4. Leave `_Recovery/0.unity` alone unless you're also pruning the recovery folder.

**Alternative — rewire** (only if `RunEmbedded` / `ButtonClear` are still meant to function): repoint their `OnClick` to the current controller's equivalent methods (e.g. on the VAPI `VVScene` / `MapManager` present in these scenes) and delete the orphan `VScene` component. Pick this only if the buttons have a real purpose in the current UX — otherwise prefer removal.

> Do not restore `AVScene.cs` from git — per decision, the deleted prototype should not come back.

## Notes / caveats

- `_Message` **is** in `EditorBuildSettings`, so this scene ships; worth confirming whether `_Message` is actually reachable in the live app or is leftover, which informs whether to remove the buttons entirely vs just their dead wiring.
- Editing `.unity` YAML by hand is error-prone; doing this in the Unity Editor (delete the flagged GameObject, clear the `OnClick` rows) is safer than a text edit. Either way, verify the scene still loads without the warning afterward.

## Verification after fix

- Open `_Message` (and `_Map`) in the Editor: the "missing script" warning is gone.
- Confirm no remaining `OnClick` entries reference fileID `1944682717` / guid `9c4252891488740caa386f9f010ed42d` (`grep` the scene files).
- If buttons were rewired rather than removed, click them in Play mode and confirm the intended behavior.
