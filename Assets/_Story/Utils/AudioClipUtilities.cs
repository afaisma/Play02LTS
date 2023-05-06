using UnityEngine;

public static class AudioClipUtilities
{
    public static AudioClip MakeSubclip(AudioClip clip, float start, float stop)
    {
        if (clip == null)
        {
            Debug.LogError("The AudioClip is null.");
            return null;
        }

        if (start < 0 || start >= clip.length || stop < 0 || stop >= clip.length || start >= stop)
        {
            Debug.LogError("Invalid start or stop time.");
            return null;
        }

        int samplesStart = Mathf.FloorToInt(start * clip.frequency);
        int samplesStop = Mathf.FloorToInt(stop * clip.frequency);
        int samplesLength = samplesStop - samplesStart;

        float[] originalData = new float[clip.samples * clip.channels];
        clip.GetData(originalData, 0);

        float[] subclipData = new float[samplesLength * clip.channels];
        System.Array.Copy(originalData, samplesStart * clip.channels, subclipData, 0, samplesLength * clip.channels);

        AudioClip subclip = AudioClip.Create(clip.name + "_Subclip", samplesLength, clip.channels, clip.frequency, false);
        subclip.SetData(subclipData, 0);

        return subclip;
    }
}