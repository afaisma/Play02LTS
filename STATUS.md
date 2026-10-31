# STATUS — ReadingBuddy app (Play6.3)
Updated: 2026-10-30 (checked against git status and the diff).

## State
- Production app ReadingBuddy 3.1.x in the App Store and Google Play; 90 published books; speech engine (read-along page turn) shipped Sept 2026.
- Story Comprehension edition (spoken comprehension questions, for children with speech difficulties) is built from the same project on branch `feature/editions`. Test app id `com.imagiration.readingteacher.test` reads `stories.test.json`. Tester: Andrey (Boston University), feedback round 5 applied (icon, Settings layout, longer wait after a repeated question).
- Last commit (2026-10-30, build 332): second speech-timing fix, confirmed by Alex as fixing Andrey's "no pause after a repeated question" bug. `DialogueController.cs` counts the app as "speaking" from the moment a recording is asked for (`_promptPending`), with try/catch around the disk cache. `SpeechListenService.cs` waits up to `ArmGraceSeconds` (0.6 s) for a prompt to start and needs `SettleSeconds` (0.4 s) of quiet before the microphone opens. Same commit: start block in CLAUDE.md, "Also read" in AGENTS.md, `/_work/` in .gitignore, this STATUS.md.
- Untracked folder `Claude outputs/` (September 2026: audience note, engine prompt, previews) is not in git and not in .gitignore.
- Freeze before the editions work: tag `freeze-2026-10-07-build314`, copy /Users/alexanderfaisman/dev/_frozen/Play6.3_2026-10-07_build314.

## Open
- Next feedback round from Andrey (ask Alex).
- Old content-server address in this CLAUDE.md (`d5wtw8f0w3ire…`) is stale; the current one is in /Users/alexanderfaisman/dev/_hub/AWS.md. Fix when the CLAUDE.md is next revised.

## Next step
- Wait for Andrey's feedback round 6, then apply it.

## Waiting for Alex
- Should `Claude outputs/` be git-ignored, moved to `_work/`, or committed?

## Decisions (append only)
- 2026-10-07: Story Comprehension is built incrementally inside this Unity project (Option 2), after a freeze; one code base, several store editions.
- 2026-10-07: dialogue script API — events add to the built-in reaction, one dialogue per page, all command names start with `Dialogue`.
- 2026-10-08: separate test app with its own catalog (`stories.test.json`), books in the one shared storage tree.
- 2026-10-11: each question book exists as four level versions (A–D); levels are tags, not letters in names; the adult picks the level; answers are saved on the device only.
- 2026-10-14: the new app gets its own screens (one merged Home / book list, level asked on first start).
- 2026-10-16: product name "Story Comprehension".
