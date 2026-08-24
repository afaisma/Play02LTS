using System;
using System.Collections;
using System.Collections.Generic;
using QFSW.QC;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;


public class Filter
{
    public int ageFrom = 0;
    public int ageTo = 0;
    public String genre = "";
    // 0 = no level filter; 1-4 = "learn to read" ladder level. Set when the
    // filter token is "level1".."level4" (see SetFilter), so a string address
    // like "library?filter=level1" becomes a level match without disturbing
    // the existing genre/age logic.
    public int level = 0;
    // Selected reader age RANGE (inclusive). (0,0) = no age filter ("All"); a single age
    // N is the range (N,N). A book passes when its own [ageFrom, ageTo] OVERLAPS this range
    // (ageFrom <= ageHiSel && ageLoSel <= ageTo). Unlike ageFrom/ageTo below (a window the
    // book must fit *inside*), this is an intersection test. ANDs with genre/level/everything
    // so "fairytales for ages 3-5" works. Set from Globals.GetAgeLo()/GetAgeHi().
    public int ageLoSel = 0;
    public int ageHiSel = 0;

    public void SetFilter(int ageFrom, int ageTo, String genre)
    {
        this.ageFrom = ageFrom;
        this.ageTo = ageTo;
        // "levelN" (case-insensitive) is a level filter, not a genre — strip it
        // out of the genre substring test and record the level instead.
        this.level = ParseLevelToken(genre);
        this.genre = this.level > 0 ? "" : genre;
    }

    // Returns N for a "levelN" (N = 1..4, case-insensitive) token, else 0.
    private static int ParseLevelToken(String token)
    {
        if (token == null)
            return 0;
        var match = System.Text.RegularExpressions.Regex.Match(
            token.Trim(), @"^level([1-4])$",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return match.Success ? int.Parse(match.Groups[1].Value) : 0;
    }

    public bool Conforms(PRBook prBook)
    {
        // Navigation tiles (entries with an action) show only on the home "All Books" view.
        // They carry no age, so the age-point gate never applies to them.
        if (!string.IsNullOrEmpty(prBook.action))
            return level == 0 && (string.IsNullOrEmpty(genre) || genre == "everything");

        // Age-range gate (ANDs with everything below). (0,0) = no age filter.
        // Pass when the book's [ageFrom, ageTo] overlaps the selected [ageLoSel, ageHiSel].
        if (ageLoSel > 0 && ageHiSel > 0 && !(prBook.ageFrom <= ageHiSel && ageLoSel <= prBook.ageTo))
            return false;

        if (level > 0)
            return prBook.level == level;

        if (genre == "everything")
            return true;

        // "new" is a DATE token, not a genre substring: it matches recently published books
        // rather than books whose genre string happens to contain "new". Checked before the
        // substring test so the two can't collide; every other token behaves exactly as before.
        if (string.Equals(genre, "new", StringComparison.OrdinalIgnoreCase))
            return IsNewBook(prBook);

        if (genre != "")
            return prBook.genre.ToLower().Contains(genre.ToLower());

        if (ageFrom != 0 && ageTo != 0)
            return ageFrom <= prBook.ageFrom && prBook.ageTo <= ageTo;

        return true;
    }

    /// <summary>
    /// Backing test for the "new" filter token: the book's catalog `added` date (ISO
    /// yyyy-MM-dd, InvariantCulture) is within AppConfig.NewBookWindowDays of the device's
    /// date. A missing, blank or unparseable date means NOT new — so a catalog that carries
    /// no dates simply yields an empty "new" shelf (and, on Home, a door that FilterHasBooks
    /// drops) instead of an error. The one day of slack absorbs timezone skew between the
    /// publisher's date and the device's local date, while still rejecting far-future typos.
    /// </summary>
    public static bool IsNewBook(PRBook prBook)
    {
        if (prBook == null || string.IsNullOrEmpty(prBook.added))
            return false;
        if (!DateTime.TryParseExact(prBook.added.Trim(), "yyyy-MM-dd",
                                    System.Globalization.CultureInfo.InvariantCulture,
                                    System.Globalization.DateTimeStyles.None,
                                    out DateTime addedOn))
            return false;
        double days = (DateTime.Now.Date - addedOn.Date).TotalDays;
        return days >= -1d && days <= AppConfig.NewBookWindowDays;
    }
}

public class BooksScrollView : MonoBehaviour
{
    [SerializeField]
    private Transform scrollViewContent;
    
    [SerializeField]
    private GameObject bookPrefab;
    
    public ScrollRect scrollRectToStoreTheScrollPosition;
    private static Vector2 storedScrollPosition = new Vector2(-1, -1);

    private List<PRBook> prBooks;
    private Filter filter = new Filter();

    // Cover-thumbnail downloads are throttled. Firing 67 parallel
    // UnityWebRequests at CloudFront on Library entry was tripping
    // intermittent 403 "Access denied" responses (visible in the log as
    // four random book covers failing per Library load). Keeping ≤8
    // requests in flight at any time stays comfortably under any AWS
    // WAF rate-based rule and the macOS HTTP stack's per-host cap.
    // Same pattern as OverlayHost.LoadAndStartSprites.
    private const int MAX_INFLIGHT_COVERS = 8;
    private int _inflightCovers = 0;
    private readonly Queue<(string url, Image image)> _pendingCovers =
        new Queue<(string, Image)>();

    private void OnDestroy()
    {
        if (scrollRectToStoreTheScrollPosition != null)
            storedScrollPosition = scrollRectToStoreTheScrollPosition.normalizedPosition;
    }

    public void AddBook(PRBook prBook)
    {
        if (prBook.bookViewItem != null)
        {
            // Rows are pooled, not rebuilt, and the category arrows swap shelves in place — so a
            // reused row can be carrying the previous shelf's labels (BookViewItem's age/level line
            // depends on which shelf is showing). Re-stamp them; the cover is already loaded.
            prBook.bookViewItem.SetBookProperties(prBook);
            prBook.bookViewItem.gameObject.SetActive(true);
            return;
        }

        GameObject newBookGameObject = Instantiate(bookPrefab, scrollViewContent);
        if (newBookGameObject.TryGetComponent<BookViewItem>(out BookViewItem bookViewItem))
        {
            bookViewItem.prBook = prBook;
            string imageBookUrl = Globals.baseURL + prBook.bookImageUrl;
            // Cache-bust the cover by this book's content_rev (in the Library g_prbook is
            // null, so DownloadImage's wrap is a no-op — the item knows its own book here).
            imageBookUrl = Globals.WithContentRev(imageBookUrl, prBook.contentRev);
            EnqueueCoverDownload(imageBookUrl, bookViewItem.imageBook);
            bookViewItem.SetBookProperties(prBook);
            prBook.bookViewItem = bookViewItem;
        }
    }

    /// <summary>Throttled wrapper around PRUtils.DownloadImage for book
    /// covers. Either kicks off the download immediately or queues it for
    /// later, based on how many cover requests are already in flight.</summary>
    private void EnqueueCoverDownload(string url, Image image)
    {
        if (_inflightCovers < MAX_INFLIGHT_COVERS)
        {
            _inflightCovers++;
            StartCoroutine(DownloadCoverThrottled(url, image));
        }
        else
        {
            _pendingCovers.Enqueue((url, image));
        }
    }

    private IEnumerator DownloadCoverThrottled(string url, Image image)
    {
        // suppressAlert=true so a single failed thumbnail doesn't pop a
        // modal dialog — the NoImage placeholder is sufficient feedback.
        yield return PRUtils.DownloadImage(url, image, true, true);
        _inflightCovers--;
        // Hand the slot to the next pending request, if any.
        if (_pendingCovers.Count > 0 && _inflightCovers < MAX_INFLIGHT_COVERS)
        {
            var next = _pendingCovers.Dequeue();
            _inflightCovers++;
            StartCoroutine(DownloadCoverThrottled(next.url, next.image));
        }
    }

    public void AddBooks(List<PRBook> prBooks)
    {
        this.prBooks = prBooks;
        ShowBooks(filter);
    }


    public void ShowBooks(Filter filter)
    {
        if (prBooks == null)
            return;

        // Inherit the age choice made on the Home hub so the Library shows the same
        // age-appropriate set. (0,0) = All (no age filtering).
        if (filter != null)
        {
            filter.ageLoSel = Globals.GetAgeLo();
            filter.ageHiSel = Globals.GetAgeHi();
        }

        ClearScrollView();

        // The learn-to-read shelf reads as a ladder, so order it by (level, ladder-first, number).
        // "Ladder-first": books TAGGED "learn to read" (the phonics readers) lead their level;
        // leveled-but-untagged books (The Tired Boy, Shapes Around Us, Peter Rabbit, Pigeon)
        // stay on the shelf as harder "bonus reads" at the END, instead of greeting the child
        // first just because their catalog numbers are lower.
        // Every other filter iterates the catalog list unchanged (byte-identical order).
        IEnumerable<PRBook> ordered = prBooks;
        if (filter != null && (filter.genre == "learn to read" || filter.level > 0))
        {
            List<PRBook> sorted = new List<PRBook>(prBooks);
            sorted.Sort((a, b) =>
            {
                int byLevel = a.level.CompareTo(b.level);
                if (byLevel != 0) return byLevel;
                bool aLtr = IsLearnToReadTagged(a), bLtr = IsLearnToReadTagged(b);
                if (aLtr != bLtr) return aLtr ? -1 : 1;
                return a.number.CompareTo(b.number);
            });
            ordered = sorted;
        }

        // Rows are POOLED (hidden/shown, never re-created), so their sibling order is frozen
        // at first creation — iterating `ordered` alone cannot reorder a reused shelf. Track
        // the intended order here and enforce it with SetSiblingIndex after the add loop
        // (the same mechanism SetSortingByAge uses). No-op when the order already matches.
        int nextSibling = 0;
        foreach (PRBook prBook in ordered)
        {
            if (this.filter != null && !filter.Conforms(prBook))
                continue;
            AddBook(prBook);
            if (prBook.bookViewItem != null && prBook.bookViewItem.gameObject != null)
                prBook.bookViewItem.transform.SetSiblingIndex(nextSibling++);
        }
        
        if (storedScrollPosition != new Vector2(-1, -1) && scrollRectToStoreTheScrollPosition != null)
            scrollRectToStoreTheScrollPosition.normalizedPosition = storedScrollPosition;
    }

    [Command()]
    public void ClearScrollView()
    {
        foreach (Transform child in scrollViewContent)
        {
            //Destroy(child.gameObject);
            child.gameObject.SetActive(false);
        }
    }
    
    [Command] 
    public void SetFilter(int ageFrom, int ageTo, String genre)
    {
        filter.SetFilter(ageFrom, ageTo, genre);
        // Debug.Log("Set filter: " + ageFrom + " " + ageTo + " " + genre);
        ShowBooks(filter);
    }
    
    public void SetSortingByAge(bool bAscending)
    {
        List<Transform> children = new List<Transform>();

        foreach (Transform child in scrollViewContent)
        {
            children.Add(child);
        }

        children.Sort((t1, t2) => 
        {
            BookViewItem bvi1 = t1.GetComponent<BookViewItem>();
            BookViewItem bvi2 = t2.GetComponent<BookViewItem>();

            if (bvi1 != null && bvi2 != null)
            {
                if (bAscending)
                    return bvi1.prBook.ageFrom.CompareTo(bvi2.prBook.ageFrom);
                else
                    return bvi2.prBook.ageFrom.CompareTo(bvi1.prBook.ageFrom);
            }
            return 0;  // Consider how you wish to handle the case where BookViewItem component is missing.
        });

        for (int i = 0; i < children.Count; i++)
        {
            children[i].SetSiblingIndex(i);
        }
    }


    /// <summary>
    /// True when the book carries the "learn to read" genre tag — the actual phonics readers,
    /// as opposed to books that only carry a difficulty level. Same lowercase-contains
    /// convention Filter.Conforms uses for genre tokens.
    /// </summary>
    private static bool IsLearnToReadTagged(PRBook b)
    {
        return b != null && !string.IsNullOrEmpty(b.genre)
            && b.genre.ToLower().Contains("learn to read");
    }

    public void ResetScrollPosition()
    {
        storedScrollPosition = new Vector2(-1, -1);
        scrollRectToStoreTheScrollPosition.normalizedPosition = new Vector2(0, 1);
    }
    
}
