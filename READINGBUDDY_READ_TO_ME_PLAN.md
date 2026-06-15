# "Read to Me" — Child-Reads-Aloud Mode: Implementation Plan

**Status:** Proposal / build brief
**Date:** 2026-05-30
**Audience:** Claude Code (implementation), product owner (scope decisions)
**One-liner:** Add a mode where the child reads a page aloud and the app highlights words as it recognizes them, using offline speech recognition (Recognissimo / Vosk) so it costs nothing per use and keeps the app free.

> This is a planning document, not code. Actual C# changes belong in Claude Code per the repo's working conventions.

---

## 1. Goal and product framing

Today ReadingBuddy reads *to* the child: it narrates a page and highlights each word as the audio plays. This feature reverses the roles — the child reads aloud and the app follows along, lighting up words as they are read correctly. It turns a read-aloud player into a reading *practice* tool, which is the strategic differentiator and the foundation for a future subscription.

Two constraints shape every decision below:

- **Free-tier safe.** Recognition runs fully offline on-device (Recognissimo wraps the Vosk engine). There is no per-read cloud cost, so the feature can ship in the free tier without breaking the economics. Optional cloud pronunciation scoring is reserved for a future paid tier (§9).
- **Encouraging, never punishing.** This is a reading buddy, not an exam. Nothing turns red, nothing says "wrong." Unrecognized words simply stay un-highlighted and the child moves on.

---

## 2. Why this is cheap to build (the reuse insight)

Three things already in the codebase map almost one-to-one onto what this feature needs:

1. **The highlighter already exists.** `AudioAndTextPlayer` rebuilds two stacked TMP layers every frame and advances a forward-only "active word" index. Today that index is driven by audio playback time (`audioSource.time * 1000 - 500`). "Read to Me" is the *same highlighter driven by a different signal* — the speech recognizer sets the active index instead of playback position. No new reading screen.

2. **The answer key already exists.** Each page's script calls `PlayAudioAndText(chunk, content)`, where `content` is the exact expected text. Tokenizing that string yields the per-page word list to constrain the recognizer (the "Vocabulary" feature), which is what makes children's voices tractable.

3. **A muted-narration mode already exists.** The `novoice` voice mode already mutes narration. "Read to Me" behaves like a fourth voice mode in which the child is the narrator.

Net effect: this is largely a matter of wiring an existing recognizer to an existing highlighter through one new controller — not building speech UI from scratch.

---

## 3. Architecture

### 3.1 New and changed components

| Component | Type | Responsibility |
|---|---|---|
| `ReadAlongController` | **new** MonoBehaviour | Per-page lifecycle: tokenize expected text, configure recognizer vocabulary, consume recognition events, advance the highlight pointer, detect stuck words and page completion. |
| `AudioAndTextPlayer` | **change** | Add a mode flag so the active-word index can be set externally (by `ReadAlongController`) instead of derived from playback time. Reuse the existing two-layer TMP rendering unchanged. |
| Recognissimo recognizer | **integrate** | One reusable instance; speech source = microphone; vocabulary reconfigured per page. |
| Voice-mode selector (`ButtonSelectionController` / `PreparePlayVoiceSettings`) | **change** | Add a fourth mode ("My Turn") alongside `human` / `computer` / `novoice`. Selecting it mutes narration and activates `ReadAlongController`. |
| `EnableReadAlong` intrinsic (optional) | **new** | Per-book manual override in story scripts (same pattern as existing intrinsics in `PRScript.SetupInterpreter()`). See §6. |

### 3.2 Per-page data flow (in My Turn mode)

```
Page enter
  → take page's expected text (the `content` arg of PlayAudioAndText)
  → tokenize into words vs. punctuation/space
      (reuse the SAME tokenizer AudioAndTextPlayer uses, so both stay in lockstep)
  → build vocabulary = {expected words} + "[unk]"
  → reconfigure recognizer vocabulary; start mic capture
  → as recognized words stream in:
        fuzzy-match against expected sequence with a forward-only pointer
        on match → advance highlight index, color word "got it" (green)
        no match / "[unk]" → ignore, keep listening
  → word unread for N seconds → gentle hint (pulse + replay that one word)
  → last word matched → page complete → celebrate → optional auto-advance
Page leave
  → stop capture; keep recognizer instance for reuse
```

The recognizer is instantiated once and its vocabulary swapped per page (Vosk supports reconfigurable vocabulary), avoiding repeated model load cost.

---

## 4. Microphone and permissions

- **Permission request:** ask once, on first entry to My Turn mode, **behind the existing parental gate** to satisfy COPPA-style consent expectations for a children's app.
- **Platform manifests:** iOS requires `NSMicrophoneUsageDescription`; Android requires `RECORD_AUDIO`.
- **Capture path:** configure Recognissimo's speech source as microphone directly; this likely avoids hand-rolling Unity's `Microphone` class plumbing. Confirm during the spike (§8, P0).
- **Privacy posture:** audio is processed on-device and never leaves the phone. This is both the compliance-simplifying choice and a marketing line ("your child's voice stays on your device"). Any future cloud scoring (§9) must be a separate, explicitly consented path.

---

## 5. What it looks like to the child (UX)

- Entry: a **mic icon ("My Turn")** in the existing voice-mode selector. Tapping it mutes narration (like `novoice`) — the child becomes the narrator.
- Continuity: in listen mode the app lights words as *it* reads; in My Turn the child lights the *same* words by reading them. Identical visual language, roles reversed — nothing new to learn.
- A subtle "listening" indicator (e.g. a soft pulsing mic) shows the app is following.
- **Stuck handling:** if a word goes unread for a few seconds, it pulses gently and a tap replays just that word — sliced from the chunk audio using the stored word-timing range, or re-spoken via TTS. Optional auto-hint after a longer pause.
- **No failure states:** no red, no "incorrect." Unrecognized words stay un-lit; the child can move on freely (including skipping ahead — the forward-only pointer tolerates jumps).
- **Completion:** a small celebration on finishing the page; a quiet words-correct-per-minute (WCPM) tally recorded for a future parent dashboard.

---

## 6. Availability: universal capability, surfaced selectively

**Decision: a universal capability gated by an eligibility rule — not a hand-built per-book feature, and not a blunt always-on toggle.**

- *Not per-book special UI*, because every book already carries per-page expected text, so the feature works everywhere for free. Bespoke per-book UI would discard that and create authoring debt.
- *Not a blunt everywhere-toggle* (unlike puzzle/unpuzzle), because the catalog spans ages 2–12. A beginning reader will fail on a dense fairytale page and a toddler can't read at all. Failing a puzzle is fun; failing to read is discouraging. So the mode must only appear where a beginner can plausibly succeed.

**Eligibility rule:** the "My Turn" button appears only on books/pages where the text is short and within a beginner band. Derive from a simple readability signal — words-per-page combined with the existing `age_from` CSV column. Provide a cheap manual override:

- a one-line script directive `EnableReadAlong(1)` / `EnableReadAlong(0)` (new intrinsic), **or**
- a new optional column in `stories.csv`.

**Curated launch set (doubles as the accuracy test cohort, §8/§9):** enable first on true-beginner titles already in the catalog — the Rhymebooks and single-word/short-line books (`Alphabet`, `Counting`, `Colors`, `Run`, `Jump`, `Eat`, etc.). Validate there, then widen by rule.

---

## 7. Phased plan

### P0 — Spike & feasibility (1–2 weeks)
- Import Recognissimo; get offline recognition running in the `_Story` scene on one device per platform.
- Confirm mic capture path and per-page vocabulary reconfiguration.
- Throwaway UI: log recognized words against a hard-coded expected list for one page.
- **Exit criterion:** recognized-word stream visibly tracks an adult reading a short page with vocabulary constraint on.

### P1 — MVP (the feature, free tier)
- `ReadAlongController` with forward-only matching against the page's expected text.
- `AudioAndTextPlayer` external-index mode; reuse existing highlight rendering.
- Fourth voice mode in the selector; narration mutes on entry.
- Parental-gated mic permission flow.
- Stuck-word hint (replay single word) and page-completion celebration.
- Eligibility rule + manual override; enabled on the curated beginner set only.
- **Exit criterion:** a child can read a curated beginner book end to end, words light up as read, no failure states, no per-use cost.

### P2 — Measurement & parent value
- WCPM + accuracy capture per page/session; persist via `PlayerPrefs` (consistent with existing progress keys).
- Minimal parent view: minutes read, words read, level progress.
- Widen eligibility by rule beyond the curated set.

### P3 — Premium pronunciation (paid tier, optional)
- Add Azure Speech **Pronunciation Assessment** (you already use Azure TTS) as an opt-in, consented, cloud path for per-word accuracy/fluency scoring.
- Cloud cost attaches only to paying users.

---

## 8. Validation: prove accuracy on real kids before committing

Do not decide from datasheets. During/after P0:

- Record **5–10 children in the target age band** reading two pages from the curated set.
- Run through Recognissimo **with the per-page vocabulary constraint on**.
- Measure word-hit rate.
- **Go/no-go bar:** hit-rate high enough that the experience feels encouraging (target: the large majority of correctly-read words light up promptly). If it clears the bar, proceed to P1; if not, evaluate a children's-speech-adapted Vosk model before investing further.

---

## 9. Risks and mitigations

| Risk | Mitigation |
|---|---|
| Children's speech is hard for ASR (pitch, disfluency, invented pronunciation). | Per-page **vocabulary constraint** turns open transcription into near multiple-choice. Generous thresholds. Target ages ~5+; treat 2–4 as listen-only. |
| Noisy home environments degrade recognition. | Voice-activity detection (Recognissimo includes one); forgiving stuck-word handling rather than hard fails. |
| Single-vendor wrapper (Recognissimo) risk. | Underlying engine is open-source Vosk; worst case, drop to Vosk directly and keep the matching/UI work. |
| Latency / per-page recognizer setup cost. | One reused recognizer instance; swap vocabulary per page, don't reload the model. |
| Discouraging a struggling reader. | No red, no "wrong"; gentle hints; child can skip ahead freely. |
| COPPA / mic consent. | On-device processing; parental-gated permission; cloud scoring kept separate and explicitly opt-in. |

---

## 10. Open decisions

1. **Eligibility source:** derive from words-per-page + `age_from`, or require explicit opt-in per book via `EnableReadAlong` / CSV column, or both (auto with override)? *Recommended: auto rule + override.*
2. **Auto-advance on page completion** in My Turn mode: on by default, or require a tap? *Recommended: respect the existing Autopage toggle.*
3. **Stuck-word replay source:** slice chunk audio via stored word timings vs. re-speak via TTS. *Recommended: timing-slice when available, TTS fallback.*
4. **Where WCPM lives:** `PlayerPrefs` only (offline) for MVP, with parent dashboard deferred to P2.

---

## 11. Licensing checklist (before shipping)

- Recognissimo license permits redistribution inside a commercial app.
- The specific bundled Vosk model has a compatible license (most are Apache-2.0; some large models differ).

---

## 12. Out of scope (for this plan)

- Open-vocabulary dictation (not needed — the expected text is always known).
- Cloud ASR in the free tier.
- Multi-language read-along (English first; revisit alongside any broader localization decision).
- Updating `CLAUDE.md` / overview docs to reflect new intrinsics (separate doc-debt task).
