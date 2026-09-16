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
//   library|<filter> Library shelf (everything, fairytales, learn to read, level2, new, ...)
//   book|<folder>    open the book whose bookUrl contains <folder> (e.g. FarmAnimalsRhymebook)
//   mode|<Mode>      reading-mode picker tile: Storyteller | AppVoice | IRead | Pictures
//                    (IRead is NOT supported in the Simulator — Recognissimo is stubbed there)
//   picker           toggle the reading-mode picker (open <-> closed)
//   click|<name>     press any active Button by GameObject name (e.g. ShowAllAges)
//   next | prev      page-turn arrows
//   rate | ratelater the rate-app panel in / out
//   offline | online | dismissoffline   the no-internet dialog (drives NetworkStatus directly)
//   stall | stallclear                  the read-along stall hint (arrow pulse + once-ever caption)
// Conditions for "wait|<what>": catalog | playing | idle | picker | scene=<name>
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
            case "learn":     Call(null, T("Navigation"), "GoToLearnToRead"); break;
            case "settings":  Call(null, T("Navigation"), "GoToSettings"); break;
            case "parents":   Call(null, T("Navigation"), "GoToParents"); break;
            case "bookstore": Call(null, T("Navigation"), "GoToBookstore"); break;
            case "library":   Call(null, T("Globals"), "GotoLibrary", string.IsNullOrEmpty(arg) ? "everything" : arg); break;

            case "book":
            {
                var gT = T("Globals");
                var list = gT.GetField("g_listPRBooks", Any).GetValue(null) as IList;
                if (list == null || list.Count == 0) throw new Exception("catalog not loaded");
                object target = null;
                foreach (var b in list)
                {
                    var url = b.GetType().GetField("bookUrl", Any)?.GetValue(b) as string;
                    if (url != null && url.IndexOf(arg, StringComparison.OrdinalIgnoreCase) >= 0) { target = b; break; }
                }
                if (target == null) throw new Exception("no book matching '" + arg + "'");
                gT.GetMethod("GotoPrBook", Any).Invoke(null, new[] { target });
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
            case "next": ClickByName("btnNext"); break;
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

    // ------------------------------------------------------------------ conditions

    public static bool Condition(string what)
    {
        if (what.StartsWith("scene=")) return SceneManager.GetActiveScene().name == what.Substring(6);
        switch (what)
        {
            case "catalog":
            {
                var list = T("Globals").GetField("g_listPRBooks", Any).GetValue(null) as IList;
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
