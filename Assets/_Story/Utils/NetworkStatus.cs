using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
public class NetworkStatus : MonoBehaviour
{
    public GameObject _canvasNetworkStatus;

    public void onNetworkStatusChange(bool isConnected)
    {
        Debug.Log("onNetworkStatusChange " + isConnected);
        if (_canvasNetworkStatus != null)
            _canvasNetworkStatus.SetActive(!isConnected);
    }
}
