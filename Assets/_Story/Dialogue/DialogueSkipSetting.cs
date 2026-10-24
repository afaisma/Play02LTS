using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// "Show the Skip button" on the Settings screen. Off by default: with a Skip button a child can
/// tap it and never speak (Andrey). Nobody gets stuck without it: on the voice levels the choices
/// appear after two misses, and a sound question has "Tap to answer". The demo question on the
/// first start keeps its Skip, because there the grown-up is trying the app.
///
/// Shown only when the catalog has books made for a question level, like the Question level row.
/// Built in code into the band above "Turn pages automatically".
/// </summary>
public static class DialogueSkipSetting
{
    public const string PrefKey = "dialogue_skip";
    public const string TextSettingTitle = "Show the Skip button";
    private static readonly Vector2 AnchorMin = new Vector2(0.10f, 0.435f);
    private static readonly Vector2 AnchorMax = new Vector2(0.90f, 0.48f);
    /// <summary>The "Turn pages automatically" row ends here when this row is above it.</summary>
    public const float AutopageRowTopBelowThisRow = 0.428f;
    private const string RowName = "SkipButtonRow";

    /// <summary>True when the questions show their Skip button.</summary>
    public static bool Enabled => PlayerPrefs.GetInt(PrefKey, 0) == 1;

    public static void Attach()
    {
        if (!QuestionLevel.CatalogHasLevels(Globals.g_allPRBooks)) return;
        Transform canvas = AutopageSettingRow.FindSettingsCanvas();
        if (canvas == null || canvas.Find(RowName) != null) return;

        var row = new GameObject(RowName, typeof(RectTransform), typeof(Image), typeof(Toggle));
        row.transform.SetParent(canvas, false);
        var rt = (RectTransform)row.transform;
        rt.anchorMin = AnchorMin; rt.anchorMax = AnchorMax;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        var card = row.GetComponent<Image>();
        card.sprite = DialogChrome.RoundedSprite(); card.type = Image.Type.Sliced; card.color = UiTheme.Surface;

        // Make room: the row below gives up its upper part.
        if (canvas.Find("AutopageRow") is RectTransform autopage)
            autopage.anchorMax = new Vector2(autopage.anchorMax.x, AutopageRowTopBelowThisRow);

        TMP_Text label = DialogChrome.MakeText(row.transform, "Label", TextSettingTitle, 34f,
            TextAlignmentOptions.Left, UiTheme.TextPrimary);
        label.rectTransform.anchorMin = new Vector2(0f, 0f); label.rectTransform.anchorMax = new Vector2(0.78f, 1f);
        label.rectTransform.offsetMin = new Vector2(28f, 0f); label.rectTransform.offsetMax = Vector2.zero;

        // The check box on the right, as in the "Turn pages automatically" row.
        var box = new GameObject("Box", typeof(RectTransform), typeof(Image));
        box.transform.SetParent(row.transform, false);
        var brt = (RectTransform)box.transform;
        brt.anchorMin = brt.anchorMax = new Vector2(1f, 0.5f);
        brt.pivot = new Vector2(1f, 0.5f);
        brt.sizeDelta = new Vector2(64f, 64f);
        brt.anchoredPosition = new Vector2(-28f, 0f);
        var boxImg = box.GetComponent<Image>();
        boxImg.sprite = DialogChrome.RoundedSprite(); boxImg.type = Image.Type.Sliced; boxImg.color = UiTheme.Track;
        var check = new GameObject("Check", typeof(RectTransform), typeof(Image));
        check.transform.SetParent(box.transform, false);
        var crt = (RectTransform)check.transform;
        crt.anchorMin = new Vector2(0.15f, 0.15f); crt.anchorMax = new Vector2(0.85f, 0.85f);
        crt.offsetMin = Vector2.zero; crt.offsetMax = Vector2.zero;
        var checkImg = check.GetComponent<Image>();
        checkImg.color = UiTheme.Primary; checkImg.raycastTarget = false;

        var toggle = row.GetComponent<Toggle>();
        toggle.targetGraphic = card;
        toggle.graphic = checkImg;
        toggle.isOn = Enabled;
        toggle.onValueChanged.AddListener(on =>
        {
            PlayerPrefs.SetInt(PrefKey, on ? 1 : 0);
            PlayerPrefs.Save();
        });
    }
}
