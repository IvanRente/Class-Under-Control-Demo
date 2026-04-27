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

    [Header("Interaction")]
    [Tooltip("When enabled, board clicks are ignored until the class timer starts. Disable for debug testing before roll call finishes.")]
    public bool requireClassStartedToInteract = true;

    public TMP_Text questionText;
    public TMP_Text[] answerTexts;

    int currentQuestion = 0;
    int nextClassIndex = 0;
    bool classEnded = false;
    ClassBoardType currentBoardType = ClassBoardType.Quiz;

    public string CurrentSubjectName => currentSubjectName;
    public bool HasUpcomingClasses => upcomingClasses != null && nextClassIndex < upcomingClasses.Length;
    public QuestionData[] CurrentStudentQuestions => studentQuestions;
    public ClassBoardType CurrentBoardType => currentBoardType;

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
        if (!CanInteractWithBoard()) return;
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

    bool CanInteractWithBoard()
    {
        if (!requireClassStartedToInteract)
            return true;

        return GameManager.I == null || !GameManager.I.classTimerPaused;
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
        return TryAdvanceToNextClass(out nextSubjectName, out _);
    }

    public bool TryAdvanceToNextClass(out string nextSubjectName, out UpcomingQuizClassData loadedClass)
    {
        loadedClass = null;
        nextSubjectName = currentSubjectName;
        if (!HasUpcomingClasses)
            return false;

        UpcomingQuizClassData nextClass = upcomingClasses[nextClassIndex];
        nextClassIndex++;
        loadedClass = nextClass;

        if (nextClass != null && !string.IsNullOrWhiteSpace(nextClass.subjectName))
            currentSubjectName = nextClass.subjectName;

        currentBoardType = nextClass != null ? nextClass.boardType : ClassBoardType.Quiz;
        studentQuestions = nextClass != null && nextClass.studentQuestions != null ? nextClass.studentQuestions : new QuestionData[0];

        if (currentBoardType == ClassBoardType.Quiz)
        {
            QuestionData[] nextQuestions = nextClass != null && nextClass.quizQuestions != null && nextClass.quizQuestions.Length > 0
                ? nextClass.quizQuestions
                : questions;
            ResetBoard(nextQuestions);
        }
        else
        {
            currentQuestion = 0;
            classEnded = false;
            ClearAnswers();
        }

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
