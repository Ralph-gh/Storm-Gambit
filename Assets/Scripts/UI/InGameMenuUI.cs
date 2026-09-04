using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;

public class InGameMenuUI : MonoBehaviour
{
    [Header("Menu")]
    [SerializeField] private GameObject menuPanel;

    [Header("Scenes")]
    [SerializeField] private string mainMenuScene = "MainMenu";

    public void OpenMenu()
    {
        if (menuPanel != null)
            menuPanel.SetActive(true);
    }

    public void ResumeGame()
    {
        if (menuPanel != null)
            menuPanel.SetActive(false);
    }

    public void GoToMainMenu()
    {
        // Disconnect cleanly if this is a multiplayer game.
        if (NetworkManager.Singleton != null &&
            NetworkManager.Singleton.IsListening)
        {
            NetworkManager.Singleton.Shutdown();
        }

        SceneManager.LoadScene(mainMenuScene);
    }

    public void QuitGame()
    {
        // Disconnect cleanly if multiplayer is running.
        if (NetworkManager.Singleton != null &&
            NetworkManager.Singleton.IsListening)
        {
            NetworkManager.Singleton.Shutdown();
        }

#if UNITY_EDITOR
        Debug.Log("QUIT pressed - Application.Quit() only works in a build.");
#else
        Application.Quit();
#endif
    }
}