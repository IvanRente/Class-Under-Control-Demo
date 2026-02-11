using System.Collections;
using UnityEngine;
using TMPro;

public class ClassEndSequence : MonoBehaviour
{
    [Header("References")]
    public MonoBehaviour playerMovementScript;

    [Header("Cameras (No Cinemachine Needed)")]
    public Camera playerCamera;
    public Camera speakerCamera;
    public AudioListener playerListener;
    public AudioListener speakerListener;

    [Header("Speaker")]
    public AudioSource speakerAudio;

    [Header("UI")]
    public GameObject bottomPanel;
    public TMP_Text bottomText;
    [TextArea(3, 6)]
    public string message;

    [Header("Timings")]
    public float delayBeforeCutscene = 3f;
    public float wordInterval = 0.05f;
    public float camBlendExtraTime = 0.3f;

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

        SetCamSpeaker(true);
        yield return new WaitForSeconds(camBlendExtraTime);

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

        SetCamSpeaker(false);
        yield return new WaitForSeconds(camBlendExtraTime);

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

        // Keep only one AudioListener active at a time.
        if (speakerListener != null) speakerListener.enabled = speaker;
        if (playerListener != null) playerListener.enabled = !speaker;
    }
}
