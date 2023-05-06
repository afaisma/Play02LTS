using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;


public class Globals : MonoBehaviour
{
    public static string g_scriptName;

    Dictionary<string, string> mapImages = new Dictionary<string, string>();


    void Start()
    {
        mapImages.Add("Book1", "Assets/_Story/Scripts/Book1.cs");
        mapImages.Add("Book2", "Assets/_Story/Scripts/Book2.cs");
        mapImages.Add("Book3", "Assets/_Story/Scripts/Book3.cs");
    }
}