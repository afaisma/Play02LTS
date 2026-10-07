using UnityEngine;

/// <summary>
/// The loudness at the microphone right now, for "any sound counts" dialogues. Uses Unity's own
/// Microphone (no speech recognizer). Level is the RMS of the last ~30 ms, 0..1.
/// Not available where there is no microphone or no permission: the dialogue then only offers
/// its "Tap to answer" button.
/// </summary>
public class DialogueMic : MonoBehaviour
{
    private const int SampleRate = 16000;
    private const int Window = 512;

    private AudioClip _clip;
    private string _device;
    private readonly float[] _samples = new float[Window];

    public bool Running => _clip != null;
    public float Level { get; private set; }

    public bool Begin()
    {
        if (Running) return true;
#if UNITY_WEBGL
        return false;
#else
        if (Microphone.devices == null || Microphone.devices.Length == 0) return false;
        if (!Application.HasUserAuthorization(UserAuthorization.Microphone)) return false;
        _device = null; // default microphone
        _clip = Microphone.Start(_device, true, 1, SampleRate);
        Level = 0f;
        return _clip != null;
#endif
    }

    public void End()
    {
#if !UNITY_WEBGL
        if (_clip != null) Microphone.End(_device);
#endif
        _clip = null;
        Level = 0f;
    }

    private void Update()
    {
#if !UNITY_WEBGL
        if (_clip == null) return;
        int pos = Microphone.GetPosition(_device);
        if (pos <= 0) return;
        int start = pos - Window;
        if (start < 0) start += _clip.samples; // GetData wraps around the end of a looping clip
        _clip.GetData(_samples, start);
        float sum = 0f;
        for (int i = 0; i < Window; i++) sum += _samples[i] * _samples[i];
        Level = Mathf.Sqrt(sum / Window);
#endif
    }

    private void OnDisable() => End();
}
