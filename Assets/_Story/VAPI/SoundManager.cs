using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SoundManager : MonoBehaviour {

    public enum SoundType {
        Birds,
        Animals,
        Humans,
        Noises,
        Ocean,
        ClicksAndMoves
    }

    [System.Serializable]
    public struct SoundScheduleSettings {
        public SoundType soundType;
        public float minDelay;
        public float maxDelay;
        public float volume;
        public bool isEnabled;

        public SoundScheduleSettings(SoundType soundType) {
            this.soundType = soundType;
            minDelay = 1.0f;      // minimum delay of 1 second between sounds
            maxDelay = 5.0f;      // maximum delay of 5 seconds between sounds
            volume = 0.5f;        // half of the maximum volume
            isEnabled = true;     // by default, sounds are enabled
        }
    }
    
    [System.Serializable]
    public class SoundAudioClip {
        public SoundType soundType;
        public List<AudioClip> audioClips;
    }

    public SoundAudioClip[] soundAudioClipArray;
    public List<SoundScheduleSettings> soundScheduleSettingsList;

    private Dictionary<SoundType, float> soundTimerDictionary;
    private GameObject oneShotGameObject;

    private void Start() {
        Initialize();
        // Schedule sounds based on settings
        foreach (SoundScheduleSettings settings in soundScheduleSettingsList) {
            if(settings.isEnabled) {
                ScheduleSound(settings.soundType, settings.minDelay, settings.maxDelay);
            }
        }
    }

    private void Initialize() {
        soundTimerDictionary = new Dictionary<SoundType, float>();
        foreach (SoundType soundType in System.Enum.GetValues(typeof(SoundType)))
        {
            soundTimerDictionary[soundType] = 0f;
        }
    }

    public void PlaySound(SoundType soundType, Vector3 position) {
        if (CanPlaySound(soundType))
        {
            AudioClip clip = GetAudioClip(soundType);
            if (clip == null)
                return;
            GameObject soundGameObject = new GameObject("Sound");
            soundGameObject.transform.position = position;
            AudioSource audioSource = soundGameObject.AddComponent<AudioSource>();
            audioSource.clip = clip;
            audioSource.volume = GetVolumeBySoundType(soundType);
            Debug.Log("oneShotAudioSource.volume=" + audioSource.volume );
            audioSource.maxDistance = 100f;
            audioSource.spatialBlend = 1f;
            audioSource.rolloffMode = AudioRolloffMode.Linear;
            audioSource.dopplerLevel = 0f;
            audioSource.Play();

            Destroy(soundGameObject, audioSource.clip.length);
        }
    }

    private bool CanPlaySound(SoundType soundType) {
        switch (soundType) {
        default:
            return true;
        case SoundType.ClicksAndMoves:
            if (soundTimerDictionary.ContainsKey(soundType)) {
                float lastTimePlayed = soundTimerDictionary[soundType];
                float playerMoveTimerMax = .15f;
                if (lastTimePlayed + playerMoveTimerMax < Time.time) {
                    soundTimerDictionary[soundType] = Time.time;
                    return true;
                } else {
                    return false;
                }
            } else {
                return true;
            }
        }
    }

    private AudioClip GetAudioClip(SoundType soundType) {
        foreach (SoundAudioClip soundAudioClip in soundAudioClipArray) {
            if (soundAudioClip.soundType == soundType) {
                if (soundAudioClip.audioClips.Count > 0) {
                    int index = Random.Range(0, soundAudioClip.audioClips.Count);
                    return soundAudioClip.audioClips[index];
                }
            }
        }
        return null;
    }
    
    public void ScheduleSound(SoundType soundType, float minDelay, float maxDelay)
    {
        StartCoroutine(PlaySoundOccasionally(soundType, minDelay, maxDelay));
    }

    private IEnumerator PlaySoundOccasionally(SoundType soundType, float minDelay, float maxDelay)
    {
        while (true)
        {
            yield return new WaitForSeconds(Random.Range(minDelay, maxDelay));

            PlaySound(soundType, new Vector3());
        }
    }
    
    private float GetVolumeBySoundType(SoundType soundType) {
        foreach (SoundScheduleSettings settings in soundScheduleSettingsList) {
            if (settings.soundType == soundType) {
                return settings.volume;
            }
        }
        return 1.0f; // return default volume if not found
    }
}
