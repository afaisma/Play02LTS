# Analysis: `music_video_pipeline` and its fit for ReadingBuddy

*Survey of `/Users/alexanderfaisman/dev/music_video_pipeline` and an honest read on which parts could be reused for refreshing TTS or generating videos for the children's books.*

---

## What the project is

A **Python pipeline that turns a song or picture book into a set of AI-generated video clips**, then assembles them into a Premiere Pro timeline.

```
scenes.yaml  (hand-edited)  →  submit_videos.py  →  per-clip MP4s
                                       │
                                       ▼
                                app3_build_timeline.py
                                       │
                                       ▼
                                Premiere Pro FCP 7 XML
```

Two Python scripts do all the work:

- **`submit_videos.py`** (1370 lines) — reads a `scenes.yaml`, expands each scene into one or more prompts, submits them to a video-generation API, polls until each task completes, downloads the resulting MP4s into `output/clips/`, and writes a `manifest.json` + `report.md`.
- **`app3_build_timeline.py`** (590 lines) — scans `output/clips/`, picks the newest MP4 per scene slot, builds an FCP 7 XML timeline that places clips at their `start_time` on V1 with the project's audio on A1. Premiere Pro and DaVinci Resolve both import it.

Everything else is config and per-project working folders:
- `config/providers.yaml` — provider API keys and per-provider settings.
- `projects/<name>/` — one folder per song or book, holds the `scenes.yaml`, the source audio, and `output/`.
- `scripts/submit_kling.sh`, `submit_veo.sh` — thin shell wrappers.
- `docs/NEXT_CHAT_HANDOFF.md`, `CLAUDE_ONBOARDING_PROMPT.md` — meta-docs for AI assistance in extending the pipeline.

The whole codebase is **two Python files + a YAML schema + per-project YAML scene plans**. Clean, scoped, easy to read.

## Services it uses

Three external APIs are configured in `config/providers.yaml`:

| Provider | Service | Endpoint | Auth | Status |
|---|---|---|---|---|
| **Kling** | Kling AI video generator (via PiAPI broker) | `https://api.piapi.ai` | API key in `providers.yaml` | **Active** — real key present, default provider |
| **Veo** | Google Veo 3.1 video generator (via Gemini API) | `google-genai` Python SDK | API key in `providers.yaml`, falls back to `GEMINI_API_KEY` / `GOOGLE_API_KEY` env vars | **Active** — real key present |
| **Runway** | Runway Gen-4 video generator | `https://api.runwayml.com` | API key field — placeholder `YOUR_RUNWAY_KEY_HERE` | **Stub** — code paths exist, never actually used |

The README also credits two services that are *not* programmatically integrated:

- **Suno** — generates the source music (referenced by hand-uploaded `.wav` files in each project folder).
- **Cursor (Claude)** — used by the human author to draft the `scenes.yaml` files.

No TTS service. No image generation service. No transcription. No editing API.

**Python dependencies** (`requirements.txt`):
```
requests >= 2.28.0
pyyaml   >= 6.0
google-genai >= 1.0.0
```

That's the entire dependency tree — three packages. `ffmpeg` is an optional external tool for stripping audio from Veo clips.

## Critical: secrets are in the repo

`config/providers.yaml` contains **real API keys in plaintext**:

```yaml
kling:
  api_key: "409b062ac426...d3ca7a18b"   # PiAPI

veo:
  api_key: "AIzaSyBJuU3...UPsCf42b8"    # Gemini
```

The file's own comment says *"Never commit this file to git"* — but:

- There's no `.gitignore` for it (this directory isn't a git repo at all — `git remote -v` returned nothing).
- They're sitting on the filesystem in cleartext anyway.

If this directory ever gets pushed to a remote, both keys are leaked. Even today, anyone with disk access has them. **Rotate both keys, move them to environment variables**, and never put a real key in the YAML again. The Veo code already supports `GEMINI_API_KEY` / `GOOGLE_API_KEY` env vars as a fallback — use that path and remove the YAML value entirely.

---

## What's stored where

The two existing projects total **~12 GB on disk**, almost all of it generated video:

- `projects/row_fisherman_row/output/clips/` — **2.0 GB** of per-scene clips (~50 MP4s).
- `projects/row_fisherman_row/Premiere/` — **5+ GB** of assembled final renders (multiple takes: `Row - Fisherman - Row_1.mp4` through `_6.mp4`, plus `_YouTube.mp4` variants).
- `projects/our_village/` — empty in this snapshot.

The Veo manifest entries look like:

```json
{
  "id": "v1_s0", "scene_id": "v1_s0", "section": "Intro",
  "index": "01", "start_time": "0:00", "clip_duration": 8,
  "task_id": "models/veo-3.1-generate-preview/operations/v2qgxod8wq0n",
  "status": "done",
  "video_path": "projects/row_fisherman_row/output/clips/01_v1_s0_veo.mp4"
}
```

Each scene records its task ID, status, and output path. The `--resume` flag uses this to skip already-completed clips on re-runs after failures. That's a smart design — the polling loop can fail or hit rate limits, and you don't lose progress.

---

## How well does this fit ReadingBuddy?

Your two goals split cleanly:

### Goal 1: refresh TTS audio for the books

**`music_video_pipeline` doesn't do TTS.** No Azure Speech, no ElevenLabs, no OpenAI TTS, no Google Cloud TTS, no SDK for any audio-generation service. It assumes the audio (a `.wav`) is delivered by the human in the project folder.

So for the TTS refresh, this pipeline isn't a starting point — you'd need to build (or pick) a separate TTS-generation pipeline. Three realistic options:

1. **Stick with Microsoft Azure Cognitive Services** (you already use it via `MicrosoftTextToSpeech.cs`). For batch regeneration of the back catalog, write a small Python script that reads each `*_chunks_script.txt`, extracts the `PlayAudioAndText` calls, generates MP3 + word-timing JSON at all five reading rates per chunk, and uploads to your S3/CloudFront layout. Azure's REST API gives you per-word timing data directly (the "word boundary" events), which is the same shape your current JSONs use. **You already have the Azure key wired in code.** This is the smallest delta.

2. **Switch to ElevenLabs** for clearly higher voice quality — addresses the "articulation is not strong" iOS review complaint head-on. ElevenLabs has a `/text-to-speech/{voice_id}/with-timestamps` endpoint that returns per-character timing, which you'd need to coalesce into per-word timing. Higher cost per character than Azure, much better child-appropriate voices. Their "children's stories" voices are specifically tuned.

3. **OpenAI TTS** (gpt-4o-mini-tts or tts-1) for the cheapest decent quality, but no word-level timing — you'd have to align it yourself with forced alignment (Whisper or aeneas). More plumbing, lower output cost.

For all three, the *shape* of `music_video_pipeline` is the right template: a Python script that walks a project tree, calls an API per scene (or per chunk), polls/streams the result, writes outputs into a manifest-tracked layout. Steal the pattern, not the code.

### Goal 2: generate videos for the books

**`music_video_pipeline` is exactly the right starting point.** The author has already solved:

- **Scene-based prompting** — each book/song is a list of scenes with text, prompts, durations.
- **Provider abstraction** — `KlingProvider`, `VeoProvider`, `RunwayProvider` all implement `submit / poll / extract_url`. Adding a fourth provider (Pika, Sora, Luma, etc.) is one class.
- **Resumable submissions** — the `--resume` flag and manifest pattern means a 50-clip book that fails on clip 37 doesn't restart from zero.
- **Multi-prompt scenes** — long passages get split into multiple shorter clips with letter suffixes (`04a`, `04b`).
- **Premiere timeline output** — for any post-production touch-up before delivery.

The mapping to ReadingBuddy's content shape is direct:

| ReadingBuddy concept | Pipeline concept |
|---|---|
| One book in `stories/` | One `projects/<book_name>/` folder |
| `*_chunks_script.txt` (the parsed scenes) | `scenes.yaml` (hand-edited or auto-generated) |
| `PlayAudioAndText "gen/chunk_5", "..."` | `prompts: - text: "..."` |
| `AddGalleryImage "img05.jpg"` | The visual the clip should depict |
| Pre-rendered MP3 + timings | A pre-rendered MP4 clip |

The natural extension: write a one-time **`script_to_scenes.py`** converter that reads a book's `*_chunks_script.txt`, extracts each `PlayAudioAndText` chunk's narration text, generates a Veo/Kling prompt from it (the existing gallery image becomes the visual reference), and writes a `scenes.yaml`. Then run the existing pipeline. You'd get back per-chunk MP4 clips that the Unity app could play instead of (or alongside) the static gallery image.

For the app's `_Story` scene, this means a small extension: have the MiniScript interpreter understand `AddGalleryVideo` (already exists as `AddVideo` in `PRScript.cs:348`) and let chunks reference per-page video clips rather than images. The `PRVideoPlayer` you've already built handles playback. The path from "I have an audio script" to "I have a video story" is shorter than it looks.

---

## What's good in `music_video_pipeline` to reuse directly

If you cherry-pick:

- **The provider abstraction in `submit_videos.py`** — the `KlingProvider` / `VeoProvider` class pattern is well-shaped and the right place to start adding `AzureTtsProvider`, `ElevenLabsTtsProvider`, etc. with `submit / poll / download` methods of their own.
- **The manifest + resume design** — robust against failures, would be exactly as useful for TTS regeneration as for video.
- **The YAML scene schema** — your existing `*_chunks_script.txt` is more compact, but the scene/prompt/alt-prompt/notes structure of the pipeline's YAML is friendlier for content review (especially with multiple TTS takes).
- **`docs/NEXT_CHAT_HANDOFF.md`** — a smart pattern. Project-level context document specifically written for AI-assisted iteration. Worth copying for ReadingBuddy.

## What's specific to song-video work and isn't transferable

- The `style:` block in scenes is *intensely* tuned for cinematography (camera composition, palette, negative prompts). For children's book illustration generation, the equivalent vocabulary is different: art style, character continuity across pages, age-appropriate visuals.
- FCP 7 XML output (`app3_build_timeline.py`) only matters if you're hand-finishing in Premiere/DaVinci. The Unity app plays MP4s directly — no editor in the pipeline.
- The clip-naming sort order (`01_`, `02_`, … `04a`, `04b`) is for editor import; the Unity content tree just needs URLs in the script.

---

## Recommended path

**For TTS refresh:**

1. Decide on the TTS provider (probably **stick with Azure** if Microsoft voices have improved since you first integrated, or **try ElevenLabs** for a quality bump that closes the iOS review complaint).
2. Write a Python `generate_audio.py` modeled on `submit_videos.py` — same project-folder + manifest + resume design. Iterate per chunk per rate per voice, output MP3 + timing JSON.
3. Add a content validation step: confirm that every `PlayAudioAndText` call in every script has the matching audio + JSON on the CDN (the same script could be the round-1 #6 validation tool from my software-design suggestion).
4. Run it as a one-time backfill for the existing 67 books. After that, it becomes the standard "add a new book" workflow.

Half a day of work if you keep Azure. A day if you switch providers.

**For videos:**

1. Pick a small book to prototype with — probably one of the rhymebooks (~10–15 short pages, each one prompt). **Goldilocks** or **Cinderella** is too ambitious for a first attempt.
2. Write `script_to_scenes.py` — converts `*_chunks_script.txt` to a `scenes.yaml` for the existing pipeline.
3. Run the existing pipeline against Veo. Hand-edit the generated `scenes.yaml` until the visuals are good.
4. In the Unity app, add a content-side opt-in: if a book has `video_url` per chunk in its catalog entry, play the video; otherwise fall back to the existing gallery image.

The Unity side is small — your `PRVideoPlayer` and the `AddVideo` intrinsic are mostly there.

**A cost note worth bookmarking.** Veo 3.1 preview pricing at the time of `providers.yaml`'s setup was ~$0.40/second of generated video, or **~$3/clip for 8s**. A 25-page book at one 8s clip per page = ~$75 per book. The whole 67-book backfill is ~$5K. Kling is cheaper but quality varies. ElevenLabs TTS is ~$5/million characters, so the entire catalog regen at 200K characters of script = **~$1**. Audio refresh is dramatically cheaper than video and answers a real user complaint; video is the bigger product bet.

---

## Summary

- `music_video_pipeline` is a focused, clean Python project that turns hand-edited YAML scene plans into AI-generated video clips via Kling (PiAPI), Veo (Gemini), or Runway.
- It **doesn't do TTS at all** — TTS for ReadingBuddy needs a separate solution.
- It **does provide the right scaffolding for video** — provider abstraction, manifest-based resumable jobs, multi-clip scenes — and the conceptual gap from "song with lyrics" to "book with chunks" is small.
- **Both API keys in the config are real and in cleartext** — rotate and move to environment variables before doing anything else with this directory.
- Recommended sequence: do the TTS refresh first (cheap, low-risk, fixes a public complaint), then experiment with one rhymebook video before committing to a full video rollout.
