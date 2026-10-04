# ReadingBuddy — Bug Findings (Round 6, 2026-10-04)

*Code reading of the whole runtime (Story, Players, Utils, Home, Library, Settings, tools), then a check of every finding against the Editor or against the published books. "Reproduced" = seen in Play mode in the Editor on the live QA catalog (90 books). "From code" = proven by reading, not run. Round 5 items O-1 … O-10 are all still open in the code; they are not repeated here.*

## Fixed in this round (3 small changes, not committed yet)

Verified after the change: 257/257 EditMode tests, and each repro below re-run in Play mode. R6-1 and R6-2 were then checked a second time as an A/B: the committed (old) files restored, bug reproduced through the real picker-close path and with an unreachable audio host (the same error as airplane mode); fixed files put back, same steps, bug gone; plus regression checks (Autopage still turns pages when narration plays; a multi-picture page with sounds keeps its pictures and sounds on Replay; Replay with the puzzle on resets the puzzle). All 1451 narrated pages of the 90 published books have their audio file, so the R6-1 fix cannot hold a normal page. **Not yet run: the iOS Simulator smoke.**

### R6-1. Autopage raced through a book when the narration could not be loaded — reproduced
`Assets/_Story/Players/AudioAndTextPlayer.cs` (LoadAudioAndTimings). When the audio fetch failed (offline and the page not in the disk cache, a missing file, a timeout), `audioSource.Play()` did nothing, the highlight loop ended at once, and Autopage (on by default) turned the page 0.5 s later. Repro: Puss in Boots, narration folder pointed at a missing folder — 46 pages in 70 s with no sound, book marked finished, Read-next sheet up, saved page lost. Fix: Autopage only fires when a narration clip exists (`hasNarration`). After the fix the page stays; with audio restored Autopage works as before.

### R6-2. Replay / closing the reading-mode picker added the page picture a second time — reproduced
`Assets/_Story/Story/PRScript.cs` (ReplayCurrenStep). A page turn clears the gallery (`StoryStepsUI.SetStep`), a replay did not, so every `AddGalleryImage` was appended again. 68 of the 90 published books use `AddGalleryImage` without `DisplayMainImage`, and the picker opens on every book start and replays on close — so the first page of those books always had the picture twice: the gallery arrow appeared at the right edge and the first swipe on the picture only moved to the duplicate. Each Replay tap added one more. Fix: `gallery.clearUpGalleryItems()` before the re-run (the same call a page turn makes).

### R6-3. The "This book had a hiccup" screen's Home button did nothing and left a blank screen — reproduced
`Assets/_Story/Story/PRScript.cs` (ShowKidSafeScriptError). `HomeButton.Create` already wraps its action in `TapFeedback.TapThenGo`; it was given `Home()`, which calls `TapThenGo` again, and the latch drops the nested call. Result: the full-screen NavCover stays up (Round 5 O-4) and the child must kill the app. Player builds only (the Editor shows the developer dialog instead), so it was never seen. Fix: pass `HomeNow`.

Also: `tools/sim_smoke.plan` opened `FarmAnimalsRhymebook`, which was hidden from the catalog on 2026-10-03. Changed to `VehiclesRhymebook`.

## Open — verified, not fixed

### R6-4. Every Library visit builds all 90 rows and loads all 90 covers — reproduced
`Assets/_Story/LIbrary/PRLibrary.cs:126-142`, `BooksScrollView.cs:229-233`. `AddBooks` calls `ShowBooks` with the empty default filter before `SetFilter(g_libraryFilter)` runs. Measured on the Science shelf: 9 rows visible, 90 rows created, 87 covers loaded = 204 MB of textures, all held by hidden rows (the image cache never evicts a sprite an Image still holds, so the 96/160 MB budget does not apply). Fix (2 lines): give the scroll view its filter before `AddBooks`.
Content side, no app release needed: 31 covers are 1536x1024 (6 MB decoded each) and one is 2700x2700 (`TVStory/images/alexandr-vstrecha-psd.jpg`, 28 MB); 50 are 128-150 px. All covers decoded = 223 MB. Resizing covers to about 512 px in the publish step brings that to about 25 MB.

### R6-5. The disk cache is in Documents, so iOS backs it up to iCloud — from code
`Assets/_Story/Utils/DiskCache.cs:35` (`Application.persistentDataPath/cache`, up to ~284 MB of re-downloadable images and audio). Apple's data storage rule says such data must not be backed up; it also uses the family's iCloud quota. Fix: `UnityEngine.iOS.Device.SetNoBackupFlag(Root)` once, behind `#if UNITY_IOS && !UNITY_EDITOR`.

### R6-6. The read-along microphone can stay on after the child leaves — from code (Recognissimo source checked)
`Assets/_Story/Players/ReadAlongService.cs:346-356`. `Stop()` only calls `StopProcessing()` when `_recognizing` is true, and that flag is set by the asynchronous `Started` event (first start loads the Vosk model: seconds). Pick "I read it myself", then tap Home during that window: `Stop()` does nothing, the recognizer then starts, and the mic stays live until the next book opens. Same family as Round 5 O-2 and O-3; one fix covers all three: stop in `Stop()` whenever the recognizer state is not Inactive, call it from `NextStep`/`PrevStep` and `OpenPicker`.

### R6-7. Closing the "No internet" card only lasts until the next scene — from code + scenes
`Assets/_Story/Utils/NetworkStatus.cs:14`. The dismiss latch is an instance field and the prefab sits separately in 5 scenes, so the card comes back within 5 s in every scene. Fix: make the field `static`.

### R6-8. A gallery sound tapped on one page can start playing on the next — from code
`Assets/_Story/Story/SoundBar.cs:30-69`. `Clear()` stops the AudioSource but not the download coroutine. 5 published books use `AddGallerySound`.

### R6-9. A finished book loses its Library dot when it is opened again — from code
`Assets/_Story/LIbrary/BookViewItem.cs:48`. A finished book always reopens at page 1, which saves page 0, and the dot is shown only when `currentPage != 0`. Fix together with Round 5 O-6. Related: the `_done` flag is never cleared, so a finished book never resumes mid-book again.

### R6-10. `tools/sim_smoke.sh` reports "ok" when steps failed — from code
`run` sets `RESULT=ok` when the log has a `done` line, even if steps logged `ERR`; `all` and `smoke` always end with `finish ok 0`. Until fixed, read the `_log.txt` for `ERR` lines after every smoke.

## Latent — not reachable with today's catalog
- Age chips hide a book whose `age_to` is 0 (`BooksScrollView.cs:74-76`). Today every book has `age_to` 4-12.
- `Globals.defaultAudioRateFromPRBook`: `age_from` outside 2-5 falls to rate -30. Today `age_from` is 2-5 for every book, and every book has its default-rate audio.
- `ListenFor` plus "I read it myself" would run two microphones. No published book uses `ListenFor`.
- A timings fetch that fails while the audio succeeds is cached for the session (no highlight on that page until restart).
- `BuildStamp` reads git after bumping the counter, so every build is stamped dirty (`*`).

## Reported by the code review, not checked in the Editor yet
- Puzzle mode stretches a square picture on 4:3 tablets (`PuzzleImage.LayoutSlotsAndPieces` uses the full rect).
- A sideways drag of a puzzle piece may turn the page (`SwipeDetector` does not exclude `PuzzlePieceUI`).
- `ParentalGate` keeps the correct answer in the field after a successful check.

## Simulator smoke of the three fixes — 2026-10-04, passed
Build "3.1.0 · build 314 · 7a5a78a*". Four devices (iPhone SE 3rd generation, iPhone 17 Pro Max, iPad mini A17 Pro, iPad Pro 13-inch M5): every log has 41 `ok` lines, 0 `ERR`, ends with `done`. The new capture `10b_after_picker_closed` shows one picture and no gallery arrow on all four. Memory footprint 300-320 MB, as before. Contact sheet: `Build/_smoke/smoke_2026-10-04_sheet.jpg`.

### R6-11. The Simulator export fails when the Editor's active build target is not iOS — seen in this run
`Assets/_Story/Editor/RbSimSmoke.cs`: `RbSimStubs` (the Recognissimo stubs for the Simulator) is inside `#if UNITY_IOS`, so it is compiled only when the active target is already iOS. With Android active the export succeeds but Xcode fails (exit code 65, undefined `_Recognissimo_*` symbols). This run: the 19 stubs were added by hand to `Build/iOS-sim/Libraries/Plugins/iOS/MemFootprint.mm` (export folder only, not in git). The export also left the Editor's active target on iOS. Fix later: the smoke tool should switch the target to iOS first, or stop with a clear message.
