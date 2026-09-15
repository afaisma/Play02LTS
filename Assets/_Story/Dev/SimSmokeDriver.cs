// Dev-only: runs a sweep plan INSIDE an iOS Simulator build (tier-2 smoke; ported from
// EveryWord 2026-09-15). Compiled only with READINGBUDDY_DEV (RbSimSmoke.ExportSimulator's extra
// scripting define) — never in a store build. The plan arrives as a launch argument:
// `xcrun simctl launch <udid> <bundle> -sweep "<plan>"` puts "sweep" into NSUserDefaults'
// argument domain, and PlayerPrefs on iOS IS NSUserDefaults, so PlayerPrefs.GetString("sweep")
// reads it (Environment.GetCommandLineArgs is the fallback).
//
// Plan = "frames|action|arg;frames|action|arg;..." — wait N frames, then do the action.
//   cap|<name>    write Documents/sweep/<nn>_<name>.req and wait (<=25 s) for the host's .ack
//                 (tools/sim_smoke.sh takes the screenshot with `simctl io` and acks)
//   wait|<what>   poll (<=25 s) until: catalog (books loaded) | playing (narration audible) |
//                 scene=<name> | idle (no narration) — ReadingBuddy streams content from the
//                 CDN, so frame counts alone would race the download
//   log|<text>    write a line; everything else -> SweepActions.Perform (reflection)
// Log: Documents/sweep/_log.txt, header with device/screen/safe area, "ok"/"ERR" per step,
// last line "done".
#if UNITY_EDITOR || READINGBUDDY_DEV
// (compiled in the editor too so a compile error shows before an export; Boot returns at once there)
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class SimSmokeDriver : MonoBehaviour
{
    private const float AckTimeout = 25f;
    private const float WaitTimeout = 25f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        if (Application.isEditor) return;
        string plan = PlayerPrefs.GetString("sweep", "");
        if (!string.IsNullOrEmpty(plan)) { PlayerPrefs.DeleteKey("sweep"); PlayerPrefs.Save(); }
        if (string.IsNullOrEmpty(plan))
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == "-sweep") plan = args[i + 1];
        }
        if (string.IsNullOrEmpty(plan) || FindFirstObjectByType<SimSmokeDriver>() != null) return;
        var go = new GameObject("SimSmokeDriver");
        DontDestroyOnLoad(go);
        go.AddComponent<SimSmokeDriver>().StartCoroutine(go.GetComponent<SimSmokeDriver>().Run(plan));
    }

    private IEnumerator Run(string planText)
    {
        string dir = Path.Combine(Application.persistentDataPath, "sweep");
        Directory.CreateDirectory(dir);
        var log = new List<string>();
        string logPath = Path.Combine(dir, "_log.txt");
        Action flush = () => File.WriteAllLines(logPath, log.ToArray());
        var sa = Screen.safeArea;
        log.Add($"# {Application.productName} {Application.version} ({BuildInfo.Line()}) unity {Application.unityVersion} | {SystemInfo.deviceModel} {SystemInfo.operatingSystem} | screen {Screen.width}x{Screen.height} dpi {Screen.dpi} safe {sa.x},{sa.y},{sa.width},{sa.height}");
        flush();

        string[] plan = planText.Split(';');
        for (int idx = 0; idx < plan.Length; idx++)
        {
            var parts = plan[idx].Split('|');
            int delay; if (!int.TryParse(parts[0], out delay)) delay = 0;
            for (int f = 0; f < delay; f++) yield return null;
            string a = parts.Length > 1 ? parts[1] : "", arg = parts.Length > 2 ? parts[2] : "";
            if (a == "cap")
            {
                string stem = $"{idx:D2}_{arg}";
                yield return new WaitForEndOfFrame();
                File.WriteAllText(Path.Combine(dir, stem + ".req"), plan[idx]);
                float t0 = Time.realtimeSinceStartup;
                while (!File.Exists(Path.Combine(dir, stem + ".ack")) && Time.realtimeSinceStartup - t0 < AckTimeout)
                    yield return new WaitForSecondsRealtime(0.2f);
                log.Add((File.Exists(Path.Combine(dir, stem + ".ack")) ? "ok " : "ERR no-ack ") + plan[idx] + " f=" + Time.frameCount);
            }
            else if (a == "wait")
            {
                float t0 = Time.realtimeSinceStartup;
                bool met = false;
                while (Time.realtimeSinceStartup - t0 < WaitTimeout)
                {
                    try { met = SweepActions.Condition(arg); } catch (Exception e) { log.Add("ERR wait " + arg + " :: " + e.Message); break; }
                    if (met) break;
                    yield return new WaitForSecondsRealtime(0.25f);
                }
                log.Add((met ? "ok " : "ERR timeout ") + plan[idx] + " f=" + Time.frameCount
                        + " t=" + (Time.realtimeSinceStartup - t0).ToString("F1"));
            }
            else if (a == "log") log.Add(arg);
            else if (a == "dev") log.Add("skip " + plan[idx]);
            else
            {
                try { SweepActions.Perform(a, arg); log.Add("ok " + plan[idx] + " f=" + Time.frameCount); }
                catch (Exception e) { log.Add("ERR " + plan[idx] + " :: " + (e.InnerException != null ? e.InnerException.Message : e.Message)); }
            }
            flush();
        }
        log.Add("done");
        flush();
    }
}
#endif
