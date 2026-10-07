using System.Collections.Generic;

/// <summary>
/// The rules of one dialogue while it is on screen: what an answer, a silence or Skip does.
/// Pure logic (no Unity), driven by DialogueController and covered by EditMode tests.
///
/// The child is never told "wrong". Another answer fades that choice and the question is asked
/// again; when only the right choice is left, or after a second miss, the right choice is outlined.
/// </summary>
public class DialogueFlow
{
    public enum Step { Ask, Praise, Closed }
    public enum Reaction { None, Correct, Retry, Close }
    public enum SilenceReaction { None, Repeat, Close }

    /// <summary>The question is repeated at most this many times after silences; then it waits quietly.</summary>
    public const int MaxSilenceRepeats = 3;

    public readonly DialogueSpec spec;
    public Step step = Step.Ask;
    public int attempts;
    public int silences;
    public readonly HashSet<string> faded = new HashSet<string>();
    /// <summary>Outline the right choice.</summary>
    public bool hint;
    /// <summary>The accepted answer ("" in sound mode).</summary>
    public string chosen = "";

    public DialogueFlow(DialogueSpec spec) { this.spec = spec; }

    public bool IsRight(string id) => spec.EveryAnswerCounts || id == spec.answer;

    public Reaction Answer(string id)
    {
        if (step != Step.Ask) return Reaction.None;
        if (!spec.IsSound && (spec.Find(id) == null || faded.Contains(id))) return Reaction.None;

        attempts++;
        if (IsRight(id))
        {
            chosen = spec.IsSound ? "" : id;
            step = Step.Praise;
            return Reaction.Correct;
        }
        if (spec.settings.onOther == DialogueOnOther.Close)
        {
            step = Step.Closed;
            return Reaction.Close;
        }
        faded.Add(id);
        if (faded.Count >= 2 || spec.choices.Count - faded.Count <= 1) hint = true;
        return Reaction.Retry;
    }

    public SilenceReaction Silence()
    {
        if (step != Step.Ask) return SilenceReaction.None;
        silences++;
        if (spec.settings.onSilence == DialogueOnSilence.Close)
        {
            step = Step.Closed;
            return SilenceReaction.Close;
        }
        if (silences > MaxSilenceRepeats) return SilenceReaction.None;
        if (spec.settings.onSilence == DialogueOnSilence.Hint && spec.answer.Length > 0) hint = true;
        return SilenceReaction.Repeat;
    }

    public void Skip() => step = Step.Closed;

    /// <summary>The choices the child can still pick.</summary>
    public List<DialogueChoice> Open()
    {
        var list = new List<DialogueChoice>();
        foreach (DialogueChoice c in spec.choices)
            if (!faded.Contains(c.id)) list.Add(c);
        return list;
    }
}
