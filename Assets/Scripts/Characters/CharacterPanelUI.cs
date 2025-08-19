using UnityEngine;

public class CharacterPanelUI : MonoBehaviour
{
    public GameObject cardPrefab;  // same CardUI prefab
    public Transform slot;         // empty RectTransform child

    private CardUI active;

    public void Show(CardData character)
    {
        Clear();
        var go = Instantiate(cardPrefab, slot);
        active = go.GetComponent<CardUI>();
        active.LoadCard(character, false, null); // normal load
        active.SetInteractable(false);           // keep displayed, not clickable (for now)
        gameObject.SetActive(true);
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
