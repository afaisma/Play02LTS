using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>
/// Reusable overlay subsystem. Attach to any GameObject whose
/// RectTransform you want to host overlays on top of; overlays are
/// parented under that transform.
///
/// Originally lived inside Gallery (the _Story page-picture host).
/// Lifted out so other scenes (_Message, _Library, ...) can decorate
/// themselves with the same primitives: AddOverlayVideo / Sprites /
/// Picture, Show/Hide/Toggle, position teleport / animate / freeze,
/// scheduled callbacks, and the onOverlayEvent dispatch.
///
/// Multiple OverlayHosts can coexist — each holds independent state.
/// </summary>
public class OverlayHost : MonoBehaviour
{
    /// <summary>
    /// Invoked by OverlayHost whenever a script-visible overlay event
    /// fires. Currently used by onTap (overlay buttons) and by Schedule()
    /// callbacks. Signature: (eventName, targetOverlayName) -> void.
    /// </summary>
    public Action<string, string> onOverlayEvent;

    // Overlays are parented under this OverlayHost's transform so they
    // share its on-screen rectangle automatically. Keyed by name (or a
    // synthetic __anon_N for unnamed overlays) so SetOverlayProperty and
    // similar can look an overlay up by id. One dictionary, polymorphic
    // over the OverlayEntry hierarchy — video / sprite-sequence / picture
    // overlays are sibling subtypes that share a common lifecycle.
    private abstract class OverlayEntry
    {
        public GameObject go;
        public bool  prepareHandled;  // initial setup done once content loaded
        public bool  userInteracted;  // user has tapped at least once
        public bool  tapPlayback = true;  // when false, tap skips built-in
                                          // play/pause toggle and only fires
                                          // onOverlayEvent. Useful for overlays
                                          // (e.g. butterflies) whose tap should
                                          // mean something other than "stop
                                          // the wings animation".
    }

    private class OverlayVideoEntry : OverlayEntry
    {
        public VideoPlayer vp;
        public bool  autoplay;        // start playback when Prepare completes
        public float volume;          // 0..1 audio gain
        public bool  wasPlayingWhenHidden;  // pause-on-hide, resume-on-show
    }

    private class OverlaySpritesEntry : OverlayEntry
    {
        public Image    image;        // displays the current frame
        public Sprite[] frames;       // null until async loader populates
        public float    fps;          // frames per second (from manifest or override)
        public bool     autoplay;     // start playing when all frames loaded
        public bool     loop;
        public bool     playing;      // is the playback coroutine currently advancing
        public int      currentFrame;
    }

    private class OverlayPictureEntry : OverlayEntry
    {
        public Image image;           // displays the static sprite once downloaded
    }

    [Serializable]
    private class SpritesManifest
    {
        public float fps;
        public int   count;
        public int[] size;
    }

    private readonly Dictionary<string, OverlayEntry> _overlayVideos =
        new Dictionary<string, OverlayEntry>();

    private int _anonOverlayCounter = 0;

    // Flip to true (and recompile) when authoring/debugging overlays to get
    // per-overlay init logs (added/prepared/loaded). const so the guarded
    // logs are dead-code eliminated in release builds. Errors and warnings
    // are always logged regardless of this flag.
    private const bool VERBOSE_OVERLAY = false;

    // Active timer coroutines keyed by (eventName, target). Schedule() with
    // the same key replaces the existing coroutine; CancelSchedule() stops it.
    // Cleared in Clear() so host-resets don't leak callbacks.
    private readonly Dictionary<(string evt, string target), Coroutine> _scheduled =
        new Dictionary<(string evt, string target), Coroutine>();

    /// <summary>Tear down every overlay and stop every pending scheduled
    /// callback. Caller responsibility: invoke this when the page (or
    /// host scope) is changing — e.g. Gallery does it from
    /// clearUpGalleryItems.</summary>
    public void Clear()
    {
        ClearScheduledCallbacks();
        ClearOverlayVideos();
    }

    private void ClearScheduledCallbacks()
    {
        foreach (var c in _scheduled.Values)
            if (c != null) StopCoroutine(c);
        _scheduled.Clear();
    }

    /// <summary>
    /// Add a video overlay on top of the host area.
    /// <para>
    /// <paramref name="name"/> identifies the overlay for SetOverlayProperty
    /// (and any future PlayOverlayVideo / StopOverlayVideo calls). Pass an
    /// empty string for anonymous overlays.
    /// </para>
    /// <para>
    /// Coordinates are floats 0..1 with top-left origin: (0,0) = top-left
    /// of the host area, (1,1) = bottom-right. (0,0,1,1) fills the host.
    /// Per-overlay configuration (autoplay, loop, volume, hint, tappable,
    /// poster, ...) is applied via SetOverlayProperty after creation; the
    /// overlay defaults to a silent, tap-to-play still.
    /// </para>
    /// </summary>
    public void AddOverlayVideo(string name, string url, float x1, float y1, float x2, float y2)
    {
        if (string.IsNullOrEmpty(url))
            return;

        // Synthetic key for anonymous overlays so cleanup and lookup are
        // uniform across named and unnamed overlays.
        string key = string.IsNullOrEmpty(name)
            ? $"__anon_{++_anonOverlayCounter}"
            : name;
        if (_overlayVideos.TryGetValue(key, out OverlayEntry existing))
        {
            Debug.LogWarning($"AddOverlayVideo: duplicate name '{key}' — replacing");
            DestroyOverlayEntry(existing);
            _overlayVideos.Remove(key);
        }

        GameObject go = new GameObject($"OverlayVideo[{key}]");
        go.transform.SetParent(transform, worldPositionStays: false);

        // Anchor to the host area, flipping y from author top-left origin
        // to Unity's bottom-left anchor space. With offsetMin/offsetMax both
        // zero the RectTransform exactly fills (x1,y1)-(x2,y2) of the parent.
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(x1, 1f - y2);
        rt.anchorMax = new Vector2(x2, 1f - y1);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.pivot     = new Vector2(0.5f, 0.5f);
        rt.localScale = Vector3.one;

        // Per-instance RenderTexture the VideoPlayer paints into and the
        // RawImage displays. 1024x1024 is close to typical Kling/Veo native
        // output (1440²) so the downscale is mild; small enough to avoid
        // GPU memory pressure with many overlays.
        RenderTexture rtex = new RenderTexture(1024, 1024, 0, RenderTextureFormat.ARGB32);
        rtex.name = $"OverlayVideo_RT[{key}]";
        rtex.Create();
        // Clear the RT to fully transparent on creation so the overlay
        // doesn't flash a white rectangle for the ~100-300ms between
        // GameObject creation and the VideoPlayer painting its first
        // frame. Without this, paging into a chunk with video overlays
        // briefly shows their bounding boxes as white squares.
        {
            var prev = RenderTexture.active;
            RenderTexture.active = rtex;
            GL.Clear(false, true, Color.clear);
            RenderTexture.active = prev;
        }

        RawImage rawImage = go.AddComponent<RawImage>();
        rawImage.texture = rtex;
        rawImage.color   = Color.white;
        rawImage.raycastTarget = true;   // Button needs a Graphic for hit-testing

        VideoPlayer vp = go.AddComponent<VideoPlayer>();
        vp.playOnAwake      = false;
        vp.isLooping        = false;                       // SetOverlayProperty "loop" enables
        vp.source           = VideoSource.Url;
        vp.url              = url;
        vp.renderMode       = VideoRenderMode.RenderTexture;
        vp.targetTexture    = rtex;
        // audioOutputMode MUST be set before Prepare() — switching it later
        // leaves the audio decoder uninitialized and playback silent. So we
        // always wire up Direct output; the entry's `volume` field (default
        // 0) is what controls audibility, applied in prepareCompleted.
        vp.audioOutputMode  = VideoAudioOutputMode.Direct;
        vp.waitForFirstFrame = true;
        vp.skipOnDrop       = true;

        var entry = new OverlayVideoEntry { go = go, vp = vp };
        _overlayVideos[key] = entry;

        vp.errorReceived += (player, message) =>
        {
            Debug.LogError($"OverlayVideo[{key}] error: {message}  (url={player.url})");
        };

        vp.prepareCompleted += (player) =>
        {
            if (VERBOSE_OVERLAY)
                Debug.Log($"OverlayVideo[{key}] prepared: " +
                          $"size={player.width}x{player.height}, " +
                          $"frameCount={player.frameCount}, " +
                          $"audioTracks={player.audioTrackCount}, " +
                          $"autoplay={entry.autoplay}, loop={player.isLooping}, " +
                          $"volume={entry.volume}");
            if (entry.prepareHandled || entry.userInteracted) return;
            entry.prepareHandled = true;

            // Apply the desired audio gain now that the audio track is known.
            // SetDirectAudioVolume(0, 0) effectively mutes; default entry.volume
            // is 0, so silent overlays stay silent until SetOverlayProperty
            // "volume" raises the level.
            if (player.audioTrackCount > 0)
            {
                player.SetDirectAudioVolume(0, Mathf.Clamp01(entry.volume));
            }

            if (entry.autoplay)
            {
                // Start playback immediately. Loop / volume already configured
                // via SetOverlayProperty are now in effect.
                player.Play();
            }
            else
            {
                // Tap-to-play overlay: play+pause-after-one-frame so the
                // RenderTexture shows frame 0 as a still. Calling Play and
                // Pause in the same call stack is a no-op on Unity macOS —
                // the engine needs an Update tick between them for the
                // video pipeline to actually rasterize the frame.
                player.Play();
                StartCoroutine(PauseAfterOneFrame(entry));
            }
        };
        // Preparing now (rather than at tap time) means the first tap starts
        // playback immediately — no 1–3s buffering pause.
        vp.Prepare();
        if (VERBOSE_OVERLAY)
            Debug.Log($"OverlayVideo[{key}] added: url={url} rect=({x1},{y1})-({x2},{y2})");

        // Sort the overlay above sibling order of the parent canvas so other
        // UI elements drawn after the host (audio-and-text panel, video panel,
        // alert dialog, etc.) don't intercept clicks. The nested Canvas needs
        // its own GraphicRaycaster to keep receiving UI events.
        Canvas overlayCanvas = go.AddComponent<Canvas>();
        overlayCanvas.overrideSorting = true;
        overlayCanvas.sortingOrder    = 100;
        go.AddComponent<GraphicRaycaster>();

        // Tap toggles play/pause. If the video has run to the end, the next
        // tap restarts from frame 0 rather than no-op'ing at the last frame.
        Button btn = go.AddComponent<Button>();
        btn.targetGraphic = rawImage;
        btn.transition    = Selectable.Transition.None;
        btn.onClick.AddListener(() =>
        {
            if (entry.vp == null) return;
            entry.userInteracted = true;
            // If tapPlayback is disabled, skip the built-in play/pause
            // and let the script-side [event onTap] handler decide what
            // tapping means for this overlay (e.g. pause its motion).
            if (!entry.tapPlayback)
            {
                onOverlayEvent?.Invoke("onTap", key);
                return;
            }
            if (entry.vp.isPlaying)
            {
                entry.vp.Pause();
            }
            else if ((long)entry.vp.frameCount > 0
                     && entry.vp.frame >= (long)entry.vp.frameCount - 1)
            {
                // Reached end on a previous play — restart from frame 0.
                entry.vp.Stop();
                entry.vp.Play();
            }
            else
            {
                entry.vp.Play();
            }
            // Script-side [event onTap KEY] handler, if any, runs after the
            // built-in pause/play behavior.
            onOverlayEvent?.Invoke("onTap", key);
        });
    }

    /// <summary>
    /// Add a PNG-sequence sprite overlay on top of the host area.
    /// <paramref name="folderUrl"/> must point at a directory (trailing
    /// slash) containing <c>manifest.json</c> and zero-padded frame files
    /// (e.g. <c>000.png</c> .. <c>060.png</c>). The manifest carries fps,
    /// count and size. Async load runs in a coroutine; the overlay shows
    /// frame 0 as a still once frames are downloaded, and SetOverlayProperty
    /// "autoplay"/"loop"/"fps" control playback. Tap toggles play/pause
    /// the same way it does for video overlays.
    /// </summary>
    public void AddOverlaySprites(string name, string folderUrl,
                                  float x1, float y1, float x2, float y2)
    {
        if (string.IsNullOrEmpty(folderUrl))
            return;

        string key = string.IsNullOrEmpty(name)
            ? $"__anon_{++_anonOverlayCounter}"
            : name;
        if (_overlayVideos.TryGetValue(key, out OverlayEntry existing))
        {
            Debug.LogWarning($"AddOverlaySprites: duplicate name '{key}' — replacing");
            DestroyOverlayEntry(existing);
            _overlayVideos.Remove(key);
        }

        GameObject go = new GameObject($"OverlaySprites[{key}]");
        go.transform.SetParent(transform, worldPositionStays: false);

        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(x1, 1f - y2);
        rt.anchorMax = new Vector2(x2, 1f - y1);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.pivot     = new Vector2(0.5f, 0.5f);
        rt.localScale = Vector3.one;

        // Image (not RawImage) so we can use Sprite assignment and the
        // preserveAspect flag — overlay rect aspect won't always match
        // frame aspect, and we want the subject to letterbox cleanly
        // rather than stretch.
        // color.a starts at 0 so the Image is invisible until LoadAndStart-
        // Sprites assigns frame 0; without this, Unity's Image-with-no-sprite
        // default renders as a solid white square for the duration of the
        // manifest+frame downloads (often hundreds of ms).
        Image image = go.AddComponent<Image>();
        image.color   = new Color(1f, 1f, 1f, 0f);
        image.raycastTarget = true;
        image.preserveAspect = true;

        // Sub-canvas + raycaster, same pattern as video overlays, so
        // sibling-order interception by other UI doesn't block clicks.
        Canvas overlayCanvas = go.AddComponent<Canvas>();
        overlayCanvas.overrideSorting = true;
        overlayCanvas.sortingOrder    = 100;
        go.AddComponent<GraphicRaycaster>();

        var entry = new OverlaySpritesEntry
        {
            go    = go,
            image = image,
            fps   = 0f,            // 0 = sentinel "not yet set". LoadAndStartSprites
                                   // fills it from manifest.json only if still 0;
                                   // SetOverlayProperty's fps case can set it earlier
                                   // and the manifest will then leave it alone.
        };
        _overlayVideos[key] = entry;

        Button btn = go.AddComponent<Button>();
        btn.targetGraphic = image;
        btn.transition    = Selectable.Transition.None;
        btn.onClick.AddListener(() =>
        {
            if (entry.go == null) return;
            int total = entry.frames?.Length ?? 0;
            entry.userInteracted = true;
            // tapPlayback=false: skip the play/pause toggle (e.g. butterflies
            // whose wing animation should keep flapping regardless of tap)
            // and route straight to the script-side handler.
            if (!entry.tapPlayback)
            {
                onOverlayEvent?.Invoke("onTap", key);
                return;
            }
            if (entry.prepareHandled && total > 0)
            {
                if (entry.playing)
                {
                    entry.playing = false;
                }
                else if (entry.currentFrame >= total - 1)
                {
                    // At the end — restart from frame 0.
                    entry.currentFrame = 0;
                    entry.playing      = true;
                }
                else
                {
                    entry.playing = true;
                }
            }
            // Script-side [event onTap KEY] handler.
            onOverlayEvent?.Invoke("onTap", key);
        });

        if (VERBOSE_OVERLAY)
            Debug.Log($"OverlaySprites[{key}] added: url={folderUrl} " +
                      $"rect=({x1},{y1})-({x2},{y2})");

        StartCoroutine(LoadAndStartSprites(entry, key, folderUrl));
    }

    /// <summary>
    /// Add a static picture overlay on top of the host area. The URL
    /// points at a single image (PNG with alpha is the typical case for
    /// transparent stickers; JPG works too but has no transparency). No
    /// playback / lifecycle — it just renders the image once it's loaded.
    /// Pair with SetOverlayProperty("draggable", 1) to make it interactive.
    /// </summary>
    public void AddOverlayPicture(string name, string url,
                                  float x1, float y1, float x2, float y2)
    {
        if (string.IsNullOrEmpty(url))
            return;

        string key = string.IsNullOrEmpty(name)
            ? $"__anon_{++_anonOverlayCounter}"
            : name;
        if (_overlayVideos.TryGetValue(key, out OverlayEntry existing))
        {
            Debug.LogWarning($"AddOverlayPicture: duplicate name '{key}' — replacing");
            DestroyOverlayEntry(existing);
            _overlayVideos.Remove(key);
        }

        GameObject go = new GameObject($"OverlayPicture[{key}]");
        go.transform.SetParent(transform, worldPositionStays: false);

        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(x1, 1f - y2);
        rt.anchorMax = new Vector2(x2, 1f - y1);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.pivot     = new Vector2(0.5f, 0.5f);
        rt.localScale = Vector3.one;

        Image image = go.AddComponent<Image>();
        // Start transparent so the overlay doesn't flash a white square
        // while PRUtils.DownloadImage is fetching the bytes. LoadPictureSprite
        // flips it opaque once the sprite is assigned.
        image.color = new Color(1f, 1f, 1f, 0f);
        image.raycastTarget = true;
        image.preserveAspect = true;

        // Same canvas-sort + raycaster pattern the video/sprites overlays
        // use so clicks aren't intercepted by other UI under canvasMain.
        Canvas overlayCanvas = go.AddComponent<Canvas>();
        overlayCanvas.overrideSorting = true;
        overlayCanvas.sortingOrder    = 100;
        go.AddComponent<GraphicRaycaster>();

        // Button gives us a place to hang onClick handlers for future
        // tapSpeak / tapSound properties. Default action is a noop log; if
        // the picture isn't meant to be tappable, set tappable:0 via
        // SetOverlayProperty (disables the Button without removing it).
        Button btn = go.AddComponent<Button>();
        btn.targetGraphic = image;
        btn.transition    = Selectable.Transition.None;
        btn.onClick.AddListener(() =>
        {
            // Pictures have no built-in tap behavior; the only effect of a
            // tap is whatever the script-side [event onTap KEY] handler does.
            onOverlayEvent?.Invoke("onTap", key);
        });

        var entry = new OverlayPictureEntry { go = go, image = image };
        _overlayVideos[key] = entry;

        // Async load. PRUtils.DownloadImage assigns the resulting sprite
        // directly onto the Image component. PNG alpha is preserved.
        // LoadPictureSprite wraps that call so we can flip the Image
        // opaque once the sprite has been assigned, hiding the brief
        // pre-content window.
        StartCoroutine(LoadPictureSprite(image, url));

        if (VERBOSE_OVERLAY)
            Debug.Log($"OverlayPicture[{key}] added: url={url} " +
                      $"rect=({x1},{y1})-({x2},{y2})");
    }

    /// <summary>Wrap PRUtils.DownloadImage so the picture overlay's Image
    /// stays transparent (alpha=0, set in AddOverlayPicture) for the
    /// duration of the download, then flips to opaque once the sprite has
    /// been assigned. Prevents the brief white-square flash on page change.
    /// Safe against the GameObject being destroyed mid-download.</summary>
    private IEnumerator LoadPictureSprite(Image image, string url)
    {
        yield return PRUtils.DownloadImage(url, image);
        if (image != null) image.color = Color.white;
    }

    private IEnumerator LoadAndStartSprites(OverlaySpritesEntry entry,
                                            string key, string folderUrl)
    {
        // 1. Fetch the manifest.
        string baseUrl    = folderUrl.EndsWith("/") ? folderUrl : folderUrl + "/";
        string manifestUrl = baseUrl + "manifest.json";
        SpritesManifest manifest = null;
        using (var req = UnityWebRequest.Get(manifestUrl))
        {
            req.timeout = 15;  // tiny JSON file — short timeout
            yield return req.SendWebRequest();
            // Page change during the fetch could have torn down the entry.
            // Unity disposes the `using` UnityWebRequest for us on yield break.
            if (entry.go == null) yield break;
            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"OverlaySprites[{key}] manifest fetch failed: " +
                               $"{req.error} ({manifestUrl})");
                yield break;
            }
            try
            {
                manifest = JsonUtility.FromJson<SpritesManifest>(req.downloadHandler.text);
            }
            catch (Exception e)
            {
                Debug.LogError($"OverlaySprites[{key}] manifest parse failed: {e.Message}  (url={manifestUrl})");
                yield break;
            }
        }
        if (manifest == null || manifest.count <= 0)
        {
            Debug.LogError($"OverlaySprites[{key}] manifest has no frames  (url={manifestUrl})");
            yield break;
        }
        // Manifest fills fps ONLY if the user hasn't already set it via
        // SetOverlayProperty("fps", X) between AddOverlaySprites and now.
        // entry.fps starts at 0 (sentinel); the property setter writes a
        // positive value, in which case we leave it untouched here.
        if (entry.fps <= 0f && manifest.fps > 0f) entry.fps = manifest.fps;
        // PlaySpritesLoop has a Mathf.Max(1f, fps) guard, so a manifest
        // with no fps and no user override defaults to 1 fps rather than
        // dividing by zero.
        entry.frames = new Sprite[manifest.count];

        // 2. Download frames in parallel, throttled to MAX_INFLIGHT to avoid
        // flooding CloudFront / macOS HTTP stack. A 5-butterfly page firing
        // 5×61 ≈ 300 simultaneous requests can trip AWS WAF rate-based rules
        // or NSURLSession per-host connection caps, producing intermittent
        // 403 "Access denied" responses on some frames. Throttling to ~8
        // in-flight requests keeps every file fetchable while still
        // finishing the whole download in a couple of seconds.
        const int MAX_INFLIGHT = 8;
        UnityWebRequest[] reqs = new UnityWebRequest[manifest.count];
        int next = 0;   // index of the next request to start

        while (true)
        {
            if (entry.go == null)
            {
                DisposeRequests(reqs);
                yield break;
            }

            // Count requests that are started but not finished.
            int inflight = 0;
            for (int i = 0; i < next; i++)
                if (reqs[i] != null && !reqs[i].isDone) inflight++;

            // Top up the inflight pool from the head of the queue.
            while (next < manifest.count && inflight < MAX_INFLIGHT)
            {
                string url = baseUrl + next.ToString("D3") + ".png";
                reqs[next] = UnityWebRequestTexture.GetTexture(url);
                reqs[next].timeout = 30;   // per-frame PNG — slow connections ok
                reqs[next].SendWebRequest();
                inflight++;
                next++;
            }

            // Done when every request has been started AND every one is done.
            if (next >= manifest.count && inflight == 0) break;
            yield return null;
        }

        // Final guard before we materialise Textures + Sprites — once those
        // are created they need either a live Image to be displayed on, or
        // explicit Destroy. The cleanest path if the page is already gone
        // is to dispose the requests (which discards their textures) and
        // never call Sprite.Create.
        if (entry.go == null)
        {
            DisposeRequests(reqs);
            yield break;
        }

        // 4. Build Sprites.
        int loaded = 0;
        for (int i = 0; i < reqs.Length; i++)
        {
            var req = reqs[i];
            if (req.result == UnityWebRequest.Result.Success)
            {
                Texture2D tex = DownloadHandlerTexture.GetContent(req);
                tex.name = $"OverlaySprites[{key}]_{i:D3}";
                entry.frames[i] = Sprite.Create(
                    tex,
                    new Rect(0, 0, tex.width, tex.height),
                    new Vector2(0.5f, 0.5f));
                loaded++;
            }
            else
            {
                Debug.LogError($"OverlaySprites[{key}] frame {i} failed: {req.error}  (url={req.url})");
            }
            req.Dispose();
        }

        if (VERBOSE_OVERLAY)
            Debug.Log($"OverlaySprites[{key}] loaded: {loaded}/{manifest.count} frames, " +
                      $"fps={entry.fps}, size={(manifest.size != null && manifest.size.Length == 2 ? $"{manifest.size[0]}x{manifest.size[1]}" : "?")}, " +
                      $"autoplay={entry.autoplay}, loop={entry.loop}");

        if (loaded == 0 || entry.go == null)
        {
            // Page changed during the build-loop: the entry was destroyed
            // before we got here. DestroyOverlayEntry already ran when
            // entry.frames was still null, so it found nothing to dispose
            // — but we then materialized textures/sprites into the array.
            // Clean them up here so they don't leak.
            DisposeFrameSprites(entry);
            yield break;
        }

        // 5. Paint frame 0 as a still so the overlay is visible before tap,
        // and flip the Image opaque (it was created transparent in
        // AddOverlaySprites to avoid the white-rectangle flash while
        // the manifest + frame textures were downloading).
        if (entry.image != null && entry.frames[0] != null)
        {
            entry.image.sprite = entry.frames[0];
            entry.image.color  = Color.white;
        }

        entry.prepareHandled = true;

        if (entry.autoplay && !entry.userInteracted)
            entry.playing = true;

        // 6. Run the playback loop until the GameObject is destroyed.
        yield return PlaySpritesLoop(entry, key);
    }

    private static void DisposeRequests(UnityWebRequest[] reqs)
    {
        if (reqs == null) return;
        for (int i = 0; i < reqs.Length; i++)
        {
            if (reqs[i] == null) continue;
            try { reqs[i].Abort(); } catch { /* already done */ }
            reqs[i].Dispose();
        }
    }

    private IEnumerator PlaySpritesLoop(OverlaySpritesEntry entry, string key)
    {
        while (entry.go != null)
        {
            if (entry.playing && entry.frames != null && entry.frames.Length > 0)
            {
                if (entry.image != null && entry.frames[entry.currentFrame] != null)
                    entry.image.sprite = entry.frames[entry.currentFrame];

                yield return new WaitForSeconds(1f / Mathf.Max(1f, entry.fps));

                // A page change during the yield could have torn down the
                // entry (DestroyOverlayEntry sets frames=null and destroys
                // the GameObject). Recheck before continuing.
                if (entry.go == null || entry.frames == null) yield break;

                entry.currentFrame++;
                if (entry.currentFrame >= entry.frames.Length)
                {
                    if (entry.loop)
                    {
                        entry.currentFrame = 0;
                    }
                    else
                    {
                        entry.currentFrame = entry.frames.Length - 1;
                        entry.playing      = false;
                    }
                }
            }
            else
            {
                // Idle while paused; resume immediately when state flips.
                yield return null;
            }
        }
    }

    /// <summary>
    /// Set a property on a previously-added overlay. Dispatches by entry
    /// type — video and sprite-sequence overlays have overlapping property
    /// names (autoplay, loop) with type-appropriate behaviour, and a few
    /// type-specific extras (volume for video, fps for sprites). Unknown
    /// properties log a warning and are ignored.
    /// </summary>
    public void SetOverlayProperty(string name, string property, float value)
    {
        if (string.IsNullOrEmpty(name)
            || !_overlayVideos.TryGetValue(name, out OverlayEntry entry)
            || entry == null || entry.go == null)
        {
            Debug.LogWarning($"SetOverlay: no overlay named '{name}'");
            return;
        }

        string prop = (property ?? "").ToLowerInvariant();

        // Common properties — apply to all overlay types before the
        // type-specific dispatch below.
        switch (prop)
        {
            case "draggable":
                SetOverlayDraggable(entry, value != 0f);
                return;
            case "tappable":
                SetOverlayTappable(entry, value != 0f);
                return;
            case "tapplayback":
                entry.tapPlayback = value != 0f;
                return;
        }

        if (entry is OverlayVideoEntry v)
        {
            switch (prop)
            {
                case "autoplay":
                    v.autoplay = value != 0f;
                    if (v.autoplay && v.prepareHandled
                        && !v.userInteracted && !v.vp.isPlaying)
                    {
                        v.vp.Play();
                    }
                    return;

                case "loop":
                    v.vp.isLooping = value != 0f;
                    return;

                case "volume":
                    v.volume = Mathf.Clamp01(value);
                    if (v.prepareHandled && v.vp.audioTrackCount > 0)
                        v.vp.SetDirectAudioVolume(0, v.volume);
                    return;
            }
        }
        else if (entry is OverlaySpritesEntry s)
        {
            switch (prop)
            {
                case "autoplay":
                    s.autoplay = value != 0f;
                    if (s.autoplay && s.prepareHandled
                        && !s.userInteracted && !s.playing)
                    {
                        s.playing = true;
                    }
                    return;

                case "loop":
                    s.loop = value != 0f;
                    return;

                case "fps":
                    if (value > 0f) s.fps = value;
                    return;
            }
        }

        Debug.LogWarning($"SetOverlay: unknown property '{property}' on '{name}' " +
                         $"(entry is {entry.GetType().Name})");
    }

    /// <summary>
    /// Make a named overlay visible. No-op if it's already shown or if no
    /// overlay with that name exists. Hidden overlays don't receive
    /// raycasts, so they can't be tapped or dragged while hidden.
    /// </summary>
    public void ShowOverlay(string name) { SetOverlayActive(name, true); }

    /// <summary>Make a named overlay invisible (and non-interactive).</summary>
    public void HideOverlay(string name) { SetOverlayActive(name, false); }

    /// <summary>Flip a named overlay's visibility.</summary>
    public void ToggleOverlay(string name)
    {
        if (!_overlayVideos.TryGetValue(name, out OverlayEntry entry)
            || entry == null || entry.go == null)
        {
            Debug.LogWarning($"ToggleOverlay: no overlay named '{name}'");
            return;
        }
        SetOverlayActive(name, !IsOverlayShown(entry));
    }

    /// <summary>Teleport a named overlay to a new rect. Coordinates use the
    /// same author top-left convention as AddOverlay* (0..1 normalized; y=0
    /// is top of the host). Kills any in-flight AnimateOverlayTo on the
    /// same overlay so the final position is deterministic.</summary>
    public void SetOverlayPosition(string name, float x1, float y1, float x2, float y2)
    {
        if (!_overlayVideos.TryGetValue(name, out OverlayEntry entry)
            || entry == null || entry.go == null)
        {
            Debug.LogWarning($"SetOverlayPosition: no overlay named '{name}'");
            return;
        }
        var rt = entry.go.GetComponent<RectTransform>();
        if (rt == null) return;
        DOTween.Kill(rt);
        rt.anchorMin = new Vector2(x1, 1f - y2);
        rt.anchorMax = new Vector2(x2, 1f - y1);
    }

    /// <summary>Smoothly tween a named overlay to a new rect over <c>duration</c>
    /// seconds (linear ease). A new call cancels any in-flight tween on the
    /// same overlay. Tween targets the RectTransform so DOTween.Kill(rt)
    /// stops both the anchorMin and anchorMax tweens together.</summary>
    public void AnimateOverlayTo(string name, float x1, float y1, float x2, float y2, float duration)
    {
        if (!_overlayVideos.TryGetValue(name, out OverlayEntry entry)
            || entry == null || entry.go == null)
        {
            Debug.LogWarning($"AnimateOverlayTo: no overlay named '{name}'");
            return;
        }
        var rt = entry.go.GetComponent<RectTransform>();
        if (rt == null) return;
        DOTween.Kill(rt);
        if (duration <= 0f)
        {
            rt.anchorMin = new Vector2(x1, 1f - y2);
            rt.anchorMax = new Vector2(x2, 1f - y1);
            return;
        }
        Vector2 newMin = new Vector2(x1, 1f - y2);
        Vector2 newMax = new Vector2(x2, 1f - y1);
        // DOTween free doesn't include DOAnchorMin/Max helpers, so use the
        // generic To(getter, setter, ...) form and tag the tween with the
        // RectTransform so DOTween.Kill(rt) can find them.
        DOTween.To(() => rt.anchorMin, v => rt.anchorMin = v, newMin, duration)
            .SetTarget(rt).SetEase(Ease.Linear);
        DOTween.To(() => rt.anchorMax, v => rt.anchorMax = v, newMax, duration)
            .SetTarget(rt).SetEase(Ease.Linear);
    }

    /// <summary>Stop any in-flight position tween on a named overlay. The
    /// overlay stays at its current intermediate position. Used by tap
    /// handlers to genuinely "freeze" a moving overlay.</summary>
    public void StopOverlayAnimation(string name)
    {
        if (!_overlayVideos.TryGetValue(name, out OverlayEntry entry)
            || entry == null || entry.go == null)
        {
            Debug.LogWarning($"StopOverlayAnimation: no overlay named '{name}'");
            return;
        }
        var rt = entry.go.GetComponent<RectTransform>();
        if (rt != null) DOTween.Kill(rt);
    }

    /// <summary>Fire <c>onOverlayEvent(eventName, target)</c> after a delay.
    /// Calling Schedule again with the same (eventName, target) cancels the
    /// pending one — the most recent call wins. <c>target</c> may be empty
    /// for non-targeted events. Clear() cancels everything; no leak.</summary>
    public void Schedule(float seconds, string eventName, string target = "")
    {
        if (string.IsNullOrEmpty(eventName))
        {
            Debug.LogWarning("Schedule: eventName is empty");
            return;
        }
        var key = (eventName, target ?? "");
        if (_scheduled.TryGetValue(key, out Coroutine existing) && existing != null)
            StopCoroutine(existing);
        _scheduled[key] = StartCoroutine(ScheduleTick(Mathf.Max(0f, seconds), key));
    }

    /// <summary>Cancel a pending Schedule by (eventName, target). No-op if
    /// no matching schedule is active.</summary>
    public void CancelSchedule(string eventName, string target = "")
    {
        if (string.IsNullOrEmpty(eventName)) return;
        var key = (eventName, target ?? "");
        if (_scheduled.TryGetValue(key, out Coroutine c))
        {
            if (c != null) StopCoroutine(c);
            _scheduled.Remove(key);
        }
    }

    private IEnumerator ScheduleTick(float seconds, (string evt, string target) key)
    {
        yield return new WaitForSeconds(seconds);
        // Remove ourselves before dispatching so a handler that calls
        // Schedule(...) with the same key doesn't get clobbered when our
        // coroutine returns.
        _scheduled.Remove(key);
        onOverlayEvent?.Invoke(key.evt, key.target);
    }

    private static bool IsOverlayShown(OverlayEntry entry)
    {
        var cg = entry.go.GetComponent<CanvasGroup>();
        return cg == null || cg.alpha > 0f;
    }

    // We hide overlays via CanvasGroup (alpha + blocksRaycasts) rather than
    // SetActive(false) because deactivating the GameObject tears down the
    // VideoPlayer's prepared state, forcing an async re-prepare on the next
    // show. Keeping the GameObject active lets Unity preserve the player's
    // frame, isLooping, and prepared decoder for us — we just need to
    // pause on hide and resume on show.
    private void SetOverlayActive(string name, bool active)
    {
        if (!_overlayVideos.TryGetValue(name, out OverlayEntry entry)
            || entry == null || entry.go == null)
        {
            Debug.LogWarning($"{(active ? "Show" : "Hide")}Overlay: no overlay named '{name}'");
            return;
        }

        var cg = entry.go.GetComponent<CanvasGroup>();
        if (cg == null) cg = entry.go.AddComponent<CanvasGroup>();
        if ((cg.alpha > 0f) == active) return;

        cg.alpha          = active ? 1f : 0f;
        cg.blocksRaycasts = active;
        cg.interactable   = active;

        if (entry is OverlayVideoEntry v && v.vp != null)
        {
            if (!active)
            {
                v.wasPlayingWhenHidden = v.vp.isPlaying;
                if (v.vp.isPlaying) v.vp.Pause();
            }
            else if (v.wasPlayingWhenHidden)
            {
                v.vp.Play();
                v.wasPlayingWhenHidden = false;
            }
        }
    }

    private void SetOverlayDraggable(OverlayEntry entry, bool draggable)
    {
        if (entry?.go == null) return;
        var handler = entry.go.GetComponent<OverlayDragHandler>();
        if (draggable && handler == null)
        {
            entry.go.AddComponent<OverlayDragHandler>();
        }
        else if (!draggable && handler != null)
        {
            Destroy(handler);
        }
    }

    private void SetOverlayTappable(OverlayEntry entry, bool tappable)
    {
        if (entry?.go == null) return;
        // Toggle the Button's interactable flag — leaves the Image's
        // raycastTarget on so the drag handler still receives events.
        var btn = entry.go.GetComponent<Button>();
        if (btn != null) btn.interactable = tappable;
    }

    private static IEnumerator PauseAfterOneFrame(OverlayVideoEntry entry)
    {
        // Two ticks of slack: one for the video pipeline to draw frame 0
        // onto the RenderTexture, one extra for safety on slower devices.
        yield return null;
        yield return null;
        if (entry?.vp != null && !entry.userInteracted)
            entry.vp.Pause();
    }

    private void ClearOverlayVideos()
    {
        foreach (var entry in _overlayVideos.Values)
            DestroyOverlayEntry(entry);
        _overlayVideos.Clear();
        _anonOverlayCounter = 0;
    }

    private void DestroyOverlayEntry(OverlayEntry entry)
    {
        if (entry == null || entry.go == null) return;

        // Video: release the RenderTexture before destroying the GameObject.
        if (entry is OverlayVideoEntry)
        {
            var rawImage = entry.go.GetComponent<RawImage>();
            if (rawImage != null && rawImage.texture is RenderTexture rtex)
            {
                rtex.Release();
                Destroy(rtex);
            }
        }
        // Sprites: release each frame's Texture2D + Sprite. Without this,
        // 61 textures per page would leak GPU memory on each page turn.
        else if (entry is OverlaySpritesEntry s)
        {
            DisposeFrameSprites(s);
        }

        Destroy(entry.go);
    }

    /// <summary>Destroy every materialized Texture2D + Sprite in
    /// <c>entry.frames</c> and clear the array. Safe to call when
    /// <c>frames</c> is null (no-op).</summary>
    private void DisposeFrameSprites(OverlaySpritesEntry entry)
    {
        if (entry?.frames == null) return;
        foreach (var sprite in entry.frames)
        {
            if (sprite == null) continue;
            if (sprite.texture != null) Destroy(sprite.texture);
            Destroy(sprite);
        }
        entry.frames = null;
    }
}
