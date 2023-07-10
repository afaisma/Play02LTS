using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ParentalGate : MonoBehaviour
{
    public TMP_Text questionText;
    public TMP_InputField answerInputField;
    public GameObject parentalGatePanel;
    public Button cancelButton;
    public string url = "";

    public string question =
        "Please type in the number twenty five to proceed to the Amazon store to purchase the paper book or the ebook.";
    public string answer1 = "25";
    public string answer2 = "twenty five";

    private int correctAnswer;

    void Start()
    {
        GenerateQuestionText();
        cancelButton.onClick.AddListener(Cancel);
    }

    void GenerateQuestionText()
    {
        questionText.text = question;
    }

    public void CheckAnswerText()
    {
        string playerAnswer = answerInputField.text;
        if (playerAnswer.Trim() == answer1 || playerAnswer.Trim() == answer2)
        {
            Application.OpenURL(url);
            parentalGatePanel.SetActive(false);
        }
        else
        {
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
        int playerAnswer = int.Parse(answerInputField.text);

        if (playerAnswer == correctAnswer)
        {
            Application.OpenURL(url);
            parentalGatePanel.SetActive(false);
        }
        else
        {
            answerInputField.text = "";
            GenerateQuestionCounting();
        }
    }

    public void Cancel()
    {
        parentalGatePanel.SetActive(false);
    }
}