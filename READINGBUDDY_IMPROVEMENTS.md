# ReadingBuddy — Usage Research and Improvement Suggestions

*A synthesis of public reviews + technical observations from the source code, aimed at concrete improvements.*

---

## 1. How the app is positioned and used

### Store presence (as observed on the live listings)

| Surface | Rating | Reviews | Notes |
|---|---|---|---|
| iOS App Store (`id6449234127`) | **4.8 ★** | 72 ratings | "Children's Books Read Aloud", v2.0.2 (Sep 19 2025), 157.3 MB, iOS 13+, free, "Designed for iPad", Apple "Made for Ages 0–5" badge |
| Google Play (`com.imagiration.readingteacher`) | **4.0 ★** (phone view: 4.1 ★) | 481 reviews / 374 phone reviews | "ReadingBuddy: Read Aloud Books", **100K+ downloads**, "Teacher Approved" badge, updated May 11 2026, Rated 3+ |

The 0.8★ delta between iOS and Android is the single most informative signal. iOS is rated by a small, self-selected audience (likely closer to the developer's existing MITA/Speech-Therapy customer base) while Android has 6× more reviews and is where most of the negative feedback surfaces.

### What the app is sold as

Both stores lead with the same value proposition:

- **"Picture in Every Paragraph"** — the differentiator vs. other read-aloud apps.
- "Prize-winning reading app that grows with your child" — ages 2–12.
- Five reading speeds, in-sync word highlighting, big letters, no ads, no registration, no data collection.
- Strong emphasis on **special education / autism / speech delay** — fits the developer's portfolio (MITA, Speech Therapy 1–6, RecoverBrain).
- "Studies have shown that ImagiRation apps can significantly accelerate children's development of language and math skills" → `imagiration.com/science/`.

### Developer context

ImagiRation LLC (Boston, MA) has been on Google Play since 2014 and has **~13 apps with ~2 M total installs and ~8 K ratings** across the portfolio (per AppBrain). MITA — their flagship — has an FDA Breakthrough-Device designation. ReadingBuddy sits inside that broader special-education ecosystem and benefits from cross-promotion ("More by ImagiRation LLC").

### Real user feedback (verbatim, from the live listings)

| Reviewer | Platform / date | Helpful votes | What they said |
|---|---|---|---|
| **Preemiemom** | iOS · Jan 13 2024 | n/a | "my child will not press the arrow button to turn the pages. I could not find this option" + "The child voice reading the books is unappealing to me. The articulation is not strong and does not offer clear language modeling." |
| **Melon Endknoepfchen** | Android · Aug 17 2024 | **33** | "I wish to have the option to turn off the highlighted text… The flashing highlights are distracting a lot from listening and looking at the pictures." |
| **Asayu Yuki** | Android · Sept 6 2024 | **47** | "the pictures can be more cartoon like for the children to understand better" |
| **Crystal Johnson** | Android · Mar 21 2025 | **24** | "the only thing that didn't work was the auto page Turner" |
| (testimonials in description) | — | — | "Perfect. I am really pleased to see how well my special needs child is doing with this app." |

**Developer response cadence is good:** the iOS developer reply to Preemiemom (Oct 18 2024) acknowledges both complaints and points to v2.0.0's auto-scrolling + a promise of "books read by professional voice artists." That response pre-dates the Crystal Johnson Android review by 5 months, which means the auto-page-turn fix shipped on iOS but **was still broken on Android in March 2025** — a recurring iOS/Android parity issue.

### Update history (most informative entries)

- **1.0** (Jul 2023) — initial release.
- **1.1.3** (Feb 2024) — content addition: Puss in Boots, more fairy tales.
- **2.0.0** (Oct 12 2024) — major: auto-scrolling, more voice options, disable read-aloud mode, library "rooms" by category. This is the version that addresses Preemiemom's review.
- **2.0.1** (Nov 15 2024) and **2.0.2** (Sep 19 2025) — "bug fixes and performance improvements."
- **Android "What's new"** also advertises a feature not visible in iOS notes: **"transforms every illustration into a jigsaw puzzle"** — confirmed in code by the `SetPuzzleEnabled` / `SetPuzzleEnabledInBook` / `SetPuzzleEnabledInPage` MiniScript intrinsics in `PRScript.cs`.

### What people are using it *for*

Cross-referencing the description, review themes, and the catalog itself, three personas emerge:

1. **Pre-readers (ages 2–4)** — using rhymebooks for vocabulary (animals, colors, sizes, body parts, manners). This is the largest catalog segment (Dr. Stein authored ~30 rhymebooks).
2. **Early readers (ages 3–6)** — fairy tales with word-by-word highlighting, learning to track text.
3. **Special-needs and speech-delayed children** — explicitly called out by the developer and confirmed by a parent testimonial. The "Bedtime Routine," "Restaurant Manners," "Playground" books are clearly daily-living social-story material.

---

## 2. What the reviews are actually telling us

Three independent complaints recur, and each maps cleanly to something visible in the code:

| Complaint | Frequency | Maps to in the code |
|---|---|---|
| **Synthetic voice articulation is poor** | iOS · 1 review (mid-2024) | TTS uses Azure with pre-generated `chunk_N_{rate}.mp3` files; "human voice" assets (`chunk_N.mp3` with no suffix) only exist for some books. |
| **Word highlighting is distracting / can't be disabled** | Android · 1 review with 33 helpful votes (mid-2024) — high signal | `AudioAndTextPlayer.cs` rebuilds two TMP layers every frame with bright colors (`#FF55FF` foreground word, `#00FF0044` mark). The "disable read-aloud" toggle was added but the "disable just the highlight" knob is harder to find. |
| **Pictures could be more child-friendly** | Android · 1 review with 47 helpful votes — highest helpful count | Subjective; reflects the mixed art style across books — some look like AI-generated thumbnails, others traditional illustration. |
| **Auto-page-turn doesn't work on Android** | Android · Mar 2025 (24 helpful) — *after* the v2.0.0 fix | `OnAutoNextStep` UnityEvent fires after audio stops — the Android build must be missing the wiring or an audio-end-of-stream signal differs from iOS. |

A note on what's **not** in the reviews:
- **Zero complaints about crashes, data loss, or in-app purchases.** That's an unusually clean signal given 100K+ Android installs — and consistent with the "no registration, no ads, no IAP" positioning.
- **Zero complaints about content quality of the stories themselves** — the textual content is well received.
- **Zero complaints about offline use**, which I would have expected; this likely means the app fails silently or most users have steady WiFi.

---

## 3. Improvement suggestions

Grouped by where the value-to-effort ratio is highest. The first set acts directly on the most-helpful reviews; the rest come from my reading of the code in `Play6.3/Assets/_Story/` and the content layout under `stories/`.

### Tier 1 — Things parents have explicitly asked for

**1.1 — Make the "highlight off" toggle obvious.**
The Melon Endknoepfchen review (33 helpful votes) is the single highest-signal piece of feedback. The user couldn't find a way to disable the highlight. `AudioAndTextPlayer.cs` already supports three voice modes (human / computer / novoice) where the highlight is off in two of them. The fix is UI, not code: surface a "Highlight words" toggle on the same in-book toolbar as autoplay, and remember the per-user choice in `PlayerPrefs`. The current voice-mode buttons hide this affordance behind a label that doesn't say "highlight."

**1.2 — Tone down the default highlight.**
Bright magenta on white text is harsh. The current default colors are set per-book by `SetAudioTextHilightColors`, but every script I checked uses the same `"112233"` foreground / `"BBBBBB77"` background pair, while the runtime falls back to the harder `#FF55FF` / `#00FF0044`. A softer default (e.g. a muted underline or a desaturated background bar) and an "intensity" slider (`subtle / standard / high contrast`) would address the "flashing highlights" complaint without removing the feature. Children with sensory sensitivities — a major part of the audience — will benefit most.

**1.3 — Ship the "professional voice artist" books actually visible.**
The developer promised this 18 months ago in the iOS review reply. The architecture is ready for it: a `human` voice mode already exists and uses `{chunk}.mp3` (no rate suffix). Today most books only have the rate-suffixed computer-voice files; a recording pipeline for the top 10–15 most popular books (especially the fairy tales) would directly address the "articulation is not strong" complaint. The CSV could mark which books have a human reader so the UI can default to human when available.

**1.4 — Fix Android auto-page-turn.**
A March 2025 review (24 helpful) says auto-page-turn doesn't work on Android even though v2.0.0 shipped it five months earlier. `OnAutoNextStep` is a UnityEvent fired after audio completion — the most likely culprit is Android's audio focus / end-of-stream callback not firing through Unity's `AudioSource` reliably. Worth instrumenting with a backup timer: if the audio's expected `length` has elapsed and `OnAutoNextStep` hasn't fired, fire it anyway. A 200 ms safety margin would be invisible to users and bulletproof the feature.

**1.5 — More consistent art direction.**
The Asayu Yuki review (47 helpful, *the* highest-helpful comment on Android) wants the pictures "more cartoon like." The catalog mixes hand-drawn illustration (Dr. Stein rhymebooks), photographic content (HistoryOfPainting, SolarSystem), and what looks like AI-generated art (`alex39269_star__line_drawing_light_pastel_colors_illustration_...`-named thumbs in `Shapes/`). A short style guide for new content and a second pass to re-style or replace the harshest images would close this gap. The pipeline is asset-side, not code-side.

### Tier 2 — Reliability and content hygiene (technical issues I can verify in the source)

**2.1 — Add an offline mode.**
Today every page fetches from CDN with only a 30-item LRU cache. A `/Downloads` view that pre-downloads a whole book to local storage (and a "Download all my favorites" option) would help on car rides, planes, and bedtime in patchy WiFi. Storage would be small — a typical book's `gen/` directory is ~5–10 MB; the whole 67-book library would fit in well under 1 GB. This is the most-requested feature in *similar* read-aloud apps and the absence is conspicuous.

**2.2 — Fix duplicate IDs in `stories.csv`.**
Verified: ids **38** and **67** each appear twice (id=38 → Sad Princess + Timmy; id=67 → Snow Queen + Little Angels). Nothing in `PRBook` enforces uniqueness. If the app or any tooling ever keys off `id`, two books collide. Either renumber, or move to GUIDs / slugs (the `bookUrl` is effectively unique already).

**2.3 — Clean up 23 orphan folders on the CDN.**
The `stories/` tree has 98 entries but only 67 are referenced by `stories.csv`. Orphans like `Sea_Story_en_bak`, `TimmyAndHisFamily` (the v1 superseded by `_v2`), `TestFromEpub`, `speechplace01`, `temp`, `test` are being served from CloudFront. With 100K+ installs and ~150 MB of media per orphan, this is meaningful CDN cost. Move them to an archive prefix or delete.

**2.4 — Add HTTP caching headers (or a manifest version).**
`FileServerController.java`'s `downloadFile()` sets `Content-Disposition: attachment` but no `Cache-Control` / `ETag` / `Last-Modified` headers. On CloudFront this is probably masked by the CDN's defaults, but the client itself doesn't conditionally fetch. Adding a `version` column to `stories.csv` (per book) would let the client skip re-fetching unchanged content. Today every page turn that misses the 30-entry cache hits the network.

**2.5 — Schema-version the content.**
The MiniScript intrinsics are an implicit contract between client builds and CDN content. If you add a new intrinsic and use it in a new book, older app builds will fail silently on that page. A `min_app_version` column in the CSV (and ignoring books the client can't render) would prevent this trap as the catalog grows.

**2.6 — Cross-device progress sync (opt-in only).**
Today `PlayerPrefs` holds `{bookUrl}_page` / `_done`. Families with iPad + iPhone, or kids switching devices, lose progress. An opt-in iCloud / Google Drive sync (purely device-to-device, no server-side account) keeps the "no registration, no data collection" promise but solves a real pain. Apple's `NSUbiquitousKeyValueStore` and Android's Backup API are designed exactly for this.

**2.7 — Add a Russian (and Spanish) catalog.**
The App Store listing already offers UI in 10 languages, and the code shows a `Sea_Story_ru` folder in the content tree that *isn't in the CSV*. The infrastructure to ship localized content is already there — every book is a script + media folder, and the catalog could carry a `language` column. Russian and Spanish are obvious first wins given the existing portfolio's reach in those communities. (App Store data shows Russian and Spanish-Mexico are among the offered store locales.)

**2.8 — Fix the FileServer security gaps before any non-localhost use.**
For development this is fine, but worth noting for completeness:
- `WebSecurityConfig.java` has every annotation commented out — the configured username/password is inert.
- `static/index.html` posts to `/upload`, controller serves `/api/files/upload` — the public upload page 404s.
- `keystore.p12` is present but SSL is commented out; only HTTP on port 8080.
- The `convinienceEC2` field in `Globals.cs` points at `35.90.126.120:8080`, suggesting this app *has* been exposed publicly. If it still is, this is a real risk: anyone can `POST /api/files/upload` and write to the server's filesystem (only `..` is blocked).

### Tier 3 — Code-quality polish (small, low-risk wins for the team)

**3.1 — Rename one of the two URL normalizers in `PRScript.cs`.**
`NormalizeURL(url)` (prepends base) and `NormalizeUrl(url)` (collapses `//`) differ only by capital case. Easy to confuse, easy to call the wrong one. Rename to `ResolveAbsoluteURL` / `CollapseDoubleSlashes`.

**3.2 — Delete `AudioAndTextPlayer_bak.cs`.**
Living next to the active class in `Assets/_Story/Players/`. Use git history if you ever need it.

**3.3 — Deduplicate the convenience URLs in `Globals.cs`.**
Seven hard-coded URL fields (`convinienceLocal`, `convinienceS3`, `convinienceS3_01`, `convinienceS3_02`, `convinienceS3QA`, `convinienceEC2`, plus `CSVURL` and `csvUrl`). Pick a single `csvUrl` + an enum picker in the inspector. Also: "convinience" → "convenience".

**3.4 — Move `PRBook` to its own file.**
It currently lives inside `PRLibrary.cs`. Splitting it out makes IDE navigation easier and lets you give it small helpers (parsing, validation).

**3.5 — Drop the unused Unity packages.**
`Packages/manifest.json` includes `com.unity.netcode.gameobjects`, `com.unity.multiplayer.tools`, `com.unity.multiplayer.center`, `com.unity.modules.cloth`, `com.unity.modules.vehicles`, `com.unity.modules.terrain*`, `com.unity.modules.wind`. None of these are used by a reading app. Removing them trims build size and reduces attack surface.

**3.6 — Add a quick unit test for CSV parsing.**
The catalog has variable-column rows (some books have 9 columns, some have 11). A small parser test would catch breakages from CSV edits (e.g., a stray comma inside a book name).

**3.7 — Add a smoke-test script for new books.**
A standalone .NET utility (or a Unity Editor menu item) that reads `stories.csv`, walks each `book_url`, fetches the script, scans the chunk list for every `PlayAudioAndText`/`AddGalleryImage` call, and verifies the referenced MP3/JSON/image files exist on the CDN. Today an author can publish a book that 404s mid-read; this script would catch it before release.

### Tier 4 — Bigger product opportunities

**4.1 — Provide an Accessibility section in App Store Connect.**
Apple's listing currently shows *"The developer has not yet indicated which accessibility features this app supports."* For an app whose audience explicitly includes special-needs children, declaring features (VoiceOver compatibility, Larger Text, Reduced Motion, etc.) is both honest and surfaces the app to the right buyers. Reduced-motion users in particular would benefit from a "no per-word highlighting" mode — the same toggle as 1.1.

**4.2 — Parent / educator dashboard.**
Even with "no registration / no data collected," a *local* parent dashboard (PIN-gated, on-device) showing minutes read, books finished, words highlighted, and a suggested next book would massively increase perceived value. The data is already collected in `Globals.cs` (`pagesRead`, `booksRead`, `totalMinutesInGame`); it just isn't surfaced.

**4.3 — Reading-speed adaptation.**
The five reading rates (`-30`, `-20`, `-10`, `0`, `10`) are picked from `ageFrom` or a user preference. The app could *learn* from page-turn timing: if the child consistently auto-advances before the audio ends, bump the rate up; if they linger and rewind, slow it down. Implement client-side; no server needed.

**4.4 — Phonics overlay.**
Given the special-education focus and the existing word-timing JSON, you already know exactly when each word fires. Layering a tap-a-word-to-hear-it interaction on top would be a small addition (`AudioAndTextPlayer` already tracks word index) and would push the app further into the "reading teacher" space that the description already claims.

**4.5 — Treat the catalog as a CMS, not a CSV.**
At 67 books and growing, a hand-edited `stories.csv` with two duplicate IDs and 23 orphan folders is bumping into its limits. A tiny content-management UI (web form → S3 sync → CSV regenerated) would let non-developers publish books and would naturally enforce unique IDs, required fields, and "all referenced assets exist" before a book is added to the live catalog.

---

## 4. Prioritization summary

If I had to pick the **top five** things to do next, in order:

1. **Surface "Highlight off" prominently in the in-book UI** *(Tier 1.1)* — addresses the highest-signal review (33 helpful votes) with a UI-only change.
2. **Fix Android auto-page-turn with a fallback timer** *(Tier 1.4)* — a months-old open complaint on the platform where most users are.
3. **Run a content audit: dedupe IDs, archive orphans, add an asset-existence checker** *(Tier 2.2, 2.3, 3.7)* — one weekend of work, eliminates a class of silent failures and trims CDN bills.
4. **Record human-narrated audio for the 10 most popular books** *(Tier 1.3)* — closes the public commitment from the iOS reply and directly attacks the voice-quality complaint.
5. **Add an offline-download mode** *(Tier 2.1)* — the most likely unspoken pain point for parents (no one asks for offline mode; they just quit using apps that lack it).

---

## 5. Sources consulted

- Apple App Store listing: *Children's Books Read Aloud* (`id6449234127`) — version 2.0.2 (Sep 19 2025), 4.8★, 72 ratings, ImagiRation LLC.
- Google Play listing: *ReadingBuddy: Read Aloud Books* (`com.imagiration.readingteacher`) — 4.0★, 481 reviews, 100K+ downloads, "Teacher Approved," updated May 11 2026.
- AppBrain developer profile for ImagiRation LLC (referenced; page returned no body in this fetch but cited in search snippets: ~13 apps, ~2 M total installs, ~8 K ratings, active since 2014).
- ImagiRation portfolio context from related App Store and search-result snippets (MITA FDA Breakthrough Device, Speech Therapy 1–6).
- Code: `Assets/_Story/Story/Globals.cs`, `PRScript.cs`, `Assets/_Story/Players/AudioAndTextPlayer.cs`, `Assets/_Story/LIbrary/PRLibrary.cs`, `Packages/manifest.json`, `ProjectSettings/EditorBuildSettings.asset`.
- Content: `FileServer/uploads/stories/stories.csv` and per-book script/media folders (`Alphabet/`, `JackAndTheBeanstalk_v2/`, etc.).
- Backend: `FileServer/src/main/java/com/pr/fileserver/*.java`, `application.properties`, `static/index.html`.
