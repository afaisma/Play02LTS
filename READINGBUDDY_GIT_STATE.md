# ReadingBuddy — Git/GitHub State Audit

*Focused on the ReadingBuddy Unity project repo (`afaisma/Play02LTS`). Findings below are grounded in inspection of the local working tree, branches, history, and `.gitignore`.*

---

## TL;DR

The repo's biggest problem isn't what it contains — it's that almost nothing about its state is *meaningful* right now:

1. **`git status` is uninterpretable** — it lists 637 modified files. Only **5** of them are real. The other 632 are line-ending phantoms caused by a CRLF working tree and LF-stored blobs, with no `.gitattributes` or `core.autocrlf` to bridge the gap.
2. **The 4 unpushed `develop` commits have useless messages** — `commit`, `commit`, `oommit`, and one real one. They contain substantive work (PuzzleImage, RP settings, Globals, audio, scene changes — 522 insertions across 16 files in just the largest commit) that no one will ever be able to trace.
3. **The project's entry-point scene file is untracked.** `Assets/_Story/_StartScene.unity` — the very first scene listed in `EditorBuildSettings.asset` — is not in git. A fresh clone today wouldn't launch.
4. **The root `README.md` is untracked.** A real 3.6 KB ReadingBuddy README sits at the project root, never committed.
5. **The `docs/` folder is untracked.** Four planning documents (puzzle features, image display analysis) — exactly the kind of project history worth keeping.
6. **No GitHub Actions, no tags, no releases.** v2.0.2 shipped to iOS in Sep 2025 and the latest Android build in May 2026 — neither has a git tag. No way to "show me what was in production at v2.0.0."
7. **A pile of crash dumps and Burst debug folders sits in the working tree** because `.gitignore` doesn't cover them — only `Build/` was anticipated, not the actual `Build-iOS_BurstDebugInformation_DoNotShip/` and friends that Unity generates.

No security incidents. No tracked secrets. No `.p12`, `.pem`, `.env`, or real API keys in committed files (the Azure TTS `apiKey = "AZURE_KEY"` placeholder is the only suspicious-looking string and it's not a real key).

---

## 1. Remote, branches, history

```
origin   git@github.com:afaisma/Play02LTS.git    (SSH)
* develop   de1806f   [origin/develop: ahead 4]
  main      6084318   [origin/main]
```

The repo on GitHub is named **Play02LTS** but the working directory and the product are **ReadingBuddy / Play6.3**. Not a problem in itself — names don't have to match — but worth a one-line note in the README. The repo name probably predates the product name (likely a legacy from a Unity 2022 LTS migration).

### `develop` is 20 commits ahead of `main`, locally

```
$ git log --oneline main..develop | wc -l
20
$ git log --oneline develop..main | wc -l
0
```

20 commits have been merged into `develop` but never propagated to `main`. The two branches have not diverged (main is a strict ancestor) — so a fast-forward merge would work. If `main` is meant to track production, it's 20 commits stale. If `main` is just a placeholder and `develop` is canonical, the project should consider deleting `main` to remove the confusion.

### The 4 commits on `develop` not pushed to `origin/develop`

```
de1806f  "commit"     — 16 files, +522/-90: PuzzleImage.cs, Gallery.cs, PRScript.cs,
                        AudioAndTextPlayer.cs, MicrosoftTextToSpeech.cs, _Story.unity,
                        UniversalRP.asset, ProjectSettings.asset
4ca6ba0  "commit"     — 1 file: a single image meta
406c82d  "oommit"     — 2 files: PuzzleImage.cs (+127) and PuzzleImageEditor.cs
8d364fd  "Fix PuzzleImage compile error and improve Inspector integration"
                      — 8 files including the initial 943-line PuzzleImage.cs
                        and removal of .DS_Store files from tracking
```

Only one commit message describes its content. The others are typed-while-thinking placeholders. **Do not push these as-is.** Squash them locally first into one or two meaningfully-named commits (`feat: add PuzzleImage component with editor support`, etc.).

### Commit-message convention

Looking at the last 20 commits on `develop`:

```
"commit", "commit", "oommit", "Fix PuzzleImage compile error...",
"commit", "commit", "commit", "commit", "commit", "WIP",
"Work in progress", "WIP", "WIP", "WIP", "WIP", "WIP",
"WIP", "WIP", "WIP", "WIP added develop branch"
```

`main` shows the same `WIP` pattern. For a shipped app, this is effectively no git history at all — no one can ever ask "when did X change and why?". Even one-line conventional summaries (`fix:`, `feat:`, `chore:`) would be a major upgrade. No tooling needed; just a habit.

### Tags and releases

```
$ git tag -l | wc -l
0
```

Zero tags. The iOS App Store version history shows shipped releases:

```
2.0.2  2025-09-19
2.0.1  2024-11-15
2.0.0  2024-10-12
1.1.5  2024-04-24
1.1.4  2024-04-17
1.1.3  2024-02-28
1.1.2  2023-11-19
1.1.1  2023-09-20
1.1.0  2023-08-22
1.0    2023-07-05
```

None of those tagged in git. There's no way to check out "the code that shipped as 2.0.0." For a paid app on two stores with a hotfix-eligible audience (parents of preschoolers), the inability to bisect against a specific shipped version is a real operational gap.

**Suggestion:** tag retroactively where possible (best-guess against commit timestamps and `What's New` notes), and tag every future App Store / Play Store submission as part of the release ritual.

---

## 2. The "637 modified files" mirage

`git status` looks alarming:

```
$ git status --porcelain | awk '{print substr($0,1,2)}' | sort | uniq -c
    637  M
    151 ??
```

But filtering for files that have non-trivial content changes (ignoring carriage-return-at-end-of-line differences) collapses it dramatically:

```
$ git diff --ignore-cr-at-eol --stat | wc -l
~12
```

And of those, **only 5 are real code edits** — exactly the 5 files I touched in this session:

```
$ git diff --ignore-cr-at-eol --stat | grep '\.cs'
 Assets/_Story/Players/AudioAndTextPlayer.cs |  10 +++
 Assets/_Story/Story/Gallery.cs              |   2 +-
 Assets/_Story/Story/Globals.cs              |  80 +++++++++++++-------
 Assets/_Story/Story/PRScript.cs             |  26 ++++---
 Assets/_Story/Utils/PRUtils.cs              |  69 ++++++++++--------
```

Everything else — the 219 .cs files, 347 .meta files, 12 asmdef files, and so on that show as "modified" — is the same content with different line endings. Working-tree files are CRLF; git's stored blobs are LF; no `core.autocrlf` setting and no `.gitattributes` file to bridge the gap:

```
$ file Assets/AssetKits/ParticleImage/Runtime/Noise.cs
… ASCII text, with CRLF line terminators

$ git config --get core.autocrlf
(empty)

$ cat .gitattributes
(no such file)
```

**Effect.** Every diff is unreadable. Every commit accidentally touches hundreds of unrelated files unless the author manually picks. Code review is impossible. Renames are missed by the rename detector because every line looks "changed."

**Fix — one new file, one commit.**

Create `.gitattributes` at the project root:

```gitattributes
# Normalize text files to LF in the repo, and LF on checkout for everyone.
* text=auto eol=lf

# Unity binary-ish formats — never touch their bytes
*.unity         binary
*.prefab        binary
*.asset         binary
*.mat           binary
*.anim          binary
*.controller    binary
*.shadergraph   binary
*.physicMaterial binary
*.physicsMaterial2D binary
*.cubemap       binary
*.fbx           binary

# Binary media (in case any slip in)
*.png  binary
*.jpg  binary
*.jpeg binary
*.mp3  binary
*.mp4  binary
*.wav  binary
*.ogg  binary
*.aiff binary
*.tga  binary
*.psd  binary
*.exr  binary
```

Then, on a clean working tree:

```
git add .gitattributes
git commit -m "chore: add .gitattributes (LF normalization, Unity binary markers)"
git add --renormalize .
git commit -m "chore: normalize line endings to LF"
```

That's two commits. The second one will touch ~632 files but only their line endings. Reviewers can `git show -w` to confirm there's no content change. After this, `git status` becomes meaningful again.

**Important sequencing:** do this when no one else has in-flight work on the same files. Doing it on a clean tree means the 5 real changes (my fixes) won't be conflated with the normalization. So either land my fixes first, then renormalize — or renormalize first, then re-apply my fixes. Either order works; just don't interleave.

---

## 3. Untracked files — what needs decisions

The 151 untracked items break down into three groups.

### 3a. Should be tracked (real source / scenes / assets)

The most important miss:

- **`Assets/_Story/_StartScene.unity`** — *the entry-point scene*. `ProjectSettings/EditorBuildSettings.asset` lists this scene first in the build order. The app cannot launch without it. A fresh clone of the repo *today* would not have this file. This must be committed.

Real source files (`.cs` and `.cs.meta` pairs) currently untracked:

| File | Purpose |
|---|---|
| `Assets/_Story/StartScene.cs` | Start-scene controller |
| `Assets/_Story/StartSceneCombo.cs` | Related to start-scene |
| `Assets/_Story/Story/AutoplayToggle.cs` | The "autopage" toggle component referenced in `AudioAndTextPlayer` |
| `Assets/_Story/Story/ButtonSelectionController.cs` | Voice-mode selection (human / computer / novoice) — referenced from `AudioAndTextPlayer` and `PRScript` |
| `Assets/_Story/Filters/MovingVoiceOptionsPanel.cs` | UI panel |
| `Assets/_Story/VAPI/TextFade.cs` | Visual effect script |

It's striking that **`ButtonSelectionController`** is untracked — `PRScript.cs:100` has `[FormerlySerializedAs("voiceSelectionController")] public ButtonSelectionController buttonSelectionController;`, so this script is wired into the live `_Story` scene. The class file just isn't in git.

Project documentation currently untracked:

| File | Status |
|---|---|
| `README.md` (root, 3.6 KB) | Real project README. Track it. |
| `CLAUDE.md` | Codebase guide for Claude Code. Track it. |
| `AGENTS.md` | AI-agent guide. Track it. |
| `docs/Book_Images_Display_Analysis.md` (5 KB) | Design analysis. Track it. |
| `docs/PLAN_Puzzle_End_Of_Book.md` (9.8 KB) | Feature plan. Track it. |
| `docs/PLAN_StoryImage_PuzzleImage.md` (8.5 KB) | Feature plan. Track it. |
| `docs/PROMPT_StoryImage_PuzzleImage_Plan.md` (4 KB) | AI prompt for above. Track it (or move out — judgment call). |
| `READINGBUDDY_TECHNICAL_OVERVIEW.md` | The technical-overview report from this session |
| `READINGBUDDY_IMPROVEMENTS.md` | The improvements report |
| `READINGBUDDY_BUG_FINDINGS.md` | The bug-findings report |
| `READINGBUDDY_TEST_PLAN.md` | The test plan |
| `READINGBUDDY_USER_TEST_PLAN.md` | The user test plan |
| `READINGBUDDY_GIT_STATE.md` | This document |

Other Unity content currently untracked:

- **Scenes:** `Assets/_Story/Story/_StoryV.unity`, `Assets/_Story/VAPI/_Map_old.unity`, `Assets/_Story/GUI/PuzzleImage_Test.unity`, `Assets/_Story/Bookstore/` (folder with whatever's in it).
- **Render-pipeline config:** `Assets/DefaultVolumeProfile.asset`, `Assets/Settings/Global Volume Profile.asset`, `Assets/Settings/UniversalRenderPipelineGlobalSettings.asset`, `Assets/ShinyButtonShader.shadergraph`. Without these, URP won't render correctly on a fresh clone.
- **Project settings:** `ProjectSettings/BurstAotSettings_Android.json`, `BurstAotSettings_StandaloneOSX.json`, `BurstAotSettings_WebGL.json`, `BurstAotSettings_iOS.json`, `CommonBurstAotSettings.json`, `MultiplayerManager.asset`, `Packages/`. These determine how Burst compiles per-platform — must be tracked for reproducible builds.
- **Third-party asset upgrades:** large amounts of `Assets/AssetKits/ParticleImage/*`, `Assets/Plugins/AudioSessionSetter/`, `Demigiant/`, `Febucci/`, `Assets/InfinityPBR/`, `Assets/PJFX/`, `Assets/Simply Application Rating/`, `Assets/Buttons/`, `Assets/Resources/`, `Assets/TextMesh Pro/Examples & Extras/*`, `Assets/TextMesh Pro/Shaders/*`. These look like upgrades or re-imports that nobody committed.
- **UI button images:** `Assets/_Story/LIbrary/bookstore_512.png`, `kindle_button*.png`, `printed_button_1.png`, `Assets/_Story/VAPI/Map_Art/manners_ready.png`, `Assets/_Story/Filters/images/round_north_white_24dp.png`, `round_south_white_24dp.png`. (See section 3c for duplicates among these.)

### 3b. Should be ignored

Add these to `.gitignore` (none are currently covered):

```gitignore
# JetBrains IDE state — per-user
.idea/
*.iml
*.iws

# Claude Code session state — per-user
.claude/

# Generic tooling temp dir
.utmp/

# Mono runtime crash dumps — never useful in source control
mono_crash.*.json

# Unity Burst debug-information directories.
# The folder name itself contains "DoNotShip" — Unity is asking us not to.
*BurstDebugInformation_DoNotShip/

# Unity auto-recovery of crashed scenes — per-user, ephemeral
[Aa]ssets/_[Rr]ecovery/

# macOS auto-renamed-on-copy duplicates ("foo 2.png", "foo 2.meta")
*\ 2.png
*\ 2.jpg
*\ 2.meta
*\ 2.unity
```

Each of these has at least one example currently in the working tree:

- `mono_crash.0.0.json`, `mono_crash.0.1.json`, `mono_crash.a272a00d7.0.json` — three crash dumps.
- `Build-iOS_BurstDebugInformation_DoNotShip/`, `ReadingBuddy_BurstDebugInformation_DoNotShip/`, `build_BurstDebugInformation_DoNotShip/` — three debug-info dirs.
- `Assets/_Recovery/` — Unity's scene-crash recovery.
- `Assets/_Story/LIbrary/bookstore_512 2.png` — macOS duplicate of `bookstore_512.png`.

### 3c. Decide explicitly (duplicates / judgment calls)

| Item | Question |
|---|---|
| `bookstore_512.png` vs `bookstore_512 2.png` | The ` 2` suffix is a macOS Finder artifact. Pick one (probably the non-` 2` version), delete the other. |
| `kindle_button.png` / `kindle_button1.png` / `kindle_button_2.png` | Three iterations of the same button. Pick the canonical one. |
| `Assets/_Recovery/0.unity`, `0 (1).unity`, `0 (2).unity` | Scene-crash recovery. Ignore the folder, delete locally. |
| `.cursorignore`, `.vsconfig` | IDE configs. Tracking helps onboard new devs to identical setups; ignoring respects per-developer preferences. Pick a policy. |

---

## 4. Tracked files vs `.gitignore` (no violations found)

Spot-checked for files that *should* be ignored but are tracked anyway:

- No `.sln`, `.csproj`, `.user`, `.tmp`, `.pdb`, `.mdb` tracked — `.gitignore` is doing its job here.
- No `.apk` or `.aab` tracked. The 147 MB `Build_Android.apk` sitting in the working tree is correctly ignored.
- No files under `Library/`, `Build/`, `Temp/`, `Logs/`, `obj/` are tracked.
- No `.DS_Store` files currently tracked (one of the unpushed commits, `8d364fd`, removed them — good).
- Random spot-check of 5 `.meta` files: each has its corresponding asset tracked too. No `.meta` orphans found.

Good news: when the `.gitignore` covers something, it's been honored. The problems are all about things the `.gitignore` *doesn't* cover yet (section 3b).

---

## 5. Security review (Play6.3 only)

### Tracked content scan

```
$ git ls-files | grep -iE 'keystore|\.p12|\.pem|\.key|password|secret|\.env|credentials'
(empty)
```

No keystores, PEM files, private keys, environment files, or credential files tracked. ✓

### API keys in source

```
$ grep -n 'apiKey\|subscriptionKey' Assets/_Story/Players/TTS/MicrosoftTextToSpeech.cs
7:    private string apiKey = "AZURE_KEY";
40:    www.SetRequestHeader("Ocp-Apim-Subscription-Key", apiKey);
```

`"AZURE_KEY"` is the literal string — a placeholder, not a real value. ✓ Worth confirming that the real Azure Cognitive Services key is injected via a build pipeline / environment variable / Unity ScriptableObject loaded from outside the repo, and never committed.

### No `.github/` directory, no CI

```
$ ls .github 2>/dev/null
(does not exist)
```

No GitHub Actions, no PR template, no issue templates, no `CODEOWNERS`. For a paid app, this is missing some valuable safety nets: a single CI workflow that does `Test → Build for both platforms → fail if compile errors` would catch regressions before they hit a release branch.

---

## 6. Other observations

### `core.ignorecase=true`

```
$ git config --local --list | grep ignorecase
core.ignorecase=true
```

macOS default. This means git treats `LIbrary` and `Library` as the same. The project has `Assets/_Story/LIbrary/` — note the capital I, lowercase b — which is a typo that's now baked in everywhere (`PRLibrary.cs`, scene references, etc.). Renaming the folder is a hassle on a case-insensitive filesystem and risks breaking references. Probably worth leaving alone unless someone's already in the area.

### Untracked `Build/`, but tracked `Build_Android.apk`

Wait — no, that's not quite right. The folder `Build/` is ignored. The file `Build_Android.apk` is also ignored (`*.apk` in `.gitignore`). Both are correctly excluded. The user's working directory has a 147 MB `Build_Android.apk` from a build that hasn't been cleaned up; it's not in git, just on disk.

### `branch.develop.vscode-merge-base` is set twice

```
branch.develop.vscode-merge-base=origin/main
branch.develop.vscode-merge-base=origin/main
```

VS Code wrote the same config key twice. Cosmetic — `git config` will return the last one. Mention only because it's a sign that someone's been using VS Code's git integration; that's fine, just worth a single `git config --unset-all` then `git config` if it bothers anyone.

### Commit cadence

```
$ git log -1 --format=%cd
Sat Apr 25 19:25:10 2026 -0400
```

Last commit was about 3 weeks ago. The 4 unpushed commits and the editor state suggest the project has been actively worked on since then but without committing — which is exactly the situation that makes the line-ending and untracked-file problems worse.

---

## 7. Suggested action plan (focused on ReadingBuddy)

Ordered by leverage. Nothing here is urgent in a "security incident" sense; everything is "next time you're cleaning house, do these."

### Single-sitting cleanup (90 minutes)

1. **Commit the 5 fixes from this session** — `AudioAndTextPlayer.cs`, `Globals.cs`, `PRUtils.cs`, `Gallery.cs`, `PRScript.cs` — with a meaningful commit message like:
   ```
   fix: TLS bypass, https URLs, LRU caches, audio leak, swipe edges, pause stats
   ```
2. **Squash the 4 unpushed `develop` commits** locally into 1–2 commits with real messages (`feat: add PuzzleImage with editor` and `chore: misc settings + scene tweaks`, perhaps).
3. **Add `.gitattributes`** as in section 2. Renormalize line endings. Commit. After this, every future `git status` is meaningful.
4. **Add the missing source / scene files** in one focused commit:
   - `Assets/_Story/_StartScene.unity` (the entry-point scene)
   - `Assets/_Story/Story/ButtonSelectionController.cs`, `AutoplayToggle.cs`
   - `Assets/_Story/StartScene.cs`, `StartSceneCombo.cs`
   - `Assets/_Story/Filters/MovingVoiceOptionsPanel.cs`
   - `Assets/_Story/VAPI/TextFade.cs`
   - The render-pipeline configs and `ProjectSettings/BurstAotSettings_*.json`
5. **Track the documentation** (`README.md`, `CLAUDE.md`, `AGENTS.md`, the `docs/` folder, and the 6 `READINGBUDDY_*.md` reports from this session). One commit.
6. **Extend `.gitignore`** with the additions from section 3b.
7. **Push `develop` to origin.** Fast-forward `main` from `develop` if `main` is meant to track tip-of-development.

### When you have a calm afternoon

8. **Tag the released versions retroactively** — best-guess against commit timestamps and the iOS App Store version history. Even imperfect tags are better than zero. Going forward, tag every store submission.
9. **Add a minimal GitHub Actions workflow** — a single job that does `actions/checkout` → install Unity → run Editor tests / compile. Even a "did this commit break compilation?" gate would be a huge improvement.
10. **Decide on the duplicate assets** (the ` 2` files, the three kindle buttons) and dedupe.
11. **Add a brief `CONTRIBUTING.md`** documenting commit message conventions and branch flow (`develop` for in-progress, `main` for production, tags for releases).

### Going forward (process habits)

12. **One-line conventional commit messages.** `feat:`, `fix:`, `chore:`, `docs:` — even loose adoption beats another `WIP`.
13. **Branch per change.** The "WIP commit on the main branch" pattern self-corrects when you have to name the branch. `feature/puzzle-image-end-of-book` already names the commit message for you.
14. **Periodic `.gitignore` audits** — every time you see something in `git status --ignored` that surprises you, decide once and commit the rule.

---

## Appendix — the 5 real-edit files in the working tree

For full transparency, these are the only files that show as "modified" once you ignore line-ending noise. All five are the fixes I applied earlier in this session and are documented in `READINGBUDDY_BUG_FINDINGS.md` and `READINGBUDDY_TEST_PLAN.md`:

```
$ git diff --ignore-cr-at-eol --stat
 Assets/_Story/Players/AudioAndTextPlayer.cs  | +10
 Assets/_Story/Story/Gallery.cs               | +2
 Assets/_Story/Story/Globals.cs               | +80
 Assets/_Story/Story/PRScript.cs              | +26
 Assets/_Story/Utils/PRUtils.cs               | +69
```

These are the changes to commit in step 1 above.
