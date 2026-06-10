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
    readonly List<Canvas> hiddenHudCanvases = new List<Canvas>();
    readonly List<StudentQuestionMomentController> disabledQuestionControllers = new List<StudentQuestionMomentController>();

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
    public GameObject gameOverScreen;
    public GameObject winScreen;
    bool isGameOver = false;
    ClassTransitionFlowController transitionFlowController;
    MonoBehaviour pausedMovementScript;
    PlayerItemSystem pausedItemSystem;

    public AudioSource ambientSource;
    public AudioClip highGpaMusic;
    public AudioClip lowGpaMusic;
    public float musicThreshold = 5f;

    public float classDurationSeconds = 120f;
    public TMP_Text timerText;
    public AudioSource classEndAudioSource;
    public AudioClip classEndedClip;
    public QuizBoard quizBoard;
    public HistoryTimelineBoard historyTimelineBoard;
    public CircuitBuilderBoard circuitBuilderBoard;
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
        Time.timeScale = 1f;
        remainingClassTime = Mathf.Max(0f, classDurationSeconds);

        SetEndScreen(gameOverScreen, false);
        SetEndScreen(winScreen, false);

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
        if (isGameOver) return;

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
        if (!historyTimelineBoard)
            historyTimelineBoard = FindFirstObjectByType<HistoryTimelineBoard>(FindObjectsInactive.Include);

        if (historyTimelineBoard != null && historyTimelineBoard.gameObject.activeInHierarchy)
        {
            historyTimelineBoard.EndClassDisplay();
            return;
        }

        if (!circuitBuilderBoard)
            circuitBuilderBoard = FindFirstObjectByType<CircuitBuilderBoard>(FindObjectsInactive.Include);

        if (circuitBuilderBoard != null && circuitBuilderBoard.gameObject.activeInHierarchy)
        {
            circuitBuilderBoard.EndClassDisplay();
            return;
        }

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
        if (isGameOver) return;

        currentGPA = Mathf.Clamp(currentGPA + amount, minGPA, maxGPA);
        ShowFeedback(true);
    }

    public void SubGPA(float amount)
    {
        if (isGameOver) return;

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
            ShowEndScreen(gameOverScreen, gameOverSceneName);
        }
    }

    public void WinGame()
    {
        if (isGameOver) return;

        isGameOver = true;
        ShowEndScreen(winScreen, winSceneName);
    }

    void ShowEndScreen(GameObject screen, string fallbackSceneName)
    {
        StopAmbientMusic();
        classTimerPaused = true;
        ClearBordersImmediate();
        DisableQuestionMoments();
        classTimerPaused = true;
        PauseAllStudents();
        SetGameplayInputEnabled(false);
        SetEndScreen(gameOverScreen, false);
        SetEndScreen(winScreen, false);
        HideHudForEndScreen();

        if (screen != null)
        {
            Time.timeScale = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            SetEndScreen(screen, true);
            return;
        }

        if (!string.IsNullOrWhiteSpace(fallbackSceneName))
            SceneManager.LoadScene(fallbackSceneName);
    }

    public bool RestartCurrentClassFromEndScreen()
    {
        if (!isGameOver) return false;

        Time.timeScale = 1f;
        SetEndScreen(gameOverScreen, false);
        SetEndScreen(winScreen, false);
        RestoreHudAfterEndScreen();
        RestoreQuestionMoments();
        SetGameplayInputEnabled(true);

        if (transitionFlowController == null)
            transitionFlowController = FindObjectOfType<ClassTransitionFlowController>();

        if (transitionFlowController != null)
        {
            transitionFlowController.RestartCurrentClass();
            return true;
        }

        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        return true;
    }

    public void ResetForClassRestart(float classStartGpa)
    {
        isGameOver = false;
        currentGPA = Mathf.Clamp(classStartGpa, minGPA, maxGPA);
        PrepareForNewClassCycle();
        UpdateUI();
        UpdateFog();
    }

    void SetEndScreen(GameObject screen, bool active)
    {
        if (screen != null)
            screen.SetActive(active);
    }

    void HideHudForEndScreen()
    {
        RestoreHudAfterEndScreen();

        Canvas[] canvases = FindObjectsOfType<Canvas>();
        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas canvas = canvases[i];
            if (canvas == null || !canvas.gameObject.activeInHierarchy || IsEndScreenObject(canvas.gameObject))
                continue;

            hiddenHudCanvases.Add(canvas);
            canvas.gameObject.SetActive(false);
        }
    }

    void RestoreHudAfterEndScreen()
    {
        for (int i = 0; i < hiddenHudCanvases.Count; i++)
        {
            if (hiddenHudCanvases[i] != null)
                hiddenHudCanvases[i].gameObject.SetActive(true);
        }

        hiddenHudCanvases.Clear();
    }

    bool IsEndScreenObject(GameObject candidate)
    {
        return IsSameOrChild(candidate, gameOverScreen) || IsSameOrChild(candidate, winScreen);
    }

    bool IsSameOrChild(GameObject candidate, GameObject root)
    {
        if (candidate == null || root == null) return false;

        Transform candidateTransform = candidate.transform;
        Transform rootTransform = root.transform;
        return candidateTransform == rootTransform || candidateTransform.IsChildOf(rootTransform);
    }

    void DisableQuestionMoments()
    {
        RestoreQuestionMoments();

        StudentQuestionMomentController[] questionControllers = FindObjectsOfType<StudentQuestionMomentController>();
        for (int i = 0; i < questionControllers.Length; i++)
        {
            StudentQuestionMomentController controller = questionControllers[i];
            if (controller == null || !controller.enabled)
                continue;

            disabledQuestionControllers.Add(controller);
            controller.enabled = false;
        }
    }

    void RestoreQuestionMoments()
    {
        for (int i = 0; i < disabledQuestionControllers.Count; i++)
        {
            if (disabledQuestionControllers[i] != null)
                disabledQuestionControllers[i].enabled = true;
        }

        disabledQuestionControllers.Clear();
    }

    void SetGameplayInputEnabled(bool enabled)
    {
        if (classEndSequence != null)
            pausedMovementScript = classEndSequence.playerMovementScript;

        if (pausedMovementScript == null)
            pausedMovementScript = FindObjectOfType<PlayerController>();

        if (pausedItemSystem == null)
            pausedItemSystem = FindObjectOfType<PlayerItemSystem>();

        if (pausedItemSystem != null)
        {
            if (!enabled)
                pausedItemSystem.CloseAllMenus();

            pausedItemSystem.enabled = enabled;
        }

        if (pausedMovementScript != null)
            pausedMovementScript.enabled = enabled;
    }
}
