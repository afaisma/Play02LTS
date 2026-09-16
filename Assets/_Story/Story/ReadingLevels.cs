// ============================================================================================
// The four reading-level theme names. The catalog carries `level` (1..4) but no theme label, so
// the names live here. They used to be private to LearnToReadController; the ladder screen is
// retired (the Learn-to-Read door opens the shelf directly), and the shelf's own level dividers
// need the same names — so they moved to this shared, scene-independent home.
// ============================================================================================
public static class ReadingLevels
{
    public const int Count = 4;

    private static readonly string[] Names =
    {
        "First Sounds",       // level 1
        "Blends and Friends", // level 2
        "Long Vowels",        // level 3
        "Confident Reader",   // level 4
    };

    // What the child actually practises at each level. Shown as a one-line hint under the shelf's
    // level heading, so a grown-up can tell Level 2 from Level 3 without opening a book.
    private static readonly string[] Skills =
    {
        "short vowels and first words",   // level 1
        "blends and sight words",         // level 2
        "long vowels, longer sentences",  // level 3
        "chapter-length stories",         // level 4
    };

    /// <summary>One-line skill hint for a level; "" for anything out of range.</summary>
    public static string Skill(int level) =>
        (level >= 1 && level <= Skills.Length) ? Skills[level - 1] : "";

    /// <summary>Theme name for a level ("First Sounds"); "Level N" for anything out of range.</summary>
    public static string Name(int level) =>
        (level >= 1 && level <= Names.Length) ? Names[level - 1] : ("Level " + level);

    /// <summary>
    /// Full heading ("Level 1 - First Sounds"). `separator` is passed in rather than baked in
    /// because the project's UI font (Fredoka) ships a STATIC atlas with no em dash — callers that
    /// know which font they render with pick the dash they can actually draw.
    /// </summary>
    public static string Heading(int level, string separator = " - ") =>
        "Level " + level + separator + Name(level);
}
