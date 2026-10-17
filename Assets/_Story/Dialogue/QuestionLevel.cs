using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The question level the adult chose for the child: A, B, C or D.
///
/// A book with questions exists in one copy per level. Each copy carries a catalog tag
/// "questions-a" ... "questions-d" (a copy may carry more than one). The book lists show only
/// the copies of the chosen level. A book without such a tag is shown at every level, so the
/// ordinary books are never touched by this setting.
///
/// A = answers by touch, B = sounds and single words, C = short phrases, D = full answers.
/// </summary>
public static class QuestionLevel
{
    public const string PrefKey = "question_level";
    public const string TagPrefix = "questions-";
    public static readonly string[] Letters = { "a", "b", "c", "d" };

    public const string TextSettingTitle = "Question level";
    public static readonly string[] TextAbout =
    {
        "Answers by touch",
        "Sounds and single words",
        "Short phrases",
        "Full answers",
    };

    /// <summary>"a", "b", "c" or "d". A new device starts at "a", the level every child can answer.</summary>
    public static string Current
    {
        get
        {
            string saved = PlayerPrefs.GetString(PrefKey, Letters[0]);
            return System.Array.IndexOf(Letters, saved) >= 0 ? saved : Letters[0];
        }
        set
        {
            if (System.Array.IndexOf(Letters, value) < 0) return;
            PlayerPrefs.SetString(PrefKey, value);
            PlayerPrefs.Save();
            Globals.ApplyQuestionLevel();
        }
    }

    public static bool HasLevelTag(PRBook book)
    {
        if (book == null || book.tags == null) return false;
        foreach (string tag in book.tags)
            if (tag.StartsWith(TagPrefix)) return true;
        return false;
    }

    /// <summary>The level a book is made for ("a".."d"; the first one if it has several), or "".</summary>
    public static string Of(PRBook book)
    {
        if (book == null || book.tags == null) return "";
        foreach (string tag in book.tags)
            if (tag.StartsWith(TagPrefix)) return tag.Substring(TagPrefix.Length);
        return "";
    }

    /// <summary>True when the book lists show this book at the given level.</summary>
    public static bool Shows(PRBook book, string level)
    {
        if (!HasLevelTag(book)) return true;
        return book.tags.Contains(TagPrefix + level);
    }

    /// <summary>
    /// The books of the catalog that are shown at the given level, in catalog order. With
    /// onlyLevelBooks, books without a level tag are left out too (Story Comprehension).
    /// </summary>
    public static List<PRBook> Visible(List<PRBook> all, string level, bool onlyLevelBooks = false)
    {
        if (all == null) return null;
        var shown = new List<PRBook>();
        foreach (PRBook book in all)
            if (Shows(book, level) && (!onlyLevelBooks || HasLevelTag(book))) shown.Add(book);
        return shown;
    }

    /// <summary>True once the adult has chosen a level on this device.</summary>
    public static bool IsChosen => PlayerPrefs.HasKey(PrefKey);

    /// <summary>Plain words for the level picker: what the child does now.</summary>
    public static readonly string[] TextChild =
    {
        "Does not speak yet",
        "Says single words",
        "Says three or four words",
        "Understands the story",
    };
    public static readonly string[] TextChildMore =
    {
        "Understands single words. Answers by touching one of two pictures.",
        "Answers with a sound, a word, or yes and no.",
        "Answers with a word or a short phrase.",
        "Answers questions about what happened.",
    };

    /// <summary>True when the catalog has at least one book made for a question level.</summary>
    public static bool CatalogHasLevels(List<PRBook> all)
    {
        if (all == null) return false;
        foreach (PRBook book in all)
            if (HasLevelTag(book)) return true;
        return false;
    }
}
