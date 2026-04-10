using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class StudentQuestionMomentController : MonoBehaviour
{
    [Header("Class Timing")]
    [Range(1, 3)] public int maxQuestionsPerClass = 3;
    public float minSecondsBeforeFirstQuestion = 15f;
    public float maxSecondsBeforeFirstQuestion = 30f;
    public float minSecondsBetweenQuestions = 20f;
    public float maxSecondsBetweenQuestions = 35f;

    [Header("Camera")]
    public float cameraDistanceFromStudent = 1.5f;
    public float cameraHeight = 1.45f;
    public float lookHeight = 1.15f;
    public float questionCameraFov = 52f;
    public float cameraTransitionDuration = 0.75f;
    public float returnDelaySeconds = 0.1f;
    public AnimationCurve transitionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("UI References")]
    public GameObject questionUiRoot;
    public TMP_Text questionText;
    public Button[] answerButtons = new Button[3];
    public TMP_Text[] answerLabels = new TMP_Text[3];

    readonly List<IClassStudent> assignedStudents = new List<IClassStudent>();
    readonly List<IClassStudent> eligibleStudents = new List<IClassStudent>();
    readonly List<int> availableQuestionIndices = new List<int>();
    readonly HashSet<int> askedStudentIdsThisClass = new HashSet<int>();
    readonly HashSet<int> usedQuestionIndicesThisClass = new HashSet<int>();
    readonly List<QuestionData> activeQuestions = new List<QuestionData>();

    GameManager gameManager;
    RollCallController rollCallController;
    QuizBoard quizBoard;
    ClassEndSequence classEndSequence;
    PlayerController playerController;
    PlayerItemSystem playerItemSystem;
    Camera playerCamera;
    MonoBehaviour playerMovementScript;

    bool classSessionActive;
    bool questionActive;
    bool pauseRequestedByQuestion;
    bool previousMovementScriptEnabled;
    bool previousItemSystemEnabled;
    bool answerSelected;
    bool uiWarningLogged;
    int selectedAnswerIndex = -1;
    float nextQuestionTimer;

    Vector3 savedCameraPosition;
    Quaternion savedCameraRotation;
    float savedCameraFov;

    void Awake()
    {
        ResolveReferences();
        ApplySequenceDefaults();
        BindAnswerButtons();
        SetUiVisible(false);
    }

    void Update()
    {
        ResolveReferences();

        if (gameManager == null)
            return;

        if (gameManager.IsClassEnded)
        {
            if (questionActive)
                ForceFinishQuestionMoment();

            ResetClassSession();
            return;
        }

        if (!classSessionActive)
        {
            if (!questionActive && gameManager.IsClassInProgress)
                BeginClassSession();

            return;
        }

        if (questionActive || !gameManager.IsClassInProgress || !CanAskAnotherQuestion())
            return;

        nextQuestionTimer -= Time.deltaTime;
        if (nextQuestionTimer <= 0f)
            TryStartRandomQuestionMoment();
    }

    void OnDisable()
    {
        ForceFinishQuestionMoment();
    }

    void ResolveReferences()
    {
        if (gameManager == null)
            gameManager = GameManager.I != null ? GameManager.I : FindObjectOfType<GameManager>();

        if (rollCallController == null)
            rollCallController = FindObjectOfType<RollCallController>();

        if (quizBoard == null && gameManager != null)
            quizBoard = gameManager.quizBoard;
        if (quizBoard == null)
            quizBoard = FindObjectOfType<QuizBoard>();

        if (classEndSequence == null && gameManager != null)
            classEndSequence = gameManager.classEndSequence;
        if (classEndSequence == null)
            classEndSequence = FindObjectOfType<ClassEndSequence>();

        if (playerController == null && classEndSequence != null && classEndSequence.playerMovementScript != null)
            playerController = classEndSequence.playerMovementScript.GetComponent<PlayerController>();
        if (playerController == null)
            playerController = FindObjectOfType<PlayerController>();

        if (playerMovementScript == null)
            playerMovementScript = classEndSequence != null && classEndSequence.playerMovementScript != null
                ? classEndSequence.playerMovementScript
                : playerController as MonoBehaviour;

        if (playerItemSystem == null && playerController != null)
            playerItemSystem = playerController.GetComponent<PlayerItemSystem>();
        if (playerItemSystem == null)
            playerItemSystem = FindObjectOfType<PlayerItemSystem>();

        if (playerCamera == null && classEndSequence != null && classEndSequence.playerCamera != null)
            playerCamera = classEndSequence.playerCamera;
        if (playerCamera == null && playerController != null && playerController.cam != null)
        {
            playerCamera = playerController.cam.GetComponent<Camera>();
            if (playerCamera == null)
                playerCamera = playerController.cam.GetComponentInChildren<Camera>(true);
        }
        if (playerCamera == null)
            playerCamera = Camera.main;
    }

    void ApplySequenceDefaults()
    {
        if (classEndSequence == null)
            return;

        if (cameraTransitionDuration <= 0f)
            cameraTransitionDuration = classEndSequence.cameraTransitionDuration;

        if (transitionCurve == null || transitionCurve.length == 0)
            transitionCurve = classEndSequence.transitionCurve;
    }

    void BindAnswerButtons()
    {
        if (answerButtons == null)
            return;

        int buttonCount = Mathf.Min(3, answerButtons.Length);
        for (int i = 0; i < buttonCount; i++)
        {
            Button button = answerButtons[i];
            if (button == null)
                continue;

            int capturedIndex = i;
            button.onClick.AddListener(() => OnAnswerButtonPressed(capturedIndex));
        }
    }

    void BeginClassSession()
    {
        classSessionActive = true;
        askedStudentIdsThisClass.Clear();
        usedQuestionIndicesThisClass.Clear();
        RefreshQuestionPoolForCurrentClass();
        ScheduleNextQuestion(true);
    }

    void ResetClassSession()
    {
        classSessionActive = false;
        nextQuestionTimer = 0f;
        askedStudentIdsThisClass.Clear();
        usedQuestionIndicesThisClass.Clear();
    }

    bool CanAskAnotherQuestion()
    {
        if (activeQuestions.Count == 0 || !HasRequiredUiReferences())
            return false;

        int allowedQuestions = Mathf.Clamp(maxQuestionsPerClass, 0, 3);
        if (askedStudentIdsThisClass.Count >= allowedQuestions)
            return false;

        if (usedQuestionIndicesThisClass.Count >= activeQuestions.Count)
            return false;

        return HasEligibleStudent();
    }

    void RefreshQuestionPoolForCurrentClass()
    {
        activeQuestions.Clear();

        if (quizBoard == null || quizBoard.CurrentStudentQuestions == null)
            return;

        QuestionData[] sourceQuestions = quizBoard.CurrentStudentQuestions;
        for (int i = 0; i < sourceQuestions.Length; i++)
        {
            QuestionData question = sourceQuestions[i];
            if (IsValidQuestion(question))
                activeQuestions.Add(question);
        }
    }

    bool HasEligibleStudent()
    {
        RefreshEligibleStudents();
        return eligibleStudents.Count > 0;
    }

    void RefreshEligibleStudents()
    {
        eligibleStudents.Clear();
        GetAssignedStudents(assignedStudents);

        for (int i = 0; i < assignedStudents.Count; i++)
        {
            IClassStudent student = assignedStudents[i];
            if (!ClassStudentUtility.IsStudentActive(student))
                continue;

            MonoBehaviour behaviour = student as MonoBehaviour;
            if (behaviour == null || askedStudentIdsThisClass.Contains(behaviour.GetInstanceID()))
                continue;

            eligibleStudents.Add(student);
        }
    }

    void GetAssignedStudents(List<IClassStudent> results)
    {
        if (results == null)
            return;

        if (rollCallController != null)
        {
            rollCallController.GetAssignedStudents(results);
            return;
        }

        ClassStudentUtility.GetObjectsImplementing(results);
    }

    void ScheduleNextQuestion(bool firstQuestion)
    {
        float min = firstQuestion ? minSecondsBeforeFirstQuestion : minSecondsBetweenQuestions;
        float max = firstQuestion ? maxSecondsBeforeFirstQuestion : maxSecondsBetweenQuestions;
        min = Mathf.Max(1f, min);
        max = Mathf.Max(min, max);
        nextQuestionTimer = Random.Range(min, max);
    }

    void TryStartRandomQuestionMoment()
    {
        if (!TrySelectStudent(out IClassStudent selectedStudent))
            return;

        if (!TrySelectQuestion(out QuestionData selectedQuestion, out int selectedQuestionIndex))
            return;

        MonoBehaviour selectedStudentBehaviour = selectedStudent as MonoBehaviour;
        if (selectedStudentBehaviour == null)
            return;

        askedStudentIdsThisClass.Add(selectedStudentBehaviour.GetInstanceID());
        usedQuestionIndicesThisClass.Add(selectedQuestionIndex);
        StartCoroutine(RunQuestionMoment(selectedStudentBehaviour.transform, selectedStudent, selectedQuestion));
    }

    bool TrySelectStudent(out IClassStudent selectedStudent)
    {
        RefreshEligibleStudents();

        if (eligibleStudents.Count == 0)
        {
            selectedStudent = null;
            return false;
        }

        selectedStudent = eligibleStudents[Random.Range(0, eligibleStudents.Count)];
        return selectedStudent != null;
    }

    bool TrySelectQuestion(out QuestionData selectedQuestion, out int questionIndex)
    {
        availableQuestionIndices.Clear();
        for (int i = 0; i < activeQuestions.Count; i++)
        {
            if (!usedQuestionIndicesThisClass.Contains(i))
                availableQuestionIndices.Add(i);
        }

        if (availableQuestionIndices.Count == 0)
        {
            selectedQuestion = null;
            questionIndex = -1;
            return false;
        }

        questionIndex = availableQuestionIndices[Random.Range(0, availableQuestionIndices.Count)];
        selectedQuestion = activeQuestions[questionIndex];
        return selectedQuestion != null;
    }

    IEnumerator RunQuestionMoment(Transform studentTransform, IClassStudent student, QuestionData question)
    {
        questionActive = true;
        answerSelected = false;
        selectedAnswerIndex = -1;
        pauseRequestedByQuestion = false;

        if (studentTransform == null || playerCamera == null)
        {
            RestoreAfterQuestion();
            yield break;
        }

        SaveCameraPose();
        PauseGameplay();
        ShowQuestionUi(student, question);

        Vector3 focusPoint = GetStudentFocusPoint(studentTransform);
        Vector3 cameraPosition = GetQuestionCameraPosition(studentTransform, focusPoint);
        Quaternion cameraRotation = Quaternion.LookRotation((focusPoint - cameraPosition).normalized, Vector3.up);

        yield return StartCoroutine(TransitionCamera(playerCamera, cameraPosition, cameraRotation, questionCameraFov, cameraTransitionDuration));

        while (questionActive && !answerSelected && !gameManager.IsClassEnded)
            yield return null;

        if (!gameManager.IsClassEnded && selectedAnswerIndex >= 0)
            ApplyAnswerResult(question);

        SetUiVisible(false);

        if (!gameManager.IsClassEnded)
        {
            if (returnDelaySeconds > 0f)
                yield return new WaitForSeconds(returnDelaySeconds);

            yield return StartCoroutine(TransitionCamera(playerCamera, savedCameraPosition, savedCameraRotation, savedCameraFov, cameraTransitionDuration));
        }

        RestoreAfterQuestion();
    }

    void PauseGameplay()
    {
        if (playerItemSystem != null)
        {
            previousItemSystemEnabled = playerItemSystem.enabled;
            playerItemSystem.CloseAllMenus();
            playerItemSystem.enabled = false;
        }

        if (playerMovementScript != null)
        {
            previousMovementScriptEnabled = playerMovementScript.enabled;
            playerMovementScript.enabled = false;
        }

        if (gameManager != null)
        {
            gameManager.classTimerPaused = true;
            pauseRequestedByQuestion = true;
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void RestoreAfterQuestion()
    {
        if (playerCamera != null)
        {
            playerCamera.transform.position = savedCameraPosition;
            playerCamera.transform.rotation = savedCameraRotation;
            playerCamera.fieldOfView = savedCameraFov;
        }

        if (playerMovementScript != null)
            playerMovementScript.enabled = previousMovementScriptEnabled;

        if (playerItemSystem != null)
            playerItemSystem.enabled = previousItemSystemEnabled;

        if (gameManager != null && pauseRequestedByQuestion && !gameManager.IsClassEnded)
            gameManager.classTimerPaused = false;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        questionActive = false;
        pauseRequestedByQuestion = false;
        answerSelected = false;
        selectedAnswerIndex = -1;
        SetUiVisible(false);

        if (classSessionActive && gameManager != null && !gameManager.IsClassEnded && CanAskAnotherQuestion())
            ScheduleNextQuestion(false);
    }

    void ForceFinishQuestionMoment()
    {
        if (!questionActive)
            return;

        StopAllCoroutines();
        RestoreAfterQuestion();
    }

    void SaveCameraPose()
    {
        if (playerCamera == null)
            return;

        savedCameraPosition = playerCamera.transform.position;
        savedCameraRotation = playerCamera.transform.rotation;
        savedCameraFov = playerCamera.fieldOfView;
    }

    Vector3 GetStudentFocusPoint(Transform studentTransform)
    {
        CapsuleCollider capsule = studentTransform.GetComponent<CapsuleCollider>();
        if (capsule != null)
            return studentTransform.TransformPoint(capsule.center + Vector3.up * Mathf.Max(0.1f, capsule.height * 0.2f));

        return studentTransform.position + Vector3.up * lookHeight;
    }

    Vector3 GetQuestionCameraPosition(Transform studentTransform, Vector3 focusPoint)
    {
        Vector3 forward = studentTransform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude <= 0.0001f)
            forward = Vector3.forward;

        forward.Normalize();
        Vector3 position = focusPoint + forward * cameraDistanceFromStudent;
        position.y = studentTransform.position.y + cameraHeight;
        return position;
    }

    IEnumerator TransitionCamera(Camera sourceCamera, Vector3 endPosition, Quaternion endRotation, float endFov, float duration)
    {
        if (sourceCamera == null)
            yield break;

        float total = Mathf.Max(0.01f, duration);
        float elapsed = 0f;
        Vector3 startPosition = sourceCamera.transform.position;
        Quaternion startRotation = sourceCamera.transform.rotation;
        float startFov = sourceCamera.fieldOfView;

        while (elapsed < total)
        {
            elapsed += Time.deltaTime;
            float normalized = Mathf.Clamp01(elapsed / total);
            float eased = transitionCurve != null ? transitionCurve.Evaluate(normalized) : normalized;

            sourceCamera.transform.position = Vector3.Lerp(startPosition, endPosition, eased);
            sourceCamera.transform.rotation = Quaternion.Slerp(startRotation, endRotation, eased);
            sourceCamera.fieldOfView = Mathf.Lerp(startFov, endFov, eased);
            yield return null;
        }

        sourceCamera.transform.position = endPosition;
        sourceCamera.transform.rotation = endRotation;
        sourceCamera.fieldOfView = endFov;
    }

    void ShowQuestionUi(IClassStudent student, QuestionData question)
    {
        if (!HasRequiredUiReferences() || question == null)
            return;

        string studentName = ClassStudentUtility.GetStudentName(student);
        questionText.text = string.IsNullOrWhiteSpace(studentName)
            ? question.question
            : studentName + " asks:\n\n" + question.question;

        int buttonCount = Mathf.Min(3, answerButtons.Length);
        for (int i = 0; i < buttonCount; i++)
        {
            bool hasAnswer = question.answers != null && i < question.answers.Length;
            if (answerButtons[i] != null)
                answerButtons[i].interactable = hasAnswer;

            if (answerLabels != null && i < answerLabels.Length && answerLabels[i] != null)
                answerLabels[i].text = hasAnswer ? question.answers[i] : string.Empty;
        }

        SetUiVisible(true);
    }

    void SetUiVisible(bool visible)
    {
        if (questionUiRoot != null)
            questionUiRoot.SetActive(visible);
    }

    void OnAnswerButtonPressed(int answerIndex)
    {
        if (!questionActive || answerSelected)
            return;

        answerSelected = true;
        selectedAnswerIndex = answerIndex;

        int buttonCount = Mathf.Min(3, answerButtons.Length);
        for (int i = 0; i < buttonCount; i++)
        {
            if (answerButtons[i] != null)
                answerButtons[i].interactable = false;
        }
    }

    void ApplyAnswerResult(QuestionData question)
    {
        if (gameManager == null)
            return;

        if (selectedAnswerIndex == question.correctIndex)
            gameManager.AddGPA(quizBoard != null ? quizBoard.gpaGainCorrect : 0.3f);
        else
            gameManager.SubGPA(quizBoard != null ? quizBoard.gpaLoseWrong : 0.2f);
    }

    bool HasRequiredUiReferences()
    {
        bool valid = questionUiRoot != null && questionText != null && answerButtons != null && answerButtons.Length >= 3;
        if (valid)
        {
            for (int i = 0; i < 3; i++)
            {
                if (answerButtons[i] == null)
                    valid = false;
            }
        }

        if (!valid && !uiWarningLogged)
        {
            uiWarningLogged = true;
            Debug.LogWarning("[StudentQuestions] Assign Question UI Root, Question Text, and 3 Answer Buttons in the Inspector.");
        }

        return valid;
    }

    bool IsValidQuestion(QuestionData question)
    {
        if (question == null || string.IsNullOrWhiteSpace(question.question))
            return false;

        if (question.answers == null || question.answers.Length < 3)
            return false;

        return question.correctIndex >= 0 && question.correctIndex <= 2;
    }
}
