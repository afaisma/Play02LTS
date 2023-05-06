using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;

public class PRVideoPlayer : MonoBehaviour
{
    public VideoPlayer videoPlayer;
    public GameObject videoDisplay;
    public string baseURL;
    private Coroutine playSegmentCoroutine;
    bool videoInterrupted = false;
    private float segmentEndTime = 0f;
    private bool isPlayingSegment = false;
    
    public bool LoadVideo(string url)
    {
        if (videoPlayer == null) return false;

        videoPlayer.url = baseURL + url;
        videoPlayer.Prepare();

        return true;
    }

    public int UnloadVideo()
    {
        if (videoPlayer == null) return -1;

        videoPlayer.Stop();
        videoPlayer.clip = null;

        return 0;
    }

    private void OnVideoPrepared(VideoPlayer source)
    {
        //videoPlayer.Play();
        SetActive(true);
    }

    public void Play()
    {
        if (videoPlayer == null) return;

        videoPlayer.Play();
    }

    public void PlaySegment(float fromSec, float toSec)
    {
        if (videoPlayer == null) return;
        
        SetActive(true);
        
        videoPlayer.time = fromSec;
        segmentEndTime = toSec;
        videoPlayer.Play();
        isPlayingSegment = true;
    }
    
    public void SetActive(bool bActive)
    {
        if (videoDisplay == null) return;
        videoDisplay.SetActive(bActive);
    }

    public void InterruptPlaySegment()
    {
        if (isPlayingSegment)
        {
            videoInterrupted = true;
            isPlayingSegment = false;
        }
    }

    public void Pause()
    {
        if (videoPlayer == null) return;

        videoPlayer.Pause();
    }

    public void Resume()
    {
        if (videoPlayer == null) return;

        videoPlayer.Play();
    }

    public void Stop()
    {
        if (videoPlayer == null) return;

        videoPlayer.Stop();
    }

    private void OnEndOfPlaying(VideoPlayer source)
    {
        // Handle end of playing logic
    }

    public void ShowVideoDisplay(bool bShow)
    {
        if (videoDisplay != null)
        {
            videoDisplay.SetActive(bShow);
        }
    }

    private void Awake()
    {
        if (videoPlayer != null)
        {
            videoPlayer.prepareCompleted += OnVideoPrepared;
            videoPlayer.loopPointReached += OnEndOfPlaying;
        }
    }

    private void Update()
    {
        if (isPlayingSegment && !videoInterrupted)
        {
            if (videoPlayer.time >= segmentEndTime)
            {
                Debug.Log("videoPlayer.time >= segmentEndTime");
                videoPlayer.Pause();
                isPlayingSegment = false;
            }
        }
        else if (videoInterrupted)
        {
            videoPlayer.Pause();
            videoInterrupted = false;
            isPlayingSegment = false;
        }
    }
    
}
