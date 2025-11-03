using TMPro;
using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;
public class GameManager : MonoBehaviour
{
    public static GameManager I;

    public float currentGPA = 5f;
    public float minGPA = 0f;
    public float maxGPA = 10f;
    public TMP_Text gpaText;

    public float minFog = 0.01f;
    public float maxFog = 0.08f;

    public string gameOverSceneName = "GameOver";
    bool isGameOver = false;
    void Awake()
    {
        I = this;
    }

    void Update()
    {
        UpdateUI();
        UpdateFog();
        CheckGameOver();
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
        RenderSettings.fogDensity = fogDensity;
    }

    public void AddGPA(float amount)
    {
        currentGPA = Mathf.Clamp(currentGPA + amount, minGPA, maxGPA);
    }

    public void SubGPA(float amount)
    {
        currentGPA = Mathf.Clamp(currentGPA - amount, minGPA, maxGPA);
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
}
