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

        using (var www = new WWW(audioURL))
        {
            yield return www;

            if (www.error == null)
            {
                AudioClipStruct audioClipStruct = new AudioClipStruct
                {
                    audioClipName = audioClipName,
                    audioClipURL = audioClipURL,
                    audioClip = www.GetAudioClip()
                };
                audioClipStructs.Add(audioClipStruct);
            }
            else
            {
                Debug.LogError($"Error loading audio clip: {www.error}");
            }
        }
    }
    
    [Command]
    public void PlayAudio1(string audioClipName, float dFrom, float dTo)
    {
        AudioClipStruct audioClipStruct = audioClipStructs.Find(x => x.audioClipName == audioClipName);
        if (audioClipStruct.audioClip != null)
        {
            audioSource.clip = audioClipStruct.audioClip;
            audioSource.time = dFrom;
            audioSource.Play();
            StartCoroutine(StopAudioAtTime(audioClipName, dTo));
        }
        else
        {
            Debug.Log($"Audio clip not found: {audioClipName}");
        }
    }

    public void PlayAudio2(string audioClipName, float dFrom, float dTo)
    {
        AudioClipStruct audioClipStruct = audioClipStructs.Find(x => x.audioClipName == audioClipName);
        if (audioClipStruct.audioClip != null)
        {
            audioSource.clip = audioClipStruct.audioClip;
            audioSource.time = dFrom;
            audioSource.Play();
            //StartCoroutine(StopAudioAtTime(audioClipName, dTo));
        }
        else
        {
            Debug.Log($"Audio clip not found: {audioClipName}");
        }
    }

    public void PlayAudio(string audioClipName, float dFrom, float dTo)
    { 
        AudioClipStruct audioClipStruct = audioClipStructs.Find(x => x.audioClipName == audioClipName);
        if (audioClipStruct.audioClip != null)
        {
            AudioClip ac = AudioClipUtilities.MakeSubclip(audioClipStruct.audioClip, dFrom,  dTo);
            audioSource.clip = ac;
            //audioSource.time = dFrom;
            audioSource.Play();
            //audioSource.Stop();
            //Invoke("StopPlaying", dTo - dFrom);
        }
        else
        {
            Debug.Log($"Audio clip not found: {audioClipName}");
        }
    }

    void StopPlaying()
    {
        audioSource.Stop();
    }

    private IEnumerator StopAudioAtTime(string audioClipName, float dTo)
    {
        yield return new WaitForSeconds(dTo - audioSource.time);
        OnAudioPlaybackFinished(audioClipName);
    }

    private void OnAudioPlaybackFinished(string audioClipName)
    {
        audioSource.Stop();
        Debug.Log("Audio playback finished.");
    }
}
