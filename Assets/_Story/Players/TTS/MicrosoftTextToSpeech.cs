using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

public class MicrosoftTextToSpeech : MonoBehaviour
{
    private string apiKey = "8269fa851a134a09acaae4ae84ddea5d";
    private string apiRegion = "eastus";
    private string apiUrl = "https://";
    private AudioSource audioSource;
    private AudioClip audioClip;
    //private string ; //""en-US-JennyNeural";

    void Start()
    {
        apiUrl = apiUrl + apiRegion + ".tts.speech.microsoft.com/cognitiveservices/v1";
        audioSource = gameObject.AddComponent<AudioSource>();
        //StartCoroutine(SynthesizeUsingMicrosoftTextToSpeech("Hello, this is a test of the Microsoft Text-to-Speech API.", "", "-20%", "+30%"));
    }

    public IEnumerator SynthesizeUsingMicrosoftTextToSpeech(string text, string voice, string rate, string pitch)
    {
        string v = voice;
        string p = pitch;
        string r = rate;
        if (v == "")
            v = "en-US-AnaNeural";
        if (p == "")
            p = "+0%";
        if (r == "")
            r = "+0%";
        
        string postData = $"<speak version='1.0' xmlns='https://www.w3.org/2001/10/synthesis' xml:lang='en-US'><voice name='{v}'><prosody rate='{r}'><prosody pitch='{p}'> "+ text + "</prosody></prosody></voice></speak>";
        byte[] postDataBytes = System.Text.Encoding.UTF8.GetBytes(postData);

        UnityWebRequest www = new UnityWebRequest(apiUrl, "POST");
        www.uploadHandler = new UploadHandlerRaw(postDataBytes);
        www.downloadHandler = new DownloadHandlerBuffer();
        www.SetRequestHeader("Content-Type", "application/ssml+xml");
        www.SetRequestHeader("Ocp-Apim-Subscription-Key", apiKey);
        www.SetRequestHeader("X-Microsoft-OutputFormat", "riff-16khz-16bit-mono-pcm");

        yield return www.SendWebRequest();

        if (www.result == UnityWebRequest.Result.Success)
        {
            byte[] audioData = www.downloadHandler.data;
            int sampleRate = 16000; // Microsoft Text-to-Speech uses a sample rate of 16000 for riff-16khz-16bit-mono-pcm encoding
            audioClip = AudioClip.Create("TTS_AudioClip", audioData.Length / 2, 1, sampleRate, false);
            float[] audioDataFloat = new float[audioData.Length / 2];

            for (int i = 0; i < audioData.Length / 2; i++)
            {
                audioDataFloat[i] = (short)(audioData[i * 2] | (audioData[i * 2 + 1] << 8)) / 32768.0f;
            }

            audioClip.SetData(audioDataFloat, 0);
            OnAudioClipIsReady();
        }
        else
        {
            Debug.LogError("Error: " + www.error);
        }
    }

    void OnAudioClipIsReady()
    {
        Debug.Log("Audio clip is ready.");
        // You can start playing the audio clip here or do any other actions
        PlayAudioClip();
    }

    public void PlayAudioClip()
    {
        if (audioClip != null)
        {
            audioSource.clip = audioClip;
            audioSource.Play();
            StartCoroutine(WaitForAudioClipCompletion());
        }
    }

    IEnumerator WaitForAudioClipCompletion()
    {
        while (audioSource.isPlaying)
        {
            yield return null;
        }
        OnAudioClipPlaybackCompleted();
    }

    void OnAudioClipPlaybackCompleted()
    {
        Debug.Log("Audio clip playback completed.");
        // You can do
    }

    public void Speak(string text, string voice, string rate, string pitch)
    {
        StartCoroutine(SynthesizeUsingMicrosoftTextToSpeech(text, voice,  rate,  pitch));
    }
}
