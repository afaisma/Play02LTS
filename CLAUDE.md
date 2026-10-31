<!-- Start block added 2026-10-31. The @ lines import files into context automatically. -->
# ReadingBuddy (Play6.3) — start here
@STATUS.md
@/Users/alexanderfaisman/dev/_hub/MAP.md
@/Users/alexanderfaisman/dev/_hub/UNITY.md
@/Users/alexanderfaisman/dev/_hub/AWS.md

- STATUS.md (imported above) is the record of state and decisions. Update it after every completed step.
- Personal rules: /Users/alexanderfaisman/.claude/CLAUDE.md (loaded automatically in Claude Code). In a linked chat the hub notes may not have loaded; then read /Users/alexanderfaisman/dev/_hub/UNITY.md and /Users/alexanderfaisman/dev/_hub/AWS.md with the file tools.
- Scratch: /Users/alexanderfaisman/dev/Play6.3/Build/_smoke/. Briefs and reports for other agents: /Users/alexanderfaisman/dev/Play6.3/_work/ (git-ignored). Discards: _to_delete/.
- Content (books, catalog, publishing) is NOT here: /Users/alexanderfaisman/dev/readingbuddy-aws.

---

## Project
**ReadingBuddy** — Unity 6 (URP) children's reading app (iOS, Android): a library of stories read page by page, with pictures and synchronized narration with word highlighting. All game code is under `Assets/_Story/`. How the code works in detail: /Users/alexanderfaisman/dev/Play6.3/docs/ARCHITECTURE.md.

## Editions — one project, several store apps
- `Assets/_Story/Editions/Edition.cs` + one JSON per product in `Assets/_Story/Resources/Editions/` (readingbuddy, readingbuddy-test, story-comprehension). `Edition.Pick` matches the running app's bundle id; no match falls back to `readingbuddy`.
- Everything product-specific that is not downloaded content lives in the edition JSON (app name, catalog URL, home scene, feature flags) — never in the code.
- The catalog is the edition's `catalogUrl`: `stories.json` (production) or `stories.test.json` (test app and Story Comprehension) on the CDN (see AWS.md). `Globals` downloads it at start; `ParseCatalog` dispatches by extension (`.json` → ParseJSON; CSV is only the rollback path). Local dev server runs on port 8090.

## Build and test
- Editor menus (Tools/ReadingBuddy): `Simulator smoke (export + build + 4 devices)`, `Simulator export only` (RbSimSmoke.cs), `Build TEST app (Android APK)`, `Build Story Comprehension (Android APK)` (RbTestBuild.cs — schedule, then run once; see UNITY.md).
- Per edition (RbTestBuild.cs, settings set from the edition JSON and put back in `finally`): Android APK → Build_Android/; iOS Xcode project (device SDK) → Build/iOS-<stem>/ via `Export Story Comprehension (iOS Xcode project)` or batch `-buildTarget iOS -executeMethod RbTestBuild.BatchExportIOSStoryComprehension` (raises the build number; result in Build/_smoke/ios_status.txt). Simulator export of an edition: batch `-executeMethod RbSimSmoke.BatchExportSimStoryComprehension` → Build/iOS-sim-story-comprehension.
- `tools/sim_smoke.sh` stages: inventory, build, launch, run "<device>" [plan], all, smoke, store, applog, netlog, launchtrace. Sweep plans: tools/*.plan. `SIM_EDITION=story-comprehension tools/sim_smoke.sh build|run "iPhone 17"` uses that edition's export, bundle id and plan (tools/sim_sc.plan).
- EditMode tests: `Assets/Tests/Editor/` (EditionTests, DialogueTests, GlobalsParseJSONTests, …).
- In-game debug console: Quantum Console; `[Command]` methods callable at runtime (tilde key).

## Scenes and state
- `_StartScene` (Globals.cs) downloads the catalog → `Edition.Current.homeScene` (`_Home` rooms or `_HomeTalk` book list) → `_Library`, `_Bookstore`, `_Settings`, `_Story` (PRScript.cs) and more under `Assets/_Story/*/`.
- `Globals.cs` is a DontDestroyOnLoad singleton (`g_listPRBooks`, `g_prbook`, `g_scriptName`). A book is ALWAYS opened through `Globals.GotoPrBook(book)` — the one write site of the recent-reads stamp. `PRBook` is defined in PRLibrary.cs.
- Book progress in PlayerPrefs: `{bookUrl}_page`, `{bookUrl}_done`.

## Story scripts (MiniScript)
- Plain-text files on the CDN. `////////[chunk name=...]` starts a page, `////////[event NAME [TARGET]]` an event block; the preamble before the first chunk runs once at load. PRScript.cs parses and runs them; each page turn executes its chunk in the MiniScript interpreter.
- New commands are intrinsics in `PRScript.SetupInterpreter()` (`Intrinsic.Create` → `AddParam` → `f.code = (context, partialResult) => …`); the dialogue commands are registered the same way in `Assets/_Story/Dialogue/DialogueCommands.cs`. Example in docs/ARCHITECTURE.md.
- Two URL normalizers in PRScript — do not confuse them: `NormalizeURL` prepends baseURL when not absolute; `NormalizeUrl` fixes `//` artifacts. `PRUtils.DownloadFile` also accepts `resources:` URLs (Unity Resources/ folder).

## Audio and highlighting (AudioAndTextPlayer.cs)
- Computer voice: `{chunk}_{rate}{voicePostfix}.mp3` + `{chunk}_{rate}_timings{voicePostfix}.json`, rate = `Globals.getReadingRate()` (-30…10). Human voice: `{chunk}.mp3` + `{chunk}_timings.json` (ASR-aligned; missing timings = no highlight). `staticText` → `{chunk}.json`. Every URL is cache-busted with `?v={contentRev}`.
- Timings JSON: array of `{word, time}`, time in **milliseconds**; spaces and punctuation are separate tokens, never highlighted.
- Voice modes (UnifiedReadingModePicker → `SetReadingVoice`):

| Mode | Audio | Highlight |
|---|---|---|
| `human` | `{chunk}.mp3` | on, when `{chunk}_timings.json` exists |
| `computer` (default) | generated TTS file | on |
| `novoice` | none | off |
- Cache: in-memory LRU of 50 (`CacheAudioAndTimingsStructs`, keyed by audio URL) over a disk cache (DiskCache). Images: `PRUtils.cacheImages`.

## Dialogue / question books (Story Comprehension)
`Assets/_Story/Dialogue/` — spoken comprehension questions: DialogueController (flow), DialogueSpeech + Players/SpeechListenService (microphone, Recognissimo), the `Dialogue*` intrinsics, QuestionLevel (levels A–D as catalog tags; the adult picks the level, answers stay on the device). One dialogue per page; events add to the built-in reaction.

## Third-party
MiniScript (Assets/MiniScript), DoTween (Plugins/Demigiant), TextAnimator (Plugins/Febucci), TextMesh Pro, Quantum Console (Plugins/QFSW), ParticleImage (AssetKits), Recognissimo speech recognition (Assets/Recognissimo), Azure TTS (Players/TTS — present but unused, the key is a placeholder). Full table in docs/ARCHITECTURE.md.
