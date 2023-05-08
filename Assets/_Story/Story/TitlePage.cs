using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UIElements;

public class TitlePage : MonoBehaviour
{
    public TextMeshProUGUI txtName;
    public TextMeshProUGUI txtAuthor;
    public Button btnLink;
    public ParentalGate parentalGate;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void SetTitlePage(string title, string author, string link)
    {
        txtName.text = title;
        txtAuthor.text = author;
        parentalGate.url = link;
    }
}
