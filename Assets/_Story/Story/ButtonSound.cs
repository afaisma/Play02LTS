using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ButtonSound : MonoBehaviour
{
    public SoundBar _soundBar;
    
    public void OnClick()
    {
        _soundBar.PlaySound(this);
    }
    
}
