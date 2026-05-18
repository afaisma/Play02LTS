# How Book Images Are Displayed — Unity Classes Used

## Summary

**Book illustrations** (from `DisplayMainImage` and `AddGalleryImage` in story scripts) are shown using:

- **UnityEngine.UI.Image** — the Unity component that actually draws the picture on screen (Canvas UI).
- **UnityEngine.Sprite** — the image content assigned to `Image.sprite` (created from the downloaded texture).
- **UnityEngine.Texture2D** — used only as an intermediate step when downloading; it is converted to a `Sprite` before display.

No `RawImage` or `SpriteRenderer` is used in the main story image path.

---

## Call Path (script → screen)

1. **Script** calls `DisplayMainImage "images//img01.jpg"` or `AddGalleryImage "images//1.jpg"`.
2. **PRScript** (intrinsic) → **StoryStepsUI.DisplayMainImage(url)** or **StoryStepsUI.AddGalleryImage(url)**.
3. **StoryStepsUI** forwards to **Gallery**:
   - `DisplayMainImage(fullUrl)` → `Gallery.DisplayMainImage(imageUrl)`.
   - `AddGalleryImage(fullUrl)` → `Gallery.addGalleryItem(fullUrl, GalleryItemType.Image)` (then gallery shows current item).
4. **Gallery** either:
   - **DisplayMainImage:** clears gallery, adds one item with that URL, then shows it on the main image.
   - **DisplayCurrentItem** (used when adding/switching gallery items): loads the current item’s URL and assigns it to the main image.
5. **Loading and display:**  
   `PRUtils.DownloadImage(url, imgMain)` is called with the gallery’s **Image** reference. That coroutine downloads the texture, converts it to a **Sprite**, and assigns it to **Image.sprite**.

So the only **renderer** in this path is **UnityEngine.UI.Image** (the `imgMain` field on `Gallery`).

---

## Unity Classes Involved (in order)

| Unity class | Namespace | Role |
|-------------|-----------|------|
| **Image** | UnityEngine.UI | The UI component that displays the illustration. Holds the sprite, preserves aspect, and is laid out on a Canvas. |
| **Sprite** | UnityEngine | The displayed asset. Created from the downloaded Texture2D and assigned to `Image.sprite`. |
| **Texture2D** | UnityEngine | Filled from the web request; converted to Sprite via `Sprite.Create(texture, rect, pivot)`. Not used directly for rendering in this path. |

---

## Where the Image component lives

- **Gallery** (`Assets/_Story/Story/Gallery.cs`):
  - **`public Image imgMain`** — the single “main” image that shows the current page’s illustration (or the current gallery item).
  - This is the **only** component that actually draws the book’s page images in the story scene.
- **StoryStepsUI** holds a reference to **Gallery** and calls its methods; it does not own another Image for page art. It does have **`public Image imgBackgound`** for **background** images (`DisplayBackgroundImage`), which also use **UnityEngine.UI.Image** and **PRUtils.DownloadImage** (same pattern: URL → texture → sprite → `Image.sprite`).

So for “book images” (page illustrations and gallery), the only display class is **UnityEngine.UI.Image**, and the asset type is **Sprite**.

---

## How the image gets into the Image (PRUtils.DownloadImage)

- **Signature:** `DownloadImage(string url, Image image, bool bPreserveAspect = true)` in **PRUtils** (`Assets/_Story/Utils/PRUtils.cs`).
- **Steps:**
  1. Set `image.preserveAspect = bPreserveAspect`.
  2. If the URL is in the image cache, set `image.sprite = cacheImages[url]` (a cached Sprite) and return.
  3. Otherwise: `UnityWebRequestTexture.GetTexture(url)` → get **Texture2D** from `DownloadHandlerTexture.GetContent(request)`.
  4. Convert to **Sprite:** `Sprite.Create(texture, new Rect(0,0, texture.width, texture.height), new Vector2(0.5f, 0.5f))` (wrapped in `Texture2DToSprite`).
  5. Assign: `image.sprite = imageSprite`.
  6. Cache the sprite by URL for reuse.
- So the **runtime content** of the book image is a **Sprite** created from a **Texture2D**; the **display** is always via **Image.sprite**.

---

## Other image-related UI in the same flow

- **Background image:** `StoryStepsUI.imgBackgound` — also an **Image**; same `DownloadImage(..., Image, false)` pattern.
- **Gallery prev/next buttons:** can have images set via `Gallery.SetButtonImage(Button, imageUrl)` → `button.GetComponent<Image>()` and `DownloadImage(imageUrl, buttonImage)` — again **Image** + **Sprite**.
- **Characters / script buttons:** created with **Image** and optionally filled via **DownloadImage** — same classes.

None of these use **RawImage** or **SpriteRenderer** for the story book images.

---

## Takeaway for puzzles (or any reuse of book art)

- The **perceived** “book image” on screen is: **one (or more) UnityEngine.UI.Image** components, with **Sprite** content.
- The **source** is: URL → **Texture2D** (download) → **Sprite** (create once) → **Image.sprite** (and optional cache by URL).
- To reuse the same art (e.g. for a puzzle), you can:
  - Reuse the **same URL** and call **DownloadImage** again (or use the same cache), and assign the resulting **Sprite** (or its **Texture2D** before conversion) to another **Image**, or to a **SpriteRenderer**, or to custom puzzle piece sprites created from the same texture.
