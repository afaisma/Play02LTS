using System.Collections;
using UnityEngine;
using QFSW.QC;
using TMPro;
using UnityEngine.UI;
using System;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using QFSW.QC;


public  class WavUtility : MonoBehaviour
{
    private const int HEADER_SIZE = 44;

    public static void SaveWavToFile(AudioClip clip, string filepath)
    {
        byte[] wavData = AudioClipToWavBytes(clip);
        File.WriteAllBytes(filepath, wavData);
    }

    private static byte[] AudioClipToWavBytes(AudioClip clip)
    {
        byte[] samples = null;
        byte[] header = GetWavHeader(clip);

        using (var memoryStream = new MemoryStream())
        {
            memoryStream.Write(header, 0, HEADER_SIZE);

            float[] floatSamples = new float[clip.samples * clip.channels];
            clip.GetData(floatSamples, 0);

            int sampleCount = floatSamples.Length;
            samples = new byte[sampleCount * 2];
            for (int i = 0; i < sampleCount; i++)
            {
                short sample = (short)(floatSamples[i] * short.MaxValue);
                byte[] sampleBytes = BitConverter.GetBytes(sample);
                sampleBytes.CopyTo(samples, i * 2);
            }

            memoryStream.Write(samples, 0, samples.Length);

            return memoryStream.ToArray();
        }
    }

    private static byte[] GetWavHeader(AudioClip clip)
    {
        byte[] header = new byte[HEADER_SIZE];
        int totalSampleCount = clip.samples * clip.channels;

        Encoding.ASCII.GetBytes("RIFF").CopyTo(header, 0);
        BitConverter.GetBytes(HEADER_SIZE + totalSampleCount * 2 - 8).CopyTo(header, 4);
        Encoding.ASCII.GetBytes("WAVE").CopyTo(header, 8);
        Encoding.ASCII.GetBytes("fmt ").CopyTo(header, 12);
        BitConverter.GetBytes(16).CopyTo(header, 16);
        BitConverter.GetBytes((ushort)1).CopyTo(header, 20);
        BitConverter.GetBytes((ushort)clip.channels).CopyTo(header, 22);
        BitConverter.GetBytes(clip.frequency).CopyTo(header, 24);
        BitConverter.GetBytes(clip.frequency * 2 * clip.channels).CopyTo(header, 28);
        BitConverter.GetBytes((ushort)(2 * clip.channels)).CopyTo(header, 32);
        BitConverter.GetBytes((ushort)16).CopyTo(header, 34);
        Encoding.ASCII.GetBytes("data").CopyTo(header, 36);
        BitConverter.GetBytes(totalSampleCount * 2).CopyTo(header, 40);

        return header;
    }
    
    public static IEnumerator SaveToFile(string url, string fileName)
    {
        using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip(url, AudioType.WAV))
        {
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.ConnectionError || www.result == UnityWebRequest.Result.ProtocolError)
            {
                Debug.LogError($"Error while downloading AudioClip from {url}: {www.error}");
            }
            else
            {
                AudioClip downloadedClip = DownloadHandlerAudioClip.GetContent(www);
                if (downloadedClip == null)
                {
                    Debug.LogError($"Failed to load AudioClip from {url}");
                }
                else
                {
                    string filePath = Path.Combine(Application.persistentDataPath, fileName + ".wav");
                    SaveWavToFile(downloadedClip, filePath);
                    Debug.Log($"AudioClip saved as WAV file at: {filePath}");
                }
            }
        }
    }
    
    [Command]
    public  void DownloadAndSaveClip()
    {
        StartCoroutine(WavUtility.SaveToFile("http://localhost:8080/api/files/download/Stories/TimmyAndHisFamily/TimmyAndHisFamily.wav", 5, 10, "/Users/alexanderfaisman/temp/temp.wav"));
    }
    
    public static IEnumerator SaveToFile(string url, float from, float to, string fileName)
    {
        using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip(url, AudioType.WAV))
        {
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.ConnectionError || www.result == UnityWebRequest.Result.ProtocolError)
            {
                Debug.LogError($"Error while downloading AudioClip from {url}: {www.error}");
            }
            else
            {
                AudioClip downloadedClip = DownloadHandlerAudioClip.GetContent(www);
                if (downloadedClip == null)
                {
                    Debug.LogError($"Failed to load AudioClip from {url}");
                }
                else
                {
                    AudioClip subclip = AudioClipUtilities.MakeSubclip(downloadedClip, from, to);
                    string filePath = Path.Combine(Application.persistentDataPath, fileName + ".wav");
                    SaveWavToFile(subclip, filePath);
                    Debug.Log($"Subclip saved as WAV file at: {filePath}");
                }
            }
        }
    }
}
