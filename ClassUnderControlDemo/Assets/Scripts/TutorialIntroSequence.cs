using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class TutorialIntroSequence : MonoBehaviour
{
    [Serializable]
    public class DialogueLine
    {
        [TextArea(2, 5)]
        public string text;
        public AudioClip voiceClip;
        public float extraDelayAfterLine = 0.2f;
    }

    public GameObject pressAnyKeyPrompt;
    public Camera controlledCamera;
    public Transform directorCameraPoint;
    public float directorCameraFov = 60f;
    public float moveToDirectorDuration = 4f;
    public float pauseBeforeDialogue = 0.35f;
    public Transform classroomCameraPoint;
    public float classroomCameraFov = 60f;
    public float moveToClassroomDuration = 4f;
    public AnimationCurve cameraMoveCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    public Transform director;
    public Transform directorExitPoint;
    public float directorExitDuration = 2.5f;
    public float delayCamera = 2f;

    public GameObject dialoguePanel;
    public TMP_Text dialogueText;
    public AudioSource speakerAudioSource;
    public DialogueLine[] dialogueLines;
    public float wordInterval = 0.05f;
    public float lineGap = 0.15f;

    public string mainSceneName = "OutdoorsScene";
    public float delayBeforeSceneLoad = 0.5f;
    public bool loadMainSceneWhenComplete = true;
    public Behaviour[] disabledUntilComplete;
    public GameObject[] hiddenUntilComplete;
    public Behaviour[] disabledAfterComplete;
    public GameObject[] hiddenAfterComplete;
    public GameObject[] shownAfterComplete;
    public bool disableControlledCameraAfterComplete;
    public bool unlockCursorDuringTutorial = true;

    bool sequenceStarted;

    void Awake()
    {
        if (unlockCursorDuringTutorial)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        SetBehaviours(disabledUntilComplete, false);
        SetObjects(hiddenUntilComplete, false);
        SetObjects(shownAfterComplete, false);

        if (controlledCamera == null)
            controlledCamera = Camera.main;

        if (dialoguePanel != null)
            dialoguePanel.SetActive(false);

    }

    void Update()
    {
        if (sequenceStarted)
            return;

        if (Input.anyKeyDown)
            StartTutorialSequence();
    }

    public void StartTutorialSequence()
    {
        if (sequenceStarted)
            return;

        StartCoroutine(RunSequence());
    }

    IEnumerator RunSequence()
    {
        sequenceStarted = true;

        if (pressAnyKeyPrompt != null)
            pressAnyKeyPrompt.SetActive(false);

        if (directorCameraPoint != null)
            yield return StartCoroutine(MoveCameraTo(directorCameraPoint, directorCameraFov, moveToDirectorDuration));

        if (pauseBeforeDialogue > 0f)
            yield return new WaitForSeconds(pauseBeforeDialogue);

        yield return StartCoroutine(PlayDialogue());

        bool directorExitFinished = true;
        bool directorExitStarted = false;
        if (director != null && directorExitPoint != null && directorExitDuration > 0f)
        {
            directorExitFinished = false;
            directorExitStarted = true;
            StartCoroutine(MoveTransformTo(director, directorExitPoint, directorExitDuration, () => directorExitFinished = true));
        }

        if (directorExitStarted && delayCamera > 0f)
            yield return new WaitForSeconds(delayCamera);

        if (classroomCameraPoint != null)
            yield return StartCoroutine(MoveCameraTo(classroomCameraPoint, classroomCameraFov, moveToClassroomDuration));

        while (!directorExitFinished)
            yield return null;

        if (delayBeforeSceneLoad > 0f)
            yield return new WaitForSeconds(delayBeforeSceneLoad);

        CompleteTutorial();
    }

    void CompleteTutorial()
    {
        if (loadMainSceneWhenComplete && !string.IsNullOrWhiteSpace(mainSceneName))
        {
            SceneManager.LoadScene(mainSceneName);
            return;
        }

        SetBehaviours(disabledUntilComplete, true);
        SetObjects(hiddenUntilComplete, true);
        SetBehaviours(disabledAfterComplete, false);
        SetObjects(hiddenAfterComplete, false);
        SetObjects(shownAfterComplete, true);

        if (disableControlledCameraAfterComplete && controlledCamera != null)
        {
            controlledCamera.enabled = false;

            AudioListener controlledListener = controlledCamera.GetComponent<AudioListener>();
            if (controlledListener != null)
                controlledListener.enabled = false;
        }
    }

    void SetBehaviours(Behaviour[] behaviours, bool enabled)
    {
        if (behaviours == null) return;

        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] != null)
                behaviours[i].enabled = enabled;
        }
    }

    void SetObjects(GameObject[] objects, bool active)
    {
        if (objects == null) return;

        for (int i = 0; i < objects.Length; i++)
        {
            if (objects[i] != null)
                objects[i].SetActive(active);
        }
    }

    IEnumerator PlayDialogue()
    {
        if (dialoguePanel != null)
            dialoguePanel.SetActive(true);

        if (dialogueText != null)
            dialogueText.text = string.Empty;

        if (dialogueLines != null)
        {
            for (int i = 0; i < dialogueLines.Length; i++)
            {
                DialogueLine line = dialogueLines[i];

                if (dialogueText != null)
                    dialogueText.text = string.Empty;

                if (speakerAudioSource != null)
                {
                    speakerAudioSource.Stop();
                    speakerAudioSource.clip = line.voiceClip;

                    if (line.voiceClip != null)
                        speakerAudioSource.Play();
                }

                yield return StartCoroutine(ShowWords(line.text));

                if (speakerAudioSource != null && speakerAudioSource.clip != null)
                {
                    while (speakerAudioSource.isPlaying)
                        yield return null;
                }

                float waitAfterLine = Mathf.Max(0f, lineGap + line.extraDelayAfterLine);
                if (waitAfterLine > 0f)
                    yield return new WaitForSeconds(waitAfterLine);
            }
        }

        if (speakerAudioSource != null)
            speakerAudioSource.Stop();

        if (dialoguePanel != null)
            dialoguePanel.SetActive(false);
    }

    IEnumerator ShowWords(string fullText)
    {
        if (dialogueText == null || string.IsNullOrWhiteSpace(fullText))
            yield break;

        string[] words = fullText.Split(' ');
        for (int i = 0; i < words.Length; i++)
        {
            dialogueText.text += (i == 0 ? string.Empty : " ") + words[i];
            yield return new WaitForSeconds(wordInterval);
        }
    }

    IEnumerator MoveCameraTo(Transform targetPoint, float targetFov, float duration)
    {
        if (controlledCamera == null || targetPoint == null)
            yield break;

        float total = Mathf.Max(0.01f, duration);
        float elapsed = 0f;
        Vector3 startPos = controlledCamera.transform.position;
        Quaternion startRot = controlledCamera.transform.rotation;
        float startFov = controlledCamera.fieldOfView;

        while (elapsed < total)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / total);
            float eased = cameraMoveCurve != null ? cameraMoveCurve.Evaluate(t) : t;

            controlledCamera.transform.position = Vector3.Lerp(startPos, targetPoint.position, eased);
            controlledCamera.transform.rotation = Quaternion.Slerp(startRot, targetPoint.rotation, eased);
            controlledCamera.fieldOfView = Mathf.Lerp(startFov, targetFov, eased);

            yield return null;
        }

        controlledCamera.transform.position = targetPoint.position;
        controlledCamera.transform.rotation = targetPoint.rotation;
        controlledCamera.fieldOfView = targetFov;
    }

    IEnumerator MoveTransformTo(Transform movingTransform, Transform targetPoint, float duration, Action onComplete)
    {
        if (movingTransform == null || targetPoint == null)
        {
            onComplete?.Invoke();
            yield break;
        }

        float total = Mathf.Max(0.01f, duration);
        float elapsed = 0f;
        Vector3 startPos = movingTransform.position;

        while (elapsed < total)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / total);
            float eased = cameraMoveCurve != null ? cameraMoveCurve.Evaluate(t) : t;
            Vector3 nextPosition = Vector3.Lerp(startPos, targetPoint.position, eased);
            Vector3 moveDirection = nextPosition - movingTransform.position;

            movingTransform.position = nextPosition;

            Vector3 flatDirection = new Vector3(moveDirection.x, 0f, moveDirection.z);
            if (flatDirection.sqrMagnitude > 0.0001f)
                movingTransform.rotation = Quaternion.LookRotation(flatDirection.normalized, Vector3.up);

            yield return null;
        }

        movingTransform.position = targetPoint.position;
        movingTransform.rotation = targetPoint.rotation;
        onComplete?.Invoke();
    }
}
