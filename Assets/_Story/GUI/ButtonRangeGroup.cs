using System.Collections;
using System.Collections.Generic;
using QFSW.QC;
using UnityEngine;
using UnityEngine.Serialization;

public class ButtonRangeGroup : MonoBehaviour
{
    [FormerlySerializedAs("toggles")] public ButtonRangedItem[] buttons;
    public int from = 0;
    public int to = 0;
    
    private void Start()
    {
        foreach (ButtonRangedItem buttonRangedItem in buttons)
        {
            if (buttonRangedItem == null)
                continue;
            buttonRangedItem.button.onClick.AddListener( () =>
            {
                ButtonClicked(buttonRangedItem);
           });
        }
        SetRange(buttons[0].GetNum(), buttons[buttons.Length - 1].GetNum());
    }

    private void ButtonClicked(ButtonRangedItem clickedOn)
    {
        int clicked = clickedOn.GetNum();
        
        if (clicked < from)
        {
            from = clicked;
            SetRange(from, to);
        }
        else if (clicked > to)
        {
            to = clicked;
            SetRange(from, to);
        }
        else
        {
            if (clicked == from)
            {
                from++;
                SetRange(from, to);
            }
            else if (clicked == to)
            {
                to--;
                SetRange(from, to);
            }
        }
        
    }

    [Command]
    public void SetRange(int from, int to)
    {
        this.from = from;
        this.to = to;
        Debug.Log("SetRangeRange from " + from + " to " + to);   
        foreach (ButtonRangedItem buttonRangedItem in buttons)
        {
            if (buttonRangedItem == null)
                continue;
            if (buttonRangedItem.GetNum() >= from && buttonRangedItem.GetNum() <= to)
                PRUtils.SetImageAlpha(buttonRangedItem.button.image, 255);
            else
                PRUtils.SetImageAlpha(buttonRangedItem.button.image, 128);
        }

    }
}