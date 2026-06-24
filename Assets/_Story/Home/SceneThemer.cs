using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Drop-in themer for the legacy editor-built scenes (_Settings, _Parents, …). On Start it walks the
// scene's UI and applies the shared UiTheme (Sage & Sand) with conservative, reversible rules — it
// never edits the scene asset, just recolors at runtime, so it's safe to add anywhere.
//
// Rules: the largest plain Image becomes the warm page background; all text gets the rounded Fredoka
// font (saturated/coloured text -> sage Primary, everything else -> warm TextPrimary); sliders and
// toggles get sage fills on a muted track; non-navigation buttons get the light Surface fill.
// Navigation/toolbar buttons (by name) are skipped so icons aren't recoloured.
public class SceneThemer : MonoBehaviour
{
    [SerializeField] private bool roundButtons = false;

    private void Start() => Apply();

    private void Apply()
    {
        var font = UiTheme.Font();

        // 1) Page background = the largest plain (non-control) Image.
        Image bg = null; float best = -1f;
        foreach (var img in FindObjectsByType<Image>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (img.GetComponent<Button>() || img.GetComponent<Slider>() ||
                img.GetComponentInParent<Slider>() || img.GetComponentInParent<Scrollbar>() ||
                img.GetComponentInParent<TMP_Dropdown>()) continue;
            var r = img.rectTransform.rect;
            float a = Mathf.Abs(r.width * r.height);
            if (a > best) { best = a; bg = img; }
        }
        if (bg != null) bg.color = UiTheme.Bg;

        // 1b) The page colour on these scenes is often the camera's solid clear colour, not an Image.
        foreach (var cam in FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (cam.clearFlags == CameraClearFlags.SolidColor) cam.backgroundColor = UiTheme.Bg;

        // 2) Text: Fredoka font; colour by saturation (coloured -> Primary, else TextPrimary).
        foreach (var t in FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            t.font = font;
            t.color = AccentOrPrimary(t.color);
        }
        foreach (var t in FindObjectsByType<Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            t.color = AccentOrPrimary(t.color);

        // 3) Sliders.
        foreach (var sl in FindObjectsByType<Slider>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var fill = sl.fillRect != null ? sl.fillRect.GetComponent<Image>() : null;
            var handle = sl.handleRect != null ? sl.handleRect.GetComponent<Image>() : null;
            if (fill) fill.color = UiTheme.Primary;
            if (handle) handle.color = UiTheme.Primary;
            // The unfilled track is usually a child Image (named "Background"), not on the Slider itself.
            foreach (var i in sl.GetComponentsInChildren<Image>(true))
                if (i != fill && i != handle) i.color = UiTheme.Track;
        }

        // 4) Toggles: box -> Surface, checkmark -> Primary.
        foreach (var tg in FindObjectsByType<Toggle>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (tg.targetGraphic is Image box) box.color = UiTheme.Surface;
            if (tg.graphic is Image check) check.color = UiTheme.Primary;
        }

        // 5) Buttons: non-navigation get the light Surface fill (skip toolbar/nav icons by name).
        foreach (var b in FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            string n = b.gameObject.name.ToLowerInvariant();
            if (n.Contains("library") || n.Contains("toolbar") || n.Contains("map") ||
                n.Contains("back") || n.Contains("prev") || n.Contains("next") || n.Contains("home"))
                continue;
            var i = b.GetComponent<Image>();
            if (i != null) i.color = UiTheme.Surface;
        }
    }

    // Coloured (saturated, not too dark) text -> sage Primary; otherwise warm TextPrimary.
    private static Color AccentOrPrimary(Color c)
    {
        Color.RGBToHSV(c, out _, out float s, out float v);
        return (s > 0.25f && v > 0.3f) ? UiTheme.Primary : UiTheme.TextPrimary;
    }
}
