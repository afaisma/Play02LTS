# Housekeeping: AudioPlayer subclip leak + mono-only MakeSubclip; SoundBar still on deprecated WWW + negative caching

**Found:** 2026-06-09 (Cowork source audit, round 2)
**Status:** Diagnosed, fixes specified, not yet applied
**Severity:** Low — dormant or slow-burn paths. Rank below BUG_AUDIO_FAILED_DOWNLOAD_REPLAYS_PREVIOUS_PAGE. Fix opportunistically; both fixes copy patterns already proven elsewhere in this repo.
**Owner:** Claude Code (code fix). This doc is the hand-off.

## A. `AudioPlayer.PlayAudio` time-range path (`Assets/_Story/Players/AudioPlayer.cs`, ~line 121)

*(Corrected 2026-06-09 after Claude Code review — the original version of this section misattributed the helper. `PlayAudio` calls `AudioClipUtilities.MakeSubclip`, which is already multi-channel-correct and names clips `clip.name + "_Subclip"`. `PRUtils.MakeSubclip` (~line 181, the one with the mono-only bug) has zero callers — dead code; per the Surgical Changes rule, leave it alone.)*

1. **Subclip leak (the only real fix here).** Each call with `dTo > dFrom` creates a new clip via `AudioClipUtilities.MakeSubclip` and assigns it to `audioSource.clip`; the previous subclip is never `Destroy()`ed. Mirror the H4 fix from `AudioAndTextPlayer` (line ~454): before assigning, `Destroy(audioSource.clip)` if the current clip's name ends with `"_Subclip"` — so AddAudio'd originals are never destroyed.

**Dormancy note (verified):** no script in `stories_02` production content currently calls `PlayAudio` with a time range, so this path is untestable against real content — verify with a temporary test script or QA content.

## B. `SoundBar.LoadAudio` (`Assets/_Story/Story/SoundBar.cs`, ~line 39)

1. **Deprecated `WWW`, no timeout** — the same hang class already fixed everywhere else (H1/H3). Migrate to `UnityWebRequestMultimedia.GetAudioClip` with `timeout = 60`, reusing `AudioPlayer.GuessAudioTypeFromUrl` for the AudioType (gallery sounds may be .wav/.mp3). This is a copy of the already-proven `AudioPlayer.LoadAudioClip` migration.
2. **Negative caching** — on download error the clip-less `AudioStruct` is still cached (`AddToCache`, ~line 57), so a failed gallery sound stays silent for the whole session. Skip caching when the clip is null (same principle as the main audio bug doc).

## C. `MicrosoftTextToSpeech` — explicitly NOT to be fixed now
Placeholder API key ("AZURE_KEY"), undisposed `UnityWebRequest`, clip never destroyed, raw text interpolated into SSML unescaped. **Dead code**: no production script calls the `Speak` intrinsic (verified against `stories_02` content 2026-06-09). Leave as-is; revisit only if Speak is revived.

## Test plan
- A: temporary script with `AddAudio` + `PlayAudio("x", 1, 3)` — repeated calls don't grow memory (Profiler shows old `_Subclip` clips freed), full-clip `PlayAudio("x", 0, 0)` one-shot path unchanged.
- B: a gallery-sound book (sound bar buttons) — sounds play, repeat taps reuse cache; with airplane mode, a failed sound retries after network returns instead of staying silent.
