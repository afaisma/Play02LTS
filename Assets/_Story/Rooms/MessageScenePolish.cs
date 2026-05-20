using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Decorates the _Message scene (the launchpad / hub) with a few
/// drifting butterflies. Wraps the OverlayHost API — same primitives
/// used by the story pages, but here driven entirely from C# since the
/// hub doesn't run MiniScript.
///
/// To enable: in the _Message scene, attach an <see cref="OverlayHost"/>
/// to a GameObject whose RectTransform spans the decoration area (a
/// child under the main Canvas works well — stretch it to fill the
/// safe area), then attach this script to that same GameObject and
/// drop the OverlayHost reference into the Inspector.
///
/// Disabling: remove the component from the scene, or uncheck its
/// <see cref="autoStart"/> flag. The rest of _Message keeps working
/// either way — this script is purely additive.
/// </summary>
public class MessageScenePolish : MonoBehaviour
{
    [Tooltip("OverlayHost that hosts the decorations. Defaults to one on this GameObject if left empty.")]
    [SerializeField] private OverlayHost overlayHost;

    [Tooltip("Base URL for the butterfly sprite folders. Defaults to the local FileServer's TestBook content.")]
    [SerializeField] private string baseURL =
        "http://localhost:8080/api/files/download/stories/TestBook/gen/";

    [Tooltip("Butterfly sprite folder names (relative to baseURL). One overlay is spawned per entry.")]
    [SerializeField] private List<string> butterflyFolders = new List<string>
    {
        "butterfly_orange",
        "butterfly_blue",
        "butterfly_pink",
    };

    [Tooltip("Uncheck to disable decoration without removing the component.")]
    [SerializeField] private bool autoStart = true;

    [Tooltip("Butterfly rect size as a fraction of the host area (width, height).")]
    [SerializeField] private Vector2 butterflySize = new Vector2(0.08f, 0.06f);

    [Tooltip("Min/max seconds for a single drift segment (random per segment). " +
             "Controls how fast a butterfly moves across the screen.")]
    [SerializeField] private Vector2 driftDurationRange = new Vector2(3f, 6f);

    [Tooltip("Wing-flap rate, in frames per second. The sprite manifest defaults " +
             "to 12 fps; higher values make wings flap faster without changing " +
             "where the butterfly is on screen. 0 keeps the manifest's value.")]
    [SerializeField] private float wingFps = 24f;

    private void Start()
    {
        if (!autoStart) return;
        if (overlayHost == null) overlayHost = GetComponent<OverlayHost>();
        if (overlayHost == null)
        {
            Debug.LogWarning("MessageScenePolish: no OverlayHost found. Set one in the Inspector or attach to a GameObject that has one.");
            return;
        }

        // Listen for our own scheduled callbacks (each butterfly schedules
        // its own next move under event name "msgFly", target=overlay name).
        overlayHost.onOverlayEvent += OnOverlayEvent;

        for (int i = 0; i < butterflyFolders.Count; i++)
        {
            string folder = butterflyFolders[i];
            if (string.IsNullOrEmpty(folder)) continue;

            string name = $"msg_bf_{i}";
            // Random starting position in the upper third of the host area
            // so they're decorative-not-distracting and stay clear of the
            // button row that occupies the lower portion.
            Vector2 origin = new Vector2(
                Random.Range(0.05f, 1f - butterflySize.x - 0.05f),
                Random.Range(0.05f, 0.35f));
            overlayHost.AddOverlaySprites(name, baseURL + folder + "/",
                origin.x, origin.y,
                origin.x + butterflySize.x, origin.y + butterflySize.y);
            overlayHost.SetOverlayProperty(name, "autoplay",    1f);
            overlayHost.SetOverlayProperty(name, "loop",        1f);
            // Decorations shouldn't intercept clicks meant for the buttons
            // underneath. tappable=0 disables the Button's interactable flag.
            overlayHost.SetOverlayProperty(name, "tappable",    0f);
            // Override the manifest's fps if the Inspector value is > 0.
            // SetOverlayProperty ignores fps <= 0 internally too, so passing
            // 0 explicitly is a no-op rather than a bug.
            if (wingFps > 0f)
                overlayHost.SetOverlayProperty(name, "fps", wingFps);
            // Stagger initial drift kickoff so all 3 don't move in sync.
            overlayHost.Schedule(0.2f + Random.value * 0.8f, "msgFly", name);
        }
    }

    private void OnDestroy()
    {
        if (overlayHost != null)
            overlayHost.onOverlayEvent -= OnOverlayEvent;
    }

    private void OnOverlayEvent(string evName, string target)
    {
        if (evName != "msgFly") return;
        if (overlayHost == null) return;
        // Pick a new destination in the upper third again; chain the next move.
        float x = Random.Range(0.05f, 1f - butterflySize.x - 0.05f);
        float y = Random.Range(0.05f, 0.35f);
        float duration = Random.Range(driftDurationRange.x, driftDurationRange.y);
        overlayHost.AnimateOverlayTo(target,
            x, y, x + butterflySize.x, y + butterflySize.y, duration);
        overlayHost.Schedule(duration + 0.05f + Random.value * 0.5f, "msgFly", target);
    }
}
