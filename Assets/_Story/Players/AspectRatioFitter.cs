using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

[RequireComponent(typeof(RawImage))]
public class AspectRatioFitter : MonoBehaviour
{
    public VideoPlayer videoPlayer;

    private RawImage rawImage;

    private void Awake()
    {
        rawImage = GetComponent<RawImage>();
    }

    private void Update()
    {
        if (videoPlayer.texture != null)
        {
            float videoAspectRatio = (float)videoPlayer.texture.width / (float)videoPlayer.texture.height;
            rawImage.rectTransform.localScale = new Vector3(1/videoAspectRatio, 1, 1);
        }
    }
}