using UnityEngine;
using UnityEngine.SceneManagement;

public class GameOverController : MonoBehaviour
{
    public string gameSceneName = "OutdoorsScene";

    void Update()
    {
        if (Input.anyKeyDown)
        {
            SceneManager.LoadScene(gameSceneName);
        }
    }
}
