using UnityEngine;
using UnityEngine.UI;

public class RateTheApp : MonoBehaviour {
    public Button[] starButton;
    public Button rateButton;
    public Button rateLaterButton;
    public GameObject panelEmailUs;
    public MovingRatingsOptionsPanel movingRatingsOptionsPanel;
        
    public int rateValue;

    public void RateApplication(int rate)
    {
        rateValue = rate;

        // active rate button if use click some stars
        if (rateValue > 3)
            rateButton.GetComponent<Button>().interactable = true;
        else
            rateButton.GetComponent<Button>().interactable = false;
        
        // enable stars equal than user rated
        for (int i=0; i < rateValue; i++)
        {
            foreach (Transform t in starButton[i].transform)
            {
                t.gameObject.SetActive(true);
            }
        }

        // enable stars greater than user rated
        for (int i = rateValue; i < starButton.Length; i++)
        {
            foreach (Transform t in starButton[i].transform)
            {
                t.gameObject.SetActive(false);
            }

        }
    }

    public void RateLater()
    {
        movingRatingsOptionsPanel.MoveOut();
    }
    
    public void RateNow()
    {
        if (rateValue >= 5)
        {
            //Got to the app store
            movingRatingsOptionsPanel.MoveOut();
        }
        else
        {
            panelEmailUs.SetActive(true);
        }
    }
}
