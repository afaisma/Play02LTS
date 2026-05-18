# ReadingBuddy — Tester's Guide

*A plain-language test plan for non-technical reviewers. You don't need Unity, you don't need to read code, you don't need a special build setup. Just install the latest version on a phone or tablet and follow the steps below.*

---

## What this is

A new build of ReadingBuddy is ready. Nine small fixes have been made under the hood — most are invisible to a user, but a couple should feel noticeably better. This guide walks you through what to try, what should happen, and what to flag if something looks off.

You'll need:
- An iPad, iPhone, or Android phone/tablet with the new build installed.
- About **30 minutes** for the full pass.
- A working WiFi connection.

You **don't** need: Unity, a developer cable, a Mac, or any technical setup.

---

## Before you start

1. **Install the new build** (your developer will send you a TestFlight link for iOS or an APK / Play Internal Testing link for Android).
2. **Fully close any old version** of ReadingBuddy from your recent-apps list before installing the new one. This makes sure you're testing the new code, not the cached old code.
3. **Have WiFi turned on.** The app downloads its books from the internet.
4. Optional but helpful: have a **timer** (your phone's built-in clock app is fine) for one of the tests below.

---

## Round 1 — The 5-minute smoke test

Goal: confirm the basics still work. None of this is *new* — it's just to make sure none of the fixes accidentally broke something obvious.

| # | Do this | You should see |
|---|---|---|
| 1 | Open the app. | The opening / loading screen appears. After a few seconds, the **Library** opens showing rows of book covers. |
| 2 | Scroll up and down through the library. | Smooth scrolling. Cover images load (no permanently blank rectangles). |
| 3 | Tap any book — try **Alphabet Rhymebook** or **Cinderella**. | The book opens to the first page. You see a picture and some text. |
| 4 | Tap the **Next** (right) arrow at the bottom. | The page advances. New picture, new text. |
| 5 | Tap **Next** a few more times, then tap **Previous** (left arrow) once. | You go back a page. |
| 6 | If you don't hear narration, look for a row of icons near the bottom (human face / robot / muted icon) and tap the **robot** icon. | A voice reads the page. Words are highlighted as they're spoken. |
| 7 | Tap the **Home** button (top-left or top of screen) to go back to the Library. | Library reappears with all the books. |

**If any of those steps fail** — the app crashes, books don't load, no pictures appear — **stop and report it now**. There's no point continuing if the basics are broken.

---

## Round 2 — The fix that you should actually notice

This is the one user-facing change worth testing carefully. It's about **swiping when a page has multiple pictures**.

### Background

Some pages in ReadingBuddy show **more than one picture** — you swipe sideways inside the page to look through them. (Example: a "knights" book might show a knight, a horse, and a castle on the same page.)

In the **old** version of the app, when you swiped to the last picture in such a page and tried to swipe further, **nothing happened.** You were stuck. The only way to advance was to tap the Next arrow. Many parents didn't realize swipe and arrow were different controls.

In the **new** version, when you're on the last picture and swipe again, the app **turns the page** for you. Same logic going backwards: swiping back past the first picture takes you to the previous page.

### The test

**Find a book with multiple pictures per page.** Good candidates:
- **Goldilocks and the Three Bears**
- **Cinderella**
- **Knights of Camelot**
- **Little Angels Love Science**

Open one and tap through until you see a page where **little dots appear under the picture** (or any indication that there's more than one picture). If the first book you pick has only one picture per page, just try the next one — not every page has a gallery.

| # | Do this | You should see |
|---|---|---|
| 1 | On a multi-picture page, **swipe the picture from right to left**. | A different picture appears (the next one in the gallery). The page text might stay the same — that's normal. |
| 2 | Keep swiping right-to-left until you reach the **last picture** in that page's set. | You're now on the last picture for this page. |
| 3 | **Swipe right-to-left one more time.** This is the key moment. | **NEW:** the page should advance to the **next chapter / page** of the story. The picture *and* text change to the next page. |
| 4 | Now **swipe left-to-right** to go back. | If the previous page also had multiple pictures, you should land on its **last** picture. If it only had one picture, you should land on that page. |
| 5 | Keep swiping left-to-right to go through the previous page's pictures. | Pictures cycle backwards through that page's gallery. |
| 6 | When you're on the **first picture** of a page, swipe left-to-right **one more time**. | **NEW:** the page should go **back** to the previous chapter / page of the story. |

**What old behavior looked like:** in steps 3 and 6, **nothing** would happen. The swipe would be silently ignored. You'd be stuck and have to tap the arrow buttons.

**Report it if:**
- Step 3 or step 6 still does nothing (the swipe is ignored at the gallery edge).
- The swipe sometimes advances two pages instead of one.
- The swipe direction feels wrong (swiping left-to-right advances forward, etc.).

---

## Round 3 — Things to keep an eye on (passive observation)

These fixes aren't directly testable by tapping things, but you can notice them while you read books normally. Spend 10 minutes just **using the app like a parent would** — open a few books, read a few pages each — and stay alert for the following.

### 3a. Pages you've already opened should feel snappy

Open the **Alphabet** book and tap through all 27 pages. Then open a different book (say **Cinderella**) and read 5 pages of it. Now **go back to Alphabet** and re-open page 1.

What you should feel: the page comes up **without a noticeable delay or loading pause**. The audio starts within a second.

In the **old** version, the app would forget previously-played audio after you'd opened too many new pages, and would re-download it from the internet. With the fix, recently-played audio stays in memory and replays instantly.

**Report it if:** pages you've already read make you wait several seconds before audio plays, even though you're on WiFi.

### 3b. The library should always have books in it

Every time you launch the app, the Library should show a full grid of book covers. You should never see an empty Library with no message.

This is paranoia-testing for a rare bug that used to be possible (a typo in the catalog file could wipe out the whole library). It's *unlikely* you'll ever trigger it as a regular user — but if you ever see an empty library, screenshot it and report it immediately.

### 3c. No "ghost" memory pressure on long sessions

Read **one whole book front to back, with auto-page-turn on**, without leaving the app. The book should play smoothly the whole way. The app should not get sluggish, the audio should not start crackling, and the app should not crash near the end of long books.

If you have a device with a lot of other apps open in the background, this is especially worth testing — the new build releases more memory as you turn pages.

**Report it if:** audio starts stuttering after 10+ pages, the app feels slower the longer you stay in a book, or the app crashes during a long read.

---

## Round 4 — Try to break it

A loose collection of "weird things a real kid might do" — none should crash the app, all should produce reasonable behavior.

| # | Do this | Acceptable result |
|---|---|---|
| 1 | Open a book, then immediately tap Next 10 times as fast as you can. | The app skips through pages. It might briefly stutter audio but should not crash or freeze. After you stop, audio should play normally on the page you land on. |
| 2 | Open a book, then immediately tap Home before audio starts. | You go back to the Library. No crash, no stuck audio playing in the background. |
| 3 | Open a book, then **press the Home button on your device** (or swipe up) to background the app. Wait 30 seconds. Reopen ReadingBuddy. | The app comes back to where you left off — same book, same page. |
| 4 | Open a book, **kill the app from the recent-apps switcher** (swipe it away on Android, or swipe up on iOS), then reopen. | When you tap the same book in the Library, you should land on the **same page** you were on. Progress is saved. |
| 5 | Turn WiFi **off** mid-book, then try to turn a page that you haven't read yet. | The app should handle this gracefully — either show a message, or fall back to whatever it has cached. It should not crash or freeze on a permanent loading spinner. |
| 6 | Turn WiFi back on, wait a few seconds, try the same page again. | The page loads normally. |

None of these are *new* tests — they're things that should have always worked. But several of the fixes touch the same code paths, so they're worth a quick once-over.

---

## What to report and how

If you see anything that looks wrong, capture:

1. **Which device** you're on (e.g. "iPad Pro 11-inch, iOS 17.3" or "Samsung Galaxy Tab A8, Android 14").
2. **Which book** you were in (book title).
3. **Which page or which screen.**
4. **What you did** (the exact taps / swipes leading up to the issue).
5. **What you saw** (vs. what you expected).
6. **A screenshot or screen recording** if you can — a video is *enormously* more useful than a description.

Send these to whoever asked you to test. Don't worry about whether something is a "real bug" or "user error" — flag anything that surprises or confuses you; sorting it out is the developer's job.

---

## Things you can't test as a user

For full transparency: of the nine fixes in this build, **most are invisible to a regular user.** They were fixes for things like memory leaks, security weaknesses, and developer-facing crashes. They don't change what you see — they make the app safer and more reliable underneath.

The things on this plan you *can* observe are:

- **The multi-picture swipe behavior** (Round 2) — directly visible.
- **The "previously-read pages feel snappy" effect** (Round 3a) — sometimes visible on flaky WiFi.
- **The "library never empty" guarantee** (Round 3b) — rarely visible, but a clear red flag if it ever fails.
- **The "no memory creep on long sessions" effect** (Round 3c) — subtle, but real on long reads.

The other five fixes (network security, network-resource cleanup, a wrong color value, a session-time tracking bug, and a couple of internal cache behaviors) can only be confirmed by the developer in Unity. Your job is mainly to make sure they didn't accidentally break the user-facing flow — which Rounds 1 and 4 of this plan cover.

---

## Quick summary card

If you only have 10 minutes, do **these five things** in order:

1. **Open the app** → Library loads with book covers.
2. **Open a book, tap Next, tap Previous** → pages turn both ways.
3. **Find a multi-picture page, swipe past the last picture** → it should advance to the next page (this is the main new behavior).
4. **Background the app, kill it, reopen, tap your last book** → progress is remembered.
5. **Turn off WiFi mid-book, try a new page** → no crash; reasonable failure.

If all five of those work, the build is in good shape.
