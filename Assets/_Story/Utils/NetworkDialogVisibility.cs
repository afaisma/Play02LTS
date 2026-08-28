// ============================================================================================
// The "should the offline dialog be on screen?" rule, factored out of NetworkStatus so it can be
// reasoned about (and tested) without a scene.
//
// NetworkStatus probes the CDN every 5 seconds, so a naive "offline -> show" would re-open the
// dialog every 5 seconds after the user has deliberately closed it. Dismissing therefore latches:
// the dialog stays down until connectivity comes BACK (which clears the latch) and drops again.
// ============================================================================================
public class NetworkDialogVisibility
{
    private bool dismissed;

    /// <summary>True while a deliberate dismiss is suppressing the dialog.</summary>
    public bool Dismissed => dismissed;

    /// <summary>
    /// Fold one connectivity poll into the rule and return whether the dialog should be visible.
    /// Regaining connectivity always hides it and clears any dismiss.
    /// </summary>
    public bool OnStatusChange(bool isConnected)
    {
        if (isConnected)
        {
            dismissed = false;
            return false;
        }
        return !dismissed;
    }

    /// <summary>The user closed the dialog: stay quiet until connectivity returns and drops again.</summary>
    public void Dismiss()
    {
        dismissed = true;
    }
}
