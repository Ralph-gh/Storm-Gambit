using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    [Header("Audio")]
    public AudioClip drawSound;
    public AudioSource audioSource;
    public AudioClip clickSound;

    [Header("Menu Panels")]
    [SerializeField] private GameObject mainMenuPanel;     // Title
    [SerializeField] private GameObject soloMenuPanel;     // Solo
    [SerializeField] private GameObject campaignPanel;     // CampaignPanel

    [Header("Scene Names")]
    [SerializeField] private string soloSceneName = "GameScene";
    [SerializeField] private string multiplayerLobbySceneName = "LobbyScene";

    private void Start()
    {
        ShowMainMenuImmediate();
    }

    // =========================================================
    // MAIN -> SOLO
    // Keep this method name so your existing Play/Solo button
    // can keep calling MainMenu.PlayGame().
    // =========================================================
    public void PlayGame()
    {
        PlayClickSound();

        if (mainMenuPanel != null)
            mainMenuPanel.SetActive(false);

        if (soloMenuPanel != null)
            soloMenuPanel.SetActive(true);

        if (campaignPanel != null)
            campaignPanel.SetActive(false);
    }

    // =========================================================
    // SOLO -> CAMPAIGN SIDE SELECTION
    // =========================================================
    public void OpenCampaignMenu()
    {
        PlayClickSound();

        if (mainMenuPanel != null)
            mainMenuPanel.SetActive(false);

        if (soloMenuPanel != null)
            soloMenuPanel.SetActive(false);

        if (campaignPanel != null)
            campaignPanel.SetActive(true);
    }

    // =========================================================
    // CAMPAIGN: PLAYER CHOOSES WHITE
    // =========================================================
    public void ChooseCampaignWhite()
    {
        SoloSession.ConfigureCampaign(TeamColor.White);
        StartCoroutine(LoadSceneAfterClick(soloSceneName));
    }

    // =========================================================
    // CAMPAIGN: PLAYER CHOOSES BLACK
    // =========================================================
    public void ChooseCampaignBlack()
    {
        SoloSession.ConfigureCampaign(TeamColor.Black);
        StartCoroutine(LoadSceneAfterClick(soloSceneName));
    }

    // =========================================================
    // PRACTICE
    // For now Practice loads GameScene without Stockfish.
    // Later this can open the Practice AI Enable/Disable setup.
    // =========================================================
    public void StartPractice()
    {
        SoloSession.Reset();
        StartCoroutine(LoadSceneAfterClick(soloSceneName));
    }

    // =========================================================
    // BACK BUTTONS
    // =========================================================
    public void BackToMainMenu()
    {
        PlayClickSound();
        ShowMainMenuImmediate();
    }

    public void BackToSoloMenu()
    {
        PlayClickSound();

        if (mainMenuPanel != null)
            mainMenuPanel.SetActive(false);

        if (soloMenuPanel != null)
            soloMenuPanel.SetActive(true);

        if (campaignPanel != null)
            campaignPanel.SetActive(false);
    }

    // =========================================================
    // MULTIPLAYER
    // =========================================================
    public void OpenMultiplayerLobby()
    {
        SoloSession.Reset();
        StartCoroutine(LoadSceneAfterClick(multiplayerLobbySceneName));
    }

    // =========================================================
    // SETTINGS
    // =========================================================
    public void OpenSettings()
    {
        PlayClickSound();

        // To be updated later with settings.
        Debug.Log("Settings menu opened");
    }

    // =========================================================
    // QUIT
    // =========================================================
    public void QuitGame()
    {
        PlayClickSound();

        Application.Quit();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    // =========================================================
    // HELPERS
    // =========================================================
    private void ShowMainMenuImmediate()
    {
        SoloSession.Reset();

        if (mainMenuPanel != null)
            mainMenuPanel.SetActive(true);

        if (soloMenuPanel != null)
            soloMenuPanel.SetActive(false);

        if (campaignPanel != null)
            campaignPanel.SetActive(false);
    }

    private void PlayClickSound()
    {
        if (audioSource != null && clickSound != null)
            audioSource.PlayOneShot(clickSound);
    }

    private IEnumerator LoadSceneAfterClick(string sceneName)
    {
        if (audioSource != null && clickSound != null)
        {
            audioSource.PlayOneShot(clickSound);
            yield return new WaitForSeconds(clickSound.length);
        }

        SceneManager.LoadScene(sceneName);
    }
}
