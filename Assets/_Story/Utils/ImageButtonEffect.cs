using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ImageButtonEffect : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerEnterHandler, IPointerExitHandler
{
    public Color normalColor = Color.white;
    public Color pressedColor = new Color(0.8f, 0.8f, 0.8f);
    public Color hoverColor = new Color(0.9f, 0.9f, 0.9f);

    private Image image;

    private void Awake()
    {
        image = GetComponent<Image>();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        image.color = pressedColor;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        image.color = normalColor;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        image.color = hoverColor;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        image.color = normalColor;
    }
}