# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

**ReadingBuddy** — a Unity children's reading app (iOS/mobile) where children browse a library of stories and read page-by-page with illustrated pages and optional synchronized audio narration with word highlighting.

- Unity 6 with URP 17.3.0
- C# scripting throughout; all game code lives under `Assets/_Story/`
- Target platform: iOS (also Android)

## Development Commands

This is a Unity project. There is no CLI build system — builds and tests are run through the Unity Editor.

**Opening the project:** Open Unity Hub → Add project → select this directory. Use Unity 6 (matches `com.unity.render-pipelines.universal: 17.3.0`).

**In-game debug console:** The project uses QFSW Quantum Console. Methods marked `[Command]` are callable at runtime via the console (tilde key by default). Key commands include `SetStep`, `AddStoryStep`, `CleanupStorySteps`, `DisplayMainImage`.

**Local content server:** Several hardcoded convenience URLs point to `http://localhost:8080/api/files/download/stories/` — a local file server that mirrors the CloudFront CDN structure for development without hitting production.

**CSV content URL** (configurable in the `Globals` Inspector field `csvUrl`):
- Production: `http://d5wtw8f0w3ire.cloudfront.net/uploads/stories_02/stories.csv`
- Local dev: `http://localhost:8080/api/files/download/stories/stories.csv`
- QA: `http://d5wtw8f0w3ire.cloudfront.net/uploads/stories-qa/stories.csv`

## Architecture

### Scene Flow

```
_StartScene (Globals.cs)
  └─ downloads CSV → g_listPRBooks → navigates to →
       _Library  (PRLibrary.cs)     ← main book browser
       _Bookstore (PRBookstore.cs)  ← external purchase links
       _Settings  (SettingsScene.cs)
       _Story    (PRScript.cs)      ← reading experience
```

### Global State — `Globals.cs`

Singleton (`DontDestroyOnLoad`). The single source of truth passed between scenes:
- `g_listPRBooks` — full book catalog (populated once from CSV)
- `g_prbook` — currently selected `PRBook`
- `g_scriptName` — URL of the story script file to load
- `g_libraryFilter` — active genre filter to restore on Library re-entry
- `getReadingRate()` — returns rate suffix string (`-30`, `-20`, `-10`, `0`, `10`) used to select which pre-generated TTS audio file to play; derived from book's `ageFrom` or user preference

Entry point into a story: `Globals.GotoPrBook(book)` — sets `g_scriptName` and `g_prbook`, then loads `_Story` scene.

Book progress persists via `PlayerPrefs` using `{bookUrl}_page` and `{bookUrl}_done` as keys.

### Story Interpreter — `PRScript.cs`

The `_Story` scene controller. On `Start()`:
1. Reads `Globals.g_scriptName` as `scriptURL`
2. Downloads the script text via `PRUtils.DownloadFile()`
3. `parse()` splits the text into **scriptlets** (chunks) and **events** using `////////[chunk name=...]` and `////////[event name]` delimiters
4. Calls `storyStepsUI.AddStoryStep()` for each chunk, then executes the settings section (preamble before the first chunk)
5. Registers all MiniScript intrinsics via `SetupInterpreter()` — this is where new script commands must be added

Each page turn calls `ExecuteStep(index)` → `RunScript(scriptlet.Content)` through the MiniScript interpreter. The interpreter exposes Unity functionality as intrinsics.

**Available intrinsics** (callable from story `.txt` scripts):
`DisplayTitle`, `DisplayTitlePage`, `HideTitlePage`, `DisplayMainImage`, `DisplayBackgroundImage`, `DisplayBackgroundColor`, `AddGalleryImage`, `AddGallerySound`, `MaximizeGallery`, `AddCharacter`, `Characters`, `CreateButton`, `AddAudio`, `PlayAudio`, `PlayAudioAndText`, `PlayAudioAndShowText`, `Speak` (TTS), `GoTo` (next/prev/label), `VoiceOptions`, `SetCurrentVoice`, `SetAudioTextFont`, `SetAudioTextFontSize`, `SetAudioTextAlignment`, `SetAudioTextHilightColors`, `EnableAutoSize`, `AddVideo`, `PlayVideo`, `SetShoppingLink`

Navigation: Next/Prev buttons and left/right swipes on the `textforeground` or `gallery` objects all route through `PRScript.NextStep()` / `PrevStep()`. The gallery swipe only page-turns when there is a single gallery image; multi-image galleries scroll internally first.

### Story Script Format

Plain text files served from CDN alongside media:

```
// Preamble (MiniScript, runs once at load):
VoiceOptions(human:1, computer:1, novoice:1)
SetAudioTextFont("MyFont", 24, "FFFFFF")

////////[chunk name=page1]
DisplayMainImage("page1.jpg")
PlayAudioAndText("page1", "Once upon a time...")

////////[chunk name=page2]
AddGalleryImage("img1.jpg")
AddGalleryImage("img2.jpg")
PlayAudioAndText("page2", "...")

////////[event OnExecuteStep]
// MiniScript executed on every step change
```

### Audio + Text Highlighting — `AudioAndTextPlayer.cs`

The most complex component. Called via the `PlayAudioAndText(chunkname, content)` intrinsic.

**Audio URL construction:**
```
{chunkname}_{rate}{voicePostfix}.mp3   (computer voice)
{chunkname}.mp3                        (human voice, no rate suffix)
```
Where `rate` = `Globals.getReadingRate()` and `voicePostfix` = `_` + selected voice name (or empty).

**Timings URL construction** (controls which JSON is fetched):
- `staticText = true` → `{chunkname}.json`
- `staticText = false` → `{chunkname}_{rate}_timings{voicePostfix}.json`

**JSON timings format** — array of `{word, time}` where `time` is in **milliseconds**:
```json
[{"word": "Once", "time": 0.0}, {"word": " ", "time": 0.0}, {"word": "upon", "time": 350.0}]
```
Spaces and punctuation are separate tokens. Punctuation is never highlighted (`IsWordPunctuation()`).

**Highlight rendering — two stacked TMP layers:**
- `uiForeground` — uses `<color=#FF55FF>word</color>` for the active word
- `uiBackground` — uses `<mark=#00FF0044>word</mark>` for the background highlight marker
- Both layers are entirely rebuilt as strings every frame while audio plays
- Active word index advances forward only; `audioSource.time * 1000 - 500` provides a 500ms look-ahead offset

**Voice modes** (set by `ButtonSelectionController` → `PreparePlayVoiceSettings()`):
| Button name | Audio | Highlight | URL used |
|---|---|---|---|
| `human` | plays | off | `{chunk}.mp3` |
| `computer` | plays | on | `{chunk}_{rate}_{voice}.mp3` |
| `novoice` | muted (vol=0) | off | TTS url (ignored) |

**Auto-advance:** When the "Autopage" toggle is on (`triggerNextStep = true`), `OnAutoNextStep` UnityEvent fires 0.5s after audio stops, which calls `PRScript.NextStep()`.

**Cache:** Static `OrderedDictionary CacheAudioAndTimingsStructs` (max 30 entries, LRU eviction) keyed by audio URL, shared across all pages. Image cache similarly in `PRUtils.cacheImages`.

### Book Data — `PRBook` and CSV

`PRBook` is defined in `PRLibrary.cs`. Fields come from CSV columns:
```
bookName, bookAuthor, bookImageUrl, bookUrl, ageFrom, ageTo,
genre, notesForParents, id, bookStoreUrlPrinted, bookStoreUrlKindle
```
`bookFullUrl` is resolved from `bookUrl` — if not absolute, prepended with `Globals.baseURL` (the CDN base directory of the CSV).

### URL Handling — two methods, different behaviors

`PRScript` has two URL normalizers — be careful not to confuse them:
- `NormalizeURL(url)` — prepends `baseURL` if not absolute (`http`). Used for content URLs.
- `NormalizeUrl(url)` — fixes double-slash artifacts (`//` → `/`) from string concatenation. Applied first to raw script values.

`PRUtils.DownloadFile()` also handles `resources:` URLs as a special scheme for loading from Unity's `Resources/` folder instead of the network.

### Key Directories

```
Assets/_Story/
  Story/       — PRScript.cs (interpreter), StoryStepsUI.cs (renderer), Globals.cs (state), Gallery.cs
  Players/     — AudioAndTextPlayer.cs, AudioPlayer.cs, PRVideoPlayer.cs, TTS/MicrosoftTextToSpeech.cs
  LIbrary/     — PRLibrary.cs (also defines PRBook), BooksScrollView.cs, BookViewItem.cs
  Bookstore/   — PRBookstore.cs
  Filters/     — FilterContainer.cs, FilterItem.cs
  GUI/         — UI components (buttons, parental gate, etc.)
  Utils/       — PRUtils.cs (download/image/cache utilities), SwipeDetector.cs
  VAPI/        — Visual animation layer (sprites, particles, dissolve effects) used by the Map scene
  Settings/    — SettingsScene.cs
  Resources/   — Runtime-loaded sprites (library backgrounds keyed by genre name)
```

### Adding New Story Script Commands

Add a new intrinsic in `PRScript.SetupInterpreter()` following the existing pattern:
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
The command is then immediately available in story `.txt` script files.

### Third-Party Packages

| Package | Use |
|---|---|
| MiniScript (`Assets/MiniScript/`) | Embedded scripting language for story logic |
| DoTween (`Assets/Plugins/Demigiant/`) | UI animations and transitions |
| TextAnimator (`Assets/Plugins/Febucci/`) | Text animation effects |
| TextMesh Pro | All text rendering |
| QFSW Quantum Console | Runtime debug console; use `[Command]` attribute to expose methods |
| ParticleImage (`Assets/AssetKits/ParticleImage/`) | UI particle effects |
| Microsoft Azure TTS (`Players/TTS/MicrosoftTextToSpeech.cs`) | Computer voice narration |