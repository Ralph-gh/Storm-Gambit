using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TextCore.Text;

public class CharacterPanelUI : MonoBehaviour
{
    public GameObject cardPrefab;  // same CardUI prefab
    public Transform slot;         // empty RectTransform child
    public MageAbilityController abilityController;
    private CardUI active;

    public GameObject freezeSpellUIPrefab;
    private CardData currentCharacter;

    public void Show(CardData character)
    {
        Clear();

        currentCharacter = character;
        var go = Instantiate(cardPrefab, slot);
        active = go.GetComponent<CardUI>();

        // IMPORTANT: selectionMode=true so click just calls callback (no spell, no destroy)
        active.LoadCard(character, true, OnMageCardClicked);

        // You WANT it clickable now
        active.SetInteractable(true);

        gameObject.SetActive(true);
    }


    void OnMageCardClicked(CardData mageCard)
    {
        if (!abilityController)
        {
            Debug.LogError("[CharacterPanelUI] abilityController not assigned.");
            return;
        }

        bool used = abilityController.TryActivateMageAbility(mageCard);

       /*if (used && active != null)
        {
            active.SetInteractable(false);
            Debug.Log("[CharacterPanelUI] Mage card disabled (single-use ability).");
            active.SetSpentVisual(true);
        }*/ //old disable
    }
    public void SetInteractable(bool canClick)
    {
        if (active) active.SetInteractable(canClick);
    }
    private void OnEnable()
    {
        if (abilityController != null)
            abilityController.OnAbilityUsed += HandleAbilityUsed;
    }
    private void OnDisable()
    {
        if (abilityController != null)
            abilityController.OnAbilityUsed -= HandleAbilityUsed;
    }
    private void HandleAbilityUsed()
    {
        if (active == null)
            return;

        active.SetInteractable(false);
        active.SetSpentVisual(true);

        Debug.Log("[CharacterPanelUI] Mage card disabled after successful ability.");
    }
    public void Clear()
    {
        if (active) Destroy(active.gameObject);
        active = null;
    }
}
