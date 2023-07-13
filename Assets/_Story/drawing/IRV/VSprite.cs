using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VSprite : MonoBehaviour
{
    private Vector2 offset;
    public bool isDragging;
    public int nhits = 0;
    void Start()
    {
        
    }

  private void Update()
    {
        // If left mouse button is pressed and we're not already dragging
        if (Input.GetMouseButtonDown(0) && !isDragging)
        {
            // Convert the mouse position to world position
            Vector2 mouseWorldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);

            // Raycast to check which objects are under the mouse pointer
            RaycastHit2D[] hits = Physics2D.RaycastAll(mouseWorldPos, Vector2.zero);

            // Go through all objects hit by the raycast
            foreach (RaycastHit2D hit in hits)
            {
                if (hit.collider != null && hit.collider.gameObject == gameObject)
                {
                    // If a higher sorting object has been clicked, don't proceed
                    // if (HigherSortingObjectClicked(hits, hit.collider.gameObject.GetComponent<SpriteRenderer>()))
                    // {
                    //     return;
                    // }
                    if (HigherSortingObjectClickedByZ(hits, hit.collider.gameObject))
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
                transform.position = new Vector3(mouseWorldPos.x + offset.x, mouseWorldPos.y + offset.y,
                    transform.position.z);  
            }
            // If left mouse button was released
            else if (Input.GetMouseButtonUp(0))
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
          if (hit.collider.gameObject.transform.position.z < currentGameObject.transform.position.z)
          {
              return true;
          }
      }
      return false;
  }
    private bool HigherSortingObjectClicked(RaycastHit2D[] hits, SpriteRenderer currentSpriteRenderer)
    {
        nhits = hits.Length;
        foreach (RaycastHit2D hit in hits)
        {
            SpriteRenderer hitSpriteRenderer = hit.collider.gameObject.GetComponent<SpriteRenderer>();
            if (hitSpriteRenderer != null &&
                hitSpriteRenderer.sortingLayerID >= currentSpriteRenderer.sortingLayerID &&
                hitSpriteRenderer.sortingOrder > currentSpriteRenderer.sortingOrder)
            {
                return true;
            }
        }
        return false;
    }
    
}
