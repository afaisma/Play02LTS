using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;

public class VSpriteButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler,
    IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] 
    private UnityEvent onClick;

    public void OnPointerDown(PointerEventData eventData)
    {
        Debug.Log("OnPointerDown called.");
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        Debug.Log("OnPointerUp called.");
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        Debug.Log("OnPointerClick called.");
        onClick?.Invoke();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        Debug.Log("OnPointerEnter called.");
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        Debug.Log("OnPointerExit called.");
    }
}