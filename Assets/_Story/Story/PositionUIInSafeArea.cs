using UnityEngine;
using UnityEngine.Serialization;

// The scene-side holder of the reader's three vertical bands. The split math itself moved into
// StoryLayoutTuning (pure + unit-tested), which also keeps the art from starving the text on a
// 4:3 tablet and re-applies on rotation — something this one-shot Start() never did.
public class PositionUIInSafeArea : MonoBehaviour
{
    public RectTransform gallery;
    public RectTransform text;
    public RectTransform toolbar;

    private void Start()
    {
        StoryLayoutTuning.Install(gallery, text, toolbar);
    }
}
