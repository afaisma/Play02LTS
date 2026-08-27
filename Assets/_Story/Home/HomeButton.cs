using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

// One reusable "home" control, shared by every surface that needs to return to the _Home scene:
// the Learn-to-Read ladder header and the end-of-book Read-next sheet call Create(), and the
// scene-built reader toolbar restyles its existing button through Apply(). (Home's own title row
// no longer carries one — it navigated to the screen the child was already on.) Keeping the look in
// one place means they all render the identical round button — a house glyph tinted UiTheme.Primary (sage) on a
// rounded UiTheme.Surface background. On-palette by construction: no colours beyond UiTheme.
//
// The container (rounded Surface backing, glyph slot, press feedback) lives in ToolbarButtonStyle
// and is shared with the reader toolbar's other icon buttons; this file only owns the house glyph.
public static class HomeButton
{
    // Build a fresh round home button under `parent`, sized `size` px, invoking `onClick` on tap.
    // The outer slot carries the LayoutElement so it sits in the header/title row; the inner button
    // keeps a fixed square size so it stays round even when the row stretches it vertically.
    //
    // `onClick` must be the RAW navigation: the tap is run through TapFeedback (instant pressed
    // state, a beat, a fade, then the nav) because _Home is an async, non-trivial load and an
    // unacknowledged tap reads as dead. Callers must NOT wrap it themselves — a nested TapThenGo
    // hits the already-set latch, gets dropped, and the navigation never runs.
    public static GameObject Create(Transform parent, float size, UnityAction onClick)
    {
        var slot = new GameObject("HomeButton", typeof(RectTransform), typeof(LayoutElement));
        slot.transform.SetParent(parent, false);
        var le = slot.GetComponent<LayoutElement>();
        le.preferredWidth = size; le.minWidth = size;

        var btnGO = new GameObject("Button", typeof(RectTransform), typeof(Image), typeof(Button));
        btnGO.transform.SetParent(slot.transform, false);
        var rt = btnGO.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(size, size);

        // Same shared container + press feedback the reader toolbar uses.
        ToolbarButtonStyle.Apply(btnGO.GetComponent<Button>(), AddHouseGlyph);

        if (onClick != null)
            btnGO.GetComponent<Button>().onClick.AddListener(
                () => TapFeedback.TapThenGo(btnGO.transform, () => onClick()));
        return slot;
    }

    // Restyle an already-wired Button (e.g. the reader toolbar's serialized btnHome) to the shared
    // look without disturbing its position or its existing onClick. Idempotent: the glyph child is
    // reused on repeat calls. Only the PRESS half of the tap treatment is added — the serialized
    // onClick already points at a handler (PRScript.Home) that does its own TapThenGo, and wrapping
    // it a second time would deadlock on the latch.
    public static void Apply(Button button) => ToolbarButtonStyle.Apply(button, AddHouseGlyph);

    // The one thing this file owns: the house itself, filling the slot ToolbarButtonStyle sized.
    private static void AddHouseGlyph(Transform slot, float size)
    {
        var glyphGO = new GameObject("House", typeof(RectTransform), typeof(Image));
        glyphGO.transform.SetParent(slot, false);
        var rt = glyphGO.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        var glyph = glyphGO.GetComponent<Image>();
        glyph.sprite = HouseSprite();
        glyph.color = UiTheme.Primary;
        glyph.preserveAspect = true;
        glyph.raycastTarget = false;
    }

    // Single-colour rounded house, loaded once from Resources and tinted at use.
    private static Sprite _house;
    private static Sprite HouseSprite()
    {
        if (_house == null) _house = Resources.Load<Sprite>("Icons/home");
        return _house;
    }
}
