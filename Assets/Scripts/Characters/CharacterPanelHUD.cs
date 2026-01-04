using UnityEngine;

public class CharacterPanelHUD : MonoBehaviour
{
    public Transform slot;
    public GameObject cardPrefab;           // CardUI prefab
    //public MageAbilityHUD abilityHUD;       // optional UI that shows ability buttons

    private GameObject active;

    public void Show(MageData mage)
    {
        Clear();

        var go = Instantiate(cardPrefab, slot);
        var ui = go.GetComponent<CardUI>();
        if (!ui) { Debug.LogError("[CharacterPanelHUD] Card prefab missing CardUI."); return; }

        ui.LoadMage(mage, false, null); // display-only
        active = go;

        gameObject.SetActive(true);

        //if (abilityHUD) abilityHUD.SetMage(mage);
    }

    void Clear()
    {
        if (active) Destroy(active);
        active = null;
    }
}
