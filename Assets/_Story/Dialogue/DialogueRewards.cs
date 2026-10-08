using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>How strong the rewards are. The adult chooses (Settings); a book cannot raise it.</summary>
public enum RewardLevel { Calm, Lively, Quiet }

/// <summary>
/// Rewards for answered questions: the adult's level, the fixed texts and the two chimes.
///
/// Calm (default): a soft chime, the chosen card moves a little, a star is collected.
/// Lively: a brighter chime and confetti with every answer.
/// Quiet ("No chime"): as Calm without the chime (the spoken praise of the book still plays).
/// Every level ends the book with the collected stars and a short, slow confetti.
/// The chimes are made in code (two or four soft notes), so there is no sound file to ship.
/// </summary>
public static class DialogueRewards
{
    public const string LevelPrefKey = "dialogue_rewards";
    /// <summary>Set when this device has shown a question once: Settings then offers the reward level.</summary>
    public const string UsedPrefKey = "dialogue_used";

    public const string TextHeard = "I heard you!";
    public const string TextSettingTitle = "Rewards for answers";
    public const string TextCalm = "Calm";
    public const string TextLively = "Lively";
    public const string TextQuiet = "No chime";

    public static RewardLevel Level
    {
        get => ParseLevel(PlayerPrefs.GetString(LevelPrefKey, ""));
        set { PlayerPrefs.SetString(LevelPrefKey, value.ToString().ToLowerInvariant()); PlayerPrefs.Save(); }
    }

    public static RewardLevel ParseLevel(string value) =>
        Enum.TryParse((value ?? "").Trim(), true, out RewardLevel level) && Enum.IsDefined(typeof(RewardLevel), level)
            ? level : RewardLevel.Calm;

    public static string SummaryText(int answered) =>
        answered == 1 ? "You answered 1 question!" : "You answered " + answered + " questions!";

    // ---------------------------------------------------------------- chimes

    private static AudioClip _calm, _lively;

    public static AudioClip Chime(bool lively)
    {
        if (lively)
        {
            if (_lively == null) _lively = Notes("RewardLively", new[] { 523.25f, 659.25f, 783.99f, 1046.5f }, 0.11f, 0.26f, 0.30f);
            return _lively;
        }
        if (_calm == null) _calm = Notes("RewardCalm", new[] { 783.99f, 1046.5f }, 0.16f, 0.22f, 0.24f);
        return _calm;
    }

    // Soft bell-like notes one after another: a sine with a little second harmonic, a short
    // attack (no click) and an exponential fade.
    private static AudioClip Notes(string name, float[] hz, float step, float decay, float volume)
    {
        const int rate = 44100;
        float seconds = step * (hz.Length - 1) + decay * 4f;
        var data = new float[(int)(seconds * rate)];
        for (int n = 0; n < hz.Length; n++)
        {
            int start = (int)(n * step * rate);
            for (int i = start; i < data.Length; i++)
            {
                float t = (i - start) / (float)rate;
                float envelope = Mathf.Min(1f, t / 0.008f) * Mathf.Exp(-t / decay);
                float phase = 2f * Mathf.PI * hz[n] * t;
                data[i] += volume * envelope * (Mathf.Sin(phase) + 0.25f * Mathf.Sin(2f * phase));
            }
        }
        var clip = AudioClip.Create(name, data.Length, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}

/// <summary>
/// The stars of the open book: one place for each page with a question that earns a star.
/// A star is earned by answering (also on a second try, also by "any sound"); a skipped
/// question leaves its place empty. Pure logic, covered by EditMode tests.
/// </summary>
public class StarRow
{
    private readonly List<int> _pages = new List<int>();
    private readonly HashSet<int> _earned = new HashSet<int>();

    public int Total => _pages.Count;
    public int Earned => _earned.Count;

    public void Reset(IEnumerable<int> starPages)
    {
        _pages.Clear();
        _earned.Clear();
        if (starPages != null) _pages.AddRange(starPages);
        _pages.Sort();
    }

    /// <summary>The question of this page was answered. Returns the place of its star in the row.</summary>
    public int Earn(int page)
    {
        if (!_pages.Contains(page)) { _pages.Add(page); _pages.Sort(); } // a question the book text did not show
        _earned.Add(page);
        return _pages.IndexOf(page);
    }

    /// <summary>One entry per place, in page order: true = star earned.</summary>
    public List<bool> States()
    {
        var states = new List<bool>(_pages.Count);
        foreach (int page in _pages) states.Add(_earned.Contains(page));
        return states;
    }
}
