using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ClassTransitionFlowController : MonoBehaviour
{
    public enum DoorHingeSide
    {
        Left,
        Right
    }

    enum FlowState
    {
        WaitingForClassEnd,
        RunningKahoot,
        FreeRoam,
        StartingNextClass
    }

    public GameManager gameManager;
    public AfterClassKahootSimulator afterClassKahootSimulator;
    public RollCallController rollCallController;
    public QuizBoard quizBoard;
    public HistoryTimelineBoard historyTimelineBoard;
    public ClassEndSequence classEndSequence;
    public ClassTransitionScreen transitionScreen;

    [Header("Manual Scene References")]
    public Transform classroomDoor;
    public Transform classroomExitPoint;
    public ClassStartButton startClassButton;

    [Header("Door")]
    public float doorOpenAngle = 95f;
    public float doorAnimationDuration = 0.75f;
    public DoorHingeSide doorHingeSide = DoorHingeSide.Left;
    public float doorHingeInset = 0.02f;
    public bool useCustomDoorHingeOffset;
    public Vector3 customDoorHingeLocalOffset;

    [Header("Class End Timing")]
    public float maxWaitForSequenceStart = 30f;
    public float maxWaitForSequenceEnd = 180f;

    readonly List<IClassStudent> assignedStudents = new List<IClassStudent>();

    PlayerController playerController;
    PlayerItemSystem playerItemSystem;
    Transform playerTransform;

    Vector3 doorClosedLocalPosition;
    Quaternion doorClosedLocalRotation;
    Vector3 doorHingeLocalOffset;
    Vector3 classroomPlayerPosition;
    Quaternion classroomPlayerRotation;

    Coroutine doorAnimationCoroutine;
    float currentDoorAngle;
    bool doorPoseCaptured;
    bool startNextClassRequested;
    float classStartGpa;

    FlowState state = FlowState.WaitingForClassEnd;

    public bool CanStartNextClassFromButton => state == FlowState.FreeRoam && !startNextClassRequested;

    void Start()
    {
        ResolveReferences();
        CacheInitialSceneState();

        if (startClassButton != null)
            startClassButton.SetFlowController(this);

        SetStartButtonVisible(false);

        if (afterClassKahootSimulator != null)
            afterClassKahootSimulator.HideBoard();
        if (historyTimelineBoard != null)
            historyTimelineBoard.gameObject.SetActive(false);

        classStartGpa = gameManager != null ? gameManager.currentGPA : 0f;
        StartCoroutine(RunFlow());
    }

    void ResolveReferences()
    {
        if (!gameManager) gameManager = GameManager.I ? GameManager.I : FindObjectOfType<GameManager>();
        if (!afterClassKahootSimulator) afterClassKahootSimulator = FindObjectOfType<AfterClassKahootSimulator>();
        if (!rollCallController) rollCallController = FindObjectOfType<RollCallController>();
        if (!quizBoard && gameManager) quizBoard = gameManager.quizBoard;
        if (!quizBoard) quizBoard = FindObjectOfType<QuizBoard>();
        if (!historyTimelineBoard && gameManager) historyTimelineBoard = gameManager.historyTimelineBoard;
        if (!historyTimelineBoard) historyTimelineBoard = FindFirstObjectByType<HistoryTimelineBoard>(FindObjectsInactive.Include);
        if (!classEndSequence && gameManager) classEndSequence = gameManager.classEndSequence;
        if (!classEndSequence) classEndSequence = FindObjectOfType<ClassEndSequence>();
        if (!transitionScreen) transitionScreen = FindObjectOfType<ClassTransitionScreen>();

        if (!playerController && classEndSequence != null)
            playerController = classEndSequence.playerMovementScript as PlayerController;
        if (!playerController)
            playerController = FindObjectOfType<PlayerController>();

        if (playerController != null)
        {
            playerTransform = playerController.transform;
            playerItemSystem = playerController.GetComponent<PlayerItemSystem>();
        }
        else if (classEndSequence != null && classEndSequence.playerMovementScript != null)
        {
            playerTransform = classEndSequence.playerMovementScript.transform;
            playerItemSystem = playerTransform.GetComponent<PlayerItemSystem>();
        }
    }

    void CacheInitialSceneState()
    {
        if (playerTransform != null)
        {
            classroomPlayerPosition = playerTransform.position;
            classroomPlayerRotation = playerTransform.rotation;
        }

        if (classroomDoor != null)
        {
            doorClosedLocalPosition = classroomDoor.localPosition;
            doorClosedLocalRotation = classroomDoor.localRotation;
            doorHingeLocalOffset = ResolveDoorHingeLocalOffset();
            currentDoorAngle = 0f;
            doorPoseCaptured = true;
        }
    }

    IEnumerator RunFlow()
    {
        while (true)
        {
            state = FlowState.WaitingForClassEnd;
            yield return new WaitUntil(() => gameManager != null && gameManager.IsClassEnded);
            yield return StartCoroutine(WaitForClassEndSequenceToFinish());

            state = FlowState.RunningKahoot;
            if (afterClassKahootSimulator != null)
                yield return StartCoroutine(afterClassKahootSimulator.RunKahoot(classStartGpa));

            if (quizBoard == null || !quizBoard.HasUpcomingClasses)
            {
                if (gameManager != null)
                    gameManager.WinGame();
                yield break;
            }

            EnterFreeRoamState();

            yield return new WaitUntil(() => startNextClassRequested);
            startNextClassRequested = false;

            state = FlowState.StartingNextClass;
            yield return StartCoroutine(StartNextClassTransition());
            classStartGpa = gameManager != null ? gameManager.currentGPA : classStartGpa;
        }
    }

    IEnumerator WaitForClassEndSequenceToFinish()
    {
        if (!classEndSequence)
            yield break;

        float startTimer = maxWaitForSequenceStart;
        bool sawSequenceStart = false;

        while (startTimer > 0f)
        {
            if (IsClassEndSequenceRunning())
            {
                sawSequenceStart = true;
                break;
            }

            startTimer -= Time.deltaTime;
            yield return null;
        }

        if (!sawSequenceStart)
            yield break;

        float endTimer = maxWaitForSequenceEnd;
        while (endTimer > 0f && IsClassEndSequenceRunning())
        {
            endTimer -= Time.deltaTime;
            yield return null;
        }
    }

    bool IsClassEndSequenceRunning()
    {
        if (classEndSequence == null)
            return false;

        bool movementLocked = classEndSequence.playerMovementScript != null && !classEndSequence.playerMovementScript.enabled;
        bool panelVisible = classEndSequence.bottomPanel != null && classEndSequence.bottomPanel.activeSelf;
        bool speechPlaying = classEndSequence.speakerAudio != null && classEndSequence.speakerAudio.isPlaying;
        return movementLocked || panelVisible || speechPlaying;
    }

    void EnterFreeRoamState()
    {
        if (afterClassKahootSimulator != null)
            afterClassKahootSimulator.HideBoard();

        if (quizBoard != null)
            quizBoard.gameObject.SetActive(false);
        if (historyTimelineBoard != null)
            historyTimelineBoard.gameObject.SetActive(false);

        OpenDoor();
        SendStudentsOutOfClass();
        SetStartButtonVisible(true);
        state = FlowState.FreeRoam;
    }

    IEnumerator StartNextClassTransition()
    {
        SetStartButtonVisible(false);
        SetPlayerMovementEnabled(false);

        if (playerItemSystem != null)
            playerItemSystem.CloseAllMenus();

        string nextSubjectName;
        UpcomingQuizClassData nextClassData;
        if (quizBoard == null || !quizBoard.TryAdvanceToNextClass(out nextSubjectName, out nextClassData))
        {
            if (gameManager != null)
                gameManager.WinGame();
            yield break;
        }

        if (afterClassKahootSimulator != null)
            afterClassKahootSimulator.TryLoadNextKahootQuestions();

        Action onBlackReached = () =>
        {
            CleanupTemporaryObjects();
            CloseDoorImmediate();
            ResetStudentsForNewClass();

            if (gameManager != null)
                gameManager.PrepareForNewClassCycle();

            ApplyBoardForClass(nextClassData);

            if (rollCallController != null)
                rollCallController.PrepareForNewClass();

            TeleportPlayerBackToClassroom();
        };

        if (transitionScreen != null)
            yield return StartCoroutine(transitionScreen.PlayTransition(nextSubjectName, onBlackReached));
        else
            onBlackReached?.Invoke();

        SetPlayerMovementEnabled(true);
    }

    void ApplyBoardForClass(UpcomingQuizClassData nextClassData)
    {
        ClassBoardType boardType = nextClassData != null ? nextClassData.boardType : ClassBoardType.Quiz;
        bool showHistoryBoard = boardType == ClassBoardType.HistoryTimeline;

        if (historyTimelineBoard != null)
        {
            if (showHistoryBoard)
                historyTimelineBoard.LoadClassData(nextClassData != null ? nextClassData.historyTimelineData : null);

            historyTimelineBoard.gameObject.SetActive(showHistoryBoard);
        }

        if (quizBoard != null)
            quizBoard.gameObject.SetActive(!showHistoryBoard);
    }

    public void RequestStartNextClass()
    {
        if (!CanStartNextClassFromButton)
            return;

        startNextClassRequested = true;
        SetStartButtonVisible(false);
    }

    void SendStudentsOutOfClass()
    {
        GetAssignedStudents();
        for (int i = 0; i < assignedStudents.Count; i++)
        {
            if (assignedStudents[i] != null)
                assignedStudents[i].LeaveClassroom(classroomExitPoint);
        }
    }

    void ResetStudentsForNewClass()
    {
        if (rollCallController != null && rollCallController.UsesStudentTypeDecisionTree)
            return;

        GetAssignedStudents();
        for (int i = 0; i < assignedStudents.Count; i++)
        {
            if (assignedStudents[i] != null)
                assignedStudents[i].PrepareForNewClass();
        }
    }

    void GetAssignedStudents()
    {
        if (rollCallController != null)
        {
            rollCallController.GetAssignedStudents(assignedStudents);
            return;
        }

        assignedStudents.Clear();
        ClassStudentUtility.GetObjectsImplementing(assignedStudents);
    }

    void CleanupTemporaryObjects()
    {
        FireHazard[] fires = FindObjectsOfType<FireHazard>();
        for (int i = 0; i < fires.Length; i++)
            Destroy(fires[i].gameObject);

        PaperProjectile[] projectiles = FindObjectsOfType<PaperProjectile>();
        for (int i = 0; i < projectiles.Length; i++)
            Destroy(projectiles[i].gameObject);
    }

    void TeleportPlayerBackToClassroom()
    {
        if (playerController != null)
        {
            playerController.TeleportTo(classroomPlayerPosition, classroomPlayerRotation);
            return;
        }

        if (playerTransform == null)
            return;

        CharacterController characterController = playerTransform.GetComponent<CharacterController>();
        bool wasEnabled = characterController != null && characterController.enabled;

        if (wasEnabled)
            characterController.enabled = false;

        playerTransform.SetPositionAndRotation(classroomPlayerPosition, classroomPlayerRotation);

        if (wasEnabled)
            characterController.enabled = true;
    }

    void SetPlayerMovementEnabled(bool enabled)
    {
        if (classEndSequence != null && classEndSequence.playerMovementScript != null)
        {
            classEndSequence.playerMovementScript.enabled = enabled;
            return;
        }

        if (playerController != null)
            playerController.enabled = enabled;
    }

    void OpenDoor()
    {
        if (classroomDoor == null)
            return;

        CaptureDoorClosedPose();

        if (doorAnimationCoroutine != null)
            StopCoroutine(doorAnimationCoroutine);

        doorAnimationCoroutine = StartCoroutine(AnimateDoor(currentDoorAngle, doorOpenAngle, doorAnimationDuration));
    }

    void CloseDoorImmediate()
    {
        if (classroomDoor == null)
            return;

        CaptureDoorClosedPose();

        if (doorAnimationCoroutine != null)
        {
            StopCoroutine(doorAnimationCoroutine);
            doorAnimationCoroutine = null;
        }

        ApplyDoorPose(0f);
    }

    void CaptureDoorClosedPose()
    {
        if (classroomDoor == null || doorPoseCaptured)
            return;

        doorClosedLocalPosition = classroomDoor.localPosition;
        doorClosedLocalRotation = classroomDoor.localRotation;
        doorHingeLocalOffset = ResolveDoorHingeLocalOffset();
        currentDoorAngle = 0f;
        doorPoseCaptured = true;
    }

    IEnumerator AnimateDoor(float startAngle, float targetAngle, float duration)
    {
        if (classroomDoor == null)
            yield break;

        float total = Mathf.Max(0.01f, duration);
        float timer = 0f;

        while (timer < total)
        {
            timer += Time.deltaTime;
            float progress = Mathf.Clamp01(timer / total);
            float angle = Mathf.Lerp(startAngle, targetAngle, progress);
            ApplyDoorPose(angle);
            yield return null;
        }

        ApplyDoorPose(targetAngle);
        doorAnimationCoroutine = null;
    }

    void SetStartButtonVisible(bool visible)
    {
        if (startClassButton != null)
            startClassButton.gameObject.SetActive(visible);
    }

    void ApplyDoorPose(float angle)
    {
        if (classroomDoor == null)
            return;

        Quaternion localRotation = doorClosedLocalRotation * Quaternion.Euler(0f, angle, 0f);
        Vector3 closedHingePosition = doorClosedLocalPosition + doorClosedLocalRotation * doorHingeLocalOffset;
        Vector3 localPosition = closedHingePosition - localRotation * doorHingeLocalOffset;

        classroomDoor.localPosition = localPosition;
        classroomDoor.localRotation = localRotation;
        currentDoorAngle = angle;
    }

    Vector3 ResolveDoorHingeLocalOffset()
    {
        if (useCustomDoorHingeOffset)
            return customDoorHingeLocalOffset;

        Bounds localBounds;
        if (!TryGetDoorLocalBounds(out localBounds))
            return Vector3.zero;

        float hingeX = doorHingeSide == DoorHingeSide.Left
            ? localBounds.min.x + doorHingeInset
            : localBounds.max.x - doorHingeInset;

        return new Vector3(hingeX, localBounds.center.y, localBounds.center.z);
    }

    bool TryGetDoorLocalBounds(out Bounds localBounds)
    {
        localBounds = default;
        bool hasBounds = false;

        Renderer[] renderers = classroomDoor.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
            EncapsulateWorldBounds(renderers[i].bounds, ref hasBounds, ref localBounds);

        if (!hasBounds)
        {
            Collider[] colliders = classroomDoor.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
                EncapsulateWorldBounds(colliders[i].bounds, ref hasBounds, ref localBounds);
        }

        return hasBounds;
    }

    void EncapsulateWorldBounds(Bounds worldBounds, ref bool hasBounds, ref Bounds localBounds)
    {
        Vector3 min = worldBounds.min;
        Vector3 max = worldBounds.max;

        Vector3[] corners =
        {
            new Vector3(min.x, min.y, min.z),
            new Vector3(max.x, min.y, min.z),
            new Vector3(min.x, max.y, min.z),
            new Vector3(max.x, max.y, min.z),
            new Vector3(min.x, min.y, max.z),
            new Vector3(max.x, min.y, max.z),
            new Vector3(min.x, max.y, max.z),
            new Vector3(max.x, max.y, max.z)
        };

        for (int i = 0; i < corners.Length; i++)
        {
            Vector3 localPoint = classroomDoor.InverseTransformPoint(corners[i]);
            if (!hasBounds)
            {
                localBounds = new Bounds(localPoint, Vector3.zero);
                hasBounds = true;
            }
            else
            {
                localBounds.Encapsulate(localPoint);
            }
        }
    }
}
