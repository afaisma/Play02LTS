using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One product built from this Unity project (ReadingBuddy, and later others). Everything that
/// differs between products and is not downloaded content lives here, so product names, store
/// numbers and server addresses are not written into the code.
///
/// Every edition is a small JSON file in <c>Resources/Editions/</c> and ships in every build.
/// The running app picks the edition whose <see cref="appIdentifiers"/> contains its own
/// <c>Application.identifier</c>, so a build cannot carry one product's identifier and another
/// product's name or books. No match (a new platform, a renamed identifier) falls back to the
/// <see cref="DefaultId"/> edition.
/// </summary>
[Serializable]
public class Edition
{
    public const string DefaultId = "readingbuddy";
    public const string ResourcesFolder = "Editions";

    public string id = "";
    /// <summary>The product name shown on screen.</summary>
    public string appName = "";
    /// <summary>iOS bundle id, Android package and desktop identifier of this product.</summary>
    public string[] appIdentifiers = new string[0];
    /// <summary>Apple's numeric id of the store page ("Rate the app" on old iOS versions).</summary>
    public string appStoreId = "";
    /// <summary>Address of the catalog (stories.json). The books and home_doors.json sit beside it.</summary>
    public string catalogUrl = "";

    private static Edition _current;

    /// <summary>The edition this app is. Never null.</summary>
    public static Edition Current => _current ??= Load();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCurrent() => _current = null;

    /// <summary>Parse one edition file. Returns null when the text is not a usable edition.</summary>
    public static Edition Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            Edition e = JsonUtility.FromJson<Edition>(json);
            if (e == null || string.IsNullOrEmpty(e.id)) return null;
            if (e.appIdentifiers == null) e.appIdentifiers = new string[0];
            return e;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    /// <summary>
    /// The edition for an app identifier: the one that lists it (letter case ignored), else the
    /// <see cref="DefaultId"/> edition, else null.
    /// </summary>
    public static Edition Pick(IEnumerable<Edition> editions, string appIdentifier)
    {
        Edition fallback = null;
        foreach (Edition e in editions)
        {
            if (e == null) continue;
            foreach (string identifier in e.appIdentifiers)
            {
                if (string.Equals(identifier, appIdentifier, StringComparison.OrdinalIgnoreCase))
                    return e;
            }
            if (e.id == DefaultId) fallback = e;
        }
        return fallback;
    }

    private static Edition Load()
    {
        var editions = new List<Edition>();
        foreach (TextAsset file in Resources.LoadAll<TextAsset>(ResourcesFolder))
        {
            Edition e = Parse(file.text);
            if (e == null) Debug.LogError($"Edition: Resources/{ResourcesFolder}/{file.name} is not a valid edition file.");
            else editions.Add(e);
        }

        Edition picked = Pick(editions, Application.identifier);
        if (picked == null)
        {
            // Cannot happen while EditionTests pass (the default edition file exists and is valid).
            Debug.LogError($"Edition: no edition for \"{Application.identifier}\" and no \"{DefaultId}\" edition.");
            picked = new Edition { id = DefaultId, appName = Application.productName };
        }
        Debug.Log($"Edition: {picked.id} ({Application.identifier})");
        return picked;
    }
}
