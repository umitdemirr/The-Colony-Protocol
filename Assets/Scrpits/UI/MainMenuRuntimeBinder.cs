using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuRuntimeBinder : MonoBehaviour
{
    [SerializeField] Button newGameButton;
    [SerializeField] Button exitButton;
    [SerializeField] string gameplaySceneName = "SampleScene";

    void Awake()
    {
        if (newGameButton != null)
        {
            newGameButton.onClick.RemoveListener(OnNewGameClicked);
            newGameButton.onClick.AddListener(OnNewGameClicked);
        }

        if (exitButton != null)
        {
            exitButton.onClick.RemoveListener(OnExitClicked);
            exitButton.onClick.AddListener(OnExitClicked);
        }
    }

    void OnDestroy()
    {
        if (newGameButton != null)
        {
            newGameButton.onClick.RemoveListener(OnNewGameClicked);
        }

        if (exitButton != null)
        {
            exitButton.onClick.RemoveListener(OnExitClicked);
        }
    }

    void OnNewGameClicked()
    {
        if (string.IsNullOrWhiteSpace(gameplaySceneName))
        {
            return;
        }

        SceneManager.LoadScene(gameplaySceneName);
    }

    void OnExitClicked()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
