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
        using (UnityWebRequest request = UnityWebRequest.Get("http://www.google.com"))
        {
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                switch (Application.internetReachability)
                {
                    case NetworkReachability.NotReachable:
                        Debug.Log("No internet connection");
                        // Display your message that there is no internet connection here
                        onNetworkStatusChange(false);
                        break;
                    case NetworkReachability.ReachableViaCarrierDataNetwork:
                        //Debug.Log("Internet connection via Carrier Data Network");
                        onNetworkStatusChange(true);
                        break;
                    case NetworkReachability.ReachableViaLocalAreaNetwork:
                        //Debug.Log("Internet connection via Local Area Network");
                        onNetworkStatusChange(true);
                        break;
                }
            }
            else
            {
                Debug.Log("No internet connection");
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


