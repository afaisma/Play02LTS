using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SettingsScene : MonoBehaviour
{
    public Slider sliderRate;
    public Toggle toggleSetReadingSpeedByBooksAgeGroup;
    public TMP_Text txtReadingSpeedDescr;
    public TMP_Text versionText;

    // Start is called before the first frame update
    // The slider's positions, left to right.
    private static readonly string[] Rates = { "-30", "-20", "-10", "0", "10" };

    void Start()
    {
        // An edition without age groups (Story Comprehension): no check box, the slider alone.
        if (!Edition.Current.readingSpeedByAgeGroup && toggleSetReadingSpeedByBooksAgeGroup != null)
        {
            toggleSetReadingSpeedByBooksAgeGroup.gameObject.SetActive(false);
            toggleSetReadingSpeedByBooksAgeGroup = null;
        }
        ShowReadingRate();

        int nSetReadingSpeedByBooksAgeGroup = PlayerPrefs.GetInt("g_bSetReadingSpeedByBooksAgeGroup", 1);

        if (toggleSetReadingSpeedByBooksAgeGroup != null)
        {
            toggleSetReadingSpeedByBooksAgeGroup.isOn =
                nSetReadingSpeedByBooksAgeGroup == 1;
            if (toggleSetReadingSpeedByBooksAgeGroup.isOn)
                sliderRate.interactable = false;
            else
                sliderRate.interactable = true;
        }
        else if (sliderRate != null)
            sliderRate.interactable = true; // no check box: the slider is always the adult's

        DisplayReadingSpeedDescr(); // again, now that the check box shows the saved choice

        if (versionText != null)
            versionText.text = "Version: " + Application.version;

        // The scene's title is authored at the very top of the canvas (anchors 0.90-0.96) with no
        // inset of its own, so the Dynamic Island clips it — same as the Library and _Parents. The
        // next control down sits at 0.70, so nothing below has to give room back.
        SafeAreaInsets.ApplyTop(GameObject.Find("txtTitle")?.transform as RectTransform);

        // "Turn pages automatically" — built in code against this scene's canvas (see
        // AutopageSettingRow); it moved here out of the reading-mode modal.
        AutopageSettingRow.Attach();
        QuestionLevelSetting.Attach(ShowReadingRate); // only when the catalog has books made for a question level
        DialogueSkipSetting.Attach();   // same condition
        DialogueRewardSetting.Attach(); // only after a book with questions has been opened on this device

        // Without the age-group check box (Story Comprehension) the top of the screen would be
        // empty: the speed controls move up and the rows spread out evenly.
        if (!Edition.Current.readingSpeedByAgeGroup)
        {
            Place("txtReadingSpeed", 0.775f, 0.825f);
            Place("sliderReadingRate", 0.715f, 0.765f);
            Place("txtReadingSpeedDescr", 0.655f, 0.705f);
            Place("SkipButtonRow", 0.575f, 0.63f);
            Place("AutopageRow", 0.50f, 0.555f);
            Place("QuestionLevelRow", 0.41f, 0.485f);
            Place("RewardLevelRow", 0.295f, 0.385f);
        }
    }

    // L-R3-2: removed an empty Update() that cost a per-frame managed call for no gain.

    // The slider and its words show the speed in use (Globals.ReadingRateFromSlider): the adult's own
    // once they have moved the slider, else the default, which in Story Comprehension follows the
    // question level (so also after the level row below changes it). Saves nothing: only a move of
    // the slider by the adult does (HandleRateValueChange).
    private void ShowReadingRate()
    {
        int i = System.Array.IndexOf(Rates, Globals.ReadingRateFromSlider());
        if (sliderRate != null && i >= 0) sliderRate.SetValueWithoutNotify(i);
        DisplayReadingSpeedDescr();
    }

    public void RateThisApp()
    {
        PRUtils.RateUs();
    }
    
    public void HandleRateValueChange(Slider slider)
    {
        string rate = "0";
        switch (slider.value)
        {
            case 0:
                rate = "-30";
                break;
            case 1:
                rate = "-20";
                break;
            case 2:
                rate = "-10";
                break;
            case 3:
                rate = "0";
                break;
            case 4:
                rate = "10";
                break;
            default:
                rate = "0";
                break;
        }

        PlayerPrefs.SetString("g_Rate", rate);

        DisplayReadingSpeedDescr();
    }
    
    // Moves a control of this screen to another vertical band (anchors), keeping its width.
    private static void Place(string name, float bottom, float top)
    {
        var rt = GameObject.Find(name)?.transform as RectTransform;
        if (rt == null) return;
        rt.anchorMin = new Vector2(rt.anchorMin.x, bottom);
        rt.anchorMax = new Vector2(rt.anchorMax.x, top);
    }

    public void HandleSetReadingSpeedByBooksAgeGroupChange(Toggle toggle)
    {
        //g_bSetReadingSpeedByBooksAgeGroup = PlayerPrefs.GetInt("g_bSetReadingSpeedByBooksAgeGroup", 1);
        if (toggle.isOn)
        {
            PlayerPrefs.SetInt("g_bSetReadingSpeedByBooksAgeGroup", 1);
            // Preferences.GetInstance().g_bSetReadingSpeedByBooksAgeGroup = 1;
            sliderRate.interactable = false;
        }
        else
        {
            PlayerPrefs.SetInt("g_bSetReadingSpeedByBooksAgeGroup", 0);
            // Preferences.GetInstance().g_bSetReadingSpeedByBooksAgeGroup = 0;
            sliderRate.interactable = true;
        }

        DisplayReadingSpeedDescr();
    }

    public void DisplayReadingSpeedDescr()
    {
        string rate = Globals.ReadingRateFromSlider(); // -30, -20, -10, 0, 10
        if (txtReadingSpeedDescr != null)
        {
            string descr = "";
            switch (rate)
            {
                case "-30":
                    descr = "Beginner";
                    break;
                case "-20":
                    descr = "Intermediate";
                    break;
                case "-10":
                    descr = "Proficient";
                    break;
                case "0":
                    descr = "Advanced";
                    break;
                case "10":
                    descr = "Expert";
                    break;
            }
            
            if (toggleSetReadingSpeedByBooksAgeGroup != null && toggleSetReadingSpeedByBooksAgeGroup.isOn)
                txtReadingSpeedDescr.text = "(Set by book age group)";
            else
                txtReadingSpeedDescr.text = descr;
        }
    }


}
