using System;
using System.Collections.Generic;
using UnityEngine;

// The effect catalog. Authored as Resources/FX/FXLibrary.asset, or built in code
// by CreateDefault() when that asset is missing (fail-soft default). Holds the
// kill-switch, the effect list (looked up by id), and the per-book decoration
// map used by the Library "Lab" pilot.
[CreateAssetMenu(menuName = "FX/Library", fileName = "FXLibrary")]
public class FXLibrary : ScriptableObject
{
    // Kill-switch (HARD REQ #3a): false makes FXBootstrap a no-op without deleting anything.
    public bool enabled = true;

    public List<FXEffect> effects = new List<FXEffect>();

    // Per-book decoration map (the Lab pilot). Each entry attaches a persistent
    // effect to a book's library tile on _Library entry.
    [Serializable]
    public class LabEntry
    {
        // Match by id first, else by name. The token "*first*" in bookId means
        // "the first book in the catalog" — a safe demo/pilot target that always
        // resolves to something.
        public string bookId = "";
        public string bookName = "";
        public string effectId = "lab";
        public FXPlacement placement = FXPlacement.TopRight;
    }

    public enum FXPlacement { Center, TopRight, BottomRight, TopLeft, BottomLeft }

    public List<LabEntry> labMap = new List<LabEntry>();

    // Pure lookup — case/whitespace-insensitive. Returns null on miss (caller fail-softs).
    public FXEffect Find(string id)
    {
        string norm = FX.NormalizeId(id);
        if (norm.Length == 0 || effects == null) return null;
        for (int i = 0; i < effects.Count; i++)
        {
            if (effects[i] != null && FX.NormalizeId(effects[i].id) == norm)
                return effects[i];
        }
        return null;
    }

    // Pure resolver for a LabEntry against a catalog list. Returns the matching
    // PRBook or null. Kept static + dependency-light so it is unit-testable.
    public static PRBook ResolveBook(IList<PRBook> books, string bookId, string bookName)
    {
        if (books == null || books.Count == 0) return null;

        if (!string.IsNullOrEmpty(bookId))
        {
            if (bookId.Trim().Equals("*first*", StringComparison.OrdinalIgnoreCase))
                return books[0];
            for (int i = 0; i < books.Count; i++)
                if (books[i] != null && books[i].id == bookId) return books[i];
        }

        if (!string.IsNullOrEmpty(bookName))
        {
            for (int i = 0; i < books.Count; i++)
                if (books[i] != null &&
                    string.Equals(books[i].bookName, bookName, StringComparison.OrdinalIgnoreCase))
                    return books[i];
        }

        return null;
    }

    // Code-built default catalog so FX works with zero authored assets. An
    // editor-authored Resources/FX/FXLibrary.asset overrides this entirely.
    public static FXLibrary CreateDefault()
    {
        var lib = CreateInstance<FXLibrary>();
        lib.enabled = true;

        var stars = CreateInstance<FXEffect>();
        stars.id = "stars";
        stars.kind = FXEffect.Kind.Visual;
        stars.backend = FXEffect.Backend.Particle;
        stars.color = new Color(1f, 0.92f, 0.35f, 1f);
        stars.count = 16;
        stars.duration = 1.1f;
        stars.spread = 150f;

        var bookDone = CreateInstance<FXEffect>();
        bookDone.id = "book_done";
        bookDone.kind = FXEffect.Kind.Composite;       // burst + chime together
        bookDone.backend = FXEffect.Backend.Particle;
        bookDone.color = new Color(0.45f, 1f, 0.55f, 1f);
        bookDone.count = 28;
        bookDone.duration = 1.6f;
        bookDone.spread = 320f;

        var chime = CreateInstance<FXEffect>();
        chime.id = "chime";
        chime.kind = FXEffect.Kind.Audio;

        var lab = CreateInstance<FXEffect>();
        lab.id = "lab";
        lab.kind = FXEffect.Kind.Visual;
        lab.backend = FXEffect.Backend.SpriteOverlay;  // a persistent "Lab" bubble sprite
        lab.color = new Color(0.4f, 0.8f, 1f, 1f);
        lab.loop = true;
        lab.scale = 1f;
        lab.duration = 1.4f;   // pulse period for the looping bubble

        lib.effects.Add(stars);
        lib.effects.Add(bookDone);
        lib.effects.Add(chime);
        lib.effects.Add(lab);

        // Pilot: decorate the first catalog book with the Lab bubble.
        lib.labMap.Add(new LabEntry { bookId = "*first*", effectId = "lab", placement = FXPlacement.TopRight });

        return lib;
    }
}
