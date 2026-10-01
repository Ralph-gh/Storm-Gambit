using UnityEngine;
using UnityEngine.SceneManagement;

public class CampaignSideSelectionUI : MonoBehaviour
{
    [SerializeField]
    private string gameplaySceneName = "Game";

    public void ChooseWhite()
    {
        BeginCampaign(TeamColor.White);
    }

    public void ChooseBlack()
    {
        BeginCampaign(TeamColor.Black);
    }

    private void BeginCampaign(TeamColor chosenSide)
    {
        SoloSession.ConfigureCampaign(chosenSide);

        if (string.IsNullOrWhiteSpace(gameplaySceneName))
        {
            Debug.LogWarning(
                "[CAMPAIGN] Gameplay scene name is empty. " +
                "Session configured, but no scene loaded."
            );
            return;
        }

        SceneManager.LoadScene(gameplaySceneName);
    }
}
