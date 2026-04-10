using UnityEngine;
using TMPro;

public class QuizBoard : MonoBehaviour
{
    [Header("Current Class")]
    public string currentSubjectName = "Current Subject";
    public QuestionData[] questions;
    public QuestionData[] studentQuestions;

    [Header("Upcoming Classes")]
    public UpcomingQuizClassData[] upcomingClasses = new UpcomingQuizClassData[0];
    public float gpaGainCorrect = 0.3f;
    public float gpaLoseWrong = 0.2f;

    public TMP_Text questionText;
    public TMP_Text[] answerTexts;

    int currentQuestion = 0;
    int nextClassIndex = 0;
    bool classEnded = false;

    public string CurrentSubjectName => currentSubjectName;
    public bool HasUpcomingClasses => upcomingClasses != null && nextClassIndex < upcomingClasses.Length;
    public QuestionData[] CurrentStudentQuestions => studentQuestions;

    void Start()
    {
        ResetBoard();
    }

    void ShowQuestion()
    {
        if (classEnded)
        {
            ShowClassEndedText();
            return;
        }

        if (questions == null || questions.Length == 0)
        {
            if (questionText) questionText.text = "No questions assigned";
            ClearAnswers();
            return;
        }

        if (currentQuestion >= questions.Length)
        {
            questionText.text = "Class complete!";
            ClearAnswers();
            return;
        }

        var q = questions[currentQuestion];
        Debug.Log("Showing question " + currentQuestion + ": " + q.question);
        questionText.text = q.question;
        for (int i = 0; i < answerTexts.Length; i++)
        {
            answerTexts[i].text = (q.answers != null && i < q.answers.Length) ? q.answers[i] : "";
        }
    }

    public void AnswerButton(int index)
    {
        if (classEnded) return;
        if (GameManager.I != null && GameManager.I.classTimerPaused) return;
        if (questions == null || questions.Length == 0) return;
        if (currentQuestion >= questions.Length) return;

        Debug.Log("QuizBoard.AnswerButton called with index " + index + " | currentQuestion = " + currentQuestion);

        var q = questions[currentQuestion];
        if (index == q.correctIndex)
        {
            Debug.Log("Correct! +GPA");
            GameManager.I.AddGPA(gpaGainCorrect);
        }
        else
        {
            Debug.Log("Wrong! -GPA");
            GameManager.I.SubGPA(gpaLoseWrong);
        }

        currentQuestion++;
        Debug.Log("Next question index is now " + currentQuestion);
        ShowQuestion();
    }

    public void EndClassDisplay()
    {
        classEnded = true;
        ShowClassEndedText();
    }

    public void ResetBoard(QuestionData[] newQuestions = null)
    {
        if (newQuestions != null)
            questions = newQuestions;

        currentQuestion = 0;
        classEnded = false;
        ShowQuestion();
    }

    public bool TryAdvanceToNextClass(out string nextSubjectName)
    {
        nextSubjectName = currentSubjectName;
        if (!HasUpcomingClasses)
            return false;

        UpcomingQuizClassData nextClass = upcomingClasses[nextClassIndex];
        nextClassIndex++;

        if (!string.IsNullOrWhiteSpace(nextClass.subjectName))
            currentSubjectName = nextClass.subjectName;

        QuestionData[] nextQuestions = nextClass.questions != null && nextClass.questions.Length > 0
            ? nextClass.questions
            : questions;
        studentQuestions = nextClass.studentQuestions != null ? nextClass.studentQuestions : new QuestionData[0];

        ResetBoard(nextQuestions);
        nextSubjectName = currentSubjectName;
        return true;
    }

    void ShowClassEndedText()
    {
        if (questionText) questionText.text = "class ended";
        ClearAnswers();
    }

    void ClearAnswers()
    {
        for (int i = 0; i < answerTexts.Length; i++)
        {
            if (answerTexts[i]) answerTexts[i].text = "";
        }
    }
}
