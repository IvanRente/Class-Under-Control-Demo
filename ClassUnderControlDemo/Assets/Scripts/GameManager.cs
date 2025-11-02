using UnityEngine;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    public static GameManager I;

    public float currentGPA = 5f;
    public float minGPA = 0f;
    public float maxGPA = 10f;
    public Image gpaFill;

    public float minFog = 0.01f;
    public float maxFog = 0.08f;

    void Awake()
    {
        I = this;
    }

    void Update()
    {
        UpdateUI();
        UpdateFog();
    }

    void UpdateUI()
    {
        float t = Mathf.InverseLerp(minGPA, maxGPA, currentGPA);
        gpaFill.fillAmount = t;
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
}
