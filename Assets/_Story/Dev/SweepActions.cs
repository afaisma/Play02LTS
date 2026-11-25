// Dev-only: the ReadingBuddy actions a scripted sweep can perform (tier-2 smoke, 2026-09-15).
// Used by the iOS Simulator driver (SimSmokeDriver.cs) and available to editor-side sweeps.
// Compiled ONLY in the editor and in READINGBUDDY_DEV builds (RbSimSmoke.ExportSimulator passes
// the define) — never in a store build. Everything is reached by REFLECTION so this file never
// breaks a build when the app changes; an unknown action throws and the sweep logs "ERR".
//
// Actions (plan step "frames|action|arg"):
//   prefint|key=n  prefstr|key=s  prefdel|key      PlayerPrefs (set first-run prefs so captures
//                                                  show pages, not onboarding)
//   continue         Welcome screen's Continue (Globals.Home) — the first-run Welcome waits for it
//   home | learn | settings | parents | bookstore   navigation
//   scene|<name>     load any scene by name (e.g. _Welcome after edition|story-comprehension,
//                    to see that edition's first-run Welcome)
//   library|<filter> Library shelf (everything, fairytales, learn to read, level2, new, ...)
//   book|<folder>    open the book whose bookUrl contains <folder> (e.g. FarmAnimalsRhymebook)
//   progress|<folder>=<n>  record n answers and the saved page n-1 for that book (a long progress
//                    line on Story Comprehension's Home tile)
//   bookfile|<url>   open a book that is NOT in the catalog by the address of its script
//                    (file:///.../stories/<Folder>/<script>.txt) — for test books on this Mac
//   catalogfile|<path>  use a catalog file on this Mac instead of the downloaded catalog
//   edition|<id>     behave as another edition from now on (e.g. story-comprehension)
//   level|<letter>   question level a..d (QuestionLevel.Current; the book lists are filtered again)
//   mode|<Mode>      reading-mode picker tile: Storyteller | AppVoice | IRead | Pictures
//                    (IRead is NOT supported in the Simulator — Recognissimo is stubbed there)
//   picker           toggle the reading-mode picker (open <-> closed)
//   click|<name>     press any active Button by GameObject name (e.g. ShowAllAges)
//   scroll|<pos>     every active scroll list to a vertical position (1 = top, 0 = bottom)
//   slider|<name>=<n>  move a Slider to a value as a finger would (its change handler runs)
// Notes for "note|<what>" (a value written into the log): narration (the newest narration file,
//   with its speed) | rate (the saved slider speed g_Rate, or "not saved") | page (the reading page's
//   page number as shown, and the page index the reader is on)
//   next | prev      page-turn arrows
//   swipe|left|right a finger swipe on the page text (left = next page, right = previous)
//   rate | ratelater the rate-app panel in / out
//   offline | online | dismissoffline   the no-internet dialog (drives NetworkStatus directly;
//                    the dialog comes after two offline polls, so send offline| twice)
//   stall | stallclear                  the read-along stall hint (arrow pulse + once-ever caption)
//   A dialogue is driven with click|Choice_<name>, click|TapToAnswer, click|Skip, click|Next;
//   prefint|dialogue_mic=0 switches its microphone off (Recognissimo is stubbed in the Simulator).
// Conditions for "wait|<what>": catalog | playing | idle | picker | dialogue | scene=<name>
//   | silences=<n> (the open dialogue has gone unanswered n times — the silence-timing check)
//   | countdown=<n> (the Welcome countdown bar is showing and has emptied to <= n percent)
//   | demo (the Welcome page's demo question is showing)
#if UNITY_EDITOR || READINGBUDDY_DEV
using System;
using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class SweepActions
{
    private const BindingFlags Any = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;

    private static Type T(string name)
    {
        var t = Type.GetType(name + ", Assembly-CSharp");
        if (t == null) throw new Exception("type not found: " + name);
        return t;
    }

    private static UnityEngine.Object Find(Type t, bool includeInactive = false)
    {
        var o = UnityEngine.Object.FindFirstObjectByType(t, includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude);
        if (o == null) throw new Exception("no instance of " + t.Name + " in scene " + SceneManager.GetActiveScene().name);
        return o;
    }

    private static void Call(object target, Type t, string method, params object[] args)
    {
        var m = t.GetMethod(method, Any, null, ArgTypes(args), null) ?? t.GetMethod(method, Any);
        if (m == null) throw new Exception(t.Name + "." + method + " not found");
        m.Invoke(m.IsStatic ? null : target, args);
    }

    private static Type[] ArgTypes(object[] args)
    {
        var ts = new Type[args.Length];
        for (int i = 0; i < args.Length; i++) ts[i] = args[i] == null ? typeof(object) : args[i].GetType();
        return ts;
    }

    private static void ClickByName(string goName)
    {
        var go = GameObject.Find(goName);
        if (go == null) throw new Exception("no GameObject '" + goName + "'");
        var b = go.GetComponent<Button>();
        if (b == null) throw new Exception("'" + goName + "' has no Button");
        b.onClick.Invoke();
    }

    // The book (of the shown list) whose bookUrl contains <part>.
    private static object FindBook(string part)
    {
        var list = T("Globals").GetField("g_listPRBooks", Any).GetValue(null) as IList;
        if (list == null || list.Count == 0) throw new Exception("catalog not loaded");
        foreach (var b in list)
        {
            var url = b.GetType().GetField("bookUrl", Any)?.GetValue(b) as string;
            if (url != null && url.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0) return b;
        }
        throw new Exception("no book matching '" + part + "'");
    }

    // ------------------------------------------------------------------ actions

    public static void Perform(string a, string arg)
    {
        switch (a)
        {
            case "prefint": { var kv = arg.Split('='); PlayerPrefs.SetInt(kv[0], int.Parse(kv[1])); PlayerPrefs.Save(); break; }
            case "prefstr": { int eq = arg.IndexOf('='); PlayerPrefs.SetString(arg.Substring(0, eq), arg.Substring(eq + 1)); PlayerPrefs.Save(); break; }
            case "prefdel": PlayerPrefs.DeleteKey(arg); PlayerPrefs.Save(); break;

            case "continue":
            {
                var gT = T("Globals");
                var inst = gT.GetProperty("Instance", Any)?.GetValue(null) ?? Find(gT);
                Call(inst, gT, "Home");
                break;
            }
            case "home":      Call(null, T("Navigation"), "GoToHome"); break;
            case "scene":     Call(null, T("Navigation"), "GoToScene", arg); break;
            case "learn":     Call(null, T("Navigation"), "GoToLearnToRead"); break;
            case "settings":  Call(null, T("Navigation"), "GoToSettings"); break;
            case "parents":   Call(null, T("Navigation"), "GoToParents"); break;
            case "bookstore": Call(null, T("Navigation"), "GoToBookstore"); break;
            case "library":   Call(null, T("Globals"), "GotoLibrary", string.IsNullOrEmpty(arg) ? "everything" : arg); break;

            case "book": T("Globals").GetMethod("GotoPrBook", Any).Invoke(null, new[] { FindBook(arg) }); break;

            case "progress":
            {
                // A tile with a long progress line: n answers (pages 1..n) and the saved page n-1, so
                // Home shows "★ n answers · page n" for the book whose bookUrl contains <folder>.
                var kv = arg.Split('=');
                object target = FindBook(kv[0]);
                int n = int.Parse(kv[1]);
                var add = T("DialogueResults").GetMethod("Add", Any);
                for (int p = 1; p <= n; p++) add.Invoke(null, new object[] { target, p, "x", true, "touch", 1 });
                string url = target.GetType().GetField("bookUrl", Any).GetValue(target) as string;
                PlayerPrefs.SetInt(url + "_page", n - 1); PlayerPrefs.Save();
                break;
            }

            case "edition":
            {
                // Dev builds only: behave as another edition (Resources/Editions/<id>.json) from now
                // on, e.g. story-comprehension. The book list is filtered again for it.
                PlayerPrefs.SetString("edition_override", arg); PlayerPrefs.Save();
                T("Edition").GetMethod("Reset", Any).Invoke(null, null);
                T("Globals").GetMethod("ApplyQuestionLevel", Any).Invoke(null, null);
                break;
            }

            case "level":
            {
                var qT = T("QuestionLevel");
                qT.GetProperty("Current", Any).SetValue(null, arg);
                break;
            }

            case "catalogfile":
            {
                // Replace the loaded catalog with a catalog file on this Mac (for example a dry-run
                // stories.test.json), then go to a screen again to see it.
                var gT = T("Globals");
                object books = gT.GetMethod("ParseJSON", Any).Invoke(null, new object[] { System.IO.File.ReadAllText(arg) });
                gT.GetField("g_allPRBooks", Any).SetValue(null, books);
                gT.GetMethod("ApplyQuestionLevel", Any).Invoke(null, null);
                break;
            }

            case "bookfile":
            {
                // A book outside the catalog: only the fields the reader needs. bookUrl (the
                // progress key) is "<Folder>/<script>", as the catalog would give it.
                var bT = T("PRBook");
                object book = Activator.CreateInstance(bT);
                string[] parts = arg.Split('/');
                if (parts.Length < 2) throw new Exception("bookfile needs the full address of the script");
                bT.GetField("bookFullUrl", Any).SetValue(book, arg);
                bT.GetField("bookUrl", Any).SetValue(book, parts[parts.Length - 2] + "/" + parts[parts.Length - 1]);
                bT.GetField("bookName", Any).SetValue(book, parts[parts.Length - 2]);
                bT.GetField("ageFrom", Any).SetValue(book, 5);
                bT.GetField("ageTo", Any).SetValue(book, 12);
                T("Globals").GetMethod("GotoPrBook", Any).Invoke(null, new[] { book });
                break;
            }

            case "mode":
            {
                var pT = T("UnifiedReadingModePicker");
                var picker = Find(pT);
                var modeT = pT.GetNestedType("Mode", Any);
                pT.GetMethod("OnTileSelected", Any).Invoke(picker, new[] { Enum.Parse(modeT, arg) });
                break;
            }
            case "picker": { var pT = T("UnifiedReadingModePicker"); Call(Find(pT), pT, "TogglePicker"); break; }

            case "click": ClickByName(arg); break;   // any active GameObject with a Button, by name
            case "slider":
            {
                var kv = arg.Split('=');
                var go = GameObject.Find(kv[0]);
                var sl = go != null ? go.GetComponent<Slider>() : null;
                if (sl == null) throw new Exception("no Slider '" + kv[0] + "'");
                sl.value = float.Parse(kv[1], System.Globalization.CultureInfo.InvariantCulture);
                break;
            }
            case "scroll":
            {
                // every active scroll list of the screen to a vertical position: 1 = top, 0 = bottom
                float pos = float.Parse(arg, System.Globalization.CultureInfo.InvariantCulture);
                int n = 0;
                foreach (var sr in UnityEngine.Object.FindObjectsByType<ScrollRect>(FindObjectsSortMode.None))
                    if (sr.isActiveAndEnabled) { sr.StopMovement(); sr.verticalNormalizedPosition = pos; n++; }
                if (n == 0) throw new Exception("no active ScrollRect");
                break;
            }
            case "next": ClickByName("btnNext"); break;
            case "swipe":
            {
                // What the swipe detector does: PRScript.LeftSwipe / RightSwipe with the text's swipe object.
                var t = T("PRScript");
                var pr = Find(t);
                object target = null;
                foreach (var o in UnityEngine.Object.FindObjectsByType(T("SwipeableObject"), FindObjectsSortMode.None))
                    if (((Component)o).name.ToLower() == "textforeground") target = o;
                if (target == null) throw new Exception("no swipeable TextForeground");
                Call(pr, t, arg == "left" ? "LeftSwipe" : "RightSwipe", target);
                break;
            }
            case "prev": ClickByName("btnPrev"); break;

            case "rate":      { var t = T("MovingRatingsOptionsPanel"); Call(Find(t, true), t, "MoveIn"); break; }
            case "ratelater": { var t = T("RateTheApp"); Call(Find(t, true), t, "RateLater"); break; }

            case "offline":        { var t = T("NetworkStatus"); Call(Find(t, true), t, "onNetworkStatusChange", false); break; }
            case "online":         { var t = T("NetworkStatus"); Call(Find(t, true), t, "onNetworkStatusChange", true); break; }
            case "dismissoffline": { var t = T("NetworkStatus"); Call(Find(t, true), t, "DismissDialog"); break; }

            case "stall":      { var t = T("ReadAlongStallHint"); Call(Find(t, true), t, "OnStallHint"); break; }
            case "stallclear": { var t = T("ReadAlongStallHint"); Call(Find(t, true), t, "StopHint"); break; }

            // Home: age chip (value as shown: 2..8) and a reading-room door ("filter,label").
            case "age":  { var t = T("HomeController"); Call(Find(t), t, "OnAgeChipTapped", int.Parse(arg)); break; }
            case "door": { var kv = arg.Split(','); Call(null, T("HomeController"), "OpenDoor", kv[0], kv.Length > 1 ? kv[1] : kv[0]); break; }

            // Story: jump to the book's LAST page (sets the step index to last-1, then NextStep so the
            // page executes normally) — for end-of-book / read-next checks without paging through.
            case "laststep":
            {
                var t = T("PRScript");
                var pr = Find(t);
                var steps = t.GetField("_scriptlets", Any).GetValue(pr) as IList;
                if (steps == null || steps.Count < 2) throw new Exception("no scriptlets");
                t.GetField("nCurrentStep", Any).SetValue(pr, steps.Count - 2);
                Call(pr, t, "NextStep");
                break;
            }

            default: throw new Exception("unknown action " + a);
        }
    }

    // ------------------------------------------------------------------ notes

    public static string Note(string what)
    {
        switch (what)
        {
            case "narration":
            {
                // The narration cache is ordered by use: its last key is the newest narration file.
                var cache = T("AudioAndTextPlayer").GetField("CacheAudioAndTimingsStructs", Any).GetValue(null) as System.Collections.Specialized.OrderedDictionary;
                if (cache == null || cache.Count == 0) return "none";
                object last = null;
                foreach (DictionaryEntry e in cache) last = e.Key;
                return last as string;
            }
            case "rate": return PlayerPrefs.HasKey("g_Rate") ? PlayerPrefs.GetString("g_Rate") : "not saved";
            case "page":
            {
                var t = T("PRScript");
                var pr = Find(t);
                var label = t.GetField("_pageNumber", Any).GetValue(pr) as TMPro.TMP_Text;
                int step = (int)t.GetField("nCurrentStep", Any).GetValue(pr);
                return "shown \"" + (label != null && label.gameObject.activeInHierarchy ? label.text : "(hidden)") + "\", step index " + step;
            }
            default: throw new Exception("unknown note " + what);
        }
    }

    // ------------------------------------------------------------------ conditions

    public static bool Condition(string what)
    {
        if (what.StartsWith("scene=")) return SceneManager.GetActiveScene().name == what.Substring(6);
        if (what.StartsWith("countdown="))
        {
            var wT = T("WelcomeController");
            var w = UnityEngine.Object.FindFirstObjectByType(wT);
            if (w == null) return false;
            var bar = wT.GetField("_countdownBar", Any).GetValue(w) as Image;
            if (bar == null || !bar.gameObject.activeInHierarchy) return false;
            return bar.fillAmount * 100f <= float.Parse(what.Substring(10));
        }
        if (what.StartsWith("silences="))
        {
            var dT = T("DialogueController");
            var d = UnityEngine.Object.FindFirstObjectByType(dT);
            if (d == null) return false;
            object flow = dT.GetField("_flow", Any).GetValue(d);
            if (flow == null) return false;
            int n = (int)flow.GetType().GetField("silences", Any).GetValue(flow);
            return n >= int.Parse(what.Substring(9));
        }
        switch (what)
        {
            case "catalog":
            {
                var list = T("Globals").GetField("g_allPRBooks", Any).GetValue(null) as IList;
                return list != null && list.Count > 0;
            }
            case "playing": return AnyNarration();
            case "idle":    return !AnyNarration();
            case "picker":
            {
                var pT = T("UnifiedReadingModePicker");
                var p = UnityEngine.Object.FindFirstObjectByType(pT);
                return p != null && (bool)pT.GetField("_open", Any).GetValue(p);
            }
            case "demo":
            {
                var wT = T("WelcomeController");
                var w = UnityEngine.Object.FindFirstObjectByType(wT);
                if (w == null) return false;
                object v = wT.GetField("_demo", Any).GetValue(w);
                return v != null && (bool)v.GetType().GetProperty("Visible", Any).GetValue(v);
            }
            case "dialogue":
            {
                var dT = T("DialogueController");
                var d = UnityEngine.Object.FindFirstObjectByType(dT);
                return d != null && (bool)dT.GetProperty("IsOpen", Any).GetValue(d);
            }
            default: throw new Exception("unknown condition " + what);
        }
    }

    private static bool AnyNarration()
    {
        var aT = T("AudioAndTextPlayer");
        var prop = aT.GetProperty("IsPlaying", Any);
        foreach (var o in UnityEngine.Object.FindObjectsByType(aT, FindObjectsSortMode.None))
            if (prop != null && (bool)prop.GetValue(o)) return true;
        return false;
    }
}
#endif
