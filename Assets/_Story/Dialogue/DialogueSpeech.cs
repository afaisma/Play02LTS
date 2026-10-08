using System.Collections.Generic;
using System.Text;

/// <summary>
/// Spoken answers: which words to listen for, and which choice a recognized text means.
/// Pure logic (no Unity, no recognizer), covered by EditMode tests.
/// </summary>
public static class DialogueSpeech
{
    /// <summary>Lower case, letters / digits / apostrophes only, single spaces.</summary>
    public static string Normalize(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        var sb = new StringBuilder(s.Length);
        bool space = false;
        foreach (char raw in s.ToLowerInvariant())
        {
            char c = raw == '’' ? '\'' : raw;
            bool keep = (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '\'';
            if (keep)
            {
                if (space && sb.Length > 0) sb.Append(' ');
                sb.Append(c);
                space = false;
            }
            else space = true;
        }
        return sb.ToString();
    }

    /// <summary>
    /// What to listen for, for one choice: its extra words if the book gives any, else its text;
    /// each also without a leading "a", "an" or "the" ("A beanstalk" is heard as "beanstalk" too).
    /// </summary>
    public static List<string> Phrases(DialogueChoice choice)
    {
        var result = new List<string>();
        var source = choice.words.Count > 0 ? choice.words : new List<string> { choice.text };
        foreach (string s in source)
        {
            string p = Normalize(s);
            Add(result, p);
            foreach (string article in new[] { "a ", "an ", "the " })
                if (p.StartsWith(article)) Add(result, p.Substring(article.Length));
        }
        return result;
    }

    /// <summary>Every phrase of the given choices (the recognizer's word list).</summary>
    public static List<string> Vocabulary(IEnumerable<DialogueChoice> choices)
    {
        var result = new List<string>();
        foreach (DialogueChoice c in choices)
            foreach (string p in Phrases(c)) Add(result, p);
        return result;
    }

    /// <summary>
    /// The id of the choice named in the recognized text, or null. Whole words only ("no" is not
    /// found in "know"). When several choices are named, the one said last wins (a child who says
    /// "house... beanstalk" means the beanstalk).
    /// </summary>
    public static string Match(string recognized, IEnumerable<DialogueChoice> choices)
    {
        string text = " " + Normalize(recognized) + " ";
        string best = null;
        int bestEnd = -1;
        foreach (DialogueChoice c in choices)
        {
            foreach (string p in Phrases(c))
            {
                int at = text.LastIndexOf(" " + p + " ", System.StringComparison.Ordinal);
                if (at < 0) continue;
                int end = at + p.Length;
                if (end > bestEnd) { bestEnd = end; best = c.id; }
            }
        }
        return best;
    }

    private static void Add(List<string> list, string phrase)
    {
        if (phrase.Length > 0 && !list.Contains(phrase)) list.Add(phrase);
    }
}

/// <summary>
/// "Any sound counts": decides when the microphone level means the child made a sound.
///
/// The first moments measure the room. After that a level clearly above the room, held for a
/// short time, is a sound; a single click or a door slam is too short to count. Two things keep
/// an eager child from being missed: a clearly loud level counts at once, also while the room is
/// still being measured, and the room estimate keeps following the quiet moments, so a room that
/// was measured too high (the child was already making sounds) comes down again.
/// The numbers are first guesses; they need tuning with real children and real rooms.
/// </summary>
public class SoundTrigger
{
    public float minLevel = 0.02f;       // never trigger below this, however quiet the room
    public float aboveRoom = 3f;         // how many times louder than the room
    public float loudLevel = 0.1f;       // clearly a voice in any room; the trigger level never goes above it
    public float holdSeconds = 0.25f;    // how long the sound must last
    public float roomSeconds = 0.5f;     // how long the room is measured first
    public float followSeconds = 1f;     // how fast the room estimate follows quiet moments

    private const float MaxStep = 0.1f;  // one long frame is not seconds of sound or of silence

    private float _roomTime, _roomSum, _room, _held;

    public float Threshold => System.Math.Min(loudLevel, System.Math.Max(minLevel, _room * aboveRoom));

    public bool MeasuringRoom => _roomTime < roomSeconds;

    /// <summary>Feed the current level; true at the moment a sound is recognized.</summary>
    public bool Feed(float level, float deltaSeconds)
    {
        float dt = System.Math.Min(deltaSeconds, MaxStep);
        bool measuring = MeasuringRoom;
        if (measuring)
        {
            _roomTime += dt;
            _roomSum += level * dt;
            if (_roomTime > 0f) _room = _roomSum / _roomTime;
        }

        if (level > (measuring ? loudLevel : Threshold))
        {
            _held += dt;
            if (_held >= holdSeconds) { _held = 0f; return true; }
            return false;
        }

        _held = 0f;
        if (!measuring) _room += (level - _room) * System.Math.Min(1f, dt / followSeconds);
        return false;
    }
}
