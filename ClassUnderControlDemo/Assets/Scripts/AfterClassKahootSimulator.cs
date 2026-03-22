using System.Collections;
using UnityEngine;

public class AfterClassKahootSimulator : MonoBehaviour
{
    public GameManager gameManager;
    public KahootBoardView boardView;
    public QuizBoard quizBoard;

    [Header("Current Kahoot")]
    public QuestionData[] kahootQuestions;

    [Header("Upcoming Classes")]
    public UpcomingKahootClassData[] upcomingClasses = new UpcomingKahootClassData[0];

    [Header("Playback")]
    [Range(1, 20)] public int roundsToPlay = 4;
    public float questionReadSeconds = 5f;
    public float graphShowSeconds = 3f;
    public float finalMessageSeconds = 3f;
    public bool autoDetectStudents = true;
    public int fallbackStudentCount = 20;

    int nextClassIndex;

    public bool HasUpcomingKahootClasses => upcomingClasses != null && nextClassIndex < upcomingClasses.Length;

    void Awake()
    {
        ResolveReferences();
    }

    void Start()
    {
        HideBoard();
    }

    void ResolveReferences()
    {
        if (!gameManager) gameManager = GameManager.I ? GameManager.I : FindObjectOfType<GameManager>();
        if (!quizBoard && gameManager) quizBoard = gameManager.quizBoard;
        if (!quizBoard) quizBoard = FindObjectOfType<QuizBoard>();
        if (!boardView) boardView = FindObjectOfType<KahootBoardView>();
    }

    public IEnumerator RunKahoot(float classStartGpa)
    {
        ResolveReferences();

        if (boardView == null || kahootQuestions == null || kahootQuestions.Length == 0)
            yield break;

        if (quizBoard != null)
            quizBoard.gameObject.SetActive(false);

        int rounds = Mathf.Min(roundsToPlay, kahootQuestions.Length);
        int studentCount = GetStudentCount();
        float endGpa = gameManager != null ? gameManager.currentGPA : classStartGpa;
        float correctChance = Mathf.Clamp01(0.5f + (endGpa - classStartGpa) * 0.1f);

        int played = 0;
        for (int i = 0; i < kahootQuestions.Length && played < rounds; i++)
        {
            QuestionData question = kahootQuestions[i];
            if (!IsValidQuestion(question)) continue;

            boardView.ShowQuestion(question, played, rounds);
            yield return new WaitForSeconds(questionReadSeconds);

            int[] votes = SimulateVotes(studentCount, question.correctIndex, correctChance);
            boardView.ShowGraph(votes);
            yield return new WaitForSeconds(graphShowSeconds);

            played++;
        }

        boardView.ShowFinalMessage(
            $"Kahoot complete!\nStart GPA: {classStartGpa:0.0}  End GPA: {endGpa:0.0}\nCorrect chance: {(correctChance * 100f):0}%");

        if (finalMessageSeconds > 0f)
            yield return new WaitForSeconds(finalMessageSeconds);
    }

    public bool TryLoadNextKahootQuestions()
    {
        if (!HasUpcomingKahootClasses)
            return false;

        UpcomingKahootClassData nextClass = upcomingClasses[nextClassIndex];
        nextClassIndex++;

        if (nextClass != null && nextClass.kahootQuestions != null && nextClass.kahootQuestions.Length > 0)
            kahootQuestions = nextClass.kahootQuestions;

        return true;
    }

    public void HideBoard()
    {
        if (boardView != null)
            boardView.HideAll();
    }

    int GetStudentCount()
    {
        if (!autoDetectStudents)
            return Mathf.Max(1, fallbackStudentCount);

        int count = ClassStudentUtility.CountObjectsImplementing<IClassStudent>();
        if (count <= 0)
            count = fallbackStudentCount;

        return Mathf.Max(1, count);
    }

    bool IsValidQuestion(QuestionData question)
    {
        if (question == null || question.answers == null) return false;
        if (question.answers.Length < 4) return false;
        if (question.correctIndex < 0 || question.correctIndex > 3) return false;
        return true;
    }

    int[] SimulateVotes(int students, int correctIndex, float correctChance)
    {
        int[] counts = new int[4];

        for (int i = 0; i < students; i++)
        {
            int selected;
            if (Random.value < correctChance)
            {
                selected = correctIndex;
            }
            else
            {
                int wrong = Random.Range(0, 3);
                if (wrong >= correctIndex) wrong++;
                selected = wrong;
            }

            counts[selected]++;
        }

        return counts;
    }
}
