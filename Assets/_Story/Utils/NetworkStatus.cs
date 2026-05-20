using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Serialization;

public class NetworkStatus : MonoBehaviour
{
    private NetworkReachability lastReachability;
    public float checkFrequency = 5f;  // Check every 5 seconds.
    [FormerlySerializedAs("_canvasNetworkStatus")] public GameObject _networkStatusDialog;

    private void Start()
    {
        lastReachability = Application.internetReachability;
        ShowDialog(false);
        StartCoroutine(CheckInternetConnection());
    }

    public IEnumerator TryAgain()
    {
        lastReachability = Application.internetReachability;

        // Probe the actual CDN we depend on, not a third-party host. If the
        // device has internet but our CDN is down (WAF block, S3 outage,
        // DNS), we want the offline dialog up — and conversely we don't
        // want to claim offline just because google.com is unreachable.
        // HEAD avoids re-downloading the CSV body on every poll.
        string probeUrl = !string.IsNullOrEmpty(Globals.CSVURL)
            ? Globals.CSVURL
            : "http://d5wtw8f0w3ire.cloudfront.net/uploads/stories_02/stories.csv";

        using (UnityWebRequest request = UnityWebRequest.Head(probeUrl))
        {
            request.timeout = 5;  // connectivity probe; short timeout so the
                                  // poll itself doesn't hang on a dead network
            yield return request.SendWebRequest();

            bool cdnReachable = request.result == UnityWebRequest.Result.Success;

            if (cdnReachable && Application.internetReachability != NetworkReachability.NotReachable)
            {
                onNetworkStatusChange(true);
            }
            else
            {
                Debug.Log($"NetworkStatus: CDN probe failed ({request.result}) for {probeUrl}");
                onNetworkStatusChange(false);
            }
        }

    }
    private IEnumerator CheckInternetConnection()
    {
        while (true)
        {
            //if (lastReachability != Application.internetReachability)
            {
                StartCoroutine(TryAgain());
            }

            yield return new WaitForSeconds(checkFrequency);
        }
    }

    public void ShowDialog(bool bShow)
    {
        if (bShow)
            _networkStatusDialog.SetActive(true);
        else
            _networkStatusDialog.SetActive(false);
    }
    
    public void onNetworkStatusChange(bool isConnected)
    {
        //Debug.Log("onNetworkStatusChange " + isConnected);
        if (_networkStatusDialog != null)
            ShowDialog(!isConnected);
    }

    public void OnTryAgainClickede()
    {
        ShowDialog(false);  
        StartCoroutine(TryAgain());
    }
}


