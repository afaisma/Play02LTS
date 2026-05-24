using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class StartScene : MonoBehaviour
{
    public string startSceneName = "_Map";
    // Start is called before the first frame update
    void Start()
    {
        // string sName = PlayerPrefs.GetString("startSceneName", startSceneName);
        // if (sName != "")
        //     startSceneName = sName;        
        Navigation.GoToScene(startSceneName);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
