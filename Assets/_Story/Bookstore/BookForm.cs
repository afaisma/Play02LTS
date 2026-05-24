using UnityEngine;
using UnityEngine.UI;
using TMPro;

[RequireComponent(typeof(RectTransform))]
public class BookFormLayout : MonoBehaviour
{
    [Header("Book Information")]
    [SerializeField] private Image bookCover;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI authorText;
    [SerializeField] private Button buyKindleButton;
    [SerializeField] private Button buyPrintedButton;

    [Header("Layout Settings")]
    [SerializeField] private float leftPadding = 10f;
    [SerializeField] private float spacingBetweenSections = 10f;

    private RectTransform formRectTransform;
    private RectTransform imageRectTransform;
    private RectTransform contentPanel;

    private void Awake()
    {
        SetupLayout();
    }

    private void SetupLayout()
    {
        formRectTransform = GetComponent<RectTransform>();

        // Create main horizontal layout
        var mainLayout = gameObject.AddComponent<HorizontalLayoutGroup>();
        mainLayout.padding = new RectOffset((int)leftPadding, 0, 0, 0);
        mainLayout.spacing = spacingBetweenSections;
        mainLayout.childAlignment = TextAnchor.MiddleLeft;
        mainLayout.childForceExpandWidth = true;
        mainLayout.childForceExpandHeight = true;

        // Setup image container
        GameObject imageContainer = new GameObject("ImageContainer", typeof(RectTransform));
        imageContainer.transform.SetParent(transform, false);
        imageRectTransform = imageContainer.GetComponent<RectTransform>();

        // Add layout element to image container
        var imageLayoutElement = imageContainer.AddComponent<LayoutElement>();
        imageLayoutElement.flexibleWidth = 0; // Fixed width for the image container
        imageLayoutElement.flexibleHeight = 1;

        // Setup book cover image
        bookCover.transform.SetParent(imageContainer.transform, false);
        bookCover.preserveAspect = true;

        // Create content container
        GameObject contentContainer = new GameObject("ContentContainer", typeof(RectTransform));
        contentContainer.transform.SetParent(transform, false);
        contentPanel = contentContainer.GetComponent<RectTransform>();

        var contentLayout = contentContainer.AddComponent<VerticalLayoutGroup>();
        contentLayout.spacing = spacingBetweenSections;
        contentLayout.childAlignment = TextAnchor.MiddleLeft;
        contentLayout.childForceExpandWidth = true;
        contentLayout.childForceExpandHeight = false;

        // Add layout element to content container
        var contentLayoutElement = contentContainer.AddComponent<LayoutElement>();
        contentLayoutElement.flexibleWidth = 1; // Remaining space
        contentLayoutElement.flexibleHeight = 1;    

        // Setup text elements
        titleText.transform.SetParent(contentPanel, false);
        authorText.transform.SetParent(contentPanel, false);

        // Setup button container
        GameObject buttonContainerObj = new GameObject("ButtonContainer", typeof(RectTransform));
        buttonContainerObj.transform.SetParent(contentPanel, false);

        var buttonGroupLayout = buttonContainerObj.AddComponent<HorizontalLayoutGroup>();
        buttonGroupLayout.spacing = spacingBetweenSections;
        buttonGroupLayout.childAlignment = TextAnchor.MiddleLeft;
        buttonGroupLayout.childForceExpandWidth = false;
        buttonGroupLayout.childForceExpandHeight = true;

        SetupButton(buyKindleButton, buttonContainerObj.transform);
        SetupButton(buyPrintedButton, buttonContainerObj.transform);

        AdjustImageSize();
    }

    private void SetupButton(Button button, Transform parent)
    {
        if (button != null)
        {
            button.transform.SetParent(parent, false);
            var btnElement = button.gameObject.AddComponent<LayoutElement>();
            btnElement.flexibleWidth = 1; // Share available width
            btnElement.minHeight = 40f;
        }
    }

    private void OnRectTransformDimensionsChange()
    {
        AdjustImageSize();
    }

    private void AdjustImageSize()
    {
        Debug.Log("Adjusting image size");
        if (imageRectTransform == null || formRectTransform == null || formRectTransform.rect.width <= 0)
            return;
        float totalWidth = formRectTransform.rect.width - leftPadding - spacingBetweenSections;
        float imageWidth = Mathf.Min(totalWidth / 2, formRectTransform.rect.height);

        imageRectTransform.sizeDelta = new Vector2(imageWidth, imageWidth);
        Debug.Log("totalWidth: " + totalWidth + " imageWidth: " + imageWidth);

    }
}
