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
