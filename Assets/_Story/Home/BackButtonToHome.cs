using UnityEngine;
using UnityEngine.UI;

// Reroute a scene's back button (btnLibrary) to Home. _Settings and _Parents are now reached from
// Home's "For grown-ups" door, so their back button should return Home, not the Library. Replacing
// onClick at runtime cleanly drops the old (serialized) Library call without scene-event surgery.
public class BackButtonToHome : MonoBehaviour
{
    private void Start()
    {
        var go = GameObject.Find("btnLibrary");
        var b = go != null ? go.GetComponent<Button>() : null;
        if (b != null)
        {
            b.onClick = new Button.ButtonClickedEvent();
            b.onClick.AddListener(Navigation.GoToHome);
        }
    }
}
