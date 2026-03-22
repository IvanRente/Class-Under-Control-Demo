using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ClassTransitionScreen : MonoBehaviour
{
    public float fadeToBlackDuration = 0.45f;
    public float subjectHoldDuration = 1.5f;
    public float fadeFromBlackDuration = 0.45f;
    public TMP_FontAsset transitionFont;
    public float subjectFontSize = 72f;

    CanvasGroup canvasGroup;
    TMP_Text subjectText;

    public IEnumerator PlayTransition(string subjectName, Action onBlackReached)
    {
        EnsureOverlay();
        subjectText.text = subjectName;

        yield return StartCoroutine(FadeTo(1f, fadeToBlackDuration));

        onBlackReached?.Invoke();

        if (subjectHoldDuration > 0f)
            yield return new WaitForSeconds(subjectHoldDuration);

        yield return StartCoroutine(FadeTo(0f, fadeFromBlackDuration));
        subjectText.text = string.Empty;
    }

    void EnsureOverlay()
    {
        if (canvasGroup != null)
            return;

        GameObject canvasObject = new GameObject("ClassTransitionCanvas");
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5000;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        canvasObject.AddComponent<GraphicRaycaster>();
        canvasGroup = canvasObject.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;

        GameObject backgroundObject = new GameObject("Background");
        backgroundObject.transform.SetParent(canvasObject.transform, false);

        RectTransform backgroundRect = backgroundObject.AddComponent<RectTransform>();
        backgroundRect.anchorMin = Vector2.zero;
        backgroundRect.anchorMax = Vector2.one;
        backgroundRect.offsetMin = Vector2.zero;
        backgroundRect.offsetMax = Vector2.zero;

        Image background = backgroundObject.AddComponent<Image>();
        background.color = Color.black;

        GameObject textObject = new GameObject("SubjectText");
        textObject.transform.SetParent(backgroundObject.transform, false);

        RectTransform textRect = textObject.AddComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0.5f, 0.5f);
        textRect.anchorMax = new Vector2(0.5f, 0.5f);
        textRect.sizeDelta = new Vector2(1400f, 240f);
        textRect.anchoredPosition = Vector2.zero;

        TextMeshProUGUI textComponent = textObject.AddComponent<TextMeshProUGUI>();
        textComponent.alignment = TextAlignmentOptions.Center;
        textComponent.fontSize = subjectFontSize;
        textComponent.color = Color.white;
        textComponent.text = string.Empty;
        if (transitionFont != null)
            textComponent.font = transitionFont;

        subjectText = textComponent;
    }

    IEnumerator FadeTo(float targetAlpha, float duration)
    {
        EnsureOverlay();

        canvasGroup.blocksRaycasts = targetAlpha > 0.01f || canvasGroup.alpha > 0.01f;

        float startAlpha = canvasGroup.alpha;
        float total = Mathf.Max(0.01f, duration);
        float timer = 0f;

        while (timer < total)
        {
            timer += Time.deltaTime;
            float progress = Mathf.Clamp01(timer / total);
            canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, progress);
            yield return null;
        }

        canvasGroup.alpha = targetAlpha;
        canvasGroup.blocksRaycasts = targetAlpha > 0.01f;
    }
}
