using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

public class Preferences
{
    private static Preferences instance;
    public string g_Rate = "10";
    public int g_bSetReadingSpeedByBooksAgeGroup;
    
    // Private constructor to prevent instantiation from outside the class
    private Preferences()
    {
        g_Rate = PlayerPrefs.GetString("g_Rate", "0"); // -30, -20, -10, 0, 10
        g_bSetReadingSpeedByBooksAgeGroup = PlayerPrefs.GetInt("g_bSetReadingSpeedByBooksAgeGroup", 1);
    }

    // Public static method to get the instance of the singleton class
    public static Preferences GetInstance()
    {
        if (instance == null)
        {
            instance = new Preferences();
        }
        
        return instance;
    }
}


public class Globals : MonoBehaviour
{
    public static string g_scriptName;
    public Slider sliderRate;
    public Toggle toggleSetReadingSpeedByBooksAgeGroup;
    public TMP_Text txtReadingSpeedDescr;
    public TMP_Text versionText;
    public static PRBook g_prbook;


    

    void Start()
    {

        if (versionText != null)
            versionText.text = "Version: " + Application.version;
        
        if (sliderRate != null)
        {
            switch (Preferences.GetInstance().g_Rate)
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
        if (toggleSetReadingSpeedByBooksAgeGroup != null) 
            toggleSetReadingSpeedByBooksAgeGroup.isOn = Preferences.GetInstance().g_bSetReadingSpeedByBooksAgeGroup == 1;
        DisplayReadingSpeedDescr();

    }
    
    public void DisplayReadingSpeedDescr()
    {
        if (txtReadingSpeedDescr != null)
        {
            string descr = "";
            switch (Preferences.GetInstance().g_Rate)
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
                Preferences.GetInstance().g_Rate = "-30";
                break;
            case 1:
                Preferences.GetInstance().g_Rate = "-20";
                break;
            case 2:
                Preferences.GetInstance().g_Rate = "-10";
                break;
            case 3:
                Preferences.GetInstance().g_Rate = "0";
                break;
            case 4:
                Preferences.GetInstance().g_Rate = "10";
                break;
            default:
                Preferences.GetInstance().g_Rate = "0";
                break;
        }
        PlayerPrefs.SetString("g_Rate", Preferences.GetInstance().g_Rate);

        DisplayReadingSpeedDescr();
    }

    public void HandleetReadingSpeedByBooksAgeGroupChange(Toggle toggle)
    {
        if (toggle.isOn)
            Preferences.GetInstance().g_bSetReadingSpeedByBooksAgeGroup = 1;
        else
            Preferences.GetInstance().g_bSetReadingSpeedByBooksAgeGroup = 0;
        PlayerPrefs.SetInt("g_bSetReadingSpeedByBooksAgeGroup", Preferences.GetInstance().g_bSetReadingSpeedByBooksAgeGroup);
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

    public static string ageGroupLabelFromPRBook(PRBook prBook)
    {
        // Book level - add book level 2-3 YOA, 3-5YOA, 4-7YOA, 5-10YOA
        string ageGroup = "Any Age";
        if (prBook.ageFrom == 2)
        {
            ageGroup = "2-4 years";
        }
        else if (prBook.ageFrom == 3)
        {
            ageGroup = "3-6 years";
        }
        else if (prBook.ageFrom == 4)
        {
            ageGroup = "4-8 years";
        }
        else if (prBook.ageFrom == 5)
        {
            ageGroup = "5-12 years";
        }
        return ageGroup;
    }
    
    public static int defaultAudioRateFromPRBook(PRBook prBook)
    {
        if (prBook == null)
            return 0;
        
        // Book level - add book level 2-3 YOA, 3-5YOA, 4-7YOA, 5-10YOA
        int rate = -30;
        if (prBook.ageFrom == 2)
        {
            rate = -20;
        }
        else if (prBook.ageFrom == 3)
        {
            rate = -10;
        }
        else if (prBook.ageFrom == 4)
        {
            rate = 0;
        }
        else if (prBook.ageFrom == 5)
        {
            rate = 10;
        }

        return rate;
    }

    public static string getReadingRate()
    {
        if (Preferences.GetInstance().g_bSetReadingSpeedByBooksAgeGroup == 1)
            return "" + defaultAudioRateFromPRBook(g_prbook);
        else
            return Preferences.GetInstance().g_Rate;
    }
}