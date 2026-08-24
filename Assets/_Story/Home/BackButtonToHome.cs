using UnityEngine;
using UnityEngine.UI;

// Reroute a scene's back button (btnLibrary) to Home. _Settings and _Parents are now reached from
// Home's "For grown-ups" door, so their back button should return Home, not the Library. Replacing
// onClick at runtime cleanly drops the old (serialized) Library call without scene-event surgery.
// It also restyles the button to the shared house look: the button now goes home, but it still wore
// the old hamburger/menu icon, so "go home" looked like a different control in every screen.
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
            HomeButton.Apply(b);
        }
    }
}
