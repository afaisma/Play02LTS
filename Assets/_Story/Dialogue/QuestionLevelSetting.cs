using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// "Question level" on the Settings screen: A / B / C / D (QuestionLevel). The adult's choice.
/// The line under the title says what the chosen level asks of the child.
///
/// Shown only when the catalog has books made for a question level, so it does not puzzle anyone
/// in an app without such books. Built in code like AutopageSettingRow. The screen has no free
/// band left, so this row sits between "Turn pages automatically" and "Rewards for answers", and
/// those two rows give up some of their height when it is there (without it they stay as they are).
/// </summary>
public static class QuestionLevelSetting
{
    private static readonly Vector2 AnchorMin = new Vector2(0.10f, 0.305f);
    private static readonly Vector2 AnchorMax = new Vector2(0.90f, 0.375f);
    public const string RowName = "QuestionLevelRow";
    private const float AutopageRowBottom = 0.385f;      // instead of 0.36
    public const float RewardRowTopBelowThisRow = 0.295f; // instead of 0.345

    public static void Attach()
    {
        if (!QuestionLevel.CatalogHasLevels(Globals.g_allPRBooks)) return;
        Transform canvas = AutopageSettingRow.FindSettingsCanvas();
        if (canvas == null || canvas.Find(RowName) != null) return;

        var row = new GameObject(RowName, typeof(RectTransform), typeof(Image));
        row.transform.SetParent(canvas, false);
        var rt = (RectTransform)row.transform;
        rt.anchorMin = AnchorMin; rt.anchorMax = AnchorMax;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        var card = row.GetComponent<Image>();
        card.sprite = DialogChrome.RoundedSprite(); card.type = Image.Type.Sliced; card.color = UiTheme.Surface;

        // Make room: the row above gives up its lower part (DialogueRewardSetting looks for this row).
        if (canvas.Find("AutopageRow") is RectTransform autopage)
            autopage.anchorMin = new Vector2(autopage.anchorMin.x, AutopageRowBottom);

        // Left: the title, and under it what the chosen level means.
        TMP_Text title = DialogChrome.MakeText(row.transform, "Label", QuestionLevel.TextSettingTitle, 34f,
            TextAlignmentOptions.BottomLeft, UiTheme.TextPrimary);
        title.rectTransform.anchorMin = new Vector2(0f, 0.5f); title.rectTransform.anchorMax = new Vector2(0.56f, 1f);
        title.rectTransform.offsetMin = new Vector2(28f, 0f); title.rectTransform.offsetMax = new Vector2(0f, -6f);
        TMP_Text about = DialogChrome.MakeText(row.transform, "About", "", 26f,
            TextAlignmentOptions.TopLeft, UiTheme.TextPrimary);
        about.rectTransform.anchorMin = new Vector2(0f, 0f); about.rectTransform.anchorMax = new Vector2(0.56f, 0.5f);
        about.rectTransform.offsetMin = new Vector2(28f, 6f); about.rectTransform.offsetMax = new Vector2(0f, -2f);
        about.enableAutoSizing = true; about.fontSizeMin = 16f; about.fontSizeMax = 26f;
        about.textWrappingMode = TextWrappingModes.NoWrap;

        // Right: the four letters.
        var buttons = new GameObject("Buttons", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        buttons.transform.SetParent(row.transform, false);
        var brt = (RectTransform)buttons.transform;
        brt.anchorMin = new Vector2(0.56f, 0f); brt.anchorMax = new Vector2(1f, 1f);
        brt.offsetMin = new Vector2(8f, 16f); brt.offsetMax = new Vector2(-18f, -16f);
        var hlg = buttons.GetComponent<HorizontalLayoutGroup>();
        hlg.spacing = 10f;
        hlg.childControlWidth = true; hlg.childControlHeight = true;
        hlg.childForceExpandWidth = true; hlg.childForceExpandHeight = true;

        string[] letters = QuestionLevel.Letters;
        var fills = new Image[letters.Length];
        var labels = new TMP_Text[letters.Length];
        void Paint()
        {
            string now = QuestionLevel.Current;
            for (int i = 0; i < letters.Length; i++)
            {
                bool on = letters[i] == now;
                fills[i].color = on ? UiTheme.Primary : UiTheme.Track;
                labels[i].color = on ? UiTheme.OnPrimary : UiTheme.TextPrimary;
                if (on) about.text = QuestionLevel.TextAbout[i];
            }
        }
        for (int i = 0; i < letters.Length; i++)
        {
            string letter = letters[i];
            var go = new GameObject("QuestionLevel_" + letter, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(buttons.transform, false);
            fills[i] = go.GetComponent<Image>();
            fills[i].sprite = DialogChrome.RoundedSprite(); fills[i].type = Image.Type.Sliced;
            labels[i] = DialogChrome.MakeText(go.transform, "Label", letter.ToUpperInvariant(), 32f,
                TextAlignmentOptions.Center, UiTheme.TextPrimary);
            DialogChrome.Stretch(labels[i].rectTransform, 4f);
            var button = go.GetComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => { QuestionLevel.Current = letter; Paint(); });
            TapFeedback.AddPressFeedback(go);
        }
        Paint();
    }
}
