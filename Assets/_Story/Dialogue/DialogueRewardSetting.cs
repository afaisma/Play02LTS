using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// "Rewards for answers" on the Settings screen: Calm / Lively / No chime (DialogueRewards.Level).
/// The adult's choice, because children differ: some enjoy a lively reward, others need a calm one.
///
/// Shown only on a device that has opened a book with questions (DialogueRewards.UsedPrefKey), so
/// it does not puzzle anyone who has never seen one. Built in code into the free band under the
/// "Turn pages automatically" row, like AutopageSettingRow.
/// </summary>
public static class DialogueRewardSetting
{
    private static readonly Vector2 AnchorMin = new Vector2(0.10f, 0.215f);
    private static readonly Vector2 AnchorMax = new Vector2(0.90f, 0.345f);
    private const string RowName = "RewardLevelRow";

    public static void Attach()
    {
        if (PlayerPrefs.GetInt(DialogueRewards.UsedPrefKey, 0) != 1) return;
        Transform canvas = AutopageSettingRow.FindSettingsCanvas();
        if (canvas == null || canvas.Find(RowName) != null) return;

        var row = new GameObject(RowName, typeof(RectTransform), typeof(Image));
        row.transform.SetParent(canvas, false);
        var rt = (RectTransform)row.transform;
        rt.anchorMin = AnchorMin; rt.anchorMax = AnchorMax;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        var card = row.GetComponent<Image>();
        card.sprite = DialogChrome.RoundedSprite(); card.type = Image.Type.Sliced; card.color = UiTheme.Surface;

        TMP_Text title = DialogChrome.MakeText(row.transform, "Label", DialogueRewards.TextSettingTitle, 34f,
            TextAlignmentOptions.Left, UiTheme.TextPrimary);
        title.rectTransform.anchorMin = new Vector2(0f, 0.56f); title.rectTransform.anchorMax = new Vector2(1f, 1f);
        title.rectTransform.offsetMin = new Vector2(28f, 0f); title.rectTransform.offsetMax = new Vector2(-28f, -6f);

        var buttons = new GameObject("Buttons", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        buttons.transform.SetParent(row.transform, false);
        var brt = (RectTransform)buttons.transform;
        brt.anchorMin = new Vector2(0f, 0f); brt.anchorMax = new Vector2(1f, 0.56f);
        brt.offsetMin = new Vector2(22f, 16f); brt.offsetMax = new Vector2(-22f, 0f);
        var hlg = buttons.GetComponent<HorizontalLayoutGroup>();
        hlg.spacing = 14f;
        hlg.childControlWidth = true; hlg.childControlHeight = true;
        hlg.childForceExpandWidth = true; hlg.childForceExpandHeight = true;

        var levels = new[] { RewardLevel.Calm, RewardLevel.Lively, RewardLevel.Quiet };
        var texts = new[] { DialogueRewards.TextCalm, DialogueRewards.TextLively, DialogueRewards.TextQuiet };
        var fills = new Image[levels.Length];
        var labels = new TMP_Text[levels.Length];
        void Paint()
        {
            RewardLevel now = DialogueRewards.Level;
            for (int i = 0; i < levels.Length; i++)
            {
                bool on = levels[i] == now;
                fills[i].color = on ? UiTheme.Primary : UiTheme.Track;
                labels[i].color = on ? UiTheme.OnPrimary : UiTheme.TextPrimary;
            }
        }
        for (int i = 0; i < levels.Length; i++)
        {
            RewardLevel level = levels[i];
            var go = new GameObject("Reward_" + level.ToString().ToLowerInvariant(), typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(buttons.transform, false);
            fills[i] = go.GetComponent<Image>();
            fills[i].sprite = DialogChrome.RoundedSprite(); fills[i].type = Image.Type.Sliced;
            labels[i] = DialogChrome.MakeText(go.transform, "Label", texts[i], 30f, TextAlignmentOptions.Center, UiTheme.TextPrimary);
            DialogChrome.Stretch(labels[i].rectTransform, 8f);
            var button = go.GetComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => { DialogueRewards.Level = level; Paint(); });
            TapFeedback.AddPressFeedback(go);
        }
        Paint();
    }
}
