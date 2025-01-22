using UnityEngine;

public class AlertDialogManager : MonoBehaviour
{
    [SerializeField] private GameObject alertDialogPrefab;
    [SerializeField] private Canvas targetCanvas;
    public AlertDialog alertDialog;
    static GameObject alertDialogInstance = null;
    public static AlertDialogManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            //DontDestroyOnLoad(gameObject);
        }
        else
        {
            //Destroy(gameObject);
        }
    }
    
    public static void DestroyDialogInstance() {
        if (alertDialogInstance != null)
        {
            Destroy(alertDialogInstance);
            alertDialogInstance = null;
        }
    }
    

    public void ShowAlertDialog(string message)
    {
        DestroyDialogInstance();

        alertDialogInstance = Instantiate(alertDialogPrefab, targetCanvas.transform);
        alertDialog = alertDialogInstance.GetComponent<AlertDialog>();
        alertDialog.Show(message);
    }
    
    public void CloseAlertDialog()
    {
        if (alertDialog != null)
            alertDialog.Close();
    }
}