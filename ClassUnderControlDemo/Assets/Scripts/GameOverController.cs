using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameOverController : MonoBehaviour
{
    const string BackgroundName = "EndScreenBackground";

    public string gameSceneName = "OutdoorsScene";
    public float inputDelay = 0.5f;

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

            if (GameManager.I != null && GameManager.I.RestartCurrentClassFromEndScreen())
                return;

            if (!string.IsNullOrWhiteSpace(gameSceneName))
            {
                Time.timeScale = 1f;
                SceneManager.LoadScene(gameSceneName);
            }
            else
            {
                loadingScene = false;
            }
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
        if (existingBackground != null)
        {
            existingBackground.SetAsFirstSibling();
            return;
        }

        GameObject backgroundObject = new GameObject(BackgroundName, typeof(RectTransform), typeof(Image));
        backgroundObject.transform.SetParent(transform, false);
        backgroundObject.transform.SetAsFirstSibling();

        RectTransform backgroundRect = backgroundObject.GetComponent<RectTransform>();
        backgroundRect.anchorMin = Vector2.zero;
        backgroundRect.anchorMax = Vector2.one;
        backgroundRect.offsetMin = Vector2.zero;
        backgroundRect.offsetMax = Vector2.zero;

        Image background = backgroundObject.GetComponent<Image>();
        background.color = Color.black;
        background.raycastTarget = true;
    }
}
