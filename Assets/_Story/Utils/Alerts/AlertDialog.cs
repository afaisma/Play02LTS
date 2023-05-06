using UnityEngine;
using TMPro;

public class AlertDialog : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI messageText;

    public void Show(string message)
    {
        messageText.text = message;
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
}