# ReadingBuddy — how the code works
Moved out of CLAUDE.md on 2026-10-31 and checked against the code the same day. CLAUDE.md holds the rules; this file holds the explanations.

## Opening the project
Unity Hub → Add project → this directory. The exact Unity version and editor path are in /Users/alexanderfaisman/dev/_hub/UNITY.md. In-game debug console: Quantum Console (Assets/Plugins/QFSW); methods marked `[Command]` are callable at runtime (tilde key), e.g. `SetStep`, `AddStoryStep`, `CleanupStorySteps`, `DisplayMainImage`.

## Scene flow and global state
```
_StartScene (Globals.cs)
  └─ downloads the edition's catalog (stories.json / stories.test.json) → g_listPRBooks →
       Edition.Current.homeScene: _Home (ReadingBuddy rooms) or _HomeTalk (Story Comprehension book list)
       _Library  (PRLibrary.cs)     — book browser          _Settings, _Parents
       _Bookstore (PRBookstore.cs)  — external store links  _Map, _Message, _Welcome, _LearnToRead
       _Story    (PRScript.cs)      — the reading experience
```
`Globals.cs` is a `DontDestroyOnLoad` singleton — the single source of truth between scenes:
- `g_listPRBooks` — the catalog; `g_prbook` — selected `PRBook`; `g_scriptName` — URL of the story script; `g_libraryFilter` — genre filter restored on Library re-entry.
- `Globals.GotoPrBook(book)` is the ONE entry point into a story (library row, Home rail, door, read-next, Nav addresses all funnel here); it stamps recent-reads, cancels pending startup navigation and loads `_Story`.
- Book progress in `PlayerPrefs`: keys `{bookUrl}_page` and `{bookUrl}_done`.
- `getReadingRate()` → `ReadingRateFor(book)`: when `Edition.readingSpeedByAgeGroup` and the adult's toggle allow it, the rate follows the book's age group (ageFrom 2 → "-20", 3 → "-10", 4 → "0", 5 → "10"); otherwise the Settings slider (`g_Rate` pref, default `Edition.defaultReadingRate`). Values: -30, -20, -10, 0, 10.

## Catalog and PRBook
`ParseCatalog(url, content)` dispatches by extension: `.json` → `ParseJSON` (the normal path), anything else → `ParseCSV` (kept as rollback, untouched). `PRBook` (defined in PRLibrary.cs) fields: bookName, bookAuthor, bookImageUrl, bookUrl, ageFrom, ageTo, genre, notesForParents, id, level, phonicsFocus, action (non-empty = navigation tile, not a book), contentRev (catalog hash, folded into media URLs as `?v=`), tags (lower case, e.g. `questions-a`), added (publish date, drives the "new" filter), voices (e.g. `["human","tts"]`), readToMe, bookStoreUrlPrinted, bookStoreUrlKindle. `bookFullUrl` = `bookUrl`, prefixed with `Globals.baseURL` (the catalog's CDN directory) when not absolute.

## Story interpreter — PRScript.cs
The `_Story` scene controller. On `Start()`:
1. reads `Globals.g_scriptName`, downloads the script text via `PRUtils.DownloadFile()`;
2. `parse()` splits it into scriptlets (chunks) and events on `////////[chunk name=...]` and `////////[event NAME [TARGET]]` lines;
3. one `storyStepsUI.AddStoryStep()` per chunk; the preamble before the first chunk runs once;
4. `SetupInterpreter()` registers all MiniScript intrinsics (52 today); `Assets/_Story/Dialogue/DialogueCommands.cs` registers the 10 `Dialogue*` intrinsics.
Each page turn: `ExecuteStep(index)` → `RunScript(scriptlet.Content)` in the MiniScript interpreter.

Script format (plain text on the CDN beside the media):
```
VoiceOptions(human:1, computer:1, novoice:1)      // preamble, runs once

////////[chunk name=page1]
DisplayMainImage("page1.jpg")
PlayAudioAndText("page1", "Once upon a time...")

////////[event OnExecuteStep]
// runs on every step change
```

Adding a command — in `PRScript.SetupInterpreter()` (or DialogueCommands.cs for dialogue):
```csharp
f = Intrinsic.Create("MyCommand");
f.AddParam("param1", "default");
f.code = (context, partialResult) =>
{
    string value = context.GetVar("param1").ToString();
    // call into Unity here
    return new Intrinsic.Result(ValNumber.one);
};
```
The command is immediately available in story `.txt` files.

Navigation: Next/Prev buttons and left/right swipes on the `textforeground` or `gallery` objects route through `PRScript.NextStep()` / `PrevStep()`. A gallery swipe only turns the page when there is a single gallery image; multi-image galleries scroll internally first.

## Audio + highlighting — AudioAndTextPlayer.cs
The most complex component; entered via the `PlayAudioAndText(chunkname, content)` intrinsic.
- URL construction: see CLAUDE.md (computer `{chunk}_{rate}{voicePostfix}.mp3` + `{chunk}_{rate}_timings{voicePostfix}.json` from the shared `TtsUrls` helper; human `{chunk}.mp3` + `{chunk}_timings.json`; `staticText` → `{chunk}.json`; everything cache-busted with `?v={contentRev}`).
- Timings JSON: array of `{word, time}`, time in milliseconds; spaces and punctuation are separate tokens and are never highlighted (`IsWordPunctuation`).
- Rendering: two stacked TextMeshPro layers — `uiForeground` paints the active word with `<color=#...>`, `uiBackground` with `<mark=#...>`. Both strings are rebuilt every frame while audio plays. The active word index only moves forward; `audioSource.time * 1000 - 500` gives a 500 ms look-ahead.
- Voice modes come from UnifiedReadingModePicker via `SetReadingVoice` → `PreparePlayVoiceSettings()`. Human highlights too when `{chunk}_timings.json` exists (ASR forced alignment); without it the fetch 404s and the page degrades to plain text.
- Auto-advance: `SetAutopage(true)` sets `triggerNextStep`; `OnAutoNextStep` fires 0.5 s after the audio ends and calls `PRScript.NextStep()`.
- Caches: static `CacheAudioAndTimingsStructs` (OrderedDictionary, max 50, LRU) keyed by audio URL, over a disk cache (`DiskCache`). Images: `PRUtils.cacheImages`.
- Word tap: a per-book word bank (`wordbank.mp3` + `wordbank.json`, word → [start,end]) is loaded lazily once per book; a tapped word plays only from this bank.

## Speech (read-along and dialogue)
- `Players/SpeechListenService.cs` — on-device speech recognition (Recognissimo/Vosk, Assets/Recognissimo), kept warm across pages; waits `ArmGraceSeconds` for a prompt to start and `SettleSeconds` of quiet before opening the microphone.
- `Players/ReadAlongService.cs` + `ReadAlongStallHint.cs` — the child reads aloud, the page turns (shipped Sept 2026).
- `Assets/_Story/Dialogue/` — Story Comprehension: `DialogueController` (flow; counts the app as speaking from the moment a prompt is requested), `DialogueFlow`, `DialogueSpeech`, mic UI, `DialogueResults`/`DialogueRewards`, `QuestionLevel` (levels A–D as catalog tags, picked by the adult, saved on device).

## URL handling
Two normalizers in PRScript.cs — do not confuse: `NormalizeURL(url)` prepends `baseURL` when the URL is not absolute (content URLs); `NormalizeUrl(url)` fixes `//` artifacts from concatenation (applied first to raw script values). `PRUtils.DownloadFile()` treats `resources:` URLs as a pseudo-scheme reading from Unity's `Resources/` folder.

## Key directories
```
Assets/_Story/
  Story/       — PRScript.cs (interpreter), StoryStepsUI.cs, Globals.cs, Navigation.cs, Gallery.cs
  Players/     — AudioAndTextPlayer.cs, AudioPlayer.cs, ReadAlongService.cs, SpeechListenService.cs, TTS/
  Dialogue/    — Story Comprehension question system (see above)
  Editions/    — Edition.cs + Icons; the edition JSONs are in Resources/Editions/
  Home/        — _Home, _HomeTalk, _Welcome, _LearnToRead scenes
  LIbrary/     — PRLibrary.cs (defines PRBook), BooksScrollView.cs, BookViewItem.cs
  Bookstore/   — PRBookstore.cs       Filters/ — genre/age filters      Settings/ — SettingsScene.cs
  GUI/         — buttons, parental gate, pickers   Utils/ — PRUtils.cs, SwipeDetector.cs
  Rooms/       — _Map, _Message; VAPI/ — visual animation layer (sprites, particles, dissolve) for the Map
  Books/       — test books checked into the project   drawing/ — drawing features
  Resources/   — runtime-loaded sprites and Editions/*.json
Assets/Tests/Editor/ — EditMode tests    Assets/_Story/Editor/ — RbSimSmoke.cs, RbTestBuild.cs, BuildStamp.cs
```

## Third-party packages
| Package | Use |
|---|---|
| MiniScript (`Assets/MiniScript/`) | Embedded scripting language for story logic |
| DoTween (`Assets/Plugins/Demigiant/`) | UI animations and transitions |
| TextAnimator (`Assets/Plugins/Febucci/`) | Text animation effects |
| TextMesh Pro | All text rendering |
| Quantum Console (`Assets/Plugins/QFSW/`) | Runtime debug console; `[Command]` attribute |
| ParticleImage (`Assets/AssetKits/ParticleImage/`) | UI particle effects |
| Recognissimo (`Assets/Recognissimo/`) | On-device speech recognition (Vosk) for read-along and dialogue |
| Microsoft Azure TTS (`Players/TTS/`) | Present but unused — the API key is a placeholder; computer narration is pre-generated files from the CDN |
