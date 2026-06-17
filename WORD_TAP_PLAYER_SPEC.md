# Claude Code hand-off — tap a word to hear its TTS audio

**Type:** additive, self-contained, surgical (per CLAUDE.md). No change to the highlight loop or
the human-voice playback path.
**Goal:** while a story page is shown, tapping a word plays just that word's audio. It must work
**even when the human voice is selected** (highlight off, page playing `{chunk}.mp3`), by loading
the **computer (TTS) audio + word-timings on the side**.

## Why this is clean (key facts)
- `AudioAndTextPlayer.Play(chunkname, currentVoicePostfix, content, …)` already receives everything
  needed to address the TTS files, regardless of voice mode: `chunkname`, the **computer**
  `currentVoicePostfix` (the caller passes `getCurrentVoicePostfix()` in *all* modes), `baseURL`,
  `Globals.getReadingRate()`, and the `?v={contentRev}` cache-bust already applied in `Play()`
  (lines ~163–182). So the TTS URLs are deterministically constructible at all times.
- The visible page text equals the **token concatenation** of the timings (`UpdateHighlightedText`
  builds the TMP string by concatenating `WordTiming.Word`), so a tapped character maps to a timing
  token by a cumulative-length lookup — exact, no fuzzy alignment.
- Each `WordTiming` has `.Time` (start, ms); a word's **end = next token's `.Time`** (last word →
  clip end). Boundaries come straight from existing data.

## Files
- `Assets/_Story/Players/AudioAndTextPlayer.cs` — add the word-tap loader/player (it already holds
  `uiForeground`, `baseURL`, the `chunkname`/`currentVoicePostfix` from `Play()`, and the JSON/
  download helpers). Keep all new members clearly grouped (`// ---- word-tap ----`).
- A small tap handler on the **foreground TMP** GameObject (new `WordTapHandler.cs` implementing
  `IPointerClickHandler`, or reuse the existing tap path — see Gating).

## 1. Shared TTS-URL helper (avoid drift with `Play()`)
Factor the naming `Play()` already uses into one method and call it from both:
```csharp
// computer-voice (TTS) relative URLs for a chunk, with the same rate/voice/cache-bust as Play()
(string audio, string timings) TtsUrls(string chunkname, string voicePostfix)
{
    string rate = Globals.getReadingRate();
    string a = $"{chunkname}_{rate}{voicePostfix}.mp3";
    string t = $"{chunkname}_{rate}_timings{voicePostfix}.json";
    string rev = Globals.g_prbook != null ? Globals.g_prbook.contentRev : "";
    if (!string.IsNullOrEmpty(rev)) { a += "?v=" + rev; t += "?v=" + rev; }
    return (a, t);
}
```

## 2. Prefetch on page load (hide latency)
In `Play()`, after the existing setup, stash `_wordTapChunk = chunkname; _wordTapPostfix =
currentVoicePostfix;` and kick a coroutine that loads the TTS audio + timings via the **existing**
`DiskCache`/`UnityWebRequest`/`JSONNode` path into dedicated fields:
- `AudioClip _wordTapClip;`
- `List<WordTiming> _wordTapTimings;`  (parse exactly like the main timings parser)
Do this in **all** voice modes. In computer mode this loads the same files already cached (cheap);
in human mode it's the only place they're loaded. Never touch `currentWordTimings`, `audioSource`,
or `currentWordIndex`.

## 3. Tap → token → time range
On a word tap on `uiForeground`:
1. `int ci = TMP_TextUtilities.FindIntersectingCharacter(uiForeground, eventData.position, cam,
   true);` (cam = the canvas camera; null for Overlay). If `ci < 0`, ignore (let navigation handle).
2. Convert to a **visible-character position** `p` (count visible glyphs up to `ci`; markup is not
   a visible glyph, so in human mode `p == ci`). Map `p` to a token by walking `_wordTapTimings`
   accumulating `Word.Length` until the running end exceeds `p`.
3. **Snap** off non-spoken tokens: if that token is whitespace (`Word.Trim()==""`) or punctuation
   (reuse `IsWordPunctuation` logic on `_wordTapTimings`), move to the nearest spoken neighbor.
4. `startMs = _wordTapTimings[i].Time;` `endMs =` next token with strictly-greater Time (skip
   zero-width ties), else clip length.

## 4. Play the slice on a dedicated AudioSource
- Add `[SerializeField] AudioSource _wordTapSource;` (separate from `audioSource`). Set
  `_wordTapSource.clip = _wordTapClip; _wordTapSource.time = startMs/1000f; _wordTapSource.Play();`
  then a tiny coroutine stops it when `_wordTapSource.time >= endMs/1000f` (or `!isPlaying`).
- Before playing, **pause the main narration** if it's playing (`audioSource.Pause()`), so the word
  is heard clearly. Do **not** auto-resume (child can replay the page). This `Pause()` is the only
  write to the main source — keep it to that.

## 5. Gating (the one integration-sensitive spot)
Today taps on the text/gallery route to page-turn (`PRScript.NextStep`, swipes via `SwipeDetector`).
The word tap must take precedence **only when it hits a word**:
- Put the handler on the foreground TMP (it's on top). In `OnPointerClick`, if step 3 found a
  **spoken** token, play it and **consume** the event so the page-turn doesn't fire; otherwise do
  nothing and let the existing navigation handle the tap (taps on gaps/image still turn the page;
  side arrows/gallery swipe unaffected).
- Gate behind `public bool wordTapEnabled = true;` so it's trivially switchable.

## Edge cases
- **No timings for the chunk** (older/human-only content): `_wordTapTimings` stays empty → tap is a
  graceful no-op (falls through to navigation).
- **Text ≠ TTS text:** assumes the page's on-screen text equals the TTS text (true here — same page
  content). If they ever differ, mapping drifts; not a concern for current books.
- Don't apply the highlight's cosmetic `-500ms` look-ahead to playback — use raw `.Time`.

## Tests (EditMode, pure logic — no scene needed)
- `TtsUrls` builds `{chunk}_{rate}{post}.mp3` / `..._timings{post}.json` (+ `?v=rev` when set).
- token-mapping helper: given a `List<WordTiming>` whose `Word`s concatenate to a sentence, a
  visible position inside word *k* returns token *k*; a position on a space/punct snaps to the
  adjacent spoken token; `endMs` = next strictly-greater Time (clip end for the last).

## Safety / rollback
Additive: new fields + methods + one small handler component, all under a `wordTapEnabled` gate and
a `// ---- word-tap ----` region. The highlight loop, `currentWordTimings`, and human-voice playback
are untouched (only `audioSource.Pause()` on an explicit tap). Remove the region + handler + the
prefetch call in `Play()` to fully revert.
