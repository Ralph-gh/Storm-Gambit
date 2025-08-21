using UnityEngine;

public class GameBoot : MonoBehaviour
{
    //public CharacterSelectionUI selectionUI;
    public CharacterPanelUI characterPanel;

    public static CardData SelectedCharacter { get; private set; }

    void Start()
    {
        // Pause input here if needed
        Debug.Log("[GameBoot] Start()");
       // if (!selectionUI) Debug.LogError("[GameBoot] selectionUI is NULL (assign scene instance)");
       //else Debug.Log("[GameBoot] selectionUI ref OK: " + selectionUI.gameObject.name);

       // selectionUI.Show(OnMageChosen);
    }

    void OnMageChosen(CardData chosen)
    {
        SelectedCharacter = chosen;

        // Unpause input here if needed
        if (characterPanel) characterPanel.Show(chosen);
    }
}
