using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

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
        if (PRLibrary.prbooks == null)
            return;
        
        List<PRBook> books = PRLibrary.FilterById(linkID);
        if (books.Count == 0)
            return;
        PRBook prBook = books[0];
        Globals.g_scriptName = prBook.bookFullUrl;
        Globals.g_prbook = prBook;
        if (Globals.IsTablet())
        {
            //SceneManager.LoadScene("_StoryTablet");
            SceneManager.LoadScene("_Story");
        }
        else
        {
            SceneManager.LoadScene("_Story");
        }        
    }
}
