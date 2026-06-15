using UnityEngine;
using UnityEngine.UI;

// Optional Inspector-placed trigger. Pick an effect id + when to fire + what to
// target. Deleting Assets/FX/ leaves only a cosmetic "missing script" ref where
// this was hand-placed (cleared via Unity's "Remove Missing Scripts").
public class FXTrigger : MonoBehaviour
{
    public enum TriggerMode { OnEnable, OnButtonClick, Manual }
    public enum TargetMode { Self, RectTransform, FullScreen }

    public string effectId = "stars";
    public TriggerMode trigger = TriggerMode.OnButtonClick;
    public TargetMode targetMode = TargetMode.Self;
    public RectTransform targetRect;
    public bool decorate = false; // true → persistent Decorate instead of a one-shot burst

    private void OnEnable()
    {
        if (trigger == TriggerMode.OnButtonClick)
        {
            var btn = GetComponent<Button>();
            if (btn != null) btn.onClick.AddListener(Fire);
        }
        else if (trigger == TriggerMode.OnEnable)
        {
            Fire();
        }
    }

    private void OnDisable()
    {
        if (trigger == TriggerMode.OnButtonClick)
        {
            var btn = GetComponent<Button>();
            if (btn != null) btn.onClick.RemoveListener(Fire);
        }
    }

    // Public so other code / UnityEvents can fire it (Manual mode).
    public void Fire()
    {
        switch (targetMode)
        {
            case TargetMode.FullScreen:
                FX.Play(effectId, FXTarget.FullScreen);
                break;
            case TargetMode.RectTransform:
                if (decorate) FX.Decorate(effectId, targetRect);
                else FX.Play(effectId, targetRect);
                break;
            default: // Self
                var rt = transform as RectTransform;
                if (decorate) FX.Decorate(effectId, rt);
                else FX.Play(effectId, rt);
                break;
        }
    }
}
