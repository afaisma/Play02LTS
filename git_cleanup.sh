#!/usr/bin/env bash
#
# ReadingBuddy git cleanup — fully non-interactive.
#
# Run this once from /Users/alexanderfaisman/dev/Play6.3 in your normal
# terminal (NOT from Claude's sandbox — the sandbox can't delete
# .git/index.lock, which is why you're running this yourself).
#
#     cd ~/dev/Play6.3
#     ./git_cleanup.sh
#
# It will commit and push in one go, with no prompts. Each stage logs
# clearly what it's doing. Safe to re-run: stages that are already
# done become no-ops.
#
# What this script DOES rewrite:
#   - Nothing already pushed to origin. Only adds new commits on top
#     of current HEAD.
#
# What this script does NOT do:
#   - It does NOT rewrite the four pre-existing unpushed commits
#     ("commit", "commit", "oommit", "Fix PuzzleImage..."). They will
#     be pushed as-is. If you ever want to clean those up, do it later
#     with an interactive `git rebase -i origin/develop` from your
#     terminal.
#   - It does NOT touch the `main` branch.
#   - It does NOT add tags. Optional follow-ups listed at the bottom.
#
# Two pre-staged files are already in your working tree (created by Claude):
#   - .gitattributes      (new — LF normalization + Unity binary markers)
#   - .gitignore          (extended — IDE state, crash dumps, etc.)
#
# Safety check: the script aborts immediately if the renormalization
# step produces anything other than pure line-ending changes.

set -euo pipefail

cd "$(dirname "$0")"

log() { printf "\n=== %s ===\n" "$*"; }

log "Stage 0 — Pre-flight"

# Verify we're in the right place
if [[ ! -d .git ]] || ! grep -q "Play02LTS" .git/config; then
    echo "ERROR: this doesn't look like the ReadingBuddy repo. Aborting." >&2
    exit 1
fi

# Clear any stuck lock from earlier sessions
if [[ -e .git/index.lock ]]; then
    echo "Removing stuck .git/index.lock"
    rm -f .git/index.lock
fi

# Confirm git user is configured (commits will fail if not)
if ! git config user.email >/dev/null 2>&1; then
    git config user.email "afaism@gmail.com"
    echo "Set git user.email = afaism@gmail.com"
fi
if ! git config user.name >/dev/null 2>&1; then
    git config user.name "afaisma"
    echo "Set git user.name = afaisma"
fi

CURRENT_BRANCH=$(git rev-parse --abbrev-ref HEAD)
if [[ "$CURRENT_BRANCH" != "develop" ]]; then
    echo "ERROR: expected to be on 'develop', currently on '$CURRENT_BRANCH'. Aborting." >&2
    exit 1
fi
echo "On branch: develop"
echo "HEAD:      $(git rev-parse --short HEAD)"

# A small helper: only commit if there's something staged.
commit_if_staged() {
    local msg=$1
    if ! git diff --cached --quiet; then
        git commit -m "$msg"
    else
        echo "(nothing to commit for: ${msg%%$'\n'*})"
    fi
}

log "Stage 1 — Commit the 5 working-tree fixes"
# AudioAndTextPlayer / Globals / PRScript / PRUtils: the safe fixes from
# the audit (TLS bypass, https URLs, LRU caches, audio leak, swipe edges,
# pause stats, CSV try/catch, Lavender color).
# Gallery.cs: pre-existing change (puzzle button sprite fixed to index 3)
# preserved per your decision.

git add \
    Assets/_Story/Players/AudioAndTextPlayer.cs \
    Assets/_Story/Story/Globals.cs \
    Assets/_Story/Story/PRScript.cs \
    Assets/_Story/Story/Gallery.cs \
    Assets/_Story/Utils/PRUtils.cs 2>/dev/null || true

commit_if_staged "fix: TLS verification, https URLs, LRU caches, audio leak, swipe edges, pause stats

- PRUtils.DownloadFile/DownloadImage: drop AcceptAllCertificatesHandler
  (TLS verification now enabled), accept https:// scheme, dispose
  UnityWebRequest in using blocks.
- PRUtils + AudioAndTextPlayer: make caches true LRU (move-to-end on hit).
- AudioAndTextPlayer: destroy previous Fragment_* clip before reassigning
  audioSource.clip, fixing per-page memory leak.
- PRScript.LeftSwipe/RightSwipe: edge of a multi-image gallery now
  advances/retreats the page instead of swallowing the swipe.
- Globals: persist session stats on OnApplicationPause(true), not just
  OnApplicationQuit; reset gameStartTime in UpdateGameStatistics to
  avoid double-counting across pause/resume.
- Globals.ParseCSV: per-row try/catch so one malformed row no longer
  aborts the whole catalog load.
- PRUtils.pastelColors: fix Pastel Lavender (was using alpha as blue).
- PRScript.RightSwipe: log says RightSwipe now (was LeftSwipe).
- Gallery.cs: keep ShowPuzzleButton using sprites[3] (pre-existing)."

log "Stage 2 — Commit the .gitignore extensions"
git add .gitignore
commit_if_staged "chore(gitignore): ignore IDE state, crash dumps, Burst debug dirs, recovery, macOS dup files"

log "Stage 3a — Commit .gitattributes"
git add .gitattributes
commit_if_staged "chore: add .gitattributes (LF normalization, Unity binary markers)"

log "Stage 3b — Renormalize line endings (safety-checked)"
git add --renormalize . 2>/dev/null || true

if git diff --cached --quiet; then
    echo "Nothing to renormalize (already clean)."
else
    if git diff --cached -w --quiet; then
        echo "Verified: only line endings changed."
        git commit -m "chore: normalize line endings to LF

Touches many files; only line endings change. Verified with
'git diff --cached -w' showing zero content differences.

This commit's SHA is added to .git-blame-ignore-revs so
'git blame' will skip it."
        RENORM_SHA=$(git rev-parse HEAD)
        # Append to (or create) .git-blame-ignore-revs
        printf "%s\n" "$RENORM_SHA" >> .git-blame-ignore-revs
        git add .git-blame-ignore-revs
        git commit -m "chore: register line-ending renormalization in .git-blame-ignore-revs"
        git config blame.ignoreRevsFile .git-blame-ignore-revs
        echo "blame.ignoreRevsFile configured locally."
    else
        echo "ABORT: renormalization changed more than just line endings." >&2
        echo "Inspect with 'git diff --cached -w' and adjust .gitattributes." >&2
        echo "No commit has been made for this stage; later stages are skipped." >&2
        echo "All earlier stages already succeeded — repo is in a recoverable state." >&2
        exit 2
    fi
fi

log "Stage 4 — Add missing source/scene files"
# _StartScene.unity is the entry-point scene (first in EditorBuildSettings.asset).
# The others are scripts the live _Story scene references but weren't tracked.
# Use --ignore-errors so missing-on-disk files don't abort everything.

add_if_present() {
    if [[ -e "$1" ]]; then git add "$1"; fi
}

add_if_present Assets/_Story/_StartScene.unity
add_if_present Assets/_Story/_StartScene.unity.meta
add_if_present Assets/_Story/StartScene.cs
add_if_present Assets/_Story/StartScene.cs.meta
add_if_present Assets/_Story/StartSceneCombo.cs
add_if_present Assets/_Story/StartSceneCombo.cs.meta
add_if_present Assets/_Story/Story/AutoplayToggle.cs
add_if_present Assets/_Story/Story/AutoplayToggle.cs.meta
add_if_present Assets/_Story/Story/ButtonSelectionController.cs
add_if_present Assets/_Story/Story/ButtonSelectionController.cs.meta
add_if_present Assets/_Story/Filters/MovingVoiceOptionsPanel.cs
add_if_present Assets/_Story/Filters/MovingVoiceOptionsPanel.cs.meta
add_if_present Assets/_Story/VAPI/TextFade.cs
add_if_present Assets/_Story/VAPI/TextFade.cs.meta

commit_if_staged "fix: track entry-point scene and previously-untracked scripts

Most importantly: Assets/_Story/_StartScene.unity is the first scene
listed in EditorBuildSettings.asset (the launch scene). It was missing
from version control — a fresh clone could not run the project.

Also track the wiring scripts the live _Story scene references:
ButtonSelectionController (voice-mode UI), AutoplayToggle, the
StartScene controllers, MovingVoiceOptionsPanel, TextFade."

log "Stage 5a — Add project documentation"
add_if_present README.md
add_if_present CLAUDE.md
add_if_present AGENTS.md
if [[ -d docs ]]; then git add docs/; fi
commit_if_staged "docs: track README, CLAUDE.md, AGENTS.md, docs/ planning files"

log "Stage 5b — Add audit reports from this session"
for f in \
    READINGBUDDY_TECHNICAL_OVERVIEW.md \
    READINGBUDDY_IMPROVEMENTS.md \
    READINGBUDDY_BUG_FINDINGS.md \
    READINGBUDDY_TEST_PLAN.md \
    READINGBUDDY_USER_TEST_PLAN.md \
    READINGBUDDY_GIT_STATE.md; do
    add_if_present "$f"
done
commit_if_staged "docs: ReadingBuddy audit reports (overview, bugs, tests, git state)"

log "Stage 6 — Track this cleanup script too"
add_if_present git_cleanup.sh
commit_if_staged "chore: add git_cleanup.sh (the script that produced these commits)"

log "Stage 7 — Push to origin/develop"
echo "About to push these commits on top of origin/develop:"
git log --oneline origin/develop..HEAD
echo ""
echo "Total: $(git rev-list --count origin/develop..HEAD) commits"
echo ""
git push origin develop

log "Done."
cat <<'EOM'

Summary of what just happened:
  - All staged changes committed in clean, individually-named commits.
  - Line endings normalized; .git-blame-ignore-revs configured locally.
  - The previously-untracked entry-point scene and wiring scripts are
    now in version control.
  - All audit-report markdown files are tracked.
  - develop is pushed to origin.

The four pre-existing unpushed WIP-named commits were NOT rewritten —
they pushed as-is. If you want to clean their messages later, run:
    git rebase -i origin/develop~N
where N is past the new commits (count them with git log --oneline).

Optional follow-ups:
  - Tag retroactive releases against the App Store version history,
    e.g.   git tag -a v2.0.2 <sha> -m "iOS release Sep 2025"
           git push --tags
  - Fast-forward main to develop (only if main is meant to track tip):
           git checkout main && git merge --ff-only develop && git push
           git checkout develop
  - Add a minimal CI workflow at .github/workflows/build.yml

EOM
