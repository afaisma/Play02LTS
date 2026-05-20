using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using System.Collections.Generic;


public class TextLoader : MonoBehaviour, IPointerClickHandler
{
    Dictionary<string, string>  m_links = new Dictionary<string, string>
    {
        { "article_pretendplay", "https://doi.org/10.1080/21594937.2023.2235472" },
        { "imagiration_youtube", "https://www.youtube.com/channel/UCcdXD63BW9tm48j6Mn_j2vg" },
        { "imagiration_science", "http://imagiration.com/science" },
        { "imagiration_facebook", "https://www.facebook.com/ImagiRation" },
        { "bellcurveandme_web", "https://www.bellcurveandme.com" },
        { "book_language", "https://www.amazon.com/dp/B09D7KV7XT" },
        { "book_girl_and_sea", "https://www.amazon.com/dp/B0CFZ9S4L6" },
        { "book_timmy", "https://www.amazon.com/dp/1973153912" }
    };
    
    
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
                webRequest.timeout = 20;
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
        if (m_links.ContainsKey(linkID))
        {
            string linkUrl = m_links[linkID];
            Application.OpenURL(linkUrl);
        }
        
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
