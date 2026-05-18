# ReadingBuddy — Technical Overview

*A map of the three pieces that make up the shipped iOS/Android app **Children's Books Read Aloud** (a.k.a. ReadingBuddy / com.imagiration.readingteacher).*

---

## 1. The big picture

ReadingBuddy is a children's reading app with three independent moving parts:

| Piece | Location | Role |
|---|---|---|
| **Unity client** | `/Users/alexanderfaisman/dev/Play6.3` | The shipped iOS/Android app — UI, story playback, audio + highlight sync. |
| **FileServer** | `/Users/alexanderfaisman/dev/FileServer` | A small Spring Boot HTTP file server used **only for local development** — it mirrors the CDN's URL shape so the app can be tested against a laptop instead of CloudFront. |
| **Stories content** | `/Users/alexanderfaisman/dev/FileServer/uploads/stories` | The book library itself — a CSV catalog plus one folder per book containing scripts, images, MP3s, and JSON word-timing files. This same tree is published to CloudFront. |

The client never bakes book content into the build. On launch it downloads a CSV from a hard-coded URL, builds an in-memory catalog, and from then on every page of every book is fetched on demand. That makes the *content layout* — not the C# code — the real "API contract" between the three pieces.

A typical request flow:

```
App launch
  └─ Globals.cs downloads stories.csv from CloudFront (or localhost:8080 in dev)
       └─ _Library renders the catalog
            └─ user taps a book → _Story scene loads {book_url}_chunks_script.txt
                 └─ PRScript parses it into chunks (= pages)
                      └─ Each page fires MiniScript intrinsics:
                             PlayAudioAndText("gen/chunk_4", "...")
                                  └─ AudioAndTextPlayer fetches
                                        gen/chunk_4_0.mp3
                                        gen/chunk_4_0_timings.json
                                        and highlights words in sync
```

---

## 2. The Unity client (`Play6.3`)

### Engine + project

- **Unity 6000.3.9f1** (matches `ProjectVersion.txt`) with **URP 17.3.0**.
- Target platforms: iOS and Android. A `Build_Android.apk` (~141 MB) is checked in at the repo root next to the project, alongside `Build/` and `Build_Android/` output folders.
- No CLI build system. Builds and tests run through the Unity Editor. There are several stale `.sln` files (`Play2023_2_20`, `Play6.1`, `Play6.3`) from prior Unity-version migrations.
- Documentation lives in `CLAUDE.md` and `AGENTS.md` at the project root — both describe the same architecture.

### Scene flow (from `ProjectSettings/EditorBuildSettings.asset`, in build order)

```
_StartScene  →  _Library      ← main book browser
              →  _Story        ← reading experience
              →  _Settings / _Parents
              →  _Map / _Message
              →  _Bookstore   ← external Amazon purchase links
```

`_LoadingScene` and a few experimental scenes (`_StoryV`, `_Canvas`, `Graphics`, `_Map_old`) exist in the assets but are not in the build list.

### Global state — `Assets/_Story/Story/Globals.cs`

A `DontDestroyOnLoad` singleton that is the single source of truth between scenes:

- `g_listPRBooks` — the entire catalog, populated once from the CSV.
- `g_prbook` — the currently selected `PRBook`.
- `g_scriptName` — URL of the story script file to load.
- `g_libraryFilter` / `g_bookstoreFilter` — the active genre filter, preserved on scene re-entry.
- `getReadingRate()` — returns the rate suffix string (`-30`, `-20`, `-10`, `0`, `10`) used to select which pre-generated TTS audio file to play. Derived from the book's `ageFrom` or a user preference.

The CSV URL is hard-coded but switchable via Inspector — `Globals` exposes six "convenience" fields so a developer can paste a different value into `csvUrl` without rebuilding:

```csharp
public string csvUrl            = "http://d5wtw8f0w3ire.cloudfront.net/uploads/stories_02/stories.csv";  // prod (default)
public string convinienceS3     = ".../uploads/stories/...";          // older prod path
public string convinienceS3_01  = ".../uploads/stories_01/...";       // intermediate
public string convinienceS3_02  = ".../uploads/stories_02/...";       // current prod
public string convinienceS3QA   = ".../uploads/stories-qa/...";       // QA bucket
public string convinienceLocal  = "http://localhost:8080/api/files/download/stories/stories.csv";
public string convinienceEC2    = "http://35.90.126.120:8080/api/files/download/stories/stories.csv";
```

`baseURL` is derived from the CSV URL (everything up to the last slash) and is used to resolve every relative path in the CSV.

Per-book progress is persisted to `PlayerPrefs` under the keys `{bookUrl}_page` and `{bookUrl}_done`.

### The interpreter — `Assets/_Story/Story/PRScript.cs`

The `_Story` scene controller. On `Start()`:

1. Reads `Globals.g_scriptName` as `scriptURL`.
2. Downloads the script text via `PRUtils.DownloadFile()` (which also understands the special `resources:` scheme for loading from Unity's `Resources/`).
3. `parse()` splits the file into **scriptlets** (chunks) and **events** using the delimiters `////////[chunk` and `////////[event`. Anything before the first chunk is the preamble.
4. Registers all MiniScript intrinsics in `SetupInterpreter()` — this is where new script commands are added.
5. Runs the preamble, then calls `ExecuteStep(0)` on the first chunk.

Every page turn calls `ExecuteStep(index) → RunScript(scriptlet.Content)`. MiniScript (in `Assets/MiniScript/`) is the embedded scripting language; Unity functionality is exposed to scripts as intrinsics. Confirmed registered intrinsics (from `PRScript.cs`):

```
ScriptLog, DisplayTitle, Characters, AddCharacter, CreateButton,
CharacterSpeaks, GoTo, Speak, SelectCharacter,
AddAudio, SetShoppingLink, AddVideo,
DisplayMainImage, AddGalleryImage, MaximizeGallery, AddGallerySound,
DisplayBackgroundImage, DisplayBackgroundColor,
DisplayTitlePage, HideTitlePage,
PlayAudio, VoiceOptions, PlayAudioAndText, PlayAudioAndShowText,
SetCurrentVoice, SetAudioTextFont, EnableAutoSize,
SetAudioTextAlignment, SetAudioTextFontSize, SetAudioTextHilightColors,
PlayVideo,
SetPuzzleEnabled, SetPuzzleEnabledInBook, SetPuzzleEnabledInPage
```

Navigation (Next/Prev buttons, left/right swipes on `textforeground` or `gallery`) all route through `PRScript.NextStep()` / `PrevStep()`. Multi-image galleries scroll internally before allowing a page turn.

There are **two URL-normalizing methods** with similar names — easy to confuse:
- `NormalizeURL(url)` — prepends `baseURL` if the URL is not absolute. Used for content URLs.
- `NormalizeUrl(url)` — fixes `//` artifacts created by string concatenation. Applied first to raw script values.

### Audio + word highlighting — `Assets/_Story/Players/AudioAndTextPlayer.cs`

The most complex component, invoked via `PlayAudioAndText(chunkname, content)`.

**Audio URL is built from three things:**
- `chunkname` — what the script passed in, e.g. `"gen/chunk_4"`.
- `rate` — the suffix `getReadingRate()` returns (`-30 / -20 / -10 / 0 / 10`).
- `voicePostfix` — `_` + selected voice name, or empty.

```
{chunkname}_{rate}{voicePostfix}.mp3   ← computer voice
{chunkname}.mp3                        ← human voice (no rate, no postfix)
```

**Timings URL** chooses between two shapes:
- `staticText = true`  → `{chunkname}.json`
- `staticText = false` → `{chunkname}_{rate}_timings{voicePostfix}.json`

**Timings JSON** is an array of `{word, time}` records where `time` is in **milliseconds** (verified against `Alphabet/gen/chunk_10_0_timings.json`):

```json
[ {"word": "I ",   "time": 50.0},
  {"word": "is ",  "time": 300.0},
  {"word": "for ", "time": 550.0},
  ... ]
```

Spaces and punctuation are separate tokens (note the trailing space inside `"I "`). Punctuation is never highlighted (`IsWordPunctuation()`).

**Two stacked TextMeshPro layers render the highlight:**
- `uiForeground` — `<color=#FF55FF>word</color>` on the currently-active word.
- `uiBackground` — `<mark=#00FF0044>word</mark>` for a background highlight bar.
- Both strings are rebuilt entirely every frame while audio plays.
- The active word index only advances forward; `audioSource.time * 1000 - 500` provides a 500 ms look-ahead.

**Voice modes** (set by `ButtonSelectionController` → `PreparePlayVoiceSettings()`):

| Button | Audio | Highlight | URL pattern |
|---|---|---|---|
| `human` | plays | off | `{chunk}.mp3` |
| `computer` | plays | on | `{chunk}_{rate}_{voice}.mp3` |
| `novoice` | muted (vol = 0) | off | TTS url (ignored) |

**Auto-advance:** when the "Autopage" toggle is on (`triggerNextStep = true`), `OnAutoNextStep` fires 0.5 s after audio stops and calls `PRScript.NextStep()`.

**Cache:** a static `OrderedDictionary CacheAudioAndTimingsStructs` (max 30, LRU) keyed by audio URL, shared across all pages. Images cache similarly in `PRUtils.cacheImages`.

### Book records — `Assets/_Story/LIbrary/PRLibrary.cs`

`PRBook` is defined inside `PRLibrary.cs` (not in its own file). Its fields map directly to CSV columns:

```
bookName, bookAuthor, bookImageUrl, bookUrl, ageFrom, ageTo,
genre, notesForParents, id, bookStoreUrlPrinted, bookStoreUrlKindle
```

`bookFullUrl` is `bookUrl` resolved against `Globals.baseURL` if it is not already absolute.

### Source layout under `Assets/_Story/`

| Folder | Files | Purpose |
|---|---|---|
| `Story/` | 16 cs | Interpreter, globals, page UI, autoplay, rate-the-app, gallery |
| `Players/` | 9 cs | `AudioAndTextPlayer`, `AudioPlayer`, `PRVideoPlayer`, mic, VU meter, TTS sub-folder |
| `Players/TTS/` | — | `MicrosoftTextToSpeech.cs` — Azure TTS for the `Speak()` intrinsic |
| `LIbrary/` | 4 cs | `PRLibrary.cs` (also defines `PRBook`), scroll view, item view |
| `Bookstore/` | 4 cs | `PRBookstore.cs` — external Amazon links |
| `Filters/` | 6 cs | Genre filter chips |
| `GUI/` | 10 cs | Buttons, parental gate, fonts, icons, vignette |
| `Settings/` | 2 cs | `SettingsScene.cs`, `_Parents` scene |
| `Rooms/` | 4 cs | Map / message scenes |
| `Utils/` | 15 cs | `PRUtils.cs` (download / cache), `SwipeDetector.cs`, alerts |
| `VAPI/` | 9 cs | Visual-effects layer (sprites, particles, dissolve) used by the Map scene |
| `drawing/` | 4 cs | Canvas / IRV drawing experiment |
| `Resources/` | sprites | Library background art keyed by genre |

There is a `_Story/Players/AudioAndTextPlayer_bak.cs` left in the source tree — looks like a previous version retained for reference.

### Adding a new script command

Pattern in `PRScript.SetupInterpreter()`:

```csharp
f = Intrinsic.Create("MyCommand");
f.AddParam("param1", "default");
f.code = (context, partialResult) => {
    string value = context.GetVar("param1").ToString();
    // call into Unity here
    return new Intrinsic.Result(ValNumber.one);
};
```

The new command becomes immediately callable from any story `.txt` script.

### Third-party packages of note

| Package | Use |
|---|---|
| **MiniScript** (`Assets/MiniScript/`) | Embedded scripting language for stories. |
| **DoTween** (`Plugins/Demigiant/`) | UI tweens and transitions. |
| **TextAnimator** (`Plugins/Febucci/`) | Text animation effects. |
| **TextMesh Pro** | All text rendering, including the highlight layers. |
| **QFSW Quantum Console** | Runtime debug console (tilde key). Methods marked `[Command]` are callable: `SetStep`, `AddStoryStep`, `CleanupStorySteps`, `DisplayMainImage`. |
| **ParticleImage** (`AssetKits/ParticleImage/`) | UI particle effects. |
| **Azure TTS** | The `Speak()` intrinsic — see `Players/TTS/MicrosoftTextToSpeech.cs`. |
| **Simply Application Rating** | Triggered by `RateTheApp.cs`. |

The `Packages/manifest.json` is a fairly stock Unity 6 setup — URP, UGUI, 2D feature, web request modules, Rider/VS IDE packages. Netcode and Multiplayer packages are present but not used by the reading app.

---

## 3. The FileServer (`/Users/alexanderfaisman/dev/FileServer`)

A **dev-only convenience server**, not the production CDN. Production traffic goes to CloudFront (`d5wtw8f0w3ire.cloudfront.net`); the FileServer exists so a developer can `gradlew bootRun` locally and have the app fetch `stories.csv` + all media from `http://localhost:8080` instead.

### Stack

- **Java 17** + **Spring Boot 3.0.5** + **Gradle 8.x**, generated by Spring Initializr in April 2023.
- Dependencies: `spring-boot-starter-web`, `spring-boot-starter-thymeleaf`, Lombok, devtools.
- Spring Security starter is **commented out** in `build.gradle`.

### What it actually does

`FileServerController.java` exposes two endpoints under `/api/files`:

| Method | Path | Behavior |
|---|---|---|
| `POST` | `/api/files/upload` | Stores a multipart file into the `uploads/` directory (filename only — no subpath honored). |
| `GET`  | `/api/files/download/**` | Streams any file under `uploads/` matching the path suffix. |

`FileStorageServiceImpl` writes uploaded files to `Paths.get("uploads").toAbsolutePath()` and refuses filenames containing `..`. Downloads resolve `uploads/{subpath}` and return it as a `Resource` with `Content-Disposition: attachment`.

### Configuration (`application.properties`)

```properties
fileserver.upload-dir=uploads
spring.servlet.multipart.max-file-size=100MB
spring.servlet.multipart.max-request-size=100MB
spring.security.user.name=user
spring.security.user.password=password
# server.ssl.* — all commented out; runs HTTP on default 8080
```

A `keystore.p12` is present in `src/main/resources/` but the SSL config that would use it is commented out.

### Observations worth noting

- **`WebSecurityConfig.java` has every annotation and method body commented out** — the class is empty. Combined with the commented-out Security dependency in `build.gradle`, this means the server is fully open: the `spring.security.user.*` credentials in `application.properties` aren't actually enforced. Fine for localhost; not fit for an exposed deployment.
- **`src/main/resources/static/index.html` posts to `/upload`**, but the controller is at `/api/files/upload`. The upload form on the root URL is therefore broken — it 404s. Uploads only work via the JSON-style endpoint.
- The only test is `FileServerApplicationTests.contextLoads()` — i.e. the Spring Initializr default.
- A **294 MB `uploads.zip`** sits at the FileServer repo root next to a live `uploads/` directory. Looks like a one-time bootstrap snapshot that hasn't been gardened.
- The same EC2 IP (`35.90.126.120`) is hard-coded in `Globals.cs` as `convinienceEC2`, suggesting the same Spring Boot app *has* been deployed somewhere — but the production CSV URL goes through CloudFront, not that EC2 host.

The CDN path layout (`stories_02/...`) clearly mirrors what this server serves locally — that's the design contract. Anything served from `http://localhost:8080/api/files/download/stories/<x>` is also at `http://d5wtw8f0w3ire.cloudfront.net/uploads/stories_02/<x>`.

---

## 4. The stories library (`FileServer/uploads/stories`)

This is where most of the product actually lives. The Unity app is a generic renderer; the books **are** their files.

### Catalog — `stories.csv`

68 lines total, 1 header + **67 books** that are listed in the active catalog. The header is:

```
book_name, book_author, book_image_url, book_url,
age_from, age_to, genre, notes_For_Parents, id, book_store_url
```

Several rows include two extra trailing columns for printed-book and Kindle Amazon links (mapped to `bookStoreUrlPrinted` / `bookStoreUrlKindle` in code).

Quick stats from the CSV:

- **Authors** include Dr. Stein (most of the rhymebooks), Alex Faisman, Alex Velin, Anre Steingard, Beatrix Potter, Inigo Lopez.
- **Age range:** 2–12, mostly clustered at 2–6 (rhymebooks) and 3–8 (storybooks).
- **Genres** are colon-separated multi-tags: `Rhymebooks : Family : Special Education : Manners`, `Fairytales : Family : Adventure : Classic`, `Science : Sound & Speech : Nature`, etc.
- The `id` column is not unique — IDs 38 and 67 each appear twice (e.g. *Timmy And His Family* and *The Sad Princess* both have `id=38`). The app shouldn't be relying on `id` as a primary key without de-duping.

### Per-book folder layout

The CSV references files relative to the stories root. A typical book folder (verified against `Alphabet/`):

```
Alphabet/
├── Alphabet.txt                   ← raw original text
├── Alphabet_chunks.txt            ← intermediate, split into chunks
├── Alphabet_chunks_script.txt     ← THE file the app loads (chunks → MiniScript)
├── Alphabet Book.pkf              ← (project artifact, likely from an authoring tool)
├── Alphabet Book.wav              ← raw narration recording
├── images/                        ← 28 jpgs: cover.jpg, img01.jpg … img26.jpg
└── gen/                           ← 308 generated files
    ├── chunk_1.mp3                ← human voice (no rate suffix)
    ├── chunk_1_-30.mp3            ← computer voice, slowest
    ├── chunk_1_-30_timings.json   ← word-timing JSON for the above
    ├── chunk_1_-20.mp3
    ├── chunk_1_-20_timings.json
    ├── chunk_1_-10.mp3 / _timings
    ├── chunk_1_0.mp3   / _timings
    └── chunk_1_10.mp3  / _timings
```

So each chunk produces roughly **1 human MP3 + 5 (rate × 1 voice) computer MP3s + 5 timing JSONs = ~11 files per chunk**. Verified counts in `Alphabet/gen/`: 27 chunks (`chunk_1.mp3 … chunk_27.mp3`), 135 rate-suffixed MP3s (27 × 5 rates), 140 timing JSONs, plus a handful of extras → 308 files total.

### What a `*_chunks_script.txt` looks like

Verified from `Alphabet/Alphabet_chunks_script.txt`:

```
SetAudioTextHilightColors "112233", "BBBBBB77"
VoiceOptions 1, 1, 1
GoTo("Next")

////////[event OnExecuteStep
if nCurrentStep == 0 or nCurrentStep == nSteps - 1 then
   SetAudioTextAlignment "center"
   SetAudioTextFont "", 120, "000000"
   EnableAutoSize 0, 1, 1
else
   SetAudioTextAlignment "topleft"
   SetAudioTextFont "", 55, "000000"
   EnableAutoSize 1, 40, 100
end if

////////[chunk_1]
PlayAudioAndText "gen//chunk_1", "Alphabet Rhymebook"
AddGalleryImage "images//img01.jpg"
////////[chunk_2]
PlayAudioAndText "gen//chunk_2", "A is for Apple, a fruit so sweet."
AddGalleryImage "images//img02.jpg"
...
```

This is the exact contract: the preamble runs once on load, the `OnExecuteStep` event runs on every page change (and can read `nCurrentStep` / `nSteps`), and each `////////[chunk_N]` block is one page.

Note the `//` separator inside paths (`gen//chunk_1`, `images//img01.jpg`). That's where `PRScript.NormalizeUrl()`'s job — collapsing `//` to `/` — comes in.

### Orphans, duplicates, and noise in the stories tree

The `stories/` folder contains **98 entries** but only the 67 referenced by the CSV are live. Things in there that the CSV does not reference:

| Kind | Examples |
|---|---|
| Older versions superseded by `_v2` folders | `Sea_Story_en`, `Sea_Story_en_bak`, `Sea_Story_ru`, `TheSnowQueen`, `TimmyAndHisFamily`, `The_Tale_of_Peter_Rabbit` |
| Experimental / dev | `TestFromEpub`, `TestFromText`, `speechplace01`, `VScene`, `temp`, `test` |
| Unfinished or paused projects | `Light`, `Volcanoes`, `NotesAndNews`, `StoriesFromPictures_TheBaby`, `StoriesFromPictures_TheGoldenHarp` |
| Shared / cross-book assets | `defaultImages`, `defaultImagesBak`, `defaultSounds`, `app_art`, `adjusted_images`, `images` |
| Catalog backups | `stories_Jun_29.csv`, `stories__.csv`, `stories_bak.csv`, `stories_tmp.csv`, `stories.xlsx` |
| Sibling working copy | `uploads/stories copy/` — a parallel 101-entry directory at the same level |
| One-time loose files | `LetterToParents.txt`, `LetterToParents_rtf.txt` |

The good news: every `book_url` in the active CSV resolves to an existing script file — there are no broken references. The cleanup opportunity is the other direction: a lot of unused content is being published to the CDN.

---

## 5. How the three pieces interlock

The dependency direction is one-way and the **content tree is the schema**:

```
┌─────────────────────────────────────────────────────────────┐
│ stories/  (the schema)                                      │
│   stories.csv  ◄── catalog                                  │
│   {Book}/{book}_chunks_script.txt  ◄── MiniScript           │
│   {Book}/images/*.jpg              ◄── illustrations        │
│   {Book}/gen/chunk_N_{rate}.mp3    ◄── audio                │
│   {Book}/gen/chunk_N_{rate}_timings.json  ◄── word timing   │
└──────────────────────────┬──────────────────────────────────┘
                           │ served by
        ┌──────────────────┴──────────────────┐
        ▼                                     ▼
  CloudFront CDN                       FileServer (local dev)
  d5wtw8f0w3ire.cloudfront.net         localhost:8080/api/files/download
        │                                     │
        └──────────────────┬──────────────────┘
                           ▼
                  Unity app (Play6.3)
                  Globals.csvUrl → catalog
                  PRScript      → interpreter
                  AudioAndTextPlayer → playback + highlight
```

The same URL shape works against either backend; the only thing the developer has to change to switch is `Globals.csvUrl` in the Inspector. CDN authoring is presumably "edit the stories tree → upload to S3", since the FileServer's `/upload` endpoint is per-file and not exercised by the app.

---

## 6. Notable observations

- **No structured schema for `*_chunks_script.txt`.** The format is conventional and recognized only by `PRScript.parse()` looking for `////////[chunk` and `////////[event` literals. A typo in the marker silently drops the rest of the file into the preceding chunk.
- **MiniScript is the runtime contract.** Because intrinsics are registered in C# code, the set of legal script commands changes between app builds — older content on the CDN has to keep working against new builds. There is currently no versioning between the catalog and the client.
- **TTS variants are pre-generated, not on-device.** The reading-rate suffix in the audio filename means new rates require re-running an offline pipeline, not a client change. The same is true of additional voices (the `voicePostfix` slot).
- **`id` column in the CSV is not unique** — two pairs of books share IDs. Anything keying off `id` should be audited.
- **FileServer security is fully open.** The `WebSecurityConfig` class has every annotation/method commented out; the configured username/password is inert. Safe on localhost only.
- **FileServer's public upload form is broken.** `static/index.html` posts to `/upload`; the controller serves `/api/files/upload`.
- **Two URL normalizers in `PRScript.cs`** with confusingly similar names (`NormalizeURL` vs `NormalizeUrl`) do different things. Worth a rename.
- **`AudioAndTextPlayer_bak.cs` lives next to the live class.** Cruft worth deleting.
- **Many orphaned book folders** under `stories/` get published to the CDN even though the catalog never references them — a real bandwidth/storage cost on production.

---

## 7. Quick reference — file paths to know

| What | Where |
|---|---|
| CSV catalog (prod) | `http://d5wtw8f0w3ire.cloudfront.net/uploads/stories_02/stories.csv` |
| CSV catalog (local) | `http://localhost:8080/api/files/download/stories/stories.csv` |
| Catalog URL switch | `Globals.csvUrl` (Inspector field on Globals GameObject in `_StartScene`) |
| Story interpreter | `Assets/_Story/Story/PRScript.cs` |
| Add a new intrinsic | `PRScript.SetupInterpreter()` |
| Audio + highlight | `Assets/_Story/Players/AudioAndTextPlayer.cs` |
| Book catalog model | `Assets/_Story/LIbrary/PRLibrary.cs` (also defines `PRBook`) |
| Download / cache utils | `Assets/_Story/Utils/PRUtils.cs` |
| TTS (Azure) | `Assets/_Story/Players/TTS/MicrosoftTextToSpeech.cs` |
| Build scenes & order | `ProjectSettings/EditorBuildSettings.asset` |
| FileServer controller | `FileServer/src/main/java/com/pr/fileserver/FileServerController.java` |
| FileServer storage | `FileServer/src/main/java/com/pr/fileserver/FileStorageServiceImpl.java` |
| FileServer config | `FileServer/src/main/resources/application.properties` |
