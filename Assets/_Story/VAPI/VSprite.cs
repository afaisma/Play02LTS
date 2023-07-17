using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public class VSprite : MonoBehaviour
{
    public enum DragType
    {
        NoDragging,  // Disable dragging
        Free,       // Allow dragging in all directions
        Horizontal, // Allow dragging only in horizontal direction
        Vertical,   // Allow dragging only in vertical direction
        Range       // Allow dragging within specified bounds
    }

    public bool isReady = false;
    public string url;
    public DragType dragType = DragType.Free;
    public Vector2 minPosition;
    public Vector2 maxPosition;

    public float minHorizontalPosition = -5f;
    public float maxHorizontalPosition = 5f;

    public float minVerticalPosition = -5f;
    public float maxVerticalPosition = 5f;

    private Vector2 offset;
    public bool isDragging;
    public int nhits = 0;

  private void Update()
    {
        // If left mouse button is pressed, we're not already dragging and dragging is allowed
        if (Input.GetMouseButtonDown(0) && !isDragging && dragType != DragType.NoDragging)
        {
            // Convert the mouse position to world position
            Vector2 mouseWorldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);

            // Raycast to check which objects are under the mouse pointer
            RaycastHit2D[] hits = Physics2D.RaycastAll(mouseWorldPos, Vector2.zero);

            // Go through all objects hit by the raycast
            foreach (RaycastHit2D hit in hits)
            {
                if (gameObject.GetComponent<Renderer>() == null)
                    return;
                if (gameObject.GetComponent<Renderer>().enabled == false)
                    return;

                if (hit.collider != null && hit.collider.gameObject == gameObject)
                {
                    if (HigherSortingObjectClickedByZ(hits, gameObject))
                    {
                        return;
                    }

                    isDragging = true;
                    offset = (Vector2)transform.position - mouseWorldPos;
                    break;
                }
            }
        }

        // If we're dragging this GameObject
        if (isDragging)
        {
            // If left mouse button is still being held
            if (Input.GetMouseButton(0))
            {
                Vector2 mouseWorldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);

                // Depending on the drag type, restrict the movement
                Vector3 targetPosition;
                switch (dragType)
                {
                    case DragType.Horizontal:
                        targetPosition = new Vector3(
                            Mathf.Clamp(mouseWorldPos.x + offset.x, minHorizontalPosition, maxHorizontalPosition),
                            transform.position.y, 
                            transform.position.z);
                        break;
                    case DragType.Vertical:
                        targetPosition = new Vector3(
                            transform.position.x,
                            Mathf.Clamp(mouseWorldPos.y + offset.y, minVerticalPosition, maxVerticalPosition),
                            transform.position.z);
                        break;
                    case DragType.Range:
                        targetPosition = new Vector3(
                            Mathf.Clamp(mouseWorldPos.x + offset.x, minPosition.x, maxPosition.x),
                            Mathf.Clamp(mouseWorldPos.y + offset.y, minPosition.y, maxPosition.y),
                            transform.position.z
                        );
                        break;
                    default: // DragType.Free or DragType.NoDragging
                        targetPosition = new Vector3(mouseWorldPos.x + offset.x, mouseWorldPos.y + offset.y, transform.position.z);
                        break;
                }
                transform.position = targetPosition;
            }
            // If left mouse button was released or dragging is not allowed
            else if (Input.GetMouseButtonUp(0) || dragType == DragType.NoDragging)
            {
                isDragging = false;
            }
        }
    }
  private bool HigherSortingObjectClickedByZ(RaycastHit2D[] hits, GameObject currentGameObject)
  {
      nhits = hits.Length;
      foreach (RaycastHit2D hit in hits)
      {
          if (gameObject.GetComponent<Renderer>() == null) return true;
          if (!gameObject.GetComponent<Renderer>().enabled) return true;
          if (hit.collider.gameObject.transform.position.z < currentGameObject.transform.position.z)
          {
              return true;
          }
      }

      // Check if the mouse pointer is over a UI element.
      if (UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
      {
          return true;
      }

      return false;
  }
  
  public void SetDragType(DragType type, float minX = 0, float maxX = 0, float minY = 0, float maxY = 0)
  {
      dragType = type;

      switch(type)
      {
          case DragType.Horizontal:
              minPosition.x = minX;
              maxPosition.x = maxX;
              break;
          case DragType.Vertical:
              minPosition.y = minY;
              maxPosition.y = maxY;
              break;
          case DragType.Range:
              minPosition = new Vector2(minX, minY);
              maxPosition = new Vector2(maxX, maxY);
              break;
          default: // NoDragging or Free, no action required
              break;
      }
  }}
