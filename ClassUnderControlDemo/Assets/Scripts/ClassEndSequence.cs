using System.Collections;
using UnityEngine;
using TMPro;

public class ClassEndSequence : MonoBehaviour
{
    public MonoBehaviour playerMovementScript;
    public Camera playerCamera;
    public Camera speakerCamera;
    public AudioListener playerListener;
    public AudioListener speakerListener;
    public AudioSource speakerAudio;
    public GameObject bottomPanel;
    public TMP_Text bottomText;
    [TextArea(3, 6)]
    public string message;
    public float delayBeforeCutscene = 3f;
    public float wordInterval = 0.05f;
    public float camBlendExtraTime = 0.3f;
    public float cameraTransitionDuration = 1f;
    public AnimationCurve transitionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    bool running;

    void Awake()
    {
        if (playerCamera == null)
            playerCamera = Camera.main;

        if (playerListener == null && playerCamera != null)
            playerListener = playerCamera.GetComponent<AudioListener>();

        if (speakerListener == null && speakerCamera != null)
            speakerListener = speakerCamera.GetComponent<AudioListener>();

        SetCamSpeaker(false);
    }

    public void StartClassEndSequence()
    {
        if (running) return;
        StartCoroutine(RunSequence());
    }

    IEnumerator RunSequence()
    {
        running = true;

        yield return new WaitForSeconds(delayBeforeCutscene);

        if (playerMovementScript != null)
            playerMovementScript.enabled = false;

        Vector3 startPos = Vector3.zero;
        Quaternion startRot = Quaternion.identity;
        float startFov = 60f;
        bool canTransition = playerCamera != null && speakerCamera != null;

        if (playerCamera != null)
        {
            startPos = playerCamera.transform.position;
            startRot = playerCamera.transform.rotation;
            startFov = playerCamera.fieldOfView;
        }

        if (canTransition)
        {
            SetCamSpeaker(false);
            yield return StartCoroutine(TransitionCamera(playerCamera, speakerCamera, cameraTransitionDuration));
        }
        else
        {
            SetCamSpeaker(true);
            yield return new WaitForSeconds(camBlendExtraTime);
        }

        if (bottomPanel != null) bottomPanel.SetActive(true);
        if (bottomText != null) bottomText.text = "";

        if (speakerAudio != null)
            speakerAudio.Play();

        yield return StartCoroutine(ShowWordsFast(message));

        if (speakerAudio != null)
        {
            while (speakerAudio.isPlaying)
                yield return null;
        }

        if (bottomPanel != null) bottomPanel.SetActive(false);

        if (canTransition && playerCamera != null)
        {
            yield return StartCoroutine(TransitionCamera(playerCamera, startPos, startRot, startFov, cameraTransitionDuration));
        }
        else
        {
            SetCamSpeaker(false);
            yield return new WaitForSeconds(camBlendExtraTime);
        }

        if (playerMovementScript != null)
            playerMovementScript.enabled = true;

        running = false;
    }

    IEnumerator ShowWordsFast(string fullText)
    {
        if (bottomText == null || string.IsNullOrWhiteSpace(fullText))
            yield break;

        string[] words = fullText.Split(' ');

        for (int i = 0; i < words.Length; i++)
        {
            bottomText.text += (i == 0 ? "" : " ") + words[i];
            yield return new WaitForSeconds(wordInterval);
        }
    }

    void SetCamSpeaker(bool speaker)
    {
        if (speakerCamera != null) speakerCamera.enabled = speaker;
        if (playerCamera != null) playerCamera.enabled = !speaker;

        if (speakerListener != null) speakerListener.enabled = speaker;
        if (playerListener != null) playerListener.enabled = !speaker;
    }

    IEnumerator TransitionCamera(Camera source, Camera target, float duration)
    {
        if (source == null || target == null)
            yield break;

        yield return StartCoroutine(
            TransitionCamera(source, target.transform.position, target.transform.rotation, target.fieldOfView, duration));
    }

    IEnumerator TransitionCamera(Camera source, Vector3 endPos, Quaternion endRot, float endFov, float duration)
    {
        if (source == null)
            yield break;

        float total = Mathf.Max(0.01f, duration);
        float t = 0f;

        Vector3 startPos = source.transform.position;
        Quaternion startRot = source.transform.rotation;
        float startFov = source.fieldOfView;

        while (t < total)
        {
            t += Time.deltaTime;
            float normalized = Mathf.Clamp01(t / total);
            float eased = transitionCurve != null ? transitionCurve.Evaluate(normalized) : normalized;

            source.transform.position = Vector3.Lerp(startPos, endPos, eased);
            source.transform.rotation = Quaternion.Slerp(startRot, endRot, eased);
            source.fieldOfView = Mathf.Lerp(startFov, endFov, eased);

            yield return null;
        }

        source.transform.position = endPos;
        source.transform.rotation = endRot;
        source.fieldOfView = endFov;
    }
}
