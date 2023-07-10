using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class AddTransparentPanel : MonoBehaviour, IPointerDownHandler
{
    public GameObject panelThis;
    private GameObject panelChild;

    void Start()
    {
        AddPanel();
    }

    public void AddPanel()
    {

        // Create PanelChild
        panelChild = new GameObject("PanelChild");
        panelChild.transform.SetParent(panelThis.transform, false);

        // Add Image component to PanelChild
        Image panelChildImage = panelChild.AddComponent<Image>();
        panelChildImage.color = new Color(1f, 1f, 1f, 1f); // Transparent color (alpha value set to 0)

        // Set PanelChild's size and position to occupy the whole screen
        RectTransform panelChildRect = panelChild.GetComponent<RectTransform>();
        panelChildRect.anchorMin = Vector2.zero;
        panelChildRect.anchorMax = Vector2.one;
        panelChildRect.sizeDelta = Vector2.zero;

        // Add an EventTrigger component to PanelChild to receive touch events
        EventTrigger eventTrigger = panelChild.AddComponent<EventTrigger>();

        // Create a new entry for the PointerDown event
        EventTrigger.Entry pointerDownEntry = new EventTrigger.Entry();
        pointerDownEntry.eventID = EventTriggerType.PointerDown;
        pointerDownEntry.callback.AddListener((data) => { OnPointerDownDelegate((PointerEventData)data); });

        // Add the PointerDown event entry to the EventTrigger
        eventTrigger.triggers.Add(pointerDownEntry);
    }

    void RemovePanel()
    {
        if (panelChild != null)
        {
            Destroy(panelChild);
            panelChild = null;
        }
    }

    void OnPointerDownDelegate(PointerEventData eventData)
    {
        // Prevent the event from passing through to the underlying UI
        eventData.Use();
    }

    // Example usage for adding and removing the transparent panel
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.A))
        {
            AddPanel();
        }
        else if (Input.GetKeyDown(KeyCode.R))
        {
            RemovePanel();
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        // Prevent the event from passing through to the underlying UI
        eventData.Use();
    }
}
