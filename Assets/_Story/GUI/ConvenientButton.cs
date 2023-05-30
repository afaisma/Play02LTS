using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class ConvenientButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    private Button button;
    private RectTransform rectTransform;
    private AudioSource audioSource;

    // Sound effects
    public AudioClip pressSound;
    public AudioClip releaseSound;
    public AudioClip disabledSound;

    // Visual effects
    public ParticleSystem pressEffect;
    public ParticleSystem releaseEffect;
    public ParticleSystem disabledEffect;

    // Button transformation parameters
    public Vector3 pressedScale = new Vector3(0.9f, 0.9f, 0.9f);
    public Vector3 pressedRotation = new Vector3(20, 0, 0);

    // Original size and rotation
    private Vector3 originalScale;
    private Vector3 originalRotation;

    void Awake()
    {
        button = GetComponent<Button>();
        rectTransform = GetComponent<RectTransform>();
        audioSource = GetComponent<AudioSource>();

        // If there's no AudioSource component, add one
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        audioSource.volume = 0.2f; 

        originalScale = rectTransform.localScale;
        originalRotation = rectTransform.eulerAngles;

        // Set button colors
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.pressedColor = Color.blue;
        button.colors = colors;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        // Shrink and rotate the button, and play the sound when the button is pressed
        rectTransform.localScale = pressedScale;
        rectTransform.Rotate(pressedRotation);

        // Play press sound if available
        if (pressSound != null)
        {
            audioSource.PlayOneShot(pressSound);
        }

        // Play press visual effect if available
        if (pressEffect != null)
        {
            pressEffect.Play();
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        // Reset the button size and rotation when the button is released
        rectTransform.localScale = originalScale;
        rectTransform.eulerAngles = originalRotation;

        // Play release sound if available
        if (releaseSound != null)
        {
            audioSource.PlayOneShot(releaseSound);
        }

        // Play release visual effect if available
        if (releaseEffect != null)
        {
            releaseEffect.Play();
        }
    }

    private void OnDisable()
    {
        // Play disabled sound if available
        if (disabledSound != null)
        {
            audioSource.PlayOneShot(disabledSound);
        }

        // Play disabled visual effect if available
        if (disabledEffect != null)
        {
            disabledEffect.Play();
        }
    }
}
