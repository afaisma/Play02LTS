using Miniscript;

/// <summary>
/// The Dialogue* commands of the book script language:
///
///   DialogueQuestion text, audio            the question and its recording ("gen//q9")
///   DialogueChoice name, text, image, words, x, y, width
///                                           one choice; image is a picture path, "icon:check",
///                                           "icon:cross" or empty; words = extra spoken forms
///   DialogueAnswer name                     the right choice (leave out: every answer counts)
///   DialoguePraise text, audio              shown and played after the right answer
///   DialogueInput "touch|voice|both|sound"
///   DialogueOnOther "retry|accept|close"    after another answer
///   DialogueOnSilence seconds, "repeat|hint|close"
///   DialogueSkip 1|0                        the Skip button
///   DialogueShow ["now"]                    open it (default: after the page narration)
///
/// The four settings commands set the whole book's defaults when they run before the first page,
/// and one dialogue's when they run inside a page. Events: [event OnAnswer] (answer, isCorrect,
/// how, attempt) and [event OnNoAnswer] run in addition to the built-in reaction.
///
/// MiniScript intrinsics are global, so they are created once and reach the open book through
/// <see cref="Current"/> (set by DialogueController on every page).
/// </summary>
public static class DialogueCommands
{
    public static DialogueController Current;
    private static bool _registered;

    public static void Register()
    {
        if (_registered) return;
        _registered = true;

        Intrinsic f = Intrinsic.Create("DialogueQuestion");
        f.AddParam("text", "");
        f.AddParam("audio", "");
        f.code = (context, partialResult) =>
        {
            Current?.Script.Question(Str(context, "text"), Str(context, "audio"));
            return new Intrinsic.Result(ValNumber.one);
        };

        f = Intrinsic.Create("DialogueChoice");
        f.AddParam("name", "");
        f.AddParam("text", "");
        f.AddParam("image", "");
        f.AddParam("words", "");
        f.AddParam("x", -1);
        f.AddParam("y", -1);
        f.AddParam("width", -1);
        f.code = (context, partialResult) =>
        {
            Current?.Script.Choice(Str(context, "name"), Str(context, "text"), Str(context, "image"),
                Str(context, "words"), Num(context, "x"), Num(context, "y"), Num(context, "width"));
            return new Intrinsic.Result(ValNumber.one);
        };

        f = Intrinsic.Create("DialogueAnswer");
        f.AddParam("name", "");
        f.code = (context, partialResult) =>
        {
            Current?.Script.Answer(Str(context, "name"));
            return new Intrinsic.Result(ValNumber.one);
        };

        f = Intrinsic.Create("DialoguePraise");
        f.AddParam("text", "");
        f.AddParam("audio", "");
        f.code = (context, partialResult) =>
        {
            Current?.Script.Praise(Str(context, "text"), Str(context, "audio"));
            return new Intrinsic.Result(ValNumber.one);
        };

        f = Intrinsic.Create("DialogueInput");
        f.AddParam("value", "both");
        f.code = (context, partialResult) =>
        {
            Current?.Script.Input(Str(context, "value"));
            return new Intrinsic.Result(ValNumber.one);
        };

        f = Intrinsic.Create("DialogueOnOther");
        f.AddParam("value", "retry");
        f.code = (context, partialResult) =>
        {
            Current?.Script.OnOther(Str(context, "value"));
            return new Intrinsic.Result(ValNumber.one);
        };

        f = Intrinsic.Create("DialogueOnSilence");
        f.AddParam("seconds", 8);
        f.AddParam("value", "repeat");
        f.code = (context, partialResult) =>
        {
            Current?.Script.OnSilence(Num(context, "seconds"), Str(context, "value"));
            return new Intrinsic.Result(ValNumber.one);
        };

        f = Intrinsic.Create("DialogueSkip");
        f.AddParam("show", 1);
        f.code = (context, partialResult) =>
        {
            Current?.Script.Skip(Num(context, "show") != 0f);
            return new Intrinsic.Result(ValNumber.one);
        };

        f = Intrinsic.Create("DialogueShow");
        f.AddParam("when", "afterNarration");
        f.code = (context, partialResult) =>
        {
            Current?.Show(Str(context, "when"));
            return new Intrinsic.Result(ValNumber.one);
        };
    }

    private static string Str(TAC.Context context, string name)
    {
        Value v = context.GetVar(name);
        return v == null ? "" : v.ToString();
    }

    private static float Num(TAC.Context context, string name)
    {
        Value v = context.GetVar(name);
        return v == null ? 0f : v.FloatValue();
    }
}
