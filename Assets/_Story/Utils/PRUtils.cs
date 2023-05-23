using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using UnityEngine.UI;
using System.Collections.Specialized;

public class PRUtils
{
    public static int maxCacheImagesSize = 30;
    private static  OrderedDictionary cacheImages = new OrderedDictionary();

    static Dictionary<string, Color> pastelColors = new Dictionary<string, Color>
    {
        {"Pastel Pink", new Color(1, 0.7137f, 0.7569f, 1)},
        {"Pastel Blue", new Color(0.6824f, 0.7765f, 0.8118f, 1)},
        {"Pastel Green", new Color(0.5961f, 0.9843f, 0.5961f, 1)},
        {"Pastel Yellow", new Color(0.9922f, 0.9647f, 0.8902f, 1)},
        {"Pastel Orange", new Color(1, 0.7059f, 0.5098f, 1)},
        {"Pastel Purple", new Color(0.8392f, 0.7216f, 0.8549f, 1)},
        {"Pastel Mint", new Color(0.6784f, 1, 0.8039f, 1)},
        {"Pastel Lavender", new Color(0.9019f, 0.7451f, 1, 1)}
    };
    static List<Color> pastelColorList = new List<Color>(pastelColors.Values);
    
    public static string RemoveFileNameFromUrl(string url)
    {
        Uri uri = new Uri(url);
        string[] pathSegments = uri.AbsolutePath.Split('/');
        Array.Resize(ref pathSegments, pathSegments.Length - 1);
        string newPath = string.Join("/", pathSegments);
        return uri.GetLeftPart(UriPartial.Authority) + newPath + "/";
    }
    
    public static IEnumerator DownloadFile(string url, System.Action<string> onComplete)
    {
        UnityWebRequest request = UnityWebRequest.Get(url);
        request.certificateHandler = new AcceptAllCertificatesHandler(); // Add this line

        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.ConnectionError || request.result == UnityWebRequest.Result.ProtocolError)
        {
            Debug.LogError($"Error: {request.error}");
        }
        else
        {
            onComplete?.Invoke(request.downloadHandler.text);
        }
    }
    
    public static List<string> SplitStringIntoLines(string input)
    {
        string[] splitArray = input.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        return new List<string>(splitArray);
    }

    public static AudioClip MakeSubclip(AudioClip clip, float start, float stop)
    {
        /* Create a new audio clip */
        int frequency = clip.frequency;
        float timeLength = stop - start;
        int samplesLength = (int)(frequency * timeLength);
        AudioClip newClip = AudioClip.Create(clip.name + "-sub", samplesLength, 1, frequency, false);
        /* Create a temporary buffer for the samples */
        float[] data = new float[samplesLength];
        /* Get the data from the original clip */
        clip.GetData(data, (int)(frequency * start));
        /* Transfer the data to the new clip */
        newClip.SetData(data, 0);
        /* Return the sub clip */
        return newClip;
    }
    public static GameObject FindChildGameObjectByName(GameObject parentGameObject, string childName)
    {
        Transform childTransform = parentGameObject.transform.Find(childName);

        if (childTransform != null)
        {
            return childTransform.gameObject;
        }
        else
        {
            Debug.LogError($"Child GameObject '{childName}' not found.");
            return null;
        }
    }
    
    public static Sprite Texture2DToSprite(Texture2D texture)
    {
        Rect rect = new Rect(0, 0, texture.width, texture.height);
        Vector2 pivot = new Vector2(0.5f, 0.5f);
        return Sprite.Create(texture, rect, pivot);
    }

    public static IEnumerator DownloadImage(string url, Image image, bool bPreserveAspect = true )
    {
        image.preserveAspect = bPreserveAspect;
        if (cacheImages.Contains(url))
        {
            image.sprite = cacheImages[url] as Sprite;
            yield break;
        }

        UnityWebRequest request = UnityWebRequestTexture.GetTexture(url);
        request.certificateHandler = new AcceptAllCertificatesHandler();
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success) //
        {
            Debug.Log($"Failed to download image {url}: " + request.error);
            AlertDialogManager.Instance.ShowAlertDialog($"Failed to download image {url}: \n" + request.error);
            image.sprite = Resources.Load<Sprite>("NotFound");;
        }
        else
        {
            Texture2D texture = DownloadHandlerTexture.GetContent(request);
            Sprite imageSprite = Texture2DToSprite(texture);
            image.sprite = imageSprite;

            AddToCacheImages(url, imageSprite);
        }
    }
    
    private static void AddToCacheImages(string url, Sprite sprite)
    {
        if (cacheImages.Count >= maxCacheImagesSize)
        {
            cacheImages.RemoveAt(0);
        }
        cacheImages[url] = sprite;
    }

    public static Color GetOppositeColor(Color color)
    {
        float hue, saturation, value;
        Color.RGBToHSV(color, out hue, out saturation, out value);

        hue += 0.5f; // Add 180 degrees (0.5 in normalized 0-1 range) to get the opposite hue

        if (hue > 1f)
            hue -= 1f;

        return Color.HSVToRGB(hue, saturation, value);
    }
    public static Color textToColor(string text)
    {
        if (pastelColors.ContainsKey(text))
        {
            return pastelColors[text];
        }
        else
        {
            return MapStringToPastelColor(text);
        }
    }

    public static Color MapStringToPastelColor(string input)
    {
        int hash = input.GetHashCode();
        int index = Mathf.Abs(hash) % pastelColorList.Count;
        return pastelColorList[index];
    }
    
    public static Color DarkenColorByPercentage(Color color, float percentage)
    {
        float factor = 1 - Mathf.Clamp01(percentage);
        float r = color.r * factor;
        float g = color.g * factor;
        float b = color.b * factor;
        return new Color(r, g, b, color.a);
    }
}


