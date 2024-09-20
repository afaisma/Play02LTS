using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LoadingScreenManager : MonoBehaviour
{
    public Slider progressBar;  // Assign the Slider component here via the Inspector.

    // Static variable to hold the name of the next scene to load.
    public string nextScene;

    private void Start()
    {
        if (!string.IsNullOrEmpty(nextScene))
        {
            LoadScene(nextScene);
        }
        else
        {
            Debug.LogError("No scene specified for loading!");
        }
    }

    private void LoadScene(string sceneName)
    {
        StartCoroutine(LoadSceneAsync(sceneName));
    }

    private IEnumerator LoadSceneAsync(string sceneName)
    {
        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);

        while (!operation.isDone)
        {
            float progress = Mathf.Clamp01(operation.progress / 0.9f);
            progressBar.value = progress;

            yield return null;
        }
    }
}