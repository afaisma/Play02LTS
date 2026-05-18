using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

public class ParentalGate : MonoBehaviour
{
    public TMP_Text questionText;
    public TMP_InputField answerInputField;
    public GameObject parentalGatePanel;
    public Button checkOrNextButton;
    public Button cancelButton;
    public string url = "";
    public string sceneNavigateTo = "";

    public string question =
        "Please type the number <b>twenty five</b> to proceed to the list of books.\n";
    public string answer1 = "25";
    public string answer2 = "twenty five";

    private int correctAnswer;

    void Start()
    {
        GenerateQuestionText();
        // C-R2-2: initialize to a sentinel so the default value of 0 doesn't
        // accidentally let a child bypass the counting-variant gate by typing 0
        // before any question has been generated.
        correctAnswer = -1;
        cancelButton.onClick.AddListener(Cancel);
    }

    void GenerateQuestionText()
    {
        questionText.text = question;
    }

    public void CheckAnswerText()
    {
        string playerAnswer = answerInputField.text;
        if (playerAnswer.Trim() == answer1 || playerAnswer.Trim().ToLower() == answer2)
        {
            Navigate();
        }
        else
        {
            if (checkOrNextButton !=null)
            {
                TextMeshProUGUI textMeshProUGUI = checkOrNextButton.GetComponentInChildren<TextMeshProUGUI>();
                textMeshProUGUI.text = "Try Again";
            }
            answerInputField.text = "";
        }
    }

    void GenerateQuestionCounting()
    {
        int num1 = Random.Range(1, 10);
        int num2 = Random.Range(1, 10);

        correctAnswer = num1 * num2;

        questionText.text = $"What is {num1} * {num2}?";
    }
    
    public void CheckAnswerCounting()
    {
        // C-R2-1: TryParse so non-numeric input (a child mashing keys) doesn't
        // crash the gate with FormatException. Treat invalid input as wrong.
        if (!int.TryParse(answerInputField.text, out int playerAnswer))
        {
            ShowTryAgain();
            // Make sure there's a real question to answer next time
            // (relevant on the very first invalid attempt when correctAnswer is still -1).
            GenerateQuestionCounting();
            return;
        }

        if (playerAnswer == correctAnswer)
        {
            Navigate();
        }
        else
        {
            ShowTryAgain();
            GenerateQuestionCounting();
        }
    }

    private void ShowTryAgain()
    {
        answerInputField.text = "";
        if (checkOrNextButton != null)
        {
            TextMeshProUGUI textMeshProUGUI = checkOrNextButton.GetComponentInChildren<TextMeshProUGUI>();
            if (textMeshProUGUI != null)
                textMeshProUGUI.text = "Try Again";
        }
    }

    public void Navigate()
    {
        if (sceneNavigateTo != "")
        {
            SceneManager.LoadScene(sceneNavigateTo);
        }
        else
        {
            Application.OpenURL(url);
        }

        // L-R2-1: hide the gate panel after a successful answer so it isn't
        // still visible when the user returns from the external URL.
        if (parentalGatePanel != null)
            parentalGatePanel.SetActive(false);
    }
    
    public void Cancel()
    {
        parentalGatePanel.SetActive(false);
    }
}