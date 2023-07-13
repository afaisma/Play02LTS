using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class UITest : MonoBehaviour
{
   public Sprite YourSprite; // Assign this in the Inspector
   public static GameObject canvasObject; 

    private void Start()
    {
        // Assume the Canvas is already created and assigned via Inspector
        Canvas canvas = canvasObject.GetComponent<Canvas>();

        // Create the Background
        GameObject background = new GameObject("Background");
        background.transform.parent = canvasObject.transform;
        Image bgImage = background.AddComponent<Image>();
        bgImage.sprite = YourSprite;

        RectTransform bgTransform = background.GetComponent<RectTransform>();
        bgTransform.anchorMin = new Vector2(0, 0);
        bgTransform.anchorMax = new Vector2(1, 1);
        bgTransform.pivot = new Vector2(0.5f, 0.5f);
        bgTransform.offsetMin = bgTransform.offsetMax = new Vector2(0, 0);
    }
    



}
