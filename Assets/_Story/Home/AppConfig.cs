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

    // The end-of-book "Read next" sheet: after the LAST page finishes, a bottom sheet slides up over
    // the text area offering the next unread book in the current shelf (see ReadNextSheet).
    // For a QA A/B against the old silent ending, set this to false and recompile.
    public const bool ShowReadNextSheet = true;

    // How recently a book must have been published (its catalog "added" date, ISO yyyy-MM-dd)
    // to match the "new" library filter token — see Filter.IsNewBook. Books with no "added"
    // date are never new, so a catalog that carries no dates leaves the New Books door hidden.
    public const int NewBookWindowDays = 45;

    // Open a book at the page the child last reached ({bookUrl}_page, written on every page turn
    // by PRScript.SetCurrentStep) instead of always at page 1.
    // For a QA A/B against the old always-start-at-page-1 behavior, set this to false and recompile.
    public const bool ResumeAtSavedPage = true;
}
