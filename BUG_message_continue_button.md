# Bug: `_Message` "Continue" button shows "button" and does nothing

**Status:** Diagnosed, fix not yet applied
**Date:** 2026-05-31
**Severity:** High — blocks the user from leaving the launch/info screen
**Component:** `Assets/_Story/Story/Globals.cs` (loading-button configuration) + `_Message` scene
**Decision:** Label it **"Enter Library"**; clicking it goes **straight to the Library**

> Diagnosis only. The fix is a C# change and belongs in Claude Code.

---

## Symptom

On the `_Message` launch/info screen, the large button is labeled with the placeholder **"button"** and nothing happens when tapped.

## Root cause

The button is `ButtonLoadingRetryContinue` (scene `_Message.unity`, GO `&1554775494`, fully active under an active `Canvas`). It has **no persistent `OnClick`** in the scene by design — `Globals` is supposed to configure both its label and its click handler at runtime. The problem is *when* that configuration happens:

- `Globals.TryBindLoadingButton()` (Globals.cs:144) binds the field via `GameObject.Find("ButtonLoadingRetryContinue")`, triggered from `Awake`'s `SceneManager.activeSceneChanged` hook (Globals.cs:131) and once in `Start` (Globals.cs:161). Binding works — but binding only stores the reference; it does **not** set text or `onClick`.
- The button's **label and `onClick` are only ever set inside the one-shot CSV download flow** (`DownloadCSV`, Globals.cs:~590): on success it sets text → "Continue" and `onClick` → `LoadTargetScene`; on failure → "Connect to the Internet and Retry" + `RetryDownload`. `Start` separately sets "Loading Library Catalog" + `interactable=false`.
- `PreLoadBooks` only downloads when `g_listPRBooks == null`. Since `Globals` now boots once at app launch (DontDestroyOnLoad, BeforeSceneLoad), by the time `_Message` is shown the catalog is usually **already loaded**, so the download does **not** re-run, and nothing re-applies the "Continue" text/handler to `_Message`'s freshly-loaded button.

Result: on `_Message`, the button is bound but never configured — it keeps its scene placeholder label "button" and an empty `onClick`, so it's inert. This is the same regression class as the jump-to-Library bug: button state was implicitly coupled to the now-one-shot, app-launch download flow.

## Fix

Decouple button configuration from the one-shot download: configure the button **from current state every time it is (re)bound**, and apply the chosen behavior.

In `TryBindLoadingButton` (or a new `ConfigureLoadingButton()` called right after a successful bind and on `activeSceneChanged`), set state based on status:

- **Catalog ready** (`g_listPRBooks != null` and not downloading): label **"Enter Library"**, `interactable = true`, `onClick` → go to the Library. Use the existing guarded `Globals.Library()` (Globals.cs:341, which calls `Navigation.GoToLibrary()` when not downloading), or `Navigation.GoToLibrary()` directly. **Per decision, always go to the Library — do not use `LoadTargetScene`/`targetScene` for this button.**
- **Downloading:** label "Loading Library Catalog", `interactable = false`.
- **Download failed:** label "Connect to the Internet and Retry", `interactable = true`, `onClick` → `RetryDownload`.

Always `onClick.RemoveAllListeners()` before adding, to avoid stacking handlers across scene changes. The button should never display the raw scene placeholder — code should set its label on every bind. (Optionally also change the scene's default TMP label off "button" so it never flashes, but the code path is the real fix.)

## Files / lines

- `Assets/_Story/Story/Globals.cs`
  - `Awake` `activeSceneChanged` hook — line ~131
  - `TryBindLoadingButton` — lines ~144–150 (extend to configure, or call a new configure method here)
  - `Start` initial button setup — lines ~161–168
  - `RetryDownload` — line ~331; `Library()` — line ~341
  - `DownloadCSV` success/failure button wiring — lines ~577–596 (move the shared config into the reusable method so all paths agree)
- `Assets/_Story/Rooms/_Message.unity` — button `ButtonLoadingRetryContinue` `&1554775494` (no scene change required if handled in code; optionally update its placeholder label)

## Verification after fix

- Cold start into `_Message` with the catalog already cached: button reads **"Enter Library"** and tapping it goes to the Library.
- Re-enter `_Message` after navigating away: button is still correctly labeled and functional (no stale/destroyed reference, no stacked listeners).
- Offline / failed download: button shows the retry text and retries on tap; while downloading it shows the loading text and is disabled.

## Not in scope here

The other `_Message` buttons `RunEmbedded` / `ButtonClear` (wired to the deleted `AVScene` script) are a separate issue — see `BUG_message_scene_button_missing_script.md`.
