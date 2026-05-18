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

        // M-R3-2: allow stop == clip.length (whole-clip subclip is a legitimate
        // case). Previously `stop >= clip.length` rejected it.
        if (start < 0 || start >= clip.length || stop < 0 || stop > clip.length || start >= stop)
        {
            Debug.LogError("Invalid start or stop time.");
            return null;
        }

        int samplesStart = Mathf.FloorToInt(start * clip.frequency);
        int samplesStop = Mathf.FloorToInt(stop * clip.frequency);
        int samplesLength = samplesStop - samplesStart;

        // M-R3-3: read only the needed window directly from the source clip,
        // instead of allocating a full-clip copy and slicing it. For a 60-second
        // 44 kHz stereo clip this used to allocate ~5 MB per call; now it
        // allocates only what the subclip needs.
        float[] subclipData = new float[samplesLength * clip.channels];
        clip.GetData(subclipData, samplesStart);

        AudioClip subclip = AudioClip.Create(clip.name + "_Subclip", samplesLength, clip.channels, clip.frequency, false);
        subclip.SetData(subclipData, 0);

        return subclip;
    }
}