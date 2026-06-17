# Claude Code hand-off — tap-a-word plays ONLY from the per-book word bank (no fallback)

**Type:** simplification + feature. The tap-to-hear feature now plays a tapped word **only** from the
per-book `wordbank.mp3`/`wordbank.json`. If there is no word bank, or the tapped word isn't in it,
**nothing plays** and the tap falls through to navigation (so books without a bank behave exactly as
before the feature existed). The old per-page audio-slicing path is removed entirely.
**File:** `Assets/_Story/Players/AudioAndTextPlayer.cs` + `WordTapHandler.cs`.

## Behavior
- Tap maps to the on-screen word via TMP's own word info (no timings needed).
- Word bank loaded AND contains the (normalized) tapped word → play its clean isolated slice.
- Otherwise → no playback, do NOT consume the tap (let Next/Prev/swipe navigation handle it).

## KEEP (the word-bank path)
- `_wordBankClip`, `_wordBankMap`, `_wordBankReady`, `_wordBankLoadedRev`.
- `MaybeLoadWordBank()` (called from `Play()`), `LoadWordBankAssets()`, `ParseWordBank()`,
  `NormalizeWord()`, the section-2a full-URL cache key + per-book reload guard.
- The dedicated `_wordTapSource` and `PlaySlice(clip, startMs, endMs)` with the fade-in → play →
  fade-out → restore-volume coroutine (used now only by the bank).

## REMOVE (the per-page slice machinery — dead under bank-only)
- Per-page TTS prefetch: `_wordTapChunk`, `_wordTapPostfix`, `StartWordTapPrefetch()`, its loader,
  `_wordTapClip`, `_wordTapTimings`, and the `MaybeLoadWordBank`-adjacent prefetch call in `Play()`.
  (Leave `TtsUrls`/`BuildTtsUrls` if `Play()` still uses them for the page narration URL; only drop
  the word-tap *prefetch*.)
- The position→token mapping over timings (`MapPositionToToken`, `SnapToSpoken`), `WordSliceEndMs`,
  `WordTapPlaySource`/`ChooseWordTapSource`, and the `Page`/`None` branches.
- `WordTiming.End` parsing/use is no longer needed by the tap feature; leave the field if other code
  reads it, otherwise remove. (The SceneForge `end` output is harmless and can stay.)

## New tap entry point
`WordTapHandler.OnPointerClick`:
```csharp
int wi = TMP_TextUtilities.FindIntersectingWord(foreground, eventData.position, cam);
if (wi < 0) return;                               // missed a word → let navigation handle it
string word = foreground.textInfo.wordInfo[wi].GetWord();
if (player != null && player.wordTapEnabled && player.TryPlayWord(word))
    eventData.Use();                              // consume ONLY when we actually played
```
`AudioAndTextPlayer`:
```csharp
public bool TryPlayWord(string wordText)
{
    if (!wordTapEnabled || !_wordBankReady || _wordBankClip == null || _wordBankMap == null)
        return false;
    string nw = NormalizeWord(wordText);
    if (nw.Length == 0 || !_wordBankMap.TryGetValue(nw, out var span)) return false;
    float endMs = span.end > span.start ? span.end : _wordBankClip.length * 1000f;
    PlaySlice(_wordBankClip, span.start, endMs);
    return true;   // played → caller consumes the tap
}
```

## Minimal diagnostics (keep briefly, tagged // TEMP)
- In `LoadWordBankAssets`: `Debug.Log($"[WB] READY words={map.Count}")` on success, and on the
  no-audio / no-json early exits log which one (with the url).
- In `TryPlayWord`: `Debug.Log($"[WB] tap '{wordText}' -> '{NormalizeWord(wordText)}' ready={_wordBankReady} inBank={(_wordBankMap!=null && _wordBankMap.ContainsKey(NormalizeWord(wordText)))}")`.

## Tests (EditMode)
- `NormalizeWord`: `"Me,"`→`me`, `"Don't"`→`don't`, punctuation/empty→`""`.
- `TryPlayWord` decision (pure): bank ready + word present → true; bank absent OR word missing →
  false. (Audio playback itself stays out of the test.)

## Safety
Net reduction in code. Books without `wordbank.*` get silent taps that pass through to navigation —
identical to pre-feature behavior. All remaining word-tap code stays in the `// ---- word-tap ----`
region; deleting it + the handler fully reverts.
