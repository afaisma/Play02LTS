using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using  UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

public class BookViewItem : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] public Image imageBook;
    [SerializeField] public Image imageBaclground;
    [SerializeField] public Image imageStatus;
    [SerializeField] public  TextMeshProUGUI txtBookName;
    [SerializeField] public  TextMeshProUGUI txtBookAuthor;
    [SerializeField] public  TextMeshProUGUI txtBookAgeGroup;
    public PRBook prBook;
    
    public void SetBookImage(Sprite image)
    {
        imageBook.sprite = image;
    }
    public void SetBookProperties(PRBook prBook)
    {
        txtBookName.text = prBook.bookName;
        // "Level N" is a Learn-to-Read idea and only means anything on that shelf. A leveled book
        // that also lives in another room (The Tale of Peter Rabbit is level 4 AND a classic) used
        // to announce "Level 4" in Classic, where nothing else is levelled and there is no ladder
        // to read it against; there it gets the age label every other book gets.
        bool onLadderShelf = IsLearnToReadShelf();
        if (prBook.level > 0 && onLadderShelf)
        {
            // Level text AND band colour are ladder ideas: off the ladder a leveled book
            // renders exactly like its shelf-mates (age label, pastel band).
            txtBookAgeGroup.text = $"Level {prBook.level}";
            imageBaclground.color = LevelBandColor(prBook.level);
        }
        else
        {
            txtBookAgeGroup.text = Globals.ageGroupLabelFromPRBook(prBook);
            imageBaclground.color = PRUtils.GetNthPastelColor(prBook.number);//PRUtils.textToColor(prBook.bookName);
        }
        txtBookAuthor.text = AuthorLine(prBook, onLadderShelf && prBook.level > 0);
        //Color opppositeColor = PRUtils.GetOppositeColor(imageBaclground.color);
        //txtBookName.color =  PRUtils.DarkenColorByPercentage(opppositeColor, 0.4f);
        txtBookName.color = new Color(0.4f, 0.15f, 0.15f, 1f);
        imageStatus.gameObject.SetActive(prBook.currentPage != 0);
        imageStatus.color = this.prBook.book_done != 0 ? new Color(0.40f, 1f, 0.40f, 1f) : new Color(0.10f, 0.7f, 0.10f, 1f);
    } 

    /// <summary>
    /// True when the shelf being rendered IS the learn-to-read ladder: the "learn to read" genre
    /// token, or a "levelN" token (how Nav addresses a single rung — see Filter.SetFilter).
    /// Globals.g_libraryFilter names the shelf currently showing (PRLibrary.SetFilter keeps it in
    /// step with the category arrows), so it is the shelf's own identity.
    /// </summary>
    public static bool IsLearnToReadShelf()
    {
        string filter = Globals.g_libraryFilter;
        if (string.IsNullOrEmpty(filter)) return false;
        filter = filter.Trim().ToLower();
        return filter == "learn to read" ||
               System.Text.RegularExpressions.Regex.IsMatch(filter, @"^level[1-4]$");
    }

    /// <summary>
    /// The author line, with a trailing "Level N" dropped when the row is already showing the level
    /// chip. Every ladder book's catalog author is "ReadingBuddy Level N", so a ladder row stacked
    /// "ReadingBuddy Level 2" straight above "Level 2" — the doubling the tester saw. Display-only:
    /// the catalog is untouched, and off the ladder shelf the author is printed verbatim as before.
    /// </summary>
    public static string AuthorLine(PRBook prBook, bool levelShown)
    {
        string author = prBook.bookAuthor ?? "";
        if (!levelShown) return author;

        string suffix = "Level " + prBook.level;
        if (author.EndsWith(suffix, System.StringComparison.OrdinalIgnoreCase))
            author = author.Substring(0, author.Length - suffix.Length).TrimEnd();
        return author;
    }

    // Band color per learn-to-read level (agreed convention): L1 pink, L2 orange,
    // L3 green, L4 blue. Any out-of-range level falls back to the pastel-by-number tint.
    private Color LevelBandColor(int level)
    {
        switch (level)
        {
            case 1: return PRUtils.StringToColor("F4C0D1");
            case 2: return PRUtils.StringToColor("FAC775");
            case 3: return PRUtils.StringToColor("C0DD97");
            case 4: return PRUtils.StringToColor("B5D4F4");
            default: return PRUtils.GetNthPastelColor(prBook.number);
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        // M-R4-1: guard against a tap on an item that hasn't been populated yet
        // (rare race during catalog load on slow devices). Globals.GotoPrBook
        // dereferences prBook.bookFullUrl immediately and would NRE on null.
        // BookstoreViewItem.OnPointerClick already has this guard.
        if (prBook != null)
        {
            // A navigation tile (non-empty action) runs a Nav address instead of
            // opening a story; a normal book (action "") opens as before.
            if (!string.IsNullOrEmpty(prBook.action))
                Nav.Go(prBook.action);
            else
                Globals.GotoPrBook(prBook);
        }
    }
    
}
