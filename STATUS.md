# STATUS — ReadingBuddy app (Play6.3)
Updated: 2026-11-04 (Story Comprehension tester APK build 334).

## State
- 2026-11-04 Story Comprehension tester APK **build 334** built: Build_Android/StoryComprehension_3.1.0_334.apk (161 MB, versionCode 334, reads the live stories.sc.json). Built from a6860cc (history contains the 2026-10-30 speech fix f9d661d) plus uncommitted tooling edits (NOT committed yet): tools/sim_sc.plan (catalogfile step removed — stories.sc.json is live; new voice-silence part), Assets/_Story/Dev/SweepActions.cs (`level|<letter>` action, `silences=<n>` wait condition), tools/android_smoke.sh (applog also shows [Dialogue]/[Listen]; the run stage waits 10 s before an app-died verdict — it used to race the app start). ProjectSettings: only the build number 333→334.
- 2026-11-04 emulator silence-timing check PASSED (rb_phone, all 41 plan steps ok, live catalog, no wait|catalog error). The plan now also opens Jack and the Beanstalk B at level b with the microphone on, stays silent through two repeats of the yes/no voice question, then answers by touch. Log evidence: the two `[Dialogue] no answer` lines are 18.8 s apart (≥ 15 = DialogueOnSilence 10 + ExtraWaitAfterRepeat 5) and `[Listen] prompt over, listening starts` comes 6.3–6.4 s AFTER each repeat, never before. Captures in Build/_smoke/android/story-comprehension/.
- 2026-10-31 Brief 07 done (not pushed): tools/android_smoke.sh = Android twin of sim_smoke.sh (inventory, build, launch, run, applog, all, stop; SMOKE_EDITION). New AVDs rb_phone (pixel_7) and rb_tablet (10.1in WXGA), both Android 15 google_apis arm64, no download. RbTestBuild.BatchBuildAndroidDev = dev APK with READINGBUDDY_DEV (sweep driver), build number NOT raised (BuildStamp checks RbTestBuild.DevBuildRunning); SimSmokeDriver reads the plan from the intent extra "sweep" on Android. Story Comprehension on rb_phone: build 93 s, launch 25 s, run 80 s (sim_sc.plan unchanged: _HomeTalk reached, level A, Jack A question answered; only ERR = wait|catalog because stories.sc.json gives 403). rb_tablet created but not run. The batch build leaves the project on the Android target. Report: /Users/alexanderfaisman/dev/_hub/_work/REPORT_07.md.
- 2026-10-31 Brief 05b done (build 333, not pushed): story-comprehension.json reads `stories.sc.json` (NOT published yet: Alex runs Brief 05a's publish before any SC build goes to testers). RbTestBuild.ExportIOS(edition, stem) exports the iOS Xcode project → Build/iOS-StoryComprehension (bundle id com.imagiration.storycomprehension, own icon, build 333; settings put back and checked). RbSimSmoke.ExportSimulator("story-comprehension") → Build/iOS-sim-story-comprehension; `SIM_EDITION=story-comprehension tools/sim_smoke.sh build|run "iPhone 17"` with tools/sim_sc.plan passed (home, level sheet, Jack A question answered). tools/sim_sc.plan loads the LOCAL stories.test.json (catalogfile step) because stories.sc.json is not live; remove that step once it is. EditMode tests 343/343. Report: /Users/alexanderfaisman/dev/_hub/_work/REPORT_05.md section 5b.
- 2026-10-31 Brief 02 Part 2: Assets/MiniScript/ORIGIN.md (copy A, source of truth) and LICENSE.txt (MIT, from the official repo) added; `com.coplaydev.unity-mcp` in Packages/manifest.json pinned from `#main` to commit `85c101f5329ec1b0c6f70cba44614166dd78f53c` (the hash resolved in packages-lock.json). The brief's CLAUDE.md/AGENTS.md fixes were skipped — Brief 06 already rewrote those files. Unity will create .meta files for the two new files on the next Editor open; Alex commits them then.
- 2026-10-31 rules clean-up (Brief 06): CLAUDE.md rewritten 223 → 55 lines; explanations now in docs/ARCHITECTURE.md; AGENTS.md is a symlink to CLAUDE.md (old file in _archive/AGENTS_2026-10-31.md); ledger of removed lines in _work/CLAUDE_removed_2026-10-31.md; .claude/settings.local.json (124 entries) moved to _to_delete/settings.local.json.2026-10-31.
- Production app ReadingBuddy 3.1.x in the App Store and Google Play; 90 published books; speech engine (read-along page turn) shipped Sept 2026.
- Story Comprehension edition (spoken comprehension questions, for children with speech difficulties) is built from the same project on branch `feature/editions`. Test app id `com.imagiration.readingteacher.test` reads `stories.test.json`. Tester: Andrey (Boston University), feedback round 5 applied (icon, Settings layout, longer wait after a repeated question).
- Last commit (2026-10-30, build 332): second speech-timing fix, confirmed by Alex as fixing Andrey's "no pause after a repeated question" bug. `DialogueController.cs` counts the app as "speaking" from the moment a recording is asked for (`_promptPending`), with try/catch around the disk cache. `SpeechListenService.cs` waits up to `ArmGraceSeconds` (0.6 s) for a prompt to start and needs `SettleSeconds` (0.4 s) of quiet before the microphone opens. Same commit: start block in CLAUDE.md, "Also read" in AGENTS.md, `/_work/` in .gitignore, this STATUS.md.
- September 2026 chat outputs (audience note, engine prompt, previews) live in /Users/alexanderfaisman/dev/Play6.3/_work/claude-outputs-2026-09 (git-ignored).
- Freeze before the editions work: tag `freeze-2026-10-07-build314`, copy /Users/alexanderfaisman/dev/_frozen/Play6.3_2026-10-07_build314.

## Open
- Alex: give Build_Android/StoryComprehension_3.1.0_334.apk to Andrey; commit the 2026-11-04 tooling edits (sim_sc.plan, SweepActions.cs, android_smoke.sh, ProjectSettings build number) when he is happy with them.
- Alex: create the App Store record, paste its Apple ID into story-comprehension.json `appStoreId`, archive Build/iOS-StoryComprehension in Xcode → TestFlight. (stories.sc.json is published and live since 2026-11-04 or earlier.)
- Next feedback round from Andrey (ask Alex).

## Next step
- Wait for Andrey's feedback round 6, then apply it.

## Waiting for Alex
- Brief 07 choices to confirm: a separate dev APK (sweep driver, no build-number bump) for the emulator; new AVDs rb_phone/rb_tablet next to the existing ones.
- App Store record + Apple ID for Story Comprehension; publish of stories.sc.json.

## Decisions (append only)
- 2026-10-07: Story Comprehension is built incrementally inside this Unity project (Option 2), after a freeze; one code base, several store editions.
- 2026-10-07: dialogue script API — events add to the built-in reaction, one dialogue per page, all command names start with `Dialogue`.
- 2026-10-08: separate test app with its own catalog (`stories.test.json`), books in the one shared storage tree.
- 2026-10-11: each question book exists as four level versions (A–D); levels are tags, not letters in names; the adult picks the level; answers are saved on the device only.
- 2026-10-14: the new app gets its own screens (one merged Home / book list, level asked on first start).
- 2026-10-16: product name "Story Comprehension".
- 2026-10-31: Story Comprehension iOS = the same set-build-restore pattern as Android (RbTestBuild.ExportIOS), no per-edition folders, no Select menu; iOS icon = Editions/Icons/<id>.png on every slot; Story Comprehension reads stories.sc.json from build 333 on.
