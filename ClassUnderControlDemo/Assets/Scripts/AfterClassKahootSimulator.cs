using System.Collections;
using UnityEngine;

public class AfterClassKahootSimulator : MonoBehaviour
{
    public GameManager gameManager;
    public ClassEndSequence classEndSequence;
    public KahootBoardView boardView;
    public QuestionData[] kahootQuestions;
    [Range(1, 20)] public int roundsToPlay = 4;
    public float questionReadSeconds = 5f;
    public float graphShowSeconds = 3f;
    public float maxWaitForSequenceStart = 30f;
    public float maxWaitForSequenceEnd = 180f;
    public bool autoDetectStudents = true;
    public int fallbackStudentCount = 20;

    float classStartGpa;
    bool started;

    IEnumerator Start()
    {
        if (!gameManager) gameManager = GameManager.I ? GameManager.I : FindObjectOfType<GameManager>();
        if (!classEndSequence && gameManager) classEndSequence = gameManager.classEndSequence;
        if (!classEndSequence) classEndSequence = FindObjectOfType<ClassEndSequence>();

        if (boardView) boardView.HideAll();

        while (!gameManager)
        {
            gameManager = GameManager.I ? GameManager.I : FindObjectOfType<GameManager>();
            yield return null;
        }

        classStartGpa = gameManager.currentGPA;
        yield return StartCoroutine(WaitForClassEndAndRun());
    }

    IEnumerator WaitForClassEndAndRun()
    {
        yield return new WaitUntil(() => gameManager != null && gameManager.IsClassEnded);
        yield return StartCoroutine(WaitForClassEndSequenceToFinish());

        if (started) yield break;
        started = true;

        yield return StartCoroutine(RunKahoot());
    }

    IEnumerator WaitForClassEndSequenceToFinish()
    {
        if (!classEndSequence) yield break;

        float startTimer = maxWaitForSequenceStart;
        bool sawStart = false;

        while (startTimer > 0f)
        {
            if (IsClassEndSequenceRunning())
            {
                sawStart = true;
                break;
            }

            startTimer -= Time.deltaTime;
            yield return null;
        }

        if (!sawStart)
        {
            float fallback = EstimateSequenceDuration();
            if (fallback > 0f) yield return new WaitForSeconds(fallback);
            yield break;
        }

        float endTimer = maxWaitForSequenceEnd;
        while (endTimer > 0f && IsClassEndSequenceRunning())
        {
            endTimer -= Time.deltaTime;
            yield return null;
        }
    }

    bool IsClassEndSequenceRunning()
    {
        bool movementLocked = classEndSequence.playerMovementScript != null && !classEndSequence.playerMovementScript.enabled;
        bool panelVisible = classEndSequence.bottomPanel != null && classEndSequence.bottomPanel.activeSelf;
        bool speechPlaying = classEndSequence.speakerAudio != null && classEndSequence.speakerAudio.isPlaying;

        return movementLocked || panelVisible || speechPlaying;
    }

    float EstimateSequenceDuration()
    {
        if (!classEndSequence) return 0f;

        float wordsTime = 0f;
        if (!string.IsNullOrWhiteSpace(classEndSequence.message))
        {
            string[] words = classEndSequence.message.Split(' ');
            wordsTime = words.Length * classEndSequence.wordInterval;
        }

        float audioTime = 0f;
        if (classEndSequence.speakerAudio && classEndSequence.speakerAudio.clip)
            audioTime = classEndSequence.speakerAudio.clip.length;

        float speechBlock = Mathf.Max(wordsTime, audioTime);
        bool canTransition = classEndSequence.playerCamera != null && classEndSequence.speakerCamera != null;
        float cameraBlock = canTransition
            ? classEndSequence.cameraTransitionDuration * 2f
            : classEndSequence.camBlendExtraTime * 2f;

        return classEndSequence.delayBeforeCutscene + cameraBlock + speechBlock + 0.2f;
    }

    IEnumerator RunKahoot()
    {
        if (boardView == null || kahootQuestions == null || kahootQuestions.Length == 0)
            yield break;

        int rounds = Mathf.Min(roundsToPlay, kahootQuestions.Length);
        int studentCount = GetStudentCount();

        float endGpa = gameManager.currentGPA;

        // Base 50% + (GPA delta * 10%)
        // Example: 5.0 -> 7.0 => 50 + 20 = 70%
        // Example: 6.0 -> 4.5 => 50 - 15 = 35%
        float correctChance = Mathf.Clamp01(0.5f + (endGpa - classStartGpa) * 0.1f);

        int played = 0;
        for (int i = 0; i < kahootQuestions.Length && played < rounds; i++)
        {
            QuestionData q = kahootQuestions[i];
            if (!IsValidQuestion(q)) continue;

            boardView.ShowQuestion(q, played, rounds);
            yield return new WaitForSeconds(questionReadSeconds);

            int[] votes = SimulateVotes(studentCount, q.correctIndex, correctChance);
            boardView.ShowGraph(votes);
            yield return new WaitForSeconds(graphShowSeconds);

            played++;
        }

        boardView.ShowFinalMessage(
            $"Kahoot complete!\nStart GPA: {classStartGpa:0.0}  End GPA: {endGpa:0.0}\nCorrect chance: {(correctChance * 100f):0}%");
    }

    int GetStudentCount()
    {
        if (!autoDetectStudents)
            return Mathf.Max(1, fallbackStudentCount);

        int count = FindObjectsOfType<StudentAI>().Length + FindObjectsOfType<ThrowStudent>().Length;
        if (count <= 0) count = fallbackStudentCount;

        return Mathf.Max(1, count);
    }

    bool IsValidQuestion(QuestionData q)
    {
        if (q == null || q.answers == null) return false;
        if (q.answers.Length < 4) return false;
        if (q.correctIndex < 0 || q.correctIndex > 3) return false;
        return true;
    }

    int[] SimulateVotes(int students, int correctIndex, float correctChance)
    {
        int[] counts = new int[4];

        for (int s = 0; s < students; s++)
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
