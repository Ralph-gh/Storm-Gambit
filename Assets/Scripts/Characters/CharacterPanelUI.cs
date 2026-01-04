using UnityEngine;

public class CharacterPanelUI : MonoBehaviour
{
    public GameObject cardPrefab;  // same CardUI prefab
    public Transform slot;         // empty RectTransform child
    public MageAbilityController abilityController;
    private CardUI active;

    public void Show(CardData character)
    {
        Clear();

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

        abilityController.TryActivateMageAbility(mageCard);
    }
    public void SetInteractable(bool canClick)
    {
        if (active) active.SetInteractable(canClick);
    }

    public void Clear()
    {
        if (active) Destroy(active.gameObject);
        active = null;
    }
}
