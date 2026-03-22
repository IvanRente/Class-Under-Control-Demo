using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections.Generic;
public class GameManager : MonoBehaviour
{
    public static GameManager I;
    readonly List<IClassStudent> classStudents = new List<IClassStudent>();

    public float currentGPA = 5f;
    public float minGPA = 0f;
    public float maxGPA = 10f;
    public TMP_Text gpaText;

    public Volume fogVolume;
    private Fog hdrpFog;
    public float minFog = 0.01f;
    public float maxFog = 0.08f;

    public Image topBorder, bottomBorder, leftBorder, rightBorder;

    public Gradient correctGradient;
    public Gradient wrongGradient;
    public float fadeInDuration = 0.2f;
    public float holdDuration = 0.3f;
    public float fadeOutDuration = 0.3f;

    private float feedbackTimer = 0f;
    private bool isShowingFeedback = false;
    private bool isCorrectFeedback = false;

    public string gameOverSceneName = "GameOver";
    public string winSceneName = "Win";
    bool isGameOver = false;

    public AudioSource ambientSource;
    public AudioClip highGpaMusic;
    public AudioClip lowGpaMusic;
    public float musicThreshold = 5f;

    public float classDurationSeconds = 120f;
    public TMP_Text timerText;
    public AudioSource classEndAudioSource;
    public AudioClip classEndedClip;
    public QuizBoard quizBoard;
    public ClassEndSequence classEndSequence;

    private bool? isHighMusicActive = null;
    private float remainingClassTime;
    private bool classEnded;

    public bool IsClassEnded => classEnded;
    public bool IsClassInProgress => !classEnded && !classTimerPaused;

    public bool classTimerPaused = true; 

    void Awake()
    {
        I = this;
        remainingClassTime = Mathf.Max(0f, classDurationSeconds);

        if (ambientSource != null)
        {
            ambientSource.loop = true;
            ambientSource.playOnAwake = false;
        }

        UpdateAmbientMusic(true);
        UpdateTimerUI();

        if (fogVolume != null && fogVolume.profile.TryGet(out hdrpFog))
        {
            hdrpFog.active = true;
        }

        if (correctGradient == null || correctGradient.colorKeys.Length == 0)
        {
            correctGradient = new Gradient();
            GradientColorKey[] correctColors = new GradientColorKey[2];
            correctColors[0] = new GradientColorKey(new Color(0.2f, 1f, 0.2f), 0f);
            correctColors[1] = new GradientColorKey(new Color(0f, 0.6f, 0f), 1f);

            GradientAlphaKey[] correctAlphas = new GradientAlphaKey[2];
            correctAlphas[0] = new GradientAlphaKey(1f, 0f);
            correctAlphas[1] = new GradientAlphaKey(1f, 1f);

            correctGradient.SetKeys(correctColors, correctAlphas);
        }

        if (wrongGradient == null || wrongGradient.colorKeys.Length == 0)
        {
            wrongGradient = new Gradient();
            GradientColorKey[] wrongColors = new GradientColorKey[2];
            wrongColors[0] = new GradientColorKey(new Color(1f, 0.2f, 0.2f), 0f);
            wrongColors[1] = new GradientColorKey(new Color(0.8f, 0f, 0f), 1f);

            GradientAlphaKey[] wrongAlphas = new GradientAlphaKey[2];
            wrongAlphas[0] = new GradientAlphaKey(1f, 0f);
            wrongAlphas[1] = new GradientAlphaKey(1f, 1f);

            wrongGradient.SetKeys(wrongColors, wrongAlphas);
        }

        ClearBordersImmediate();
    }

    public void StartClassNow()
    {
        if (classEnded) return;
        classTimerPaused = false;
    }

    public void PrepareForNewClassCycle()
    {
        classEnded = false;
        classTimerPaused = true;
        remainingClassTime = Mathf.Max(0f, classDurationSeconds);
        isShowingFeedback = false;
        feedbackTimer = 0f;

        if (classEndAudioSource != null && classEndAudioSource.isPlaying)
            classEndAudioSource.Stop();

        ClearBordersImmediate();
        UpdateTimerUI();
        UpdateAmbientMusic(true);
    }

    void Update()
    {
        UpdateClassTimer();
        UpdateUI();
        UpdateFog();
        UpdateFeedbackAnimation();
        CheckGameOver();
        UpdateAmbientMusic();
    }

    void UpdateClassTimer()
    {
        if (classEnded) return;
        if (classTimerPaused) return; 

        remainingClassTime = Mathf.Max(0f, remainingClassTime - Time.deltaTime);
        UpdateTimerUI();

        if (remainingClassTime <= 0f)
        {
            EndClass();
        }
    }

    void UpdateTimerUI()
    {
        if (!timerText) return;

        int totalSeconds = Mathf.CeilToInt(remainingClassTime);
        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;
        timerText.text = $"{minutes:00}:{seconds:00}";
    }

    void EndClass()
    {
        if (classEnded) return;

        classEnded = true;
        remainingClassTime = 0f;
        UpdateTimerUI();
        StopAmbientMusic();

        if (classEndAudioSource && classEndedClip)
            classEndAudioSource.PlayOneShot(classEndedClip);

        PauseAllStudents();
        ShowClassEndedOnBoard();
        StartClassEndCutscene();
    }

    void PauseAllStudents()
    {
        ClassStudentUtility.GetObjectsImplementing(classStudents);

        for (int i = 0; i < classStudents.Count; i++)
            classStudents[i].OnClassEnded();
    }

    public void StunAllStudents(float duration)
    {
        if (classEnded) return;
        if (duration <= 0f) return;

        ClassStudentUtility.GetObjectsImplementing(classStudents);
        for (int i = 0; i < classStudents.Count; i++)
            classStudents[i].Stun(duration);
    }

    void ShowClassEndedOnBoard()
    {
        if (!quizBoard)
            quizBoard = FindObjectOfType<QuizBoard>();

        if (quizBoard)
            quizBoard.EndClassDisplay();
    }

    void StartClassEndCutscene()
    {
        if (!classEndSequence)
            classEndSequence = FindObjectOfType<ClassEndSequence>();

        if (classEndSequence)
            classEndSequence.StartClassEndSequence();
    }

    void UpdateUI()
    {
        float shown = Mathf.Clamp(currentGPA, minGPA, maxGPA);
        gpaText.text = "GPA: " + shown.ToString("0.0");
    }

    void UpdateFog()
    {
        float t = Mathf.InverseLerp(minGPA, maxGPA, currentGPA);
        float fogDensity = Mathf.Lerp(maxFog, minFog, t);

        if (hdrpFog != null)
        {
            hdrpFog.meanFreePath.Override(fogDensity);
        }
    }

    void UpdateFeedbackAnimation()
    {
        if (!isShowingFeedback) return;

        feedbackTimer += Time.deltaTime;
        float totalDuration = fadeInDuration + holdDuration + fadeOutDuration;

        if (feedbackTimer >= totalDuration)
        {
            isShowingFeedback = false;
            ClearBordersImmediate();
            return;
        }

        float alpha = 0f;
        float gradientTime = 0f;

        if (feedbackTimer < fadeInDuration)
        {
            alpha = feedbackTimer / fadeInDuration;
            gradientTime = alpha;
        }
        else if (feedbackTimer < fadeInDuration + holdDuration)
        {
            alpha = 1f;
            float holdProgress = (feedbackTimer - fadeInDuration) / holdDuration;
            gradientTime = Mathf.Clamp01(holdProgress);
        }
        else
        {
            float fadeOutProgress = (feedbackTimer - fadeInDuration - holdDuration) / fadeOutDuration;
            alpha = 1f - fadeOutProgress;
            gradientTime = 1f;
        }

        Gradient currentGradient = isCorrectFeedback ? correctGradient : wrongGradient;
        Color baseColor = currentGradient.Evaluate(gradientTime);
        Color finalColor = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);

        topBorder.color = finalColor;
        bottomBorder.color = finalColor;
        leftBorder.color = finalColor;
        rightBorder.color = finalColor;
    }

    void UpdateAmbientMusic(bool force = false)
    {
        if (ambientSource == null) return;
        if (classEnded) return;

        bool shouldUseHigh = currentGPA >= musicThreshold;

        if (!force && isHighMusicActive.HasValue && isHighMusicActive.Value == shouldUseHigh)
            return;

        isHighMusicActive = shouldUseHigh;
        AudioClip target = shouldUseHigh ? highGpaMusic : lowGpaMusic;
        if (target == null) return;

        bool clipChanged = ambientSource.clip != target;
        if (clipChanged)
            ambientSource.clip = target;

        if (clipChanged || !ambientSource.isPlaying)
            ambientSource.Play();
    }

    void StopAmbientMusic()
    {
        if (ambientSource == null) return;
        if (ambientSource.isPlaying)
            ambientSource.Stop();
    }

    public void AddGPA(float amount)
    {
        currentGPA = Mathf.Clamp(currentGPA + amount, minGPA, maxGPA);
        ShowFeedback(true);
    }

    public void SubGPA(float amount)
    {
        currentGPA = Mathf.Clamp(currentGPA - amount, minGPA, maxGPA);
        ShowFeedback(false);
    }

    void ShowFeedback(bool isCorrect)
    {
        isShowingFeedback = true;
        isCorrectFeedback = isCorrect;
        feedbackTimer = 0f;
    }

    void ClearBordersImmediate()
    {
        Color transparent = new Color(1, 1, 1, 0);
        topBorder.color = transparent;
        bottomBorder.color = transparent;
        leftBorder.color = transparent;
        rightBorder.color = transparent;
    }

    void CheckGameOver()
    {
        if (isGameOver) return;

        if (currentGPA <= minGPA)
        {
            isGameOver = true;
            SceneManager.LoadScene(gameOverSceneName);
        }
    }

    public void WinGame()
    {
        if (isGameOver) return;

        isGameOver = true;
        SceneManager.LoadScene(winSceneName);
    }
}
