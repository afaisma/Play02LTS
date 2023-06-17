using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;


public class Globals : MonoBehaviour
{
    public static string g_scriptName;
    public static string g_Rate = "10";
    public static int g_bSetReadingSpeedByBooksAgeGroup;
    public Slider sliderRate;
    public Toggle toggleSetReadingSpeedByBooksAgeGroup;
    public TMP_Text txtReadingSpeedDescr;
    public TMP_Text versionText;
    
    
    Dictionary<string, string> mapImages = new Dictionary<string, string>();


    void Start()
    {
        g_Rate = PlayerPrefs.GetString("g_Rate");
        g_bSetReadingSpeedByBooksAgeGroup = PlayerPrefs.GetInt("g_bSetReadingSpeedByBooksAgeGroup");

        if (versionText != null)
            versionText.text = "Version: " + Application.version;
        
        mapImages.Add("Book1", "Assets/_Story/Scripts/Book1.cs");
        mapImages.Add("Book2", "Assets/_Story/Scripts/Book2.cs");
        mapImages.Add("Book3", "Assets/_Story/Scripts/Book3.cs");

        if (sliderRate != null)
        {
            switch (g_Rate)
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
        toggleSetReadingSpeedByBooksAgeGroup.isOn = g_bSetReadingSpeedByBooksAgeGroup == 1;
        DisplayReadingSpeedDescr();

    }
    /*
     Beginner: Leisurely
    Intermediate: Steady
    Proficient: Quick
    Advanced: Rapid
    Expert: Lightning-fast
     */
    
    public void DisplayReadingSpeedDescr()
    {
        if (txtReadingSpeedDescr != null)
        {
            string descr = "";
            switch (g_Rate)
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
            txtReadingSpeedDescr.text = descr;
        }
    }

    public void HandleRateValueChange(Slider slider)
    {
        switch (slider.value)
        {
            case 0:
                g_Rate = "-30";
                break;
            case 1:
                g_Rate = "-20";
                break;
            case 2:
                g_Rate = "-10";
                break;
            case 3:
                g_Rate = "0";
                break;
            case 4:
                g_Rate = "10";
                break;
            default:
                g_Rate = "0";
                break;
        }
        PlayerPrefs.SetString("g_Rate", g_Rate);

        DisplayReadingSpeedDescr();
    }

    public void HandleetReadingSpeedByBooksAgeGroupChange(Toggle toggle)
    {
        if (toggle.isOn)
            g_bSetReadingSpeedByBooksAgeGroup = 1;
        else
            g_bSetReadingSpeedByBooksAgeGroup = 0;
        PlayerPrefs.SetInt("g_bSetReadingSpeedByBooksAgeGroup", g_bSetReadingSpeedByBooksAgeGroup);
    }


    public void Library()
    {
        SceneManager.LoadScene("_Library");
    }

    public static bool IsTablet()
    {
        // Calculate the screen's diagonal size in inches
        float screenDiagonal = Mathf.Sqrt(Mathf.Pow(Screen.width / Screen.dpi, 2) + Mathf.Pow(Screen.height / Screen.dpi, 2));

        // If the screen size is 6.5 inches or larger, consider it a tablet
        return screenDiagonal >= 6.5f;
    }

}