using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Networking;

public class TextLoader : MonoBehaviour, IPointerClickHandler
{
    public string textUrl;
    public TMP_Text textMeshPro;
    public int maxAttempts = 3;

    private void Start()
    {
        StartCoroutine(LoadTextFromUrl());
    }

    private IEnumerator LoadTextFromUrl()
    {
        int attempts = 0;

        while (attempts < maxAttempts)
        {
            using (UnityWebRequest webRequest = UnityWebRequest.Get(textUrl))
            {
                yield return webRequest.SendWebRequest();

                if (webRequest.result == UnityWebRequest.Result.Success)
                {
                    string rtfText = webRequest.downloadHandler.text;
                    textMeshPro.richText = true;
                    textMeshPro.text = rtfText;
                    yield break; // Successful, exit the coroutine
                }
                else
                {
                    attempts++;
                    Debug.LogWarning("Attempt " + attempts + " failed. Retrying...");
                }
            }
        }

        Debug.LogError("Failed to load text from URL after " + maxAttempts + " attempts.");
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        int linkIndex = TMP_TextUtilities.FindIntersectingLink(textMeshPro, eventData.position, null);

        if (linkIndex != -1)
        {
            TMP_LinkInfo linkInfo = textMeshPro.textInfo.linkInfo[linkIndex];
            string linkID = linkInfo.GetLinkID();

            // Handle the link click based on the linkID
            HandleLinkClick(linkID);
        }
}

    private void HandleLinkClick(string linkID)
    {
        // Implement your custom logic here based on the clicked linkID
        Debug.Log("Link clicked: " + linkID);
    }
}
