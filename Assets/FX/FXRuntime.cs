using System.Collections;
using System.Collections.Generic;
using AssetKits.ParticleImage;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

// The persistent FX manager: owns the top overlay Canvas (bursts draw above
// everything in every scene) and an AudioSource. Created once in code by
// FXBootstrap (no authored prefab required) and kept alive via DontDestroyOnLoad.
// All visuals fail-soft: a missing particle prefab → code-built sparkles; a
// missing chime clip → a synthesized one; nothing here throws.
public class FXRuntime : MonoBehaviour
{
    private Canvas _canvas;
    private RectTransform _canvasRect;
    private AudioSource _audio;

    // Pooled sparkle images for bursts (HARD: pooling, v2).
    private readonly Stack<Image> _sparklePool = new Stack<Image>();

    private static Sprite _dotSprite;     // procedural soft dot, shared
    private static AudioClip _chimeClip;  // procedural chime, shared

    public static FXRuntime Create()
    {
        var go = new GameObject("FXRuntime");
        DontDestroyOnLoad(go);
        var rt = go.AddComponent<FXRuntime>();
        rt.Build();
        return rt;
    }

    private void Build()
    {
        // Top overlay canvas — Screen Space Overlay, max sorting order so bursts
        // sit above any scene UI. No GraphicRaycaster: FX never catches input.
        var canvasGo = new GameObject("FXOverlayCanvas", typeof(Canvas), typeof(CanvasScaler));
        canvasGo.transform.SetParent(transform, false);
        _canvas = canvasGo.GetComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 32760; // just under short.MaxValue — above everything
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        _canvasRect = (RectTransform)canvasGo.transform;

        _audio = gameObject.AddComponent<AudioSource>();
        _audio.playOnAwake = false;
    }

    // ---------- bursts ----------

    public FXHandle PlayBurst(FXEffect fx, RectTransform target, bool fullScreen)
    {
        Vector2 localPos = fullScreen || target == null
            ? Vector2.zero
            : OverlayLocalPointOf(target);

        // Composite fires its sound alongside the visual.
        if (fx.kind == FXEffect.Kind.Composite || fx.kind == FXEffect.Kind.Audio)
            PlaySound(fx);
        if (fx.kind == FXEffect.Kind.Audio)
            return null;

        // Preferred path: an authored ParticleImage prefab (reuses the Map/VPlayParticle engine).
        if (!string.IsNullOrEmpty(fx.particlePrefabResource))
        {
            var prefab = Resources.Load<GameObject>(fx.particlePrefabResource);
            if (prefab != null)
            {
                var inst = Instantiate(prefab, _canvasRect);
                ((RectTransform)inst.transform).anchoredPosition = localPos;
                var pi = inst.GetComponent<ParticleImage>();
                if (pi != null) pi.Play();
                StartCoroutine(DestroyAfter(inst, fx.duration + 0.5f));
                return new FXHandle { go = inst };
            }
            Debug.LogWarning($"[FX] Particle prefab '{fx.particlePrefabResource}' not found for '{fx.id}' — using built-in sparkles.");
        }

        // Fail-soft / default: a code-built sparkle burst.
        StartCoroutine(SparkleBurst(fx, localPos));
        return null;
    }

    private IEnumerator SparkleBurst(FXEffect fx, Vector2 center)
    {
        int n = Mathf.Max(1, fx.count);
        for (int i = 0; i < n; i++)
        {
            var img = GetSparkle();
            var rt = (RectTransform)img.transform;
            rt.anchoredPosition = center;
            rt.localScale = Vector3.one * (0.2f * fx.scale);
            img.color = fx.color;

            float ang = (360f / n) * i + Random.Range(-10f, 10f);
            float dist = fx.spread * Random.Range(0.5f, 1f);
            Vector2 dir = new Vector2(Mathf.Cos(ang * Mathf.Deg2Rad), Mathf.Sin(ang * Mathf.Deg2Rad));
            Vector2 dest = center + dir * dist;

            float d = fx.duration;
            var seq = DOTween.Sequence();
            seq.Append(rt.DOAnchorPos(dest, d).SetEase(Ease.OutCubic));
            seq.Join(rt.DOScale(Vector3.one * fx.scale, d * 0.35f).SetEase(Ease.OutBack));
            seq.Insert(d * 0.45f, rt.DOScale(Vector3.zero, d * 0.55f).SetEase(Ease.InCubic));
            seq.Join(img.DOFade(0f, d).SetEase(Ease.InQuad));
            Image captured = img;
            seq.OnComplete(() => ReturnSparkle(captured));
        }
        yield break;
    }

    // ---------- decorations (persistent, parented under target) ----------

    public FXHandle Decorate(FXEffect fx, RectTransform target)
    {
        // Parent UNDER the target so it scrolls + clips with the tile.
        var go = new GameObject($"FXDecoration_{fx.id}", typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(target, false);

        var img = go.GetComponent<Image>();
        img.sprite = LoadSpriteOrDefault(fx.spriteResource);
        img.color = fx.color;
        img.raycastTarget = false; // never steal the tile's click

        // Tuck into a corner of the tile.
        Vector2 size = target.rect.size;
        float bubble = Mathf.Min(size.x, size.y) * 0.34f * fx.scale;
        rt.sizeDelta = new Vector2(bubble, bubble);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = FXMath.PlacementOffset(
            FXLibrary.FXPlacement.TopRight, size, bubble * 0.5f);

        if (fx.loop)
        {
            float p = Mathf.Max(0.3f, fx.duration);
            rt.DOScale(Vector3.one * 1.35f, p * 0.5f)
              .SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetUpdate(true);
            img.DOFade(0.45f, p * 0.5f)
              .SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetUpdate(true);
        }

        return new FXHandle { go = go };
    }

    // Placement-aware decorate (used by the Lab map).
    public FXHandle Decorate(FXEffect fx, RectTransform target, FXLibrary.FXPlacement placement)
    {
        var handle = Decorate(fx, target);
        if (handle != null && handle.go != null)
        {
            var rt = (RectTransform)handle.go.transform;
            Vector2 size = target.rect.size;
            float bubble = rt.sizeDelta.x;
            rt.anchoredPosition = FXMath.PlacementOffset(placement, size, bubble * 0.5f);
        }
        return handle;
    }

    // ---------- audio ----------

    public void PlaySound(FXEffect fx)
    {
        var clip = fx.audioClip != null ? fx.audioClip : ChimeClip();
        if (clip == null) return;
        _audio.PlayOneShot(clip);
    }

    // ---------- stop ----------

    public void StopHandle(FXHandle handle)
    {
        if (handle == null) return;
        if (handle.go != null)
        {
            DOTween.Kill(handle.go.transform);
            Destroy(handle.go);
        }
    }

    // ---------- positioning ----------

    // Convert a target RectTransform's center to a local point in the overlay canvas
    // rect, across canvas render modes (the spec's positioning requirement).
    private Vector2 OverlayLocalPointOf(RectTransform target)
    {
        var corners = new Vector3[4];
        target.GetWorldCorners(corners);
        Vector3 worldCenter = FXMath.CenterOfCorners(corners);

        var targetCanvas = target.GetComponentInParent<Canvas>();
        Camera worldCam = (targetCanvas != null && targetCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
            ? targetCanvas.worldCamera
            : null;

        Vector2 screen = RectTransformUtility.WorldToScreenPoint(worldCam, worldCenter);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, screen, null, out Vector2 local);
        return local;
    }

    // ---------- sparkle pool ----------

    private Image GetSparkle()
    {
        Image img = _sparklePool.Count > 0 ? _sparklePool.Pop() : CreateSparkle();
        img.gameObject.SetActive(true);
        img.transform.SetParent(_canvasRect, false);
        return img;
    }

    private Image CreateSparkle()
    {
        var go = new GameObject("FXSparkle", typeof(RectTransform), typeof(Image));
        var img = go.GetComponent<Image>();
        img.sprite = DotSprite();
        img.raycastTarget = false;
        var rt = (RectTransform)go.transform;
        rt.sizeDelta = new Vector2(48, 48);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        return img;
    }

    private void ReturnSparkle(Image img)
    {
        if (img == null) return;
        DOTween.Kill(img.transform);
        DOTween.Kill(img);
        img.gameObject.SetActive(false);
        _sparklePool.Push(img);
    }

    // ---------- procedural fallback assets ----------

    private Sprite LoadSpriteOrDefault(string resourcePath)
    {
        if (!string.IsNullOrEmpty(resourcePath))
        {
            var s = Resources.Load<Sprite>(resourcePath);
            if (s != null) return s;
            Debug.LogWarning($"[FX] Sprite '{resourcePath}' not found — using built-in dot.");
        }
        return DotSprite();
    }

    private static Sprite DotSprite()
    {
        if (_dotSprite != null) return _dotSprite;
        const int S = 64;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
        float r = S * 0.5f;
        for (int y = 0; y < S; y++)
        for (int x = 0; x < S; x++)
        {
            float dx = x - r + 0.5f, dy = y - r + 0.5f;
            float d = Mathf.Sqrt(dx * dx + dy * dy) / r;
            float a = Mathf.Clamp01(1f - d);
            a = a * a; // soft falloff
            tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
        }
        tex.Apply();
        _dotSprite = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f));
        return _dotSprite;
    }

    private static AudioClip ChimeClip()
    {
        if (_chimeClip != null) return _chimeClip;
        const int sr = 44100;
        float dur = 0.6f;
        int total = (int)(sr * dur);
        var data = new float[total];
        // Simple C-E-G arpeggio with exponential decay.
        float[] notes = { 523.25f, 659.25f, 783.99f };
        int per = total / notes.Length;
        for (int n = 0; n < notes.Length; n++)
        {
            for (int i = 0; i < per; i++)
            {
                int idx = n * per + i;
                if (idx >= total) break;
                float t = i / (float)sr;
                float env = Mathf.Exp(-6f * t);
                data[idx] += 0.35f * env * Mathf.Sin(2f * Mathf.PI * notes[n] * t);
            }
        }
        _chimeClip = AudioClip.Create("fx_chime", total, 1, sr, false);
        _chimeClip.SetData(data, 0);
        return _chimeClip;
    }

    private IEnumerator DestroyAfter(GameObject go, float seconds)
    {
        yield return new WaitForSeconds(seconds);
        if (go != null) Destroy(go);
    }
}
