using UnityEngine;

// Keep the _Parents page out of the device insets, exactly as PRLibrary does for the shelf: the
// scene's title is authored at the very top of the canvas (anchors 0.90-0.96), where the Dynamic
// Island clips it, and the letter's scroll view is authored flush against the title's band, so it
// has to give back exactly what the title takes. _Parents has no controller of its own, so this
// two-line behaviour is the whole script. Runtime-only; the scene is untouched on devices without
// insets (SafeAreaInsets adds nothing when there is no top inset).
public class ParentsSceneLayout : MonoBehaviour
{
    private void Start()
    {
        SafeAreaInsets.ApplyTop(GameObject.Find("txtTitle")?.transform as RectTransform);
        SafeAreaInsets.ApplyTopEdge(GameObject.Find("Scroll ViewLetterToParents")?.transform as RectTransform);
    }
}
