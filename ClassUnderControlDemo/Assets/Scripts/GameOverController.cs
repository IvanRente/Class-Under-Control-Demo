using UnityEngine;
using UnityEngine.SceneManagement;

public class GameOverController : MonoBehaviour
{
    public string gameSceneName = "OutdoorsScene";
    public float inputDelay = 0.5f;

    float timer;
    bool canRestart;
    bool loadingScene;

    void OnEnable()
    {
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
            if (timer >= inputDelay && !Input.anyKey)
                canRestart = true;

            return;
        }

        if (Input.anyKeyDown && !string.IsNullOrWhiteSpace(gameSceneName))
        {
            loadingScene = true;
            SceneManager.LoadScene(gameSceneName);
        }
    }
}
