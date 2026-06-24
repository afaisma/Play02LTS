using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using UnityEngine.UI;
using System.Collections.Specialized;


public static class GameObjectExtensions
{
    public static T AddComponentOrReturnExisting<T>(this GameObject obj) where T : Component
    {
        // Check if the component already exists on the GameObject.
        T component = obj.GetComponent<T>();

        // If it doesn't exist, add and return it.
        if (component == null)
        {
            component = obj.AddComponent<T>();
        }

        // Return the existing or newly added component.
        return component;
    }
}
public class PRUtils
{
    // Cap chosen above the library's typical working set (~60+ book covers
    // visible across a session). Below that, every cover beyond the first
    // 30 evicts a Sprite that a BookViewItem.Image is still displaying —
    // not currently harmful (we don't Destroy on eviction) but it forces
    // re-download from DiskCache on every library re-entry. At 100 entries
    // × ~150 KB GPU per sprite, peak is ~15 MB, well within tablet/phone
    // budgets.
    public static int maxCacheImagesSize = 100;
    private static  OrderedDictionary cacheImages = new OrderedDictionary();

    public static float alpha = 0.35f;
    static public Dictionary<string, Color> pastelColors = new Dictionary<string, Color>
    {
        {"Pastel Pink", new Color(1, 0.7137f, 0.7569f, alpha)},
        {"Pastel Blue", new Color(0.6824f, 0.7765f, 0.8118f, alpha)},
        {"Pastel Green", new Color(0.5961f, 0.9843f, 0.5961f, alpha)},
        {"Pastel Yellow", new Color(0.9922f, 0.9647f, 0.8902f, alpha)},
        {"Pastel Orange", new Color(1, 0.7059f, 0.5098f, alpha)},
        {"Pastel Purple", new Color(0.8392f, 0.7216f, 0.8549f, alpha)},
        {"Pastel Mint", new Color(0.6784f, 1, 0.8039f, alpha)},
        {"Pastel Lavender", new Color(0.9019f, 0.7451f, 0.9412f, alpha)}
    };
    static List<Color> pastelColorList = new List<Color>(pastelColors.Values);
    static string[] unitsMap = { "Zero", "One", "Two", "Three", "Four", "Five", "Six", "Seven", "Eight", "Nine", "Ten", "Eleven", "Twelve", "Thirteen", "Fourteen", "Fifteen", "Sixteen", "Seventeen", "Eighteen", "Nineteen" };
    static string[] tensMap = { "Zero", "Ten", "Twenty", "Thirty", "Forty", "Fifty", "Sixty", "Seventy", "Eighty", "Ninety" };

    public static Color StringToColor(string rgba)
    {
        if (rgba.Contains(","))
            return StringToColor1(rgba);
        Color color;
        // Add hashtag for HTML-style color
        string htmlColor = "#" + rgba;

        if (!ColorUtility.TryParseHtmlString(htmlColor, out color))
        {
            Debug.Log("Invalid color string: " + rgba);
        }

        return color;
    }
    
    // string colorString = "255,0,0"; // Bright red
    public static Color StringToColor1(string rgb)
    {
        try
        {
            // Split the string into the components
            string[] parts = rgb.Split(',');

            // If the format is not correct, return white as a default color
            if (parts.Length != 3)
            {
                Debug.Log("Invalid format! The string should be in the format \"R,G,B\".");
                return Color.white;
            }

            // Parse each part, and divide by 255 to get a value between 0 and 1
            float r = int.Parse(parts[0]) / 255f;
            float g = int.Parse(parts[1]) / 255f;
            float b = int.Parse(parts[2]) / 255f;

            // Create and return the color
            return new Color(r, g, b);
        }
        catch (Exception e) 
        {
            Debug.Log("Error parsing color string: " + e.Message);
            return Color.white;
        }
        
    }
    
    public static Color GetNthPastelColor(int nColor)
    {
        // Convert the Dictionary to a List.
        List<Color> colorList = new List<Color>(pastelColors.Values);
        
        int n = nColor % colorList.Count;

        // Check if n is within the bounds of the list.
        if(n >= 0 && n < colorList.Count)
        {
            // Return the nth color.
            return colorList[n];
        }
        else
        {
            throw new IndexOutOfRangeException("Index is out of range of the colors dictionary");
        }
    }
    
    public static string RemoveFileNameFromUrl(string url)
    {
        try
        {
            Uri uri = new Uri(url);
            string[] pathSegments = uri.AbsolutePath.Split('/');
            Array.Resize(ref pathSegments, pathSegments.Length - 1);
            string newPath = string.Join("/", pathSegments);
            return uri.GetLeftPart(UriPartial.Authority) + newPath + "/";
        }
        catch (Exception e)
        {
            return url;
        }
    }
    
    public static IEnumerator DownloadFile(string url, System.Action<string> onComplete)
    {
        // H1: accept any URL with a scheme (http://, https://, file://, ...).
        // The `resources:` pseudo-scheme has no `://`, so it correctly falls through to the local-resource branch.
        if (!url.Contains("://"))
        {
            // Load from Resources
            string resourcePath = url.Replace("resources:", "").TrimStart('/'); // Removing the "resources:" prefix and any starting slashes
            TextAsset asset = Resources.Load<TextAsset>(resourcePath);
            if (asset != null)
            {
                onComplete?.Invoke(asset.text);
            }
            else
            {
                Debug.Log($"Error: Could not find local resource at {resourcePath}");
            }
            yield break; // Ends the coroutine here for local resources.
        }

        // H3: dispose UnityWebRequest (native download handler + buffers) when done.
        // C1: do NOT install AcceptAllCertificatesHandler — TLS verification stays on by default.
        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.timeout = 20;  // small text/JSON: fail fast if the server stalls
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.ConnectionError || request.result == UnityWebRequest.Result.ProtocolError)
            {
                // Offline / server unreachable: fall back to a disk-cached copy so a
                // previously-opened book still loads its script with no connection.
                string cached = DiskCache.TryReadText(url, "scripts", ".txt");
                if (cached != null)
                {
                    Debug.Log($"DownloadFile: served script from cache (offline) — {url}");
                    onComplete?.Invoke(cached);
                }
                else
                {
                    Debug.LogError($"DownloadFile failed: {request.error}  (url={url})");
                }
            }
            else
            {
                string text = request.downloadHandler.text;
                // Persist for offline re-open. Network-first keeps it fresh while online;
                // the cache is only consulted when the network fails (above).
                DiskCache.WriteText(url, "scripts", ".txt", text, DiskCache.ScriptBudgetBytes);
                onComplete?.Invoke(text);
            }
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
            Debug.Log($"Child GameObject '{childName}' not found.");
            return null;
        }
    }
    
    public static Sprite Texture2DToSprite(Texture2D texture)
    {
        Rect rect = new Rect(0, 0, texture.width, texture.height);
        Vector2 pivot = new Vector2(0.5f, 0.5f);
        return Sprite.Create(texture, rect, pivot);
    }

    public static IEnumerator DownloadImage(string url, Image image, bool bPreserveAspect = true, bool suppressAlert = false)
    {
        // Cache-bust story media by the current book's content_rev so a re-rendered book
        // serves fresh images. No-op when no book is open (e.g. Library, where g_prbook is
        // null and covers are busted at the call site instead). Idempotent → safe if the
        // url is already busted. LoadImageSprite applies the SAME transform; we apply it
        // here too so the alert below reports the busted url, exactly as before.
        url = Globals.WithContentRev(url, Globals.g_prbook != null ? Globals.g_prbook.contentRev : "");
        image.preserveAspect = bPreserveAspect;

        Sprite sprite = null;
        string error = null;
        yield return LoadImageSprite(url, (s, e) => { sprite = s; error = e; });

        // The caller's Image may have been destroyed during the
        // network wait (typical: user taps a book and the library
        // unloads while its cover-grid coroutines are still in flight).
        // Don't assign to a dead Unity object — Unity's overloaded ==
        // returns true for destroyed components, so this is safe.
        if (image == null) yield break;

        if (sprite != null)
        {
            image.sprite = sprite;
        }
        else
        {
            // Asset-level failure: page falls back to NoImage placeholder.
            // suppressAlert is for high-volume callers (e.g. the library
            // cover-grid load) where one failed thumbnail shouldn't pop a
            // modal dialog. The NoImage fallback below is sufficient
            // user-visible feedback in those cases.
            if (!suppressAlert)
                AlertDialogManager.Instance.ShowAlertDialog($"Failed to download image {url}: \n" + error);
            image.sprite = Resources.Load<Sprite>("NoImage");;
        }
    }

    // Shared cache-miss/hit body for image loading, with NO UI target. Applies the
    // content_rev cache-bust, then resolves the Sprite through the in-memory cache, the
    // disk cache, and finally the network — warming both caches as a side effect.
    // DownloadImage and PrefetchImage both route through here so they can never compute a
    // different cache key for the same url. Reports the Sprite (null on failure) and, on a
    // network failure, the error text via onResult.
    private static IEnumerator LoadImageSprite(string url, System.Action<Sprite, string> onResult)
    {
        url = Globals.WithContentRev(url, Globals.g_prbook != null ? Globals.g_prbook.contentRev : "");

        // 1) In-memory cache.
        if (cacheImages.Contains(url))
        {
            Sprite cached = cacheImages[url] as Sprite;
            // C3: move to most-recently-used position so frequent items survive eviction.
            cacheImages.Remove(url);
            cacheImages[url] = cached;
            onResult(cached, null);
            yield break;
        }

        // 2) Disk cache. Persists across sessions, so re-opening a book
        //    works offline once any page has been loaded once.
        byte[] diskBytes = DiskCache.TryReadBytes(url, "images", ".png");
        if (diskBytes != null)
        {
            var tex = new Texture2D(2, 2);
            // LoadImage auto-detects PNG/JPG; resizes the texture to fit.
            if (tex.LoadImage(diskBytes))
            {
                Sprite spr = Texture2DToSprite(tex);
                AddToCacheImages(url, spr);
                onResult(spr, null);
                yield break;
            }
            // If the on-disk file is corrupt, fall through to network.
            UnityEngine.Object.Destroy(tex);
        }

        // 3) Network. H3: dispose UnityWebRequest when done.
        // C1: do NOT install AcceptAllCertificatesHandler — TLS verification stays on.
        using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(url))
        {
            request.timeout = 30;  // images: tolerate slow connections for ~5 MB
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success) //
            {
                Debug.LogWarning($"PRUtils: image download failed — {request.error}  (url={url})");
                onResult(null, request.error);
            }
            else
            {
                Texture2D texture = DownloadHandlerTexture.GetContent(request);
                Sprite imageSprite = Texture2DToSprite(texture);
                AddToCacheImages(url, imageSprite);
                // Persist the encoded bytes (PNG/JPG) for the next session.
                DiskCache.WriteBytes(url, "images", ".png",
                    request.downloadHandler.data, DiskCache.ImageBudgetBytes);
                onResult(imageSprite, null);
            }
        }
    }

    // Warm the image caches for a url with NO UI target — used to prefetch the next page's
    // images so a page turn doesn't wait on a cold download. Routes through the same
    // LoadImageSprite body (and applies the same content_rev cache-bust first), so a
    // successful prefetch is a guaranteed cache hit when the page later calls DownloadImage.
    // Completely silent: it assigns the result to nothing, never alerts, and never throws;
    // a network failure logs at most one line inside LoadImageSprite.
    public static IEnumerator PrefetchImage(string url)
    {
        if (string.IsNullOrEmpty(url))
            yield break;

        // Same transform DownloadImage applies first, so this early-out tests the real key.
        url = Globals.WithContentRev(url, Globals.g_prbook != null ? Globals.g_prbook.contentRev : "");
        if (cacheImages.Contains(url))
            yield break;

        // Result is intentionally discarded — the point is the cache-warm side effect.
        yield return LoadImageSprite(url, (s, e) => { });
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
    
    public static void SetImageColor(Image image, int r, int g, int b, int a)
    {
        r = Mathf.Clamp(r, 0, 255);
        g = Mathf.Clamp(g, 0, 255);
        b = Mathf.Clamp(b, 0, 255);
        a = Mathf.Clamp(a, 0, 255);

        image.color = new Color(r / 255f, g / 255f, b / 255f, a / 255f);
    }

    public static void SetImageAlpha( Image image, int a)
    {
        a = Mathf.Clamp(a, 0, 255);
        Color newColor = image.color;
        newColor.a = a / 255f;
        image.color = newColor;
    }
    public static string UrlUp(string url, int nSteps)
    {
        for (int i = 0; i < nSteps; i++)
        {
            int lastSlashPos = url.LastIndexOf('/');
            // If there are no more slashes, we can't go up any further
            if (lastSlashPos == -1)
                return "";
            url = url.Substring(0, lastSlashPos);
        }

        return url;
    }
    
    public static void ResizeUIElementToParentMax(GameObject goToBeResized)
    {
        if (goToBeResized == null || goToBeResized.transform.parent == null) return;

        RectTransform parentRectTransform = goToBeResized.transform.parent.GetComponent<RectTransform>();
        RectTransform rectTransform = goToBeResized.GetComponent<RectTransform>();

        rectTransform.anchorMin = new Vector2(0, 0.07f);
        rectTransform.anchorMax = new Vector2(1, 1);
        rectTransform.offsetMin = new Vector2(0, 0);
        rectTransform.offsetMax = new Vector2(0, 0);

        //rectTransform.anchoredPosition = new Vector2(parentRectTransform.rect.width / 2, parentRectTransform.rect.height / 2);
        // rectTransform.sizeDelta = new Vector2(parentRectTransform.rect.width, parentRectTransform.rect.height);
    }
    
    public static string Convert(int number)
    {
        if (number == 0) return unitsMap[0];
        if (number < 20) return unitsMap[number];
        if (number < 100) return tensMap[number / 10] + ((number % 10 > 0) ? " " + Convert(number % 10) : "");
        if (number < 1000) return unitsMap[number / 100] + " Hundred" + ((number % 100 > 0) ? " and " + Convert(number % 100) : "");
        return unitsMap[number / 1000] + " Thousand" + ((number % 1000 > 0) ? " " + Convert(number % 1000) : "");
    }
    
    public static bool AlmostEqual(float num1, float num2, float delta)
    {
        return Math.Abs(num1 - num2) <= delta;
    }
    
    public static void RateUs()
    {
#if UNITY_IOS
        string systemVersion = UnityEngine.iOS.Device.systemVersion;
        if (!string.IsNullOrEmpty(systemVersion))
        {
            float version = float.Parse(systemVersion.Split('.')[0] + "." +
                                        UnityEngine.iOS.Device.systemVersion.Split('.')[1]);
            if (version >= 10.3f)
            {
                UnityEngine.iOS.Device.RequestStoreReview();
            }
            else
            {
                // For iOS versions less than 10.3
                Application.OpenURL("itms-apps://itunes.apple.com/app/id6449234127");
            }
        }
#elif UNITY_ANDROID
        // For Google Play Store
        Application.OpenURL("market://details?id=" + Application.identifier);
#else
        // For Amazon Store or other platforms
        Application.OpenURL("amzn://apps/android?p=" + Application.identifier);
#endif
        
        PlayerPrefs.SetInt("g_wasRated", 1);
    }
    
    public static string CapitalizeFirstLetter(string input)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;

        return char.ToUpper(input[0]) + input.Substring(1);
    }

}


