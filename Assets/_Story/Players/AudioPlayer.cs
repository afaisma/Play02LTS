using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Net;
using System.Threading;
using QFSW.QC;
using UnityEngine.Networking;

struct AudioClipStruct
{
    public string audioClipName;
    public string audioClipURL;
    public AudioClip audioClip;
}

public class AudioPlayer : MonoBehaviour
{
    private AudioSource audioSource;
    private List<AudioClipStruct> audioClipStructs;
    public string baseURL;

    // Per-clip AudioSources created lazily by LoopAudio. The shared
    // audioSource above only supports PlayOneShot (polyphonic, fire-and-
    // forget — no way to stop an individual one-shot mid-flight), which
    // is the right thing for short SFX layered atop the page audio but
    // not for tappable musical instruments where the script needs to
    // stop one specific looped clip without affecting any others. One
    // AudioSource per clip name lets StopAudio target precisely.
    private Dictionary<string, AudioSource> _loopSources = new Dictionary<string, AudioSource>();


    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        audioClipStructs = new List<AudioClipStruct>();
    }

    [Command]
    public IEnumerator LoadAudioClip(string audioClipName, string audioClipURL)
    {
        string audioURL = audioClipURL;
        if (!audioClipURL.ToLower().StartsWith("http"))
            audioURL = baseURL + audioClipURL;

        // Migrated from deprecated `new WWW(url)` to UnityWebRequestMultimedia
        // so we can set a timeout — `WWW` cannot, which meant a hung HTTP
        // request would stall the page indefinitely. AudioType is inferred
        // from the URL extension because AddAudio is content-driven and may
        // see .wav / .ogg / .mp3 (UnityWebRequestMultimedia requires the
        // type up front; the old WWW path auto-detected from headers).
        AudioType type = GuessAudioTypeFromUrl(audioURL);
        using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip(audioURL, type))
        {
            www.timeout = 60;  // SFX audio is small but still over the network
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                AudioClip clip = DownloadHandlerAudioClip.GetContent(www);
                if (clip != null)
                {
                    AudioClipStruct audioClipStruct = new AudioClipStruct
                    {
                        audioClipName = audioClipName,
                        audioClipURL = audioClipURL,
                        audioClip = clip
                    };
                    audioClipStructs.Add(audioClipStruct);
                }
                else
                {
                    // Asset-level failure: page still works without this SFX.
                    Debug.LogWarning($"AudioPlayer: decoded AudioClip is null for '{audioClipName}'  (url={audioURL}, type={type})");
                }
            }
            else
            {
                // Asset-level failure: page still works without this SFX.
                Debug.LogWarning($"AudioPlayer: load failed for '{audioClipName}' — {www.error}  (url={audioURL})");
            }
        }
    }

    /// <summary>Pick the AudioType matching the URL extension. Defaults to
    /// MPEG, which covers our chunk audio and most existing AddAudio assets.</summary>
    private static AudioType GuessAudioTypeFromUrl(string url)
    {
        if (string.IsNullOrEmpty(url)) return AudioType.MPEG;
        // Strip query string before extension lookup.
        int q = url.IndexOf('?');
        string clean = q >= 0 ? url.Substring(0, q) : url;
        string lower = clean.ToLowerInvariant();
        if (lower.EndsWith(".wav"))  return AudioType.WAV;
        if (lower.EndsWith(".ogg"))  return AudioType.OGGVORBIS;
        if (lower.EndsWith(".aiff") || lower.EndsWith(".aif")) return AudioType.AIFF;
        return AudioType.MPEG;  // .mp3 and everything else
    }

    public void PlayAudio(string audioClipName, float dFrom, float dTo)
    {
        AudioClipStruct audioClipStruct = audioClipStructs.Find(x => x.audioClipName == audioClipName);
        if (audioClipStruct.audioClip == null)
        {
            Debug.Log($"Audio clip not found: {audioClipName}");
            return;
        }
        // No time range specified (both 0 — the intrinsic default, or
        // dTo <= dFrom) means "play the whole clip" as a one-shot SFX.
        // PlayOneShot layers cleanly on top of anything else playing on
        // the same AudioSource, so rapid taps produce overlapping laughs
        // instead of cutting each other off.
        if (dTo <= dFrom)
        {
            audioSource.PlayOneShot(audioClipStruct.audioClip);
        }
        else
        {
            // Time range: produce a subclip and play it monophonically.
            // (This is the historical chunk-audio path.)
            audioSource.clip = AudioClipUtilities.MakeSubclip(audioClipStruct.audioClip, dFrom, dTo);
            audioSource.Play();
        }
    }

    void StopPlaying()
    {
        audioSource.Stop();
    }

    /// <summary>Start (or restart) a named clip on a dedicated looping
    /// AudioSource. Multiple instruments can loop simultaneously without
    /// interfering with each other or with the shared audioSource that
    /// PlayAudio uses. No-op if the clip hasn't been AddAudio'd yet.</summary>
    public void LoopAudio(string audioClipName)
    {
        AudioClipStruct s = audioClipStructs.Find(x => x.audioClipName == audioClipName);
        if (s.audioClip == null)
        {
            Debug.LogWarning($"LoopAudio: no clip '{audioClipName}'");
            return;
        }
        if (!_loopSources.TryGetValue(audioClipName, out AudioSource src) || src == null)
        {
            src = gameObject.AddComponent<AudioSource>();
            src.playOnAwake = false;
            _loopSources[audioClipName] = src;
        }
        src.clip = s.audioClip;
        src.loop = true;
        if (!src.isPlaying) src.Play();
    }

    /// <summary>Stop a named clip. Stops the per-clip loop source if one
    /// exists, and also stops the shared audioSource if it currently
    /// holds this clip (the subclip PlayAudio path). One-shot PlayOneShot
    /// calls can't be cancelled — Unity doesn't expose a handle for them.</summary>
    public void StopAudio(string audioClipName)
    {
        if (_loopSources.TryGetValue(audioClipName, out AudioSource src)
            && src != null && src.isPlaying)
        {
            src.Stop();
        }
        AudioClipStruct s = audioClipStructs.Find(x => x.audioClipName == audioClipName);
        if (s.audioClip != null && audioSource.clip == s.audioClip && audioSource.isPlaying)
        {
            audioSource.Stop();
        }
    }

    /// <summary>True if the named clip is currently playing on either its
    /// dedicated loop source or the shared subclip source. Used by toggle
    /// handlers ("tap to start, tap again to stop"). Cannot detect
    /// in-flight PlayOneShot calls.</summary>
    public bool IsAudioPlaying(string audioClipName)
    {
        if (_loopSources.TryGetValue(audioClipName, out AudioSource src)
            && src != null && src.isPlaying)
            return true;
        AudioClipStruct s = audioClipStructs.Find(x => x.audioClipName == audioClipName);
        if (s.audioClip != null && audioSource.clip == s.audioClip && audioSource.isPlaying)
            return true;
        return false;
    }
}
