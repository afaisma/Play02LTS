using UnityEngine;

// H-R4-1: Previously this component implemented IPointerDownHandler and called
// button.onClick.Invoke() on PointerDown — which combined with Unity's standard
// Button (which fires onClick on PointerUp) caused every tap to fire onClick
// twice. The handler has been removed, leaving an empty no-op so existing
// scene references (if any) don't break. Audit confirmed nothing in any scene
// or prefab actually attaches this component, so it's safe to delete entirely
// from a terminal:
//     rm Assets/_Story/Rooms/CombinedButton.cs Assets/_Story/Rooms/CombinedButton.cs.meta
public class CombinedButton : MonoBehaviour
{
}
