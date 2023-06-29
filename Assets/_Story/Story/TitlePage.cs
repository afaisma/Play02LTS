using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TitlePage : MonoBehaviour
{
    public TextMeshProUGUI txtName;
    public TextMeshProUGUI txtAuthor;
    public Button buttonLink;
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
        if (link == "")
            buttonLink.gameObject.SetActive(false);
        else
            buttonLink.gameObject.SetActive(true);

        txtName.text = title;
        txtAuthor.text = author;
        parentalGate.url = link;
    }
}
