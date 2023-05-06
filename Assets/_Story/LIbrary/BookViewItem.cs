using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using  UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

public class BookViewItem : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] public Image imageBook;
    [SerializeField] public Image imageBaclground;
    [SerializeField] public  TextMeshProUGUI txtBookName;
    [SerializeField] public  TextMeshProUGUI txtBookAuthor;
    public PRBook prBook;
    
    public void SetBookImage(Sprite image)
    {
        imageBook.sprite = image;
    }
    public void SetBookNameAndAuthor(PRBook prBook)
    {
        txtBookName.text = prBook.bookName;
        txtBookAuthor.text = prBook.bookAuthor;
        imageBaclground.color = PRUtils.textToColor(prBook.bookName);
        Color opppositeColor = PRUtils.GetOppositeColor(imageBaclground.color);
        txtBookName.color =  PRUtils.DarkenColorByPercentage(opppositeColor, 0.4f);
        txtBookName.color = new Color(0.4f, 0.15f, 0.15f, 1f);
    } //

    public void OnPointerClick(PointerEventData eventData)
    {
        Globals.g_scriptName = prBook.bookFullUrl;
        SceneManager.LoadScene("_Story");
    }
}
