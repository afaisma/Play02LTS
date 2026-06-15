using UnityEngine;

// Target sentinel for fullscreen bursts: FX.Play("book_done", FXTarget.FullScreen).
public enum FXTarget { FullScreen }

// Handle to a started effect (a decoration or a loop) so it can be Stop()-ed.
public sealed class FXHandle
{
    internal GameObject go;
    internal string loopAudioKey;
    public bool IsValid => go != null;
}

// Static facade — the public API. Resolves ids against the library, routes to the
// runtime's backends, and fail-softs (logs + does nothing) on any miss. Lives in
// the global namespace so callers write FX.Play(...) like Globals/Navigation.
public static class FX
{
    private static FXLibrary _library;
    private static FXRuntime _runtime;

    // The active catalog: an editor-authored Resources/FX/FXLibrary.asset if present,
    // otherwise the code-built default. Cached after first resolve.
    public static FXLibrary Library
    {
        get
        {
            if (_library == null)
            {
                _library = Resources.Load<FXLibrary>("FX/FXLibrary");
                if (_library == null)
                {
                    _library = FXLibrary.CreateDefault();
                    Debug.Log("[FX] No Resources/FX/FXLibrary.asset found — using built-in default catalog.");
                }
            }
            return _library;
        }
    }

    // Pure id normalization (trim + lower-invariant). Public for tests.
    public static string NormalizeId(string id)
        => string.IsNullOrWhiteSpace(id) ? "" : id.Trim().ToLowerInvariant();

    // ---- lifecycle (called by FXBootstrap) ----

    internal static FXRuntime Runtime => _runtime;

    internal static FXRuntime EnsureRuntime()
    {
        if (_runtime == null)
            _runtime = FXRuntime.Create();
        return _runtime;
    }

    // For EditMode tests / re-authoring: forget the cached library.
    public static void InvalidateLibraryCache() => _library = null;

    // ---- facade ----

    public static FXHandle Play(string id, RectTransform target)
    {
        var fx = Resolve(id);
        if (fx == null || _runtime == null) return null;
        return _runtime.PlayBurst(fx, target, false);
    }

    public static FXHandle Play(string id, FXTarget target)
    {
        var fx = Resolve(id);
        if (fx == null || _runtime == null) return null;
        return _runtime.PlayBurst(fx, null, true); // fullscreen
    }

    public static FXHandle Decorate(string id, RectTransform target)
    {
        var fx = Resolve(id);
        if (fx == null || _runtime == null) return null;
        if (target == null)
        {
            Debug.LogWarning($"[FX] Decorate('{id}') called with null target — ignored.");
            return null;
        }
        return _runtime.Decorate(fx, target);
    }

    public static void PlaySound(string id)
    {
        var fx = Resolve(id);
        if (fx == null || _runtime == null) return;
        _runtime.PlaySound(fx);
    }

    public static void Stop(FXHandle handle)
    {
        if (handle == null || _runtime == null) return;
        _runtime.StopHandle(handle);
    }

    // Resolve an id to an effect, honoring the kill-switch and fail-soft logging.
    private static FXEffect Resolve(string id)
    {
        var lib = Library;
        if (lib == null || !lib.enabled) return null; // kill-switch
        var fx = lib.Find(id);
        if (fx == null)
            Debug.LogWarning($"[FX] Unknown effect id '{id}' — nothing played.");
        return fx;
    }
}
