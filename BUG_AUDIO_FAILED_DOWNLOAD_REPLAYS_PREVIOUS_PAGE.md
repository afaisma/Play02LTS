# BUG: failed page-audio download replays the PREVIOUS page's narration, and the failure is cached for the session

**Found:** 2026-06-09 (Cowork source audit, round 2)
**Status:** Diagnosed, fix specified, not yet applied
**Severity:** Medium-High — wrong narration audibly plays over the wrong page on flaky networks; self-sustaining within a session due to negative caching. The child hears page N-1's audio while page N's text highlights against it; with Autopage on, the page can then auto-advance off the stale audio's end.
**Component:** `Assets/_Story/Players/AudioAndTextPlayer.cs` (`LoadAudioAndTimings`)
**Owner:** Claude Code (code fix). This doc is the hand-off.

## Symptom
With a transient network failure (timeout, dead spot, CDN hiccup) on a chunk MP3 that is not yet in the disk cache:
1. The new page plays the **previous page's audio** instead of staying silent.
2. Every later visit to that page in the same session stays broken — no retry happens.

The in-code comments ("page still renders text without audio") describe the intended behavior, not the actual behavior.

## Root cause — two coupled defects in `LoadAudioAndTimings`

1. **Negative caching.** `AddToCache(audioURL, audioAndTextStruct)` (line ~430) runs unconditionally — including when the audio fetch failed and `audioAndTextStruct.audioClip` is null. Cache hits (line ~284) return the clip-less struct, so the download is never retried until LRU eviction (cap 50).

2. **Stale `audioSource.clip`.** The clip-assignment block is guarded by `if (audioAndTextStruct.audioClip != null)` (line ~444). When the clip is null the block is skipped, `audioSource.clip` still holds the previous page's clip, and `audioSource.Play()` (line ~487) runs unconditionally → previous page's narration replays. (Only the very first page of a session is safe, because `clip` is still null then.)

## Fix spec (surgical — touch only the failure paths)

1. Make the caching conditional: skip `AddToCache` **only when `audioURL` is non-empty AND `audioAndTextStruct.audioClip == null`**.
   - Do NOT skip when `audioURL` is empty (`""`): the simple-static-text case legitimately caches a clip-less struct, keep that behavior.
   - (Equivalent alternative: cache it but treat a cache hit with non-empty audioURL + null clip as a miss and re-download. Either is fine; the conditional-cache version is the smaller diff.)

2. In the apply step (step 4), add an `else` to the `audioClip != null` guard: if the struct has **no** clip and `audioURL` was non-empty,
   - destroy `audioSource.clip` first if its name starts with `"Fragment_"` (same H4 pattern used just above),
   - then set `audioSource.clip = null`.
   `audioSource.Play()` on a null clip is a no-op, so the unconditional `Play()` below needs no change.

Happy-path behavior (cache hit with clip, successful download, static text, novoice/muted mode) must be byte-for-byte unchanged.

## Test plan (manual, Unity editor + device)
- Normal read-through of a standard book in all three voice modes (human / computer / novoice), confirm narration + highlighting unchanged.
- `PlayExt` fragment book (`TimmyAndHisFamily_v2/TimmyAndHisFamily01.txt` — `PlayAudioAndShowText` with time ranges), confirm fragments still play and prior-fragment cleanup still works.
- Autopage ON: full book auto-advances as before.
- **Failure case:** clear the disk cache (delete `persistentDataPath/cache/audio`), start a book online, enable airplane mode mid-book, turn the page:
  - page must render text with NO audio (silence — not the previous page's narration);
  - re-enable network, leave and re-enter the page → audio must now download and play (no negative cache).
- Repeat failure case on the first page of a book (clip null from the start) — no crash, silence.

## Related (do NOT bundle)
- The `dtWasPlaying` field is written but never read — pre-existing, leave it.
- A page script with no `PlayAudioAndText` call leaves old audio playing — pre-existing behavior, out of scope.
