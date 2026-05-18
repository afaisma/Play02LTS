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
    void Start()
    {
        string rate = PlayerPrefs.GetString("g_Rate", "0"); // -30, -20, -10, 0, 10
        if (sliderRate != null)
        {
            switch (rate)
            {
                case "-30":
                    sliderRate.value = 0;
                    break;
                case "-20":
                    sliderRate.value = 1;
                    break;
                case "-10":
                    sliderRate.value = 2;
                    break;
                case "0":
                    sliderRate.value = 3;
                    break;
                case "10":
                    sliderRate.value = 4;
                    break;
            }
        }

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

        DisplayReadingSpeedDescr();

        if (versionText != null)
            versionText.text = "Version: " + Application.version;
    }

    // L-R3-2: removed an empty Update() that cost a per-frame managed call for no gain.

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
        string rate = PlayerPrefs.GetString("g_Rate", "0"); // -30, -20, -10, 0, 10
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
            
            if (toggleSetReadingSpeedByBooksAgeGroup != null)
                if (!toggleSetReadingSpeedByBooksAgeGroup.isOn)
                    txtReadingSpeedDescr.text = descr;
                else
                    txtReadingSpeedDescr.text = "(Set by book age group)";
        }
    }


}
