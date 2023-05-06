using TMPro;
using UnityEngine;

public class StoryStepUI : MonoBehaviour
{ 
    public StoryStepsUI storyStepsUI;
    public int index; 
    public TextMeshProUGUI txtScriptStep;
    public TextMeshProUGUI txtIndex;

    public void Hilight(bool bHilight)
    {
        Transform childTransform = transform.Find("HilightImage");
        childTransform.gameObject.SetActive(bHilight);
    }
    
    public void OnExecuteStep()
    {
        storyStepsUI.OnExecuteStep(index);
    }
}
