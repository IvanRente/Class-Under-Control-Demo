using System.Collections;
using UnityEngine;
using TMPro;

public class ClassEndSequence : MonoBehaviour
{
    [Header("References")]
    public MonoBehaviour playerMovementScript;
    public Behaviour vcamPlayer;
    public Behaviour vcamSpeaker;

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
        // Works with Cinemachine cameras and regular Camera components.
        SetCameraActive(vcamSpeaker, speaker);
        SetCameraActive(vcamPlayer, !speaker);
    }

    void SetCameraActive(Behaviour cam, bool active)
    {
        if (cam == null) return;
        cam.gameObject.SetActive(active);
    }
}
