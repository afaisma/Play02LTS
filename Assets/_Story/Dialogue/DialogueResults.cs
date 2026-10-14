using System;
using System.Globalization;
using System.IO;
using UnityEngine;

/// <summary>
/// The record of the child's answers: one line per answer, per "no answer" and per skipped
/// question, in a plain table file, dialogue_results.csv, in the app's own data folder on the
/// device. No sound is recorded and the app sends the file nowhere (the device's own backup may
/// include it, like any app data). It is there so that progress can be looked at later; sending
/// it to a server would need the parents' consent and is not done.
/// </summary>
public static class DialogueResults
{
    public const string FileName = "dialogue_results.csv";
    public const string Header = "time_utc,book_id,book,level,page,answer,correct,how,attempt";

    public static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

    /// <summary>page: counted from 1, as the pages of the book script (chunk_1 is page 1).
    /// how: "touch", "voice", "sound"; "none" when the child gave no answer (attempt = how
    /// many times the app has waited); "skip" when the question was skipped. level = the level the
    /// book is made for, empty for a book without one.</summary>
    public static void Add(PRBook book, int page, string answer, bool correct, string how, int attempt)
    {
        try
        {
            string path = FilePath;
            string line = Line(DateTime.UtcNow, book != null ? book.id : "", book != null ? book.bookName : "",
                QuestionLevel.Of(book), page, answer, correct, how, attempt);
            File.AppendAllText(path, (File.Exists(path) ? "" : Header + "\n") + line + "\n");
        }
        catch (Exception e)
        {
            // The record is never worth stopping a book for.
            Debug.LogWarning("[Dialogue] result not saved: " + e.Message);
        }
    }

    /// <summary>How many questions of a book the child has answered on this device (distinct pages
    /// with an answer; "no answer" and "skip" do not count). 0 when nothing is recorded.</summary>
    public static int AnsweredCount(string bookId)
    {
        if (string.IsNullOrEmpty(bookId)) return 0;
        try
        {
            string path = FilePath;
            if (!File.Exists(path)) return 0;
            var pages = new System.Collections.Generic.HashSet<string>();
            foreach (string line in File.ReadAllLines(path))
            {
                // Columns: time,book_id,book,level,page,answer,correct,how,attempt.
                var cells = Cells(line);
                if (cells.Count < 9 || cells[1] != bookId) continue;
                string how = cells[7];
                if (how == "none" || how == "skip") continue;
                pages.Add(cells[4]);
            }
            return pages.Count;
        }
        catch (Exception)
        {
            return 0;
        }
    }

    public static string Line(DateTime timeUtc, string bookId, string bookName, string level, int page,
        string answer, bool correct, string how, int attempt)
    {
        return string.Join(",",
            timeUtc.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),
            Cell(bookId), Cell(bookName), Cell(level), page.ToString(CultureInfo.InvariantCulture),
            Cell(answer), correct ? "1" : "0", Cell(how), attempt.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>The cells of one line, with the quoting of <see cref="Cell"/> undone.</summary>
    public static System.Collections.Generic.List<string> Cells(string line)
    {
        var cells = new System.Collections.Generic.List<string>();
        var cell = new System.Text.StringBuilder();
        bool quoted = false;
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (quoted)
            {
                if (c == '"' && i + 1 < line.Length && line[i + 1] == '"') { cell.Append('"'); i++; }
                else if (c == '"') quoted = false;
                else cell.Append(c);
            }
            else if (c == '"') quoted = true;
            else if (c == ',') { cells.Add(cell.ToString()); cell.Clear(); }
            else cell.Append(c);
        }
        cells.Add(cell.ToString());
        return cells;
    }

    // One table cell: quoted when it holds a comma, a quote or a line break.
    private static string Cell(string text)
    {
        text = text ?? "";
        if (text.IndexOfAny(new[] { ',', '"', '\n', '\r' }) < 0) return text;
        return "\"" + text.Replace("\"", "\"\"") + "\"";
    }
}
