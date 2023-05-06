using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using QFSW.QC;
using TMPro;
using UnityEngine.UI;

class StoryStepPlate
{
    public GameObject storyPlateItemUI;
}

public class PRStageCharacter
{
    public string name;
    public GameObject characterGameObject;
}

public class StoryStepsUI : MonoBehaviour
{
    public GameObject contentPanel;
    public GameObject storyStepUIPrefab;
    public ScrollRect scrollRect;
    public GameObject buttonPrefab;
    public RectTransform buttonCanvasTransform;
    public Canvas canvasMain;

    public Image imgBackgound;
    public Image imgMain;
    public TextMeshProUGUI txtTitle;

    public PRScript prScript;
    ArrayList _alStoryPlates = new ArrayList();
    public List<PRStageCharacter> prStageCharacters = new List<PRStageCharacter>();

    private void Start()
    {
    }
    
    public void Cleanup()
    {   
        CleanupStorySteps();
    }

    [Command]
    public void CleanupStorySteps()
    {
        StoryStepUI[] instances = FindObjectsOfType<StoryStepUI>();
        foreach (StoryStepUI instance in instances)
            Destroy(instance.gameObject);
    }

    [Command]
    public void AddStoryStep(string text, int index)
    {
        StoryStepPlate storyStepPlate = new StoryStepPlate();
        GameObject goStoryStepFrame = Instantiate(storyStepUIPrefab, contentPanel.transform);
        StoryStepUI storyStepUI = goStoryStepFrame.GetComponent<StoryStepUI>();
        storyStepUI.storyStepsUI = this;
        storyStepUI.index = index;
        storyStepUI.txtScriptStep.text = text;
        storyStepUI.txtIndex.text = "" + index;
        storyStepPlate.storyPlateItemUI = goStoryStepFrame;
        _alStoryPlates.Add(storyStepPlate);
    }
    
    [Command]
    public void SelectStep(int index)
    {
        if (index >= 0 && index < _alStoryPlates.Count)
        {
            if (prScript.nCurrentStep >= 0 &&prScript.nCurrentStep < _alStoryPlates.Count && prScript.nCurrentStep != index)
                ((StoryStepPlate)_alStoryPlates[prScript.nCurrentStep]).storyPlateItemUI.GetComponent<StoryStepUI>().Hilight(false);
                
            ScrollToStoryFrame(index, true);
        }
    }
    
    public void OnExecuteStep(int index)
    {
        prScript.ExecuteStep(index);
    }
    
    [Command]
    void ScrollToStoryFrame(int index, bool bHilight = false)
    {
        if (index >= 0 && index < _alStoryPlates.Count)
            StartCoroutine(ScrollToIndex(index, bHilight));
    }

    private IEnumerator ScrollToIndex(int index, bool bHilight = false)
    {
        yield return new WaitForEndOfFrame();
        float totalHeight = contentPanel.GetComponent<RectTransform>().rect.height;
        float itemHeight = storyStepUIPrefab.GetComponent<RectTransform>().rect.height;
        float spacing = contentPanel.GetComponent<VerticalLayoutGroup>().spacing;
        float itemTopPosition = index * (itemHeight + spacing);
        float normalizedPosition = itemTopPosition / (totalHeight - itemHeight - spacing);
        scrollRect.verticalNormalizedPosition = 1 - normalizedPosition;
        GameObject goStoryFrame = ((StoryStepPlate)_alStoryPlates[index]).storyPlateItemUI;
        goStoryFrame.GetComponent<StoryStepUI>().Hilight(bHilight);    
    }
    public void NextStep()
    {
        prScript.NextStep();
    }
    

    public void CleanupBackgoundImage()
    {
        if (imgBackgound != null)
            imgBackgound.gameObject.SetActive(false);
    }

    [Command]
    public void CleanupCharButtons()
    {
        PRCharButton[] instances = FindObjectsOfType<PRCharButton>();
        foreach (PRCharButton instance in instances)
            Destroy(instance.gameObject);
    }

    [Command]
    public void DisplayTitle(string title)
    {
        txtTitle.text = title;
    }

    [Command]
    public void DisplayMainImage(string imageUrl)
    {
        StartCoroutine(PRUtils.DownloadImage(prScript.baseURL + imageUrl, imgMain));
    }

    public void DisplaybackgoundImage(string imageUrl)
    {
        CleanupBackgoundImage();
        StartCoroutine(PRUtils.DownloadImage(prScript.baseURL + imageUrl, imgBackgound.GetComponent<Image>(), false));
        imgBackgound.gameObject.SetActive(true);
    }

    [Command]
    public void AddCharacter(string name, string url, string content)
    {
        if (findPRStageCharacter(name) != null)
            return;
        
        PRStageCharacter prStageCharacter = new PRStageCharacter();
        prStageCharacter.name = name;
        GameObject characterGameObject = Instantiate(buttonPrefab, buttonCanvasTransform);
        characterGameObject.GetComponentInChildren<TextMeshProUGUI>().text = content;
        StartCoroutine(PRUtils.DownloadImage(url, characterGameObject.GetComponent<Image>()));
        prStageCharacter.characterGameObject = characterGameObject;
        prStageCharacters.Add(prStageCharacter);
    }

    public void HideAllCharactersExcept(string[] characterArray)
    {
        ArrayList leaveOnlyThese = new ArrayList();
        foreach (string character in characterArray)
        {
            leaveOnlyThese.Add(character);
        }

        foreach (PRStageCharacter prStageCharacter in prStageCharacters)
        {
            if (leaveOnlyThese.Contains(prStageCharacter.name))
            {
                prStageCharacter.characterGameObject.SetActive(true);
            }
            else
            {
                prStageCharacter.characterGameObject.SetActive(false);
            }
        }
    }
    
    PRStageCharacter findPRStageCharacter(string name)
    {
        foreach (PRStageCharacter prStageCharacter in prStageCharacters)
        {
            if (prStageCharacter.name == name)
                return prStageCharacter;
        }
        return null;
    }
    
    public Button CreateButton(float x, float y, float width,  string imageUrl, string customString = "")
    {
        //float canvasAspectRatio = (float)Screen.width / (float)Screen.height;
        float height = width; //2 * width / canvasAspectRatio;
        Button button = CreateButton(canvasMain, x, y, width, height, "", imageUrl, customString );
        return button;
    }
    
    public Button CreateButton(Canvas canvas, float x, float y, float width, float height,  string buttonText, string imageUrl, string customString)
    {
        GameObject buttonObj = new GameObject("Button");
        buttonObj.transform.SetParent(canvas.transform);

        RectTransform rectTransform = buttonObj.AddComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0, 0);
        rectTransform.anchorMax = new Vector2(0, 0);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);

        float canvasWidth = canvas.GetComponent<RectTransform>().rect.width;
        float canvasHeight = canvas.GetComponent<RectTransform>().rect.height;

        rectTransform.anchoredPosition = new Vector2(x * canvasWidth / 100, y * canvasHeight / 100);
        rectTransform.sizeDelta = new Vector2(width * canvasWidth / 100, height * canvasHeight / 100);

        Button button = buttonObj.AddComponent<Button>();
        button.targetGraphic = buttonObj.AddComponent<Image>();

        button.onClick.AddListener(() => OnButtonClick(customString));

        // Create a child GameObject for the TextMeshProUGUI component
        GameObject textObj = new GameObject("ButtonText");
        textObj.transform.SetParent(buttonObj.transform);

        // Set RectTransform of the TextMeshProUGUI component
        RectTransform textRectTransform = textObj.AddComponent<RectTransform>();
        textRectTransform.anchorMin = new Vector2(0, 0);
        textRectTransform.anchorMax = new Vector2(1, 1);
        textRectTransform.offsetMin = Vector2.zero;
        textRectTransform.offsetMax = Vector2.zero;

        // Add and configure the TextMeshProUGUI component
        TextMeshProUGUI buttonTextComponent = textObj.AddComponent<TextMeshProUGUI>();
        buttonTextComponent.text = buttonText;
        buttonTextComponent.fontSize = 18;
        buttonTextComponent.alignment = TextAlignmentOptions.Center;
        buttonTextComponent.color = Color.black;

        // Download and set the button's image
        Image buttonImage = buttonObj.GetComponent<Image>();
        if (imageUrl != "")
            StartCoroutine(PRUtils.DownloadImage(imageUrl, buttonImage));

        return button;
    }

    void OnButtonClick(string customString)
    {
        if (prScript != null)
        {
            prScript.OnButtonClick(customString);
        }
    }


}
