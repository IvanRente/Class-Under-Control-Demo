using UnityEngine;
using TMPro;

public class QuizBoard : MonoBehaviour
{
    public QuestionData[] questions;
    public float gpaGainCorrect = 0.3f;
    public float gpaLoseWrong = 0.2f;

    public TMP_Text questionText;
    public TMP_Text[] answerTexts;

    int currentQuestion = 0;

    void Start()
    {
        ShowQuestion();
    }

    void ShowQuestion()
    {
        if (currentQuestion >= questions.Length)
        {
            questionText.text = "Class complete!";
            for (int i = 0; i < answerTexts.Length; i++)
                answerTexts[i].text = "";
            return;
        }

        var q = questions[currentQuestion];
        questionText.text = q.question;
        for (int i = 0; i < 3; i++)
        {
            answerTexts[i].text = q.answers[i];
        }
    }

    public void AnswerButton(int index)
    {
        var q = questions[currentQuestion];
        if (index == q.correctIndex)
        {
            GameManager.I.AddGPA(gpaGainCorrect);
        }
        else
        {
            GameManager.I.SubGPA(gpaLoseWrong);
        }

        currentQuestion++;
        ShowQuestion();
    }
}
