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
    [SerializeField] public Image imageStatus;
    [SerializeField] public  TextMeshProUGUI txtBookName;
    [SerializeField] public  TextMeshProUGUI txtBookAuthor;
    [SerializeField] public  TextMeshProUGUI txtBookAgeGroup;
    public PRBook prBook;
    
    public void SetBookImage(Sprite image)
    {
        imageBook.sprite = image;
    }
    public void SetBookProperties(PRBook prBook)
    {
        txtBookName.text = prBook.bookName;
        txtBookAuthor.text = prBook.bookAuthor;
        // Learn-to-read ladder books show "Level N" and a band color; all others keep the
        // age-range label and the pastel-by-number tint, byte-identical to before.
        if (prBook.level > 0)
        {
            txtBookAgeGroup.text = $"Level {prBook.level}";
            imageBaclground.color = LevelBandColor(prBook.level);
        }
        else
        {
            txtBookAgeGroup.text = Globals.ageGroupLabelFromPRBook(prBook);
            imageBaclground.color = PRUtils.GetNthPastelColor(prBook.number);//PRUtils.textToColor(prBook.bookName);
        }
        //Color opppositeColor = PRUtils.GetOppositeColor(imageBaclground.color);
        //txtBookName.color =  PRUtils.DarkenColorByPercentage(opppositeColor, 0.4f);
        txtBookName.color = new Color(0.4f, 0.15f, 0.15f, 1f);
        imageStatus.gameObject.SetActive(prBook.currentPage != 0);
        imageStatus.color = this.prBook.book_done != 0 ? new Color(0.40f, 1f, 0.40f, 1f) : new Color(0.10f, 0.7f, 0.10f, 1f);
    } 

    // Band color per learn-to-read level (agreed convention): L1 pink, L2 orange,
    // L3 green, L4 blue. Any out-of-range level falls back to the pastel-by-number tint.
    private Color LevelBandColor(int level)
    {
        switch (level)
        {
            case 1: return PRUtils.StringToColor("F4C0D1");
            case 2: return PRUtils.StringToColor("FAC775");
            case 3: return PRUtils.StringToColor("C0DD97");
            case 4: return PRUtils.StringToColor("B5D4F4");
            default: return PRUtils.GetNthPastelColor(prBook.number);
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        // M-R4-1: guard against a tap on an item that hasn't been populated yet
        // (rare race during catalog load on slow devices). Globals.GotoPrBook
        // dereferences prBook.bookFullUrl immediately and would NRE on null.
        // BookstoreViewItem.OnPointerClick already has this guard.
        if (prBook != null)
            Globals.GotoPrBook(prBook);
    }
    
}
