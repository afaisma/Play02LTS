# ReadingBuddy — Testing Guide

Thanks for helping test! This guide walks you through everything the app
should do, plus the new bits we recently added. Goal: make sure the app
still works the way it always has, and catch anything weird in the new
parts before kids see it.

You don't need to be a developer to do this. Just an iPad / iPhone or
Android phone / tablet, an internet connection, and about 45–60 minutes.

If something feels off — even a tiny thing — write it down (see
section "How to report problems" at the end). It's better to flag a
non-issue than to miss a real one.

---

## Before you start

**What you need:**

- An Android device (Android 10 or newer) **or** an iPad / iPhone
  (iOS 14 or newer). Tablet preferred where you have a choice — that's
  the primary form factor for kids reading.
- The latest test build of ReadingBuddy installed on that device.
- Wi-Fi internet for most of the testing. One section asks you to
  briefly turn Wi-Fi off, so make sure you can do that.
- A way to take notes — a paper notebook, a Word doc on your Windows
  PC, a notes app on your phone, whatever's comfortable.
- Optional but very helpful: the ability to take screenshots on the
  device when something looks wrong.

**One-time setup:**

If you have a previous version of the app installed, please **delete
it first**, then install the new test build. This makes sure we start
with a clean slate (no leftover reading progress, no leftover cached
images from earlier versions). You only need to do this once at the
start.

After the first launch, the app will download about 30 MB of content
on the first few minutes of use. That's normal — it's caching things
so future launches are faster.

**How to fill out this guide:**

Each step has a checkbox. Mark it:

- ✅ if it works as described
- ❌ if it doesn't work
- ⚠️ if it kind of works but something feels off (slow, ugly, jumpy)
- ➖ if it's not applicable to your device

If you mark ❌ or ⚠️, jot a quick note about what you saw. Doesn't need
to be technical — "the cover was blurry for 2 seconds before it
sharpened" is a perfectly good note.

---

## 1. First launch

After your fresh install, tap the ReadingBuddy icon.

- [ ] **1.1** App opens within ~5 seconds. You see a loading screen,
  then the main screen appears.

- [ ] **1.2** You don't see any pop-up error messages or "something
  went wrong" dialogs.

- [ ] **1.3** The library screen shows lots of books — small cover
  pictures, titles below or beside them. Roughly 60+ books should be
  visible (scroll up and down to confirm).

- [ ] **1.4** Book covers look right — actual artwork, not grey boxes
  with "No Image" or broken-image icons. A handful of grey ones across
  60+ books is OK; a whole row of them is not.

- [ ] **1.5** Tap a category filter at the top (Fairytales, Animals,
  Numbers, whatever the categories are called in your build). The
  library narrows down to just those books. Tap to clear; everything
  comes back.

- [ ] **1.6** Tap an age filter. Same idea — only books for that age
  range appear. Clear it and everything comes back.

---

## 2. Reading a simple book

Pick a short rhymebook to start with — something like **Colors
Rhymebook**, **Counting**, or **Alphabet**. Tap it to open.

- [ ] **2.1** The book opens with a cover/title page. There's a
  picture and a title.

- [ ] **2.2** You can hear a voice reading the title (if the voice is
  set to Human or Computer — see section 3).

- [ ] **2.3** Tap "Next" (or swipe to advance to the next page).
  Page 2 appears smoothly — image changes, text changes.

- [ ] **2.4** As the voice reads aloud, the word currently being
  spoken is highlighted on screen. Highlight moves left-to-right
  through the sentence in time with the voice. It should not jump
  around, freeze, or be stuck on the wrong word.

- [ ] **2.5** Read through to the end of the book (5–10 pages). Each
  page has its picture, its text, its audio. No pages are missing.

- [ ] **2.6** When you reach the last page, tapping "Next" doesn't
  crash the app or take you somewhere weird — it should either stay
  on the last page or politely return to the library.

---

## 3. Voice options

While reading a book, look for the three voice buttons (usually
labeled or shown as icons for **Human**, **Computer**, and
**No Voice**).

- [ ] **3.1** Pick **Human**. Audio is a real human narration; words
  are NOT highlighted (text just sits there).

- [ ] **3.2** Pick **Computer**. Audio is a computer-generated voice;
  words ARE highlighted as they're spoken.

- [ ] **3.3** Pick **No Voice**. No audio plays; text sits there
  without highlighting. You can still page through.

- [ ] **3.4** Switching between the three takes effect on the next
  page (or immediately, depending on the book). No crashes, no stuck
  audio from the previous mode.

---

## 4. Reading a fairytale (longer book)

Pick a multi-page story like **Cinderella**, **Red Riding Hood**, or
**Three Little Pigs**. These are longer and use more illustrations.

- [ ] **4.1** Opens cleanly, first page renders fully.

- [ ] **4.2** Audio narration matches the text on the page.

- [ ] **4.3** Page-turn animations don't tear or flash white.

- [ ] **4.4** Read past page 5. Picture and text update correctly for
  each new page.

- [ ] **4.5** Go back a page using the Previous button or swipe.
  Previous page restores correctly — same picture, same text, audio
  ready to replay.

- [ ] **4.6** Some books have multiple pictures per page (a small
  scrollable gallery). On those pages, swiping the picture should
  scroll the gallery, NOT turn the page. Once you reach the last image
  in the gallery, one more swipe should turn the page.

---

## 5. Auto-page

Look for an "Auto-page" or "Autoplay" toggle somewhere on the reading
screen.

- [ ] **5.1** Turn it ON. Read a page. About half a second after the
  voice finishes the page, the app automatically advances to the next
  page.

- [ ] **5.2** Turn it OFF. Read a page. Audio finishes, page stays —
  you have to tap Next manually.

---

## 6. Bookstore (if your build has one)

If the main screen has a "Bookstore" or "Shop" button, tap it.

- [ ] **6.1** Bookstore opens, shows books (might be the same library
  or a different selection).

- [ ] **6.2** Filters at the top work the same way as the library
  (genre, age range).

- [ ] **6.3** Tap a book. You should see a "Buy on Kindle" or "Buy
  Printed" type button. Before opening an external page, the app
  should ask you to **solve a small math problem** (the parental
  gate) — kids shouldn't be able to make purchases.

- [ ] **6.4** Solve the math problem. The app opens your phone's web
  browser to the Amazon / Kindle / print-store page for that book.

- [ ] **6.5** Tap your device's Back button (Android) or close the
  browser (iOS) to return to the app. The bookstore should still be
  where you left it, not crashed or stuck on a loading screen.

---

## 7. Settings

Find the Settings screen (usually a gear icon somewhere in the main UI).

- [ ] **7.1** Settings opens, shows controls — reading speed, voice
  preference, maybe sound effects.

- [ ] **7.2** Change the reading speed (slower or faster). Back out to
  the library, open a book — the new speed should be in effect.

- [ ] **7.3** Re-open Settings. Your speed choice is still selected
  (the setting persists across visits).

---

## 8. Books with animations and special effects

Some books have animations — characters that move, animals that flap
their wings, things you can drag around. These are the newest parts
of the app and we want to test them carefully.

If your build includes a special test book (it might be called
**TestBook** or live under the "Test" category in QA builds), use
that. If not, look for any book where things visibly move on a page
beyond just the static picture.

- [ ] **8.1** When the page opens, animated elements (characters,
  effects, flying insects, etc.) start moving on their own within a
  few seconds. They don't appear as still frames or flashing white
  boxes.

- [ ] **8.2** Background videos (like underwater bubbles, swimming
  fish) play smoothly and loop — they shouldn't skip, freeze, or
  stutter.

- [ ] **8.3** Tap an animated character that's supposed to react to
  touch. It should respond — start playing, pause, or do its trick.
  Tap again to verify the response is consistent.

- [ ] **8.4** Drag a draggable element around the page. It moves with
  your finger. Letting go leaves it where you let go. Moving it
  doesn't accidentally turn the page.

- [ ] **8.5** Some animated elements have sounds (a butterfly might
  laugh when tapped, etc.). Tap them and verify the sound plays.
  Tap rapidly several times — sounds should layer or repeat, not
  cut each other off.

- [ ] **8.6** Page away from the animation page (Next or swipe), then
  come back (Previous or swipe back). Animations restart cleanly —
  no leftover motion from before, no missing elements.

---

## 9. Offline behavior

This is the part where you'll briefly turn off internet. Do this in a
spot where you can comfortably turn it back on afterwards.

**Step A — read a book first while online.**

- [ ] **9.1** With Wi-Fi on, open a book and read 3–4 pages. Make sure
  audio plays, pictures load. (This caches the content for offline use
  in the next steps.)

**Step B — go offline and try the same book.**

Now turn off Wi-Fi AND cellular data (or put the device in airplane
mode).

- [ ] **9.2** A small "no internet" notice may appear at the top or
  middle of the screen. That's fine — it means the app noticed.

- [ ] **9.3** Go back into the same book you just read. The pages you
  already saw should still open, with pictures and audio. (The app
  caches recently-read content so kids can keep reading on a plane,
  in a basement, etc.)

**Step C — try a book you haven't opened yet.**

- [ ] **9.4** Try to open a different book you haven't read this
  session. It might fail to load or show placeholder images. That's
  acceptable — no internet, no new content. What we don't want: app
  crash, app frozen, app showing a scary error message.

**Step D — turn internet back on.**

- [ ] **9.5** Turn Wi-Fi (or cellular) back on. Within a few seconds,
  the "no internet" notice should disappear on its own. If there's a
  "Try Again" button, tap it.

- [ ] **9.6** Open a book that didn't load before. It should now load
  normally.

---

## 10. Memory and longevity

Use the app the way a kid would — go back and forth between many
books over ~20 minutes.

- [ ] **10.1** Open 8–10 different books in sequence. Read a few
  pages of each, then back out. The app should stay responsive
  throughout — no slowdown, no crashes.

- [ ] **10.2** While in a book, fully close the app (swipe up from the
  app switcher on iOS, recent apps on Android). Reopen ReadingBuddy.
  The app should remember where you were — same book, same page (or
  at least the same book in the library marked as "in progress").

- [ ] **10.3** Open the same book again. It should resume on the page
  you left off, not page 1.

---

## 11. Things you might notice that are NOT bugs

Save yourself the trouble of reporting these — they're either
intentional or already known:

- **The book "The Strangest Machine in the World" has silent audio on
  page 1 at most speed settings.** Known issue with the source audio,
  being re-recorded separately. Skip that page for testing.

- **A "loading" pause when first opening the library after install.**
  About 10–20 seconds is normal on a fresh install — the app is
  downloading the book catalog. After the first time, it's fast.

- **Some books have version "v2" or "_v2" in their name and look
  almost identical to a non-v2 version.** Both versions are intentional
  while we transition; this isn't a bug.

- **A brief small notice that says something about "duplicate
  component" or "destroying" when entering certain rooms.** That's
  internal house-keeping, not user-visible most of the time, and not
  something to report unless it causes a visible problem.

- **Animated butterflies sometimes give a brief jittery moment when
  you turn the page away from them.** Known minor issue with the
  butterfly chunk, being addressed in a separate fix. Not blocking.

- **Older book covers may be lower resolution than newer ones.** Art
  asset, not a bug.

---

## 12. The "weird things" sweep

Spend 5–10 minutes just exploring the app like a curious 6-year-old
would. Try to break it:

- [ ] **12.1** Tap things rapidly. Double-tap, triple-tap buttons.
  Nothing should crash or get into a weird state.

- [ ] **12.2** Pinch and zoom on book pages. The app probably doesn't
  support zoom; that's fine. It shouldn't crash because you tried.

- [ ] **12.3** Rotate the device (if your build supports rotation).
  Layout should adapt without overlapping text or off-screen content.

- [ ] **12.4** Background the app for a minute (open another app,
  come back). ReadingBuddy resumes where you left it. Audio either
  stops on background and resumes on foreground, OR keeps playing —
  both are acceptable. What's not acceptable: crash, black screen,
  stuck.

- [ ] **12.5** Open a book, leave the app in the background for 5+
  minutes, come back. Still works.

---

## How to report problems

When you find something — anything — that's marked ❌ or ⚠️, please
capture:

1. **What were you doing?** ("I opened the book 'Cinderella' and
   tapped Next to go to page 3.")

2. **What did you expect to happen?** ("Page 3 should appear with a
   new picture and the voice should read it.")

3. **What actually happened?** ("Page 3 picture loaded but the audio
   never started. After about 10 seconds I tapped Next again and it
   moved on.")

4. **Could you repeat it?** Try to reproduce the same problem 2 more
   times. Note: "happened every time" or "only happened once."

5. **A screenshot** if anything visual was wrong. On iOS: press the
   Side button + Volume Up at the same time. On Android: usually
   Power + Volume Down. The screenshot saves to your photos.

6. **Your device:** model (iPad Air, Pixel 7, etc.) and OS version
   (iOS 17.2, Android 14) if you can find them in Settings.

Send everything as a single email or message — one item per problem,
labeled "Issue 1", "Issue 2", etc. If you have a Windows PC, just
copy the screenshots over and paste them into a Word document
alongside your notes. Whatever's easiest.

---

## When you're done

You're finished with this round when:

- You've worked through sections 1–7 (the standard reading
  experience) and most things are ✅.
- You've worked through section 8 (animations) and at least poked at
  the books that have moving content.
- You've done the offline test (section 9) and the longevity sweep
  (sections 10–12).
- All ❌ and ⚠️ findings are written down in one place.

Send your notes and screenshots back. We'll triage them and decide
which need fixing before the next build.

Thanks again for testing — kids using the app every day appreciate it
even if they never know who you are.
