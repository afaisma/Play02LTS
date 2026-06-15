using UnityEngine;

// One named effect. Authored as a ScriptableObject in Resources/FX/, or built in
// code by FXLibrary.CreateDefault() when no asset is present (fail-soft / works
// out of the box). All fields are optional knobs with sensible defaults so a
// minimally-configured effect still renders something.
[CreateAssetMenu(menuName = "FX/Effect", fileName = "FXEffect")]
public class FXEffect : ScriptableObject
{
    // What this effect does. Composite = a Visual burst plus its AudioClip together.
    public enum Kind { Visual, Audio, Composite }

    // How a Visual effect is rendered. Particle reuses AssetKits.ParticleImage
    // (the Map/VPlayParticle pattern); SpriteOverlay/Tween are the lighter
    // self-contained renderers used for decorations and highlights.
    public enum Backend { Particle, SpriteOverlay, Tween }

    public string id = "";
    public Kind kind = Kind.Visual;
    public Backend backend = Backend.Particle;

    [Header("Particle backend (optional)")]
    // Resources path (under any Resources/ root) to a ParticleImage prefab to
    // instantiate and Play(). If empty or unloadable, FXRuntime falls back to a
    // code-built sparkle burst so the effect still shows.
    public string particlePrefabResource = "";

    [Header("SpriteOverlay / decoration backend (optional)")]
    // Resources path to a Sprite. If empty, a procedurally-generated soft dot is
    // used (keeps the system working with zero authored art).
    public string spriteResource = "";

    [Header("Audio (optional — for Audio/Composite)")]
    // If null and the id implies a chime, FXRuntime synthesizes one.
    public AudioClip audioClip;

    [Header("Tuning")]
    public int count = 14;          // sparkles per burst
    public float duration = 1.1f;   // seconds before auto-cleanup
    public float spread = 140f;     // burst radius in canvas px
    public float scale = 1f;        // size multiplier
    public bool loop = false;       // decorations loop until Stop
    public Color color = new Color(1f, 0.9f, 0.3f, 1f);
}
