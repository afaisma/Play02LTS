# Read-to-Me / Recognissimo — reversibility & real-kids validation plan

**Written:** 2026-06-09 (Cowork)
**Companion to:** `READINGBUDDY_READ_TO_ME_PLAN.md` (the feature plan — §7 phases, §8 validation bar).
This doc answers two questions that plan left open: *how do we make every step cleanly
rollback-able* (because the realistic expectation is that recognition accuracy on real children
will NOT clear the bar on the first try), and *how do we test with real kids in a way we don't
have to repeat for every model/config iteration*.

---

## 0. Immediate housekeeping (before any feature work)

**574 MB of vendor payload is sitting UNTRACKED on `develop` right now** (verified 2026-06-09):
`Assets/Recognissimo/` (204 MB) + `Assets/StreamingAssets/LanguageModels/` (370 MB, **five**
languages: en-US, de-DE, es-ES, fr-FR, ru-RU). Until this is dealt with, any careless
`git add -A` bakes half a gigabyte into history permanently — git history never shrinks, so that
would be the single most *irreversible* act in this whole feature.

1. Create the spike branch first (`feature/read-to-me-spike` off `develop`).
2. Prune to **en-US only** (the plan is English-first, §12) — drops ~300 MB immediately.
3. Decide the model-distribution question (§2 below) BEFORE the first commit of these folders.

---

## 1. Reversibility = four nested layers

Each layer is a rollback lever; cost of rolling back rises as the feature graduates outward.
The feature only moves to the next layer after passing an explicit gate.

| Layer | Mechanism | Rollback action | Phase |
|---|---|---|---|
| L1 | **Git branch** `feature/read-to-me-spike` — all P0 spike work lives here; `develop` never sees it | delete the branch | P0 |
| L2 | **Scripting define** `READTOME` — on merge to develop, every touch point in existing files is wrapped in `#if READTOME`; the package and new code compile only when the define is set in Player Settings | remove the define → shipped code is byte-identical to pre-feature | P1 merge gate |
| L3 | **Runtime flag, default OFF** — feature activates only via the eligibility rule (plan §6) AND a master switch (PlayerPrefs + Settings) | flip the flag; no rebuild | P1 ship gate |
| L4 | **Remote kill switch** — a tiny config the app already fetches at launch governs the master switch; we own the CDN (`d1lgnf093kp9w0`), so a shipped App Store build can be silenced in minutes without app review | edit one file on the CDN | production |

**L4 implementation choice (pick one, smallest first):** (a) a new optional column in
`stories.csv` row 1 / a sentinel row — zero new fetch paths; or (b) a `readtome_config.json`
next to the CSV — cleaner, one extra small GET. *Recommended: (b);* the CSV is already
load-bearing and column-fragile (see ParseCSV tests).

### Containment rules that make L1–L2 cheap
- **All new code in one folder:** `Assets/_Story/ReadToMe/` (ReadAlongController, harness, etc.).
- **Touch points in existing files: hard cap of 3** — (1) `AudioAndTextPlayer` external-index
  mode, (2) voice-mode selector fourth button, (3) `PRScript` `EnableReadAlong` intrinsic.
  Each wrapped in `#if READTOME`. If implementation wants a 4th touch point, that's a design
  smell — bring it back for discussion.
- **REVERT manifest:** maintain `READTOME_REVERT_MANIFEST.md` listing every touched existing
  file + every added folder/asset/manifest entry/Player Setting. Full manual removal must be a
  mechanical checklist, not an archaeology project.
- **Scene changes:** none in P0 (throwaway UI instantiated from code). In P1, any `_Story.unity`
  edits (the fourth voice button) get listed in the manifest — scene diffs are the least
  revertible artifact in Unity, so keep them minimal and additive.

---

## 2. Keep the Vosk model OUT of git (recommended, decide now)

Committing even the 50 MB en-US small model means: repo +50 MB forever (even after rollback),
slower clones, and an App Store binary +50 MB **for every user including the ~100% who today
don't use the feature**.

**Recommendation:** treat language models like story content, not code — host the model zip on
our CDN (`uploads/models/vosk-model-small-en-us-0.15.zip` or similar), git-ignore
`Assets/StreamingAssets/LanguageModels/`, and have the app download + cache the model on first
entry to My Turn mode (Recognissimo supports remote model provisioning; the repo's DiskCache
pattern shows the team already does exactly this for media). Benefits:
- rollback never involves git history surgery;
- app binary size unchanged for non-users — feature cost is paid only by opt-in users;
- model upgrades (e.g. a children's-speech-adapted model after a failed accuracy gate, plan §8)
  become a CDN upload, not an app release;
- the L4 kill switch can also gate the download.

Trade-offs: first-use needs network + a download progress UI; the spike (P0) can dodge all of
this by keeping the model in StreamingAssets *on the branch only* and deferring CDN provisioning
to P1. The `Assets/Recognissimo/` plugin code (~204 MB but mostly per-platform native libs —
check what's strippable) does have to live in git once merged; prune unused platform binaries
and demo/sample folders before the merge commit.

---

## 3. Real-kids validation: collect once, evaluate many times

The expensive, unrepeatable asset is **children's audio**, not test runs. So the spike must
separate the two:

### 3.1 The corpus (collect once)
- In-person sessions, 5–10 children in the target band (plan §8), each reading 2 pages from the
  curated beginner set (Rhymebooks / `Alphabet` / `Counting` / `Colors` ...).
- **Record raw audio (WAV, 16 kHz mono) on-device in the harness build**, alongside a session
  log: per-page expected text, device, timestamps. Audio recording exists ONLY in the
  spike/harness build (L1 branch — it never ships), requires a parental-consent screen first,
  and files stay on device until the parent/we explicitly export them (share sheet). Get
  written consent; offer deletion on request. This is in-person testing with families we
  recruit — not App Store data collection — which keeps the COPPA surface minimal, but the
  consent form should still be reviewed before sessions.
- Result: `corpus/childNN_pageNN.wav` + `expected.txt` pairs — the permanent test fixture.

### 3.2 The offline evaluation harness (run many times — no kids needed)
- A small **Python script (Cowork-side work, lives outside the Unity repo** — e.g.
  `readingbuddy-aws/tools/` or a sibling folder): runs Vosk directly on the corpus WAVs with the
  same per-page vocabulary constraint, and reports per-recording **word-hit rate**,
  **false-highlight rate** (recognized words counted as matches that weren't read), and
  word-level timing.
- This is what makes the expected failure cheap: when accuracy disappoints, we iterate —
  different Vosk model sizes, a children's-speech-adapted model, vocabulary-constraint and
  fuzzy-match threshold tuning — **against the same recordings, in minutes, with zero new
  child sessions**. Only a config that wins offline graduates to an on-device re-test.
- Vosk is the same engine Recognissimo wraps, so offline numbers transfer; final confirmation
  on-device catches mic/latency effects only.

### 3.3 Metrics & gates (quantifying plan §8's "feels encouraging")
Proposed go/no-go numbers — product-owner call, adjust freely:
- **Word-hit rate ≥ 80%** (correctly-read words that light up) on the curated-set corpus;
- **False-highlight rate ≤ 5%** (words lighting without being read correctly);
- **Highlight latency ≤ ~1.5 s** median from word spoken to highlight (on-device measure);
- Bar must hold for the *median child*, not the corpus average (one clear-voiced kid must not
  carry the cohort).

### 3.4 Decision tree after the first corpus run
- **Clears the bar** → proceed to P1 under L2/L3 containment.
- **Close (e.g. 60–80% hit rate)** → iterate offline (models/tuning, §3.2); re-test on-device;
  time-box to ~2 weeks of iteration before escalating.
- **Far below** → stop. Delete the branch (L1 rollback — total cost: the spike + one corpus
  session, which we keep). Re-evaluate paths: children's-speech model fine-tuning, the Azure
  Pronunciation Assessment cloud path (plan §9) as the *primary* engine for a paid tier, or
  parking the feature. The corpus and the offline harness retain their full value for any of
  these — they are the durable output of P0 even in the failure case.

---

## 4. Suggested sequencing (delta to plan §7)

1. **Now:** housekeeping (§0) — branch, prune to en-US, model-distribution decision (§2).
2. **P0 spike on the branch** (plan §7 P0) + build the consent + WAV-recording harness page.
3. **Corpus sessions** (§3.1) — can start as soon as the harness records reliably; doesn't wait
   for the full recognition loop to be polished.
4. **Offline harness + first accuracy report** (§3.2–3.3) — Cowork-side, parallel to Unity work.
5. **Gate decision** (§3.4). Only on a clear pass does any of this graduate toward `develop`
   under the L2/L3 rules.

## 5. Hand-off split
- **Claude Code:** branch setup, package pruning, spike integration, harness page,
  `#if READTOME` containment when (if) it graduates, REVERT manifest upkeep.
- **Cowork:** consent form draft, corpus session protocol, offline Python evaluation harness,
  accuracy reports, CDN model hosting + kill-switch config when P1 arrives.
