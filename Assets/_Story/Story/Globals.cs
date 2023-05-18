using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;


public class Globals : MonoBehaviour
{
    public static string g_scriptName;
    public static string g_Rate = "10";
    public Slider sliderRate;

    Dictionary<string, string> mapImages = new Dictionary<string, string>();


    void Start()
    {
        mapImages.Add("Book1", "Assets/_Story/Scripts/Book1.cs");
        mapImages.Add("Book2", "Assets/_Story/Scripts/Book2.cs");
        mapImages.Add("Book3", "Assets/_Story/Scripts/Book3.cs");
        
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
        }
    }
    
    public void Library()
    {
        SceneManager.LoadScene("_Library");
    }

}