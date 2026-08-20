using System;
using System.Collections;
using System.Collections.Generic;
using SimpleJSON;
using UnityEngine;
using UnityEngine.Networking;

// ============================================================================================
// Home "doors" configuration — the illustrated room cards on the Home screen.
//
// The door set is CONTENT, not code: it lives in home_doors.json next to the catalog on the
// CDN, so the rooms a child sees (art, order, colour, which genre earns a slot) can change
// without an APK. Load order, cheapest first:
//
//   1) DiskCache copy   — painted synchronously on the very first frame (no flash, works offline)
//   2) fresh download   — Globals.baseURL + "home_doors.json", same UnityWebRequest / timeout /
//                         DiskCache write-through pattern as the catalog (Globals.DownloadCSV);
//                         re-delivers only when the bytes actually changed
//   3) compiled-in list — HomeController's own SectionTile array (the CURRENT room set, scene
//                         values included), so a missing/corrupt JSON degrades to exactly the
//                         screen we shipped, never to an empty hub.
//
// Every door failure mode is soft: a bad JSON, a bad door entry, a missing image → the door (or
// the whole set) falls back rather than throwing.
// ============================================================================================

/// <summary>One illustrated Home door. Field names match the JSON keys 1:1.</summary>
[Serializable]
public class HomeDoor
{
    public const string BadgeNone        = "none";
    public const string BadgeRotateDaily = "rotateDaily";

    public string id;          // stable key for analytics / debugging; not shown
    public string label;       // the caption under the art
    public string filter;      // library filter token ("fairytales") or Nav address ("library?filter=level1")
    public string imageUrl;    // absolute (http…) or catalog-relative ("ChuckTheChick/images/cover.jpg")
    public string accentHex;   // "#8FA67E"; empty -> the UiTheme card accent for this slot
    public string iconKey;     // Resources/Icons/Rooms key for the no-art fallback; empty -> derived from filter
    public bool   wide;        // spans both grid columns: art left, big label right (the hero card)
    public string badgePolicy = BadgeNone; // "none" | "rotateDaily"
    public int    minAge;      // 0 = unset. Door hides when [minAge,maxAge] misses the age chips.
    public int    maxAge;      // 0 = unset (open-ended)

    public bool RotatesBadgeDaily =>
        string.Equals(badgePolicy, BadgeRotateDaily, StringComparison.OrdinalIgnoreCase);

    /// <summary>Does this door show for the current age-chip selection? (0,0) = "All" shows every door.</summary>
    public bool MatchesAgeRange(int ageLoSel, int ageHiSel)
    {
        if (ageLoSel <= 0 || ageHiSel <= 0) return true;      // "All" chip — no age gate
        if (minAge <= 0 && maxAge <= 0) return true;          // door declares no range — always on
        int lo = minAge > 0 ? minAge : int.MinValue;          // one-sided ranges stay open-ended
        int hi = maxAge > 0 ? maxAge : int.MaxValue;
        return lo <= ageHiSel && ageLoSel <= hi;              // overlap, same test Filter.Conforms uses
    }

    /// <summary>Accent colour for the bottom bar; falls back to the palette accent for this slot.</summary>
    public Color Accent(int slot)
    {
        if (!string.IsNullOrEmpty(accentHex) &&
            ColorUtility.TryParseHtmlString(accentHex.StartsWith("#") ? accentHex : "#" + accentHex, out var c))
            return c;
        return UiTheme.Card(slot).accent;
    }
}

public static class HomeDoorsConfig
{
    public const string FileName = "home_doors.json";

    // Home is not startup-critical (the cached/compiled-in set is already on screen), so this can
    // afford to be shorter than the catalog's 20 s.
    private const int TimeoutSec = 10;

    /// <summary>Absolute URL of the door config: the catalog's own directory + home_doors.json.</summary>
    public static string Url =>
        string.IsNullOrEmpty(Globals.baseURL) ? "" : Globals.baseURL + FileName;

    /// <summary>
    /// Deliver the door set, best copy first. May invoke <paramref name="onDoors"/> twice: once
    /// synchronously with the DiskCache copy (before the first yield, so the caller can paint it
    /// on frame one), then again if the network returns different bytes. Falls back to
    /// <paramref name="fallback"/> when there is neither a cached nor a downloadable config.
    /// </summary>
    public static IEnumerator Load(List<HomeDoor> fallback, Action<List<HomeDoor>> onDoors)
    {
        string url = Url;
        bool delivered = false;

        string cached = string.IsNullOrEmpty(url) ? null : DiskCache.TryReadText(url, "catalog", ".json");
        if (!string.IsNullOrEmpty(cached))
        {
            var fromCache = Parse(cached);
            if (fromCache.Count > 0) { delivered = true; onDoors(fromCache); }
        }

        if (string.IsNullOrEmpty(url) || !url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            if (!delivered) onDoors(fallback);
            yield break;
        }

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.timeout = TimeoutSec;
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.ConnectionError ||
                request.result == UnityWebRequest.Result.ProtocolError)
            {
                // Offline, or no home_doors.json published yet: the cached copy (if any) already
                // painted; otherwise the compiled-in room set stands in.
                Debug.Log("HomeDoorsConfig: " + url + " unavailable (" + request.error +
                          "); using " + (delivered ? "cached doors" : "compiled-in doors"));
                if (!delivered) onDoors(fallback);
                yield break;
            }

            string text = request.downloadHandler.text;
            var fresh = Parse(text);
            if (fresh.Count == 0)
            {
                Debug.LogWarning("HomeDoorsConfig: " + url + " parsed to zero doors; keeping current set.");
                if (!delivered) onDoors(fallback);
                yield break;
            }

            DiskCache.WriteText(url, "catalog", ".json", text, DiskCache.CatalogBudgetBytes);
            if (!delivered || text != cached) onDoors(fresh);   // unchanged bytes → no needless rebuild
        }
    }

    /// <summary>
    /// Parse the door config. Accepts either {"doors":[…]} or a bare […] array. Malformed doors are
    /// skipped individually (mirroring Globals.ParseJSON's per-book isolation) so one bad entry can
    /// never blank the hub. Returns an empty list on a total parse failure — callers fall back.
    /// </summary>
    public static List<HomeDoor> Parse(string json)
    {
        var doors = new List<HomeDoor>();
        if (string.IsNullOrEmpty(json)) return doors;

        JSONNode root;
        try { root = JSON.Parse(json); }
        catch (Exception e) { Debug.LogWarning("HomeDoorsConfig.Parse: " + e.Message); return doors; }
        if (root == null) return doors;

        JSONNode list = root.IsArray ? root : root["doors"];
        if (list == null || !list.IsArray) return doors;

        for (int i = 0; i < list.Count; i++)
        {
            try
            {
                JSONNode d = list[i];
                var door = new HomeDoor
                {
                    id        = d["id"].Value,
                    label     = d["label"].Value,
                    filter    = d["filter"].Value,
                    imageUrl  = d["image_url"].Value,
                    accentHex = d["accent"].Value,
                    iconKey   = d["icon_key"].Value,
                    wide      = d["wide"].AsBool,
                    minAge    = d["min_age"].AsInt,
                    maxAge    = d["max_age"].AsInt,
                };
                string policy = d["badge_policy"].Value;
                door.badgePolicy = string.IsNullOrEmpty(policy) ? HomeDoor.BadgeNone : policy;
                if (string.IsNullOrEmpty(door.label) || string.IsNullOrEmpty(door.filter))
                {
                    Debug.LogWarning("HomeDoorsConfig.Parse: door " + i + " has no label/filter; skipped.");
                    continue;
                }
                if (string.IsNullOrEmpty(door.id)) door.id = door.filter;
                doors.Add(door);
            }
            catch (Exception e)
            {
                Debug.LogWarning("HomeDoorsConfig.Parse: door " + i + " skipped (" + e.Message + ")");
            }
        }
        return doors;
    }

    /// <summary>
    /// The compiled-in fallback: the CURRENT room set, converted door-for-door from HomeController's
    /// serialized SectionTile array (so Inspector edits in _Home keep driving the offline screen).
    /// No art and no badges — these doors render in the pre-redesign glyph look, which is exactly
    /// the "never regress" floor we want when home_doors.json is missing.
    /// </summary>
    public static List<HomeDoor> FromSectionTiles(IEnumerable<HomeController.SectionTile> tiles)
    {
        var doors = new List<HomeDoor>();
        if (tiles == null) return doors;
        foreach (var t in tiles)
        {
            if (string.IsNullOrEmpty(t.filter)) continue;
            doors.Add(new HomeDoor
            {
                id      = t.filter,
                label   = t.label,
                filter  = t.filter,
                iconKey = t.iconKey,
                // Learn to Read keeps the full-width hero slot it has today.
                wide   = t.filter.Trim().ToLowerInvariant() == "learn to read",
                badgePolicy = HomeDoor.BadgeNone,
            });
        }
        return doors;
    }
}
