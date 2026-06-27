// Small central place for app-level UI toggles that we want to flip without hunting through scenes.
public static class AppConfig
{
    // The search / magnifying-glass button in the Library & Bookstore toolbars.
    // Hidden by request (Home now exposes every reading room directly, so browse replaces search).
    // To RESTORE it, set this to true and recompile — the button reappears, nothing else changes.
    public const bool ShowSearch = false;

    // The grown-up toolbar icons (Settings + Parents) in the Library & Bookstore toolbars.
    // Consolidated into a single "For grown-ups" door on the Home screen, so these are hidden.
    // To RESTORE them, set this to true and recompile.
    public const bool ShowGrownupToolbarIcons = false;

    // The in-reader "buy this on Amazon" shopping button (SetShoppingLink → buttonParentalGate).
    // Hidden so all commerce goes through the gated "Our printed books" door on Home.
    // To RESTORE the per-book buy shortcut inside the reader, set this to true and recompile.
    public const bool ShowInReaderShopping = false;
}
