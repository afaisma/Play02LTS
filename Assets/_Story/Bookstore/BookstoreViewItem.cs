using UnityEngine;
using  UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class BookstoreViewItem : MonoBehaviour, IPointerClickHandler
{
    [Header("UI References")]
    [SerializeField] public Image imageBook;
    [SerializeField] private TextMeshProUGUI txtBookName;
    [SerializeField] private TextMeshProUGUI txtBookAuthor;
    [SerializeField] private TextMeshProUGUI txtBookAgeGroup;

    [Header("Store Buttons")]
    [SerializeField] private Button btnPrinted;
    [SerializeField] private Button btnKindle;
    
    public PRBook prBook;

    /// <summary>
    /// Assigns the PRBook data to this view item, populates UI fields, 
    /// and configures the store buttons (printed/Kindle) as needed.
    /// </summary>
    public void SetBookProperties(PRBook prBook)
    {
        if (prBook == null)
        {
            Debug.LogWarning("PRBook provided to SetBookProperties is null.");
            return;
        }

        this.prBook = prBook;

        // Populate text fields
        txtBookName.text = prBook.bookName;
        txtBookAuthor.text = "By " + prBook.bookAuthor;
        txtBookAgeGroup.text = Globals.ageGroupLabelFromPRBook(prBook);

        // Show or hide and wire up the Printed button
        if (btnPrinted != null)
        {
            bool hasPrintedUrl = !string.IsNullOrEmpty(prBook.bookStoreUrlPrinted);
            btnPrinted.gameObject.SetActive(hasPrintedUrl);

            // Remove any previous listeners to avoid duplicating clicks
            btnPrinted.onClick.RemoveAllListeners();

            if (hasPrintedUrl)
            {
                btnPrinted.onClick.AddListener(() => OpenUrl(prBook.bookStoreUrlPrinted));
            }
    } 

        // Show or hide and wire up the Kindle button
        if (btnKindle != null)
        {
            bool hasKindleUrl = !string.IsNullOrEmpty(prBook.bookStoreUrlKindle);
            btnKindle.gameObject.SetActive(hasKindleUrl);

            // Remove any previous listeners
            btnKindle.onClick.RemoveAllListeners();

            if (hasKindleUrl)
            {
                btnKindle.onClick.AddListener(() => OpenUrl(prBook.bookStoreUrlKindle));
            }
        }
    }

    /// <summary>
    /// Assigns the sprite for the book’s cover image.
    /// </summary>
    public void SetBookImage(Sprite image)
    {
        if (imageBook != null)
        {
            imageBook.sprite = image;
        }
    }

    /// <summary>
    /// Called when user clicks on the BookstoreViewItem (anywhere, not specifically the buttons).
    /// It will navigate to the full reading or detail view of the current book.
    /// </summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        if (prBook != null)
        {
        Globals.GotoPrBook(prBook);
    }
    }
    
    /// <summary>
    /// Utility method to open a URL in the default browser.
    /// </summary>
    private void OpenUrl(string url)
    {
        if (!string.IsNullOrEmpty(url))
        {
            Debug.Log("Opening URL: " + url);
            Application.OpenURL(url);
        }
    }
}
