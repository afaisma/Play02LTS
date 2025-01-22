using NUnit.Framework;
using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;

public class AlertDialog : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI messageText;

    public void Show(string message)
    {
        // !!!!!!!!!!
        //messageText.text = message;
        gameObject.SetActive(true);
    }

    public void CopyToClipboard()
    {
        GUIUtility.systemCopyBuffer = messageText.text;
    }

    public void Close()
    {
        Destroy(gameObject);
    }
    
    public void CloseTheApp()
    {
        Debug.Log("CloseTheApp");
#if UNITY_EDITOR
    UnityEditor.EditorApplication.isPlaying = false; // Stops play mode in Unity Editor
#else
    Application.Quit(); // Closes the app in the built version
#endif
    }

    public void RetryLibrary()
    {
        AlertDialogManager.DestroyDialogInstance();
        SceneManager.LoadScene("_Library");
    }
    
}