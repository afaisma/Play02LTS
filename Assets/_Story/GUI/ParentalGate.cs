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

    private int correctAnswer;

    void Start()
    {
        GenerateQuestion();
        cancelButton.onClick.AddListener(Cancel);
    }

    void GenerateQuestion()
    {
        int num1 = Random.Range(1, 10);
        int num2 = Random.Range(1, 10);

        correctAnswer = num1 * num2;

        questionText.text = $"What is {num1} * {num2}?";
    }

    public void CheckAnswer()
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
            GenerateQuestion();
        }
    }

    public void Cancel()
    {
        parentalGatePanel.SetActive(false);
    }
}