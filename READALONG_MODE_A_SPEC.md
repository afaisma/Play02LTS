# Claude Code hand-off — Read-Along (Mode A): child reads, highlight follows, auto-page

**Goal:** ship the validated read-along loop in the real app: the child reads the current page aloud,
recognized words advance the existing highlight, and the page auto-turns on lenient completion. The
matcher is already validated on real Vosk in `_ReadToMeTest` — this ports it into production.

**Discipline:** fully ADDITIVE + opt-in, same as the word-tap feature. With read-along mode OFF, every
existing path is byte-identical. Rollback = delete the service + the marked `// ---- read-along ----`
region in AudioAndTextPlayer + the toggle. Design source of truth:
`~/Documents/Claude/Projects/ReadingBuddySuite/RECOGNISSIMO_INTEGRATION_DESIGN.md` (§3, §4, §4a).

## Scope of THIS slice
- Mic-driven read-along on the current page, activated by one app-level toggle.
- Validated matcher (lookahead 2, stopword-safe) + lenient completion.
- NOT in this slice: the per-line dynamic window (phase 2), Mode B (`Listen`/`OnHear`), any MiniScript.
  Keep the page-restricted full-page grammar for now.

## Reuse from the harness (do not re-derive)
Lift the proven pieces from `Assets/_Story/ReadToMeTest/ReadToMeTest.cs`:
- The Recognissimo stack build (inactive GO → `MicrophoneSpeechSource` + `StreamingAssetsLanguageModelProvider`
  English `LanguageModels/en-US` + `SpeechRecognizer` with `EnableDetails=true`) and event wiring.
- `NormalizeWord`, and the `Feed(...)` matcher INCLUDING the stopword-safe guard and `[unk]` handling
  (this is the version we validated — lookahead 2, `[SKIP-SUPPRESSED]` on stopword skips, forward-only).

## 1. New `ReadAlongService` (Assets/_Story/Players/ReadAlongService.cs)
A self-contained MonoBehaviour that owns the Recognissimo stack (lazy, created once, kept warm) and the
matcher. Public surface:
- `void Begin(AudioListenerTextSource page)` — actually: take the current `AudioAndTextPlayer` + its
  expected word list; (re)start recognition with `Vocabulary` = the page's unique normalized words +
  `"[unk]"`; reset the cursor to 0.
- `void Stop()`.
- On each partial/final result: run the validated matcher to advance the cursor; call
  `audioAndTextPlayer.SetReadProgress(cursor)`; on lenient completion call `audioAndTextPlayer` →
  `PRScript.NextStep()` once.
Keep the model loaded across pages (don't dispose between pages); only the grammar/cursor reset per page.

## 2. `AudioAndTextPlayer` additions (in a marked `// ---- read-along ----` region)
- `public IReadOnlyList<string> ExpectedWords` — the current page's normalized words (built from the
  text passed to `PlayAudioAndText`/`PlayAudioAndShowText`; reuse the same tokenization the highlighter
  already has via `currentWordTimings`, normalized with `NormalizeWord`).
- `public void SetReadProgress(int wordIndex)` — set `currentWordIndex = wordIndex` and re-render the
  highlight by the EXISTING `UpdateHighlightedText` path, but driven by this index instead of audio time.
  In read-along mode there is NO page audio playing, so this must render the highlight without requiring
  `audioSource` to be playing. (Verify in the Editor that the highlight shows with no audio.)
- These are no-ops unless read-along mode is active. Do not touch the audio-driven highlight path.

## 3. Matcher (ported, already validated)
Forward cursor; for each recognized word, match `expected[cursor+look]` for `look=0..2`; on a `look>0`
match whose word is a stopword, SKIP it (`[SKIP-SUPPRESSED]`); `[unk]`/`unk` ignored; never moves
backward. Feed on BOTH partial and final. (This is the exact `Feed` we validated — copy it.)

## 4. Lenient completion (NOT literal last word — see §4a)
Auto-turn the page when ALL of: cursor ≥ ~90% of the page's words, AND at least one recognized word fell
in the last ~20% (the final line/region), AND a short trailing silence/timeout (~1.2s of no new
advance). Then call `NextStep()` exactly once and `Stop()`. (Tails get dropped / kids trail off; strict
`cursor==end` would never fire — this is the device "35/46" lesson.) Expose the threshold + timeout as
serialized fields.

## 5. Activation (app-level, no per-book script)
Add a single app-level "I Read" / read-along toggle (a Settings flag or an always-available UI control —
NOT via the `VoiceOptions` intrinsic, so existing books need no edits). When ON and a story page loads,
`ReadAlongService.Begin(currentPage)`; when the page changes, restart for the new page; when OFF, `Stop()`
and the app behaves exactly as today. Mic permission is requested by `MicrophoneSpeechSource` on first
use (ensure the iOS Microphone Usage Description is set in Player Settings).

## Safety / reversibility
Additive + opt-in; off = byte-identical. No writes to `audioSource`, the audio-driven highlight loop, or
any book script. One owner (`ReadAlongService`); AudioAndTextPlayer gets two small hooks in a marked
region. Rollback = delete the service, the region, and the toggle.

## Test plan (real mic, on a real Level-1/2 page)
1. Toggle read-along ON, open a simple page, read it aloud → words highlight in order as you read; no
   leapfrog on repeated words; page auto-turns shortly after you finish the last line.
2. Misread/skip a word → highlight tolerates it (lookahead) and keeps following.
3. Toggle OFF → the page plays/【highlights from audio exactly as before (no regression).
4. Confirm the tail recognizes on a live mic (the harness clip dropped it due to fast clip-feed; real-
   time mic audio should not).
