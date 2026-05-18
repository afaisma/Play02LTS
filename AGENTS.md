# ReadingBuddy AI Agent Guide

## Project Overview
ReadingBuddy is a Unity children's reading app (iOS/Android) with page-by-page stories, illustrated pages, and synchronized audio narration with word highlighting. Built with Unity 6 + URP 17.3.0, C# scripting under `Assets/_Story/`.

## Architecture
- **Scene Flow**: `_StartScene` (Globals.cs) downloads CSV → navigates to `_Library` (PRLibrary.cs), `_Bookstore`, `_Settings`, or `_Story` (PRScript.cs).
- **Global State**: `Globals.cs` singleton manages `g_listPRBooks` (catalog), `g_prbook` (selected book), `g_scriptName` (story URL), `g_libraryFilter`.
- **Story Interpreter**: `PRScript.cs` parses `.txt` scripts into chunks/events, executes via MiniScript intrinsics. Page turns call `ExecuteStep(index)` → `RunScript()`.
- **Audio/Text Sync**: `AudioAndTextPlayer.cs` handles TTS/audio playback with JSON timings for highlighting. URLs: `{chunkname}_{rate}{voicePostfix}.mp3` for audio, `{chunkname}_{rate}_timings{voicePostfix}.json` for timings.

## Key Workflows
- **Open Project**: Unity Hub → Add project → select directory. Use Unity 6.
- **Debug Console**: QFSW Quantum Console; `[Command]` methods callable at runtime (tilde key). E.g., `SetStep`, `AddStoryStep`, `CleanupStorySteps`, `DisplayMainImage`.
- **Local Dev**: Hardcoded URLs to `http://localhost:8080/api/files/download/stories/` mirror CDN. Configure `Globals.csvUrl` for local/QA/prod.
- **Build/Test**: No CLI; use Unity Editor. Progress persists via `PlayerPrefs` keys `{bookUrl}_page` and `{bookUrl}_done`.

## Conventions & Patterns
- **URL Handling**: Two normalizers in `PRScript.cs` - `NormalizeURL()` prepends base if not absolute; `NormalizeUrl()` fixes `//` artifacts. `PRUtils.DownloadFile()` supports `resources:` scheme for `Resources/` folder.
- **Script Format**: Plain text with `////////[chunk name=...]` delimiters. Intrinsics like `DisplayMainImage("page1.jpg")`, `PlayAudioAndText("page1", "text")`.
- **Adding Commands**: In `PRScript.SetupInterpreter()`, create `Intrinsic` with params and code lambda calling Unity APIs.
- **Voice Modes**: Human (no rate suffix, no highlight), Computer (rate + voice, highlight), Novoice (muted, no highlight).
- **Cache**: `OrderedDictionary` in `AudioAndTextPlayer.cs` (max 30, LRU) for audio/timings; `PRUtils.cacheImages` for images.
- **Navigation**: Next/Prev buttons + swipes on `textforeground`/`gallery` route to `PRScript.NextStep()`/`PrevStep()`. Gallery swipes scroll internally if multi-image.

## Key Files/Dirs
- `Assets/_Story/Story/PRScript.cs`: Interpreter core.
- `Assets/_Story/Players/AudioAndTextPlayer.cs`: Audio/text sync.
- `Assets/_Story/Library/PRLibrary.cs`: Book catalog (defines `PRBook`).
- `Assets/_Story/Utils/PRUtils.cs`: Download/cache utilities.
- `Assets/_Story/Globals.cs`: Global state.
- Third-party: MiniScript (`Assets/MiniScript/`), DoTween, TextAnimator, Azure TTS (`Players/TTS/MicrosoftTextToSpeech.cs`).
