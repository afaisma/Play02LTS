using System;
using System.Collections.Generic;

// A dialogue = one spoken question on a story page, with choices the child answers by touch or
// by voice (or by making any sound). This file is the data a book script describes with the
// Dialogue* commands (DialogueCommands.cs) and the small collector that turns those commands
// into one DialogueSpec per page. No Unity types here, so all of it is covered by EditMode tests.

public enum DialogueInput { Touch, Voice, Both, Sound }
public enum DialogueOnOther { Retry, Accept, Close }
public enum DialogueOnSilence { Repeat, Hint, Close }

public class DialogueChoice
{
    public string id = "";
    public string text = "";
    /// <summary>A picture path, "icon:check" / "icon:cross", or "" for a word-only choice.</summary>
    public string image = "";
    /// <summary>Extra spoken forms. Empty = listen for the text.</summary>
    public List<string> words = new List<string>();
    // Own position in percent of the screen (as CreateButton); negative = arranged by the app.
    // Read and kept, not laid out yet.
    public float x = -1f, y = -1f, width = -1f;

    public bool IsIcon => image.StartsWith("icon:", StringComparison.OrdinalIgnoreCase);
    public string IconName => IsIcon ? image.Substring(5).Trim().ToLowerInvariant() : "";
    public bool HasPicture => image.Length > 0 && !IsIcon;
}

public class DialogueSettings
{
    public DialogueInput input = DialogueInput.Both;
    public DialogueOnOther onOther = DialogueOnOther.Retry;
    public DialogueOnSilence onSilence = DialogueOnSilence.Repeat;
    public float silenceSeconds = 8f;
    public bool skip = true;

    public DialogueSettings Clone() => (DialogueSettings)MemberwiseClone();
}

public class DialogueSpec
{
    public string question = "";
    public string questionAudio = "";
    public readonly List<DialogueChoice> choices = new List<DialogueChoice>();
    /// <summary>The id of the right choice. "" = every answer is accepted.</summary>
    public string answer = "";
    public string praiseText = "";
    public string praiseAudio = "";
    public DialogueSettings settings = new DialogueSettings();

    public const string DefaultPraise = "Good job!";

    public bool IsSound => settings.input == DialogueInput.Sound;
    public bool VoiceWanted => settings.input == DialogueInput.Voice || settings.input == DialogueInput.Both;
    public bool EveryAnswerCounts => IsSound || answer.Length == 0 || settings.onOther == DialogueOnOther.Accept;

    public DialogueChoice Find(string id)
    {
        foreach (DialogueChoice c in choices)
            if (c.id == id) return c;
        return null;
    }

    /// <summary>Null when the dialogue can be shown, else what is wrong with it (for the log).</summary>
    public string Problem()
    {
        if (question.Length == 0) return "no DialogueQuestion on this page";
        if (IsSound) return null;
        if (choices.Count == 0) return "no DialogueChoice (and the input is not \"sound\")";
        if (answer.Length > 0 && Find(answer) == null) return "DialogueAnswer \"" + answer + "\" is not one of the choices";
        return null;
    }
}

/// <summary>
/// Collects the Dialogue* commands of a book. Commands that run before the first page set the
/// book's settings; commands inside a page describe that page's dialogue and may change the
/// settings for that dialogue only.
/// </summary>
public class DialogueScript
{
    private DialogueSettings _book = new DialogueSettings();
    private DialogueSpec _page;

    public Action<string> Warn = _ => { };

    public void BeginBook()
    {
        _book = new DialogueSettings();
        _page = null;
    }

    public void BeginPage()
    {
        _page = new DialogueSpec { settings = _book.Clone() };
    }

    private DialogueSettings Settings => _page != null ? _page.settings : _book;

    private bool NeedPage(string command)
    {
        if (_page != null) return true;
        Warn(command + " is only allowed inside a page");
        return false;
    }

    public void Question(string text, string audio)
    {
        if (!NeedPage("DialogueQuestion")) return;
        _page.question = (text ?? "").Trim();
        _page.questionAudio = (audio ?? "").Trim();
    }

    public void Choice(string id, string text, string image, string words, float x, float y, float width)
    {
        if (!NeedPage("DialogueChoice")) return;
        id = (id ?? "").Trim();
        if (id.Length == 0) { Warn("DialogueChoice needs a name"); return; }
        if (_page.Find(id) != null) { Warn("DialogueChoice \"" + id + "\" is defined twice"); return; }
        var c = new DialogueChoice { id = id, text = (text ?? "").Trim(), image = (image ?? "").Trim(), x = x, y = y, width = width };
        foreach (string w in (words ?? "").Split(','))
            if (w.Trim().Length > 0) c.words.Add(w.Trim());
        _page.choices.Add(c);
    }

    public void Answer(string id)
    {
        if (!NeedPage("DialogueAnswer")) return;
        _page.answer = (id ?? "").Trim();
    }

    public void Praise(string text, string audio)
    {
        if (!NeedPage("DialoguePraise")) return;
        _page.praiseText = (text ?? "").Trim();
        _page.praiseAudio = (audio ?? "").Trim();
    }

    public void Input(string value)
    {
        if (TryParse(value, out DialogueInput v)) Settings.input = v;
        else Warn("DialogueInput: unknown value \"" + value + "\" (touch, voice, both, sound)");
    }

    public void OnOther(string value)
    {
        if (TryParse(value, out DialogueOnOther v)) Settings.onOther = v;
        else Warn("DialogueOnOther: unknown value \"" + value + "\" (retry, accept, close)");
    }

    public void OnSilence(float seconds, string value)
    {
        if (!TryParse(value, out DialogueOnSilence v))
        {
            Warn("DialogueOnSilence: unknown value \"" + value + "\" (repeat, hint, close)");
            return;
        }
        Settings.onSilence = v;
        if (seconds > 0f) Settings.silenceSeconds = seconds;
    }

    public void Skip(bool show) => Settings.skip = show;

    /// <summary>
    /// The page's dialogue, ready to show; null (with a warning) when it is incomplete. The page
    /// keeps collecting afterwards, so a later DialogueShow on the same page gets the same one.
    /// </summary>
    public DialogueSpec Take()
    {
        if (!NeedPage("DialogueShow")) return null;
        string problem = _page.Problem();
        if (problem != null) { Warn("DialogueShow: " + problem); return null; }
        return _page;
    }

    private static readonly System.Text.RegularExpressions.Regex InputLine =
        new System.Text.RegularExpressions.Regex(@"^\s*DialogueInput\s*\(?\s*""(\w+)""");

    /// <summary>
    /// Does this book script have a question that listens (voice, both or sound)? Read from the
    /// text, before the book runs: DialogueInput before the first page is the book's default, on
    /// a page it holds for that page.
    /// </summary>
    public static bool WantsMicrophone(string script)
    {
        bool bookTouch = false, pageTouch = false, preamble = true, inPage = false, pageHasQuestion = false;
        foreach (string raw in (script ?? "").Split('\n'))
        {
            string line = raw.TrimStart();
            if (line.StartsWith("////////["))
            {
                if (pageHasQuestion && !pageTouch) return true;
                preamble = false;
                inPage = line.StartsWith("////////[chunk"); // not an event block
                pageTouch = bookTouch; pageHasQuestion = false;
                continue;
            }
            if (line.StartsWith("//")) continue;
            var m = InputLine.Match(line);
            if (m.Success)
            {
                bool touch = m.Groups[1].Value.ToLowerInvariant() == "touch";
                if (inPage) pageTouch = touch;
                else if (preamble) bookTouch = pageTouch = touch;
            }
            else if (inPage && line.StartsWith("DialogueQuestion")) pageHasQuestion = true;
        }
        return pageHasQuestion && !pageTouch;
    }

    private static bool TryParse<T>(string value, out T result) where T : struct
    {
        result = default;
        value = (value ?? "").Trim();
        // Enum.TryParse also accepts numbers ("3"); only the names are valid here.
        return value.Length > 0 && !char.IsDigit(value[0]) && value[0] != '-'
               && Enum.TryParse(value, true, out result) && Enum.IsDefined(typeof(T), result);
    }
}
