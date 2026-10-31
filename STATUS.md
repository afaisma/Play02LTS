# STATUS — ReadingBuddy app (Play6.3)
Updated: 2026-10-31 (Brief 05b: Story Comprehension iOS export + Simulator smoke per edition, build 333).

## State
- 2026-10-31 Brief 05b done (build 333, not pushed): story-comprehension.json reads `stories.sc.json` (NOT published yet: Alex runs Brief 05a's publish before any SC build goes to testers). RbTestBuild.ExportIOS(edition, stem) exports the iOS Xcode project → Build/iOS-StoryComprehension (bundle id com.imagiration.storycomprehension, own icon, build 333; settings put back and checked). RbSimSmoke.ExportSimulator("story-comprehension") → Build/iOS-sim-story-comprehension; `SIM_EDITION=story-comprehension tools/sim_smoke.sh build|run "iPhone 17"` with tools/sim_sc.plan passed (home, level sheet, Jack A question answered). tools/sim_sc.plan loads the LOCAL stories.test.json (catalogfile step) because stories.sc.json is not live; remove that step once it is. EditMode tests 343/343. Report: /Users/alexanderfaisman/dev/_hub/_work/REPORT_05.md section 5b.
- 2026-10-31 Brief 02 Part 2: Assets/MiniScript/ORIGIN.md (copy A, source of truth) and LICENSE.txt (MIT, from the official repo) added; `com.coplaydev.unity-mcp` in Packages/manifest.json pinned from `#main` to commit `85c101f5329ec1b0c6f70cba44614166dd78f53c` (the hash resolved in packages-lock.json). The brief's CLAUDE.md/AGENTS.md fixes were skipped — Brief 06 already rewrote those files. Unity will create .meta files for the two new files on the next Editor open; Alex commits them then.
- 2026-10-31 rules clean-up (Brief 06): CLAUDE.md rewritten 223 → 55 lines; explanations now in docs/ARCHITECTURE.md; AGENTS.md is a symlink to CLAUDE.md (old file in _archive/AGENTS_2026-10-31.md); ledger of removed lines in _work/CLAUDE_removed_2026-10-31.md; .claude/settings.local.json (124 entries) moved to _to_delete/settings.local.json.2026-10-31.
- Production app ReadingBuddy 3.1.x in the App Store and Google Play; 90 published books; speech engine (read-along page turn) shipped Sept 2026.
- Story Comprehension edition (spoken comprehension questions, for children with speech difficulties) is built from the same project on branch `feature/editions`. Test app id `com.imagiration.readingteacher.test` reads `stories.test.json`. Tester: Andrey (Boston University), feedback round 5 applied (icon, Settings layout, longer wait after a repeated question).
- Last commit (2026-10-30, build 332): second speech-timing fix, confirmed by Alex as fixing Andrey's "no pause after a repeated question" bug. `DialogueController.cs` counts the app as "speaking" from the moment a recording is asked for (`_promptPending`), with try/catch around the disk cache. `SpeechListenService.cs` waits up to `ArmGraceSeconds` (0.6 s) for a prompt to start and needs `SettleSeconds` (0.4 s) of quiet before the microphone opens. Same commit: start block in CLAUDE.md, "Also read" in AGENTS.md, `/_work/` in .gitignore, this STATUS.md.
- September 2026 chat outputs (audience note, engine prompt, previews) live in /Users/alexanderfaisman/dev/Play6.3/_work/claude-outputs-2026-09 (git-ignored).
- Freeze before the editions work: tag `freeze-2026-10-07-build314`, copy /Users/alexanderfaisman/dev/_frozen/Play6.3_2026-10-07_build314.

## Open
- Alex: publish stories.sc.json (Brief 05a), create the App Store record, paste its Apple ID into story-comprehension.json `appStoreId`, archive Build/iOS-StoryComprehension in Xcode → TestFlight; new Story Comprehension Android APK (reads stories.sc.json).
- Next feedback round from Andrey (ask Alex).

## Next step
- Wait for Andrey's feedback round 6, then apply it.

## Waiting for Alex
- App Store record + Apple ID for Story Comprehension; publish of stories.sc.json.

## Decisions (append only)
- 2026-10-07: Story Comprehension is built incrementally inside this Unity project (Option 2), after a freeze; one code base, several store editions.
- 2026-10-07: dialogue script API — events add to the built-in reaction, one dialogue per page, all command names start with `Dialogue`.
- 2026-10-08: separate test app with its own catalog (`stories.test.json`), books in the one shared storage tree.
- 2026-10-11: each question book exists as four level versions (A–D); levels are tags, not letters in names; the adult picks the level; answers are saved on the device only.
- 2026-10-14: the new app gets its own screens (one merged Home / book list, level asked on first start).
- 2026-10-16: product name "Story Comprehension".
- 2026-10-31: Story Comprehension iOS = the same set-build-restore pattern as Android (RbTestBuild.ExportIOS), no per-edition folders, no Select menu; iOS icon = Editions/Icons/<id>.png on every slot; Story Comprehension reads stories.sc.json from build 333 on.
