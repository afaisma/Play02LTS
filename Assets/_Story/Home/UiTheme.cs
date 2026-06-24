using UnityEngine;

// Shared kid-friendly palette for the code-built scenes (_Welcome, _Home, _LearnToRead).
// Light & playful: warm cream background, bright pastel cards, a cheerful coral call-to-action.
// Keeping it in one place so the three scenes stay visually consistent.
public static class UiTheme
{
    // "Sage & Sand" — a calm, autism-friendly palette: warm greige page, muted low-saturation
    // cards that sit close in tone, a soft sage call-to-action. No red/yellow, no pure white.
    public static readonly Color Bg            = Hex(0xF0EBE0); // warm greige page
    public static readonly Color Surface       = Hex(0xF7F2E8); // slightly lighter card base
    public static readonly Color TextPrimary   = Hex(0x4A463C); // soft warm dark (not pure black)
    public static readonly Color TextSecondary = Hex(0x938C7C); // muted warm gray
    public static readonly Color Primary       = Hex(0x8FA67E); // muted sage CTA
    public static readonly Color OnPrimary     = Color.white;
    public static readonly Color Track         = Hex(0xE0D8C6); // progress-bar track on greige

    // Muted pastel cards: (fill, accent) pairs. accent = title text / badge / progress fill.
    public static readonly (Color fill, Color accent)[] Cards =
    {
        (Hex(0xDDE6CF), Hex(0x566B43)), // sage
        (Hex(0xECE0C8), Hex(0x8A6E3E)), // sand
        (Hex(0xD7E2E6), Hex(0x436069)), // dusty blue
        (Hex(0xE8D8CE), Hex(0x8A5E4A)), // soft clay
        (Hex(0xD5E2DE), Hex(0x46665E)), // muted teal
    };

    public static (Color fill, Color accent) Card(int i)
    {
        int n = Cards.Length;
        return Cards[((i % n) + n) % n];
    }

    // Rounded kid font (Fredoka), loaded from Resources so even runtime-created UI (e.g. the
    // reading-mode picker) can use it. Falls back to TMP's default if missing.
    private static TMPro.TMP_FontAsset _font;
    public static TMPro.TMP_FontAsset Font()
    {
        if (_font == null) _font = Resources.Load<TMPro.TMP_FontAsset>("Fonts/Fredoka-Medium SDF");
        return _font != null ? _font : TMPro.TMP_Settings.defaultFontAsset;
    }

    private static Color Hex(uint rgb) =>
        new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);
}
