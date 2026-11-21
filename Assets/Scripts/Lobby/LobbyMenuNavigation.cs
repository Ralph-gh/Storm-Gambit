using UnityEngine;
using UnityEngine.UI;
using UIButton = UnityEngine.UI.Button;

// Switches UI only; the scene's existing network and service objects stay in place.
public class LobbyMenuNavigation : MonoBehaviour
{
    public GameObject landing;
    public LobbyBrowserUI browser;
    public UIButton hostButton, joinButton, mainMenuButton;

    private void Awake()
    {
        hostButton.onClick.AddListener(OpenHost);
        joinButton.onClick.AddListener(OpenJoin);
        mainMenuButton.onClick.AddListener(browser.controller.BackToMainMenu);
        ShowLanding();
    }

    public void OpenHost() => Open(true);
    public void OpenJoin() => Open(false);

    private void Open(bool host)
    {
        landing.SetActive(false);
        browser.gameObject.SetActive(true);
        browser.SelectTab(host);
    }

    public void ShowLanding()
    {
        browser.gameObject.SetActive(false);
        landing.SetActive(true);
    }
}
