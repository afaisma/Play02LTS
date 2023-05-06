using UnityEngine;

public class AlertDialogManager : MonoBehaviour
{
    [SerializeField] private GameObject alertDialogPrefab;
    [SerializeField] private Canvas targetCanvas;

    public static AlertDialogManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void ShowAlertDialog(string message)
    {
        GameObject alertDialogInstance = Instantiate(alertDialogPrefab, targetCanvas.transform);
        AlertDialog alertDialog = alertDialogInstance.GetComponent<AlertDialog>();
        alertDialog.Show(message);
    }
    
}