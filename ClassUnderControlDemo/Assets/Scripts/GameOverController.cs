using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameOverController : MonoBehaviour
{
    public enum ContinueAction
    {
        RestartCurrentClass,
        LoadScene
    }

    const string BackgroundName = "EndScreenBackground";

    public ContinueAction continueAction = ContinueAction.RestartCurrentClass;
    public string gameSceneName = "OutdoorsScene";
    public float inputDelay = 0.5f;
    public Texture endScreenTexture;

    float timer;
    bool canRestart;
    bool loadingScene;

    void OnEnable()
    {
        EnsureEndScreenOverlay();
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        timer = 0f;
        canRestart = false;
        loadingScene = false;
    }

    void Update()
    {
        if (loadingScene)
            return;

        if (!canRestart)
        {
            timer += Time.unscaledDeltaTime;
            if (timer >= inputDelay)
                canRestart = true;

            return;
        }

        if (Input.anyKeyDown)
        {
            loadingScene = true;
            ContinueFromEndScreen();
        }
    }

    void ContinueFromEndScreen()
    {
        if (continueAction == ContinueAction.RestartCurrentClass
            && GameManager.I != null
            && GameManager.I.RestartCurrentClassFromEndScreen())
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(gameSceneName))
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(gameSceneName.Trim());
        }
        else
        {
            loadingScene = false;
        }
    }

    void EnsureEndScreenOverlay()
    {
        Canvas canvas = GetComponent<Canvas>();
        if (canvas != null)
        {
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 10000;
        }

        RectTransform root = transform as RectTransform;
        if (root == null)
            return;

        Transform existingBackground = transform.Find(BackgroundName);
        GameObject backgroundObject;

        if (existingBackground != null)
        {
            backgroundObject = existingBackground.gameObject;
        }
        else
        {
            backgroundObject = new GameObject(BackgroundName, typeof(RectTransform), typeof(RawImage));
            backgroundObject.transform.SetParent(transform, false);
        }

        backgroundObject.transform.SetAsFirstSibling();

        RectTransform backgroundRect = backgroundObject.GetComponent<RectTransform>();
        backgroundRect.anchorMin = Vector2.zero;
        backgroundRect.anchorMax = Vector2.one;
        backgroundRect.offsetMin = Vector2.zero;
        backgroundRect.offsetMax = Vector2.zero;

        Image oldImage = backgroundObject.GetComponent<Image>();
        if (oldImage != null)
            oldImage.enabled = false;

        RawImage background = backgroundObject.GetComponent<RawImage>();
        if (background == null)
            background = backgroundObject.AddComponent<RawImage>();

        background.texture = endScreenTexture;
        background.color = endScreenTexture != null ? Color.white : Color.black;
        background.raycastTarget = true;

        SetDecorativeChildrenVisible(root, endScreenTexture == null);
    }

    void SetDecorativeChildrenVisible(RectTransform root, bool visible)
    {
        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child != null && child.name != BackgroundName)
                child.gameObject.SetActive(visible);
        }
    }
}
