using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using TMPro;

public class ButtonRangedItem : MonoBehaviour
{
    public int num = 0;
    public Button button;
    public TextMeshProUGUI text;

    private void Start()
    {
        SetNum(num);
    }

    public void SetNum(int num)
    {
        this.num = num;
        text.text = num.ToString();
    }

    public int GetNum()
    {
        return num;
    }
}
