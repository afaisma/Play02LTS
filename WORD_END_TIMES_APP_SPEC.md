# Claude Code hand-off — use per-word END times for word-tap (Path A, app side)

**Type:** small, additive, backward-compatible. Pairs with the SceneForge change that adds an
optional `end` (ms) to each word-timing. Slices a tapped word as `[start, end]` (the word's own
acoustic span) instead of `[start, nextWordStart]`, removing the neighbor-word bleed.
**File:** `Assets/_Story/Players/AudioAndTextPlayer.cs` (word-tap region).

## 1. Carry the optional end on the model
`WordTiming` (top of the file): add
```csharp
public float End = -1f;   // ms; -1 = absent (older / non-ElevenLabs / no-end books)
```

## 2. Parse `end` when present (graceful when absent)
Wherever a timings JSON node is converted to a `WordTiming` (the main `currentWordTimings` build
and the `_wordTapTimings` build — or the shared parse helper), set:
```csharp
wt.End = node["end"] != null ? node["end"].AsFloat : -1f;   // SimpleJSON: null when key missing
```
Old JSON (no `end`) → `End` stays `-1`. The highlight loop ignores `End` (it only reads `Time`), so
no behavior change there.

## 3. Use End for the slice end, with fallback
In `TryPlayWordAt`, replace the end computation:
```csharp
float clipLenMs = _wordTapClip != null ? _wordTapClip.length * 1000f : 0f;
float startMs = _wordTapTimings[i].Time;                 // exact onset (see step 4 — no lead-in)
float endMs   = (_wordTapTimings[i].End > startMs)
                  ? _wordTapTimings[i].End                // tight: the word's own end
                  : WordEndMs(_wordTapTimings, i, clipLenMs);  // fallback = current next-onset/clip rule
```
So Pigeon-style books (with `end`) get tight slicing; every other book uses the existing looser
boundary unchanged — no regression, no flag day.

## 4. Replace the lead-in with fades (kills clicks without pulling in the previous word)
The 70ms lead-in pulled audio from the previous word — remove it. Instead, seek to the exact start
and use short fades to avoid the seek/stop clicks:
- Drop `WordTapLeadInMs`; seek to `Mathf.Clamp(startMs/1000f, 0f, clipLen-0.001f)`.
- Add `const float WordTapFadeInMs = 18f;` (and keep `WordTapFadeOutMs`).
- Ramp `_wordTapSource.volume` 0 → target over `WordTapFadeInMs` at the start, and target → 0 over
  `WordTapFadeOutMs` before `Stop()`. **Capture and restore** the original target volume after the
  fade-out so the next tap isn't left silent (this was the earlier mute symptom).

## Tests (EditMode, pure logic)
- Parser: node with `end` → `WordTiming.End` set; without → `-1`.
- End selection: `End > start` → used as `endMs`; `End == -1` (or `<= start`) → falls back to
  `WordEndMs`. (`WordEndMs` + mapping/snap tests already exist.)

## Safety
Additive: one nullable field + one parse line + a ternary on the end. Default `-1` reproduces
current behavior exactly for books without `end`. Highlight loop and human-voice playback untouched.
All within the `// ---- word-tap ----` region.
