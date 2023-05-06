using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class VUMeter : MonoBehaviour
{
    public int audioSampleRate = 44100;
    public string microphoneDevice = null;
    public int sampleDataLength = 1024;
    public float sensitivity = 100;
    public Gradient colorGradient;

    private Image vuMeterBar;
    private AudioSource monitoringSource;
    //private AudioSource playbackSource;
    private float[] sampleData;

    void Start()
    {
        vuMeterBar = GetComponent<Image>();

        sampleData = new float[sampleDataLength];

        // Set up the monitoring AudioSource
        monitoringSource = gameObject.AddComponent<AudioSource>();
        monitoringSource.clip = Microphone.Start(microphoneDevice, true, 1, audioSampleRate);
        monitoringSource.loop = true;

        // Set up the playback AudioSource
        // playbackSource = gameObject.AddComponent<AudioSource>();
        // playbackSource.clip = monitoringSource.clip;
        // playbackSource.loop = true;

        while (!(Microphone.GetPosition(microphoneDevice) > 0)) { }
        monitoringSource.Play();
        
        //playbackSource.PlayDelayed((float)monitoringSource.clip.samples / audioSampleRate);
    }

    void Update()
    {
        monitoringSource.GetOutputData(sampleData, 0);
        float averageVolume = CalculateAverageVolume(sampleData);
        UpdateVUMeter(averageVolume);
    }

    float CalculateAverageVolume(float[] data)
    {
        float sum = 0;
        for (int i = 0; i < data.Length; i++)
        {
            sum += Mathf.Abs(data[i]);
        }
        return sum / data.Length;
    }

    void UpdateVUMeter(float volume)
    {
        float scaledVolume = volume * sensitivity;
        vuMeterBar.rectTransform.localScale = new Vector3(scaledVolume, 1, 1);
        vuMeterBar.color = colorGradient.Evaluate(scaledVolume);
    }

    private void OnDestroy()
    {
        if (Microphone.IsRecording(microphoneDevice))
        {
            Microphone.End(microphoneDevice);
        }
    }
}
