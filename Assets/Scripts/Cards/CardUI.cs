using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public class CardUI : MonoBehaviour
{
    public Image fullImage;
    public Button cardButton; // Add this in the prefab
    public CardData cardData;

    private System.Action<CardData> onSelectedCallback;
    private bool isSelectionMode;
    private void Start()
    {
        // Don't error if this is a Character or selection mode
        if (!isSelectionMode && cardData != null && cardData.cardtype != CardType.Character)
        {
            if (cardData.spellUI == null)
            {
                Debug.LogError("Spell UI is not assigned in CardData: " + cardData.name);
                return;
            }
        }
    }
    public void LoadCard(CardData data)
    {
        LoadCard(data, false, null);
    }

    public void LoadCard(CardData data, bool selectionMode, System.Action<CardData> onSelected)
    {
        cardData = data;
        isSelectionMode = selectionMode;
        onSelectedCallback = onSelected;

        if (fullImage) fullImage.sprite = data.fullCardSprite;

        cardButton.onClick.RemoveAllListeners();

        if (isSelectionMode)
        {
            // In selection mode, clicking just reports the choice; no spell, no destroy.
            cardButton.onClick.AddListener(() => onSelectedCallback?.Invoke(cardData));
        }
        else
        {
            // Normal flow (playable card)
            cardButton.onClick.AddListener(() => ActivateSpell());
        }
    }
    void ActivateSpell()
    {
        Debug.Log($"Card clicked: {cardData.name}");
        if (cardData == null) return;
        // Optional: play sound or animate

        // Characters should not trigger spell logic
        if (cardData.cardtype == CardType.Character)
        {
            Debug.Log($"Character card clicked (ignored for spell): {cardData.name}");
            return;
        }

        if (cardData.spellUI != null)
        {
            Instantiate(cardData.spellUI, GameObject.Find("MainCanvas").transform);
        }

        else
        {
            // Immediate spells can resolve directly
            //SpellManager.Instance.ResolveSpell(cardData );
        }

        Destroy(gameObject); //  Remove the card from the hand after play
    }
    public void SetInteractable(bool value)
    {
        if (cardButton) cardButton.interactable = value;
    }


}
