using UnityEngine; using UnityEngine.EventSystems; using System.Collections.Generic; using System.Text;
public class TapLogger : MonoBehaviour {
  void Update() {
    bool down = Input.GetMouseButtonDown(0) ||
                (Input.touchCount>0 && Input.GetTouch(0).phase==TouchPhase.Began);
    if (!down || EventSystem.current==null) return;
    Vector2 pos = Input.touchCount>0 ? Input.GetTouch(0).position : (Vector2)Input.mousePosition;
    var ped = new PointerEventData(EventSystem.current){ position = pos };
    var hits = new List<RaycastResult>();
    EventSystem.current.RaycastAll(ped, hits);
    var sb = new StringBuilder("[TAP] topmost-first hits:\n");
    foreach (var h in hits) {
      var t = h.gameObject.transform; string p = h.gameObject.name;
      while (t.parent != null) { t = t.parent; p = t.name + "/" + p; }
      sb.AppendLine("  " + p);
    }
    Debug.Log(sb.ToString());
  }
}
