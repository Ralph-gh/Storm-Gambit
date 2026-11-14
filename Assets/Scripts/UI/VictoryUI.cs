using TMPro;
using Unity.Netcode;
using UnityEngine.UI;
using UnityEngine;
using UnityEngine.SceneManagement;


public class VictoryPanelUI : MonoBehaviour
{
    [Header("Panel Visibility")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private GameObject resultsTabButton;

    [Header("Scene Navigation")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    [Header("Main Action")]
    [SerializeField]private UnityEngine.UI.Button restartOrRematchButton;
    [SerializeField] private TMP_Text restartOrRematchLabel;
    [SerializeField] private TMP_Text rematchStatusText;

    private bool IsNetworked =>
        NetworkManager.Singleton != null &&
        NetworkManager.Singleton.IsListening;

    private void OnEnable()
    {
        ShowPanel();
        ConfigureForCurrentMode();
    }

    private void ConfigureForCurrentMode()
    {
        if (restartOrRematchLabel != null)
        {
            restartOrRematchLabel.text =
                IsNetworked ? "REMATCH" : "RESTART";
        }

        if (restartOrRematchButton != null)
            restartOrRematchButton.interactable = true;

        if (rematchStatusText != null)
            rematchStatusText.text = "";
    }

    // Hook the large Restart / Rematch button to this.
    public void RestartGame()
    {
        Time.timeScale = 1f;

        // OFFLINE: normal local restart.
        if (!IsNetworked)
        {
            SceneManager.LoadScene(
                SceneManager.GetActiveScene().name
            );

            return;
        }

        // MULTIPLAYER: never reload the scene locally.
        // Ask the authoritative GameState for a rematch instead.
        if (GameState.Instance == null)
        {
            Debug.LogError(
                "[REMATCH] GameState.Instance is missing."
            );

            return;
        }

        if (restartOrRematchButton != null)
            restartOrRematchButton.interactable = false;

        if (rematchStatusText != null)
            rematchStatusText.text = "Waiting for opponent...";

        GameState.Instance.RequestRematchServerRpc();
    }

    // Small collapse button in the top-right corner of the panel.
    public void HidePanel()
    {
        if (panelRoot != null)
            panelRoot.SetActive(false);

        if (resultsTabButton != null)
            resultsTabButton.SetActive(true);
    }
    // Small trophy / Results tab shown while the panel is hidden.
    public void ShowPanel()
    {
        if (panelRoot != null)
            panelRoot.SetActive(true);

        if (resultsTabButton != null)
            resultsTabButton.SetActive(false);
    }

    // Called by GameState when either player requests a rematch.
    public void ApplyRematchStatus(
        bool whiteReady,
        bool blackReady)
    {
        if (!IsNetworked || NetPlayer.Local == null)
            return;

        bool localReady =
            NetPlayer.Local.Side.Value == TeamColor.White
                ? whiteReady
                : blackReady;

        bool opponentReady =
            NetPlayer.Local.Side.Value == TeamColor.White
                ? blackReady
                : whiteReady;

        if (restartOrRematchButton != null)
            restartOrRematchButton.interactable = !localReady;

        if (rematchStatusText == null)
            return;

        if (localReady && opponentReady)
        {
            rematchStatusText.text = "Starting rematch...";
        }
        else if (localReady)
        {
            rematchStatusText.text = "Waiting for opponent...";
        }
        else if (opponentReady)
        {
            rematchStatusText.text =
                "Opponent wants a rematch.";
        }
        else
        {
            rematchStatusText.text = "";
        }
    }
    private void SetRematchInteractable(bool value)
    {
        if (restartOrRematchButton != null)
            restartOrRematchButton.interactable = value;
    }
    public void GoToMainMenu()
    {
        Time.timeScale = 1f;

        // If we are in a network session, leave it cleanly first.
        if (IsNetworked && NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.Shutdown();
        }

        SceneManager.LoadScene(mainMenuSceneName);
    }
    public void QuitGame()
    {
        // Shut networking down cleanly before exiting.
        if (IsNetworked)
            NetworkManager.Singleton.Shutdown();

        Application.Quit();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}

