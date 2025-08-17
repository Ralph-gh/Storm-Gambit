using UnityEngine;
using UnityEngine.UI;

public class CardUI : MonoBehaviour
{
    public Image fullImage;
    public Button cardButton; // Add this in the prefab
    public CardData cardData;
    private void Start()
    {
        if (cardData.spellUI == null)
        {
            Debug.LogError("Spell UI is not assigned in CardData: " + cardData.name);
            return;
        }
    }
    
public void LoadCard(CardData data)
    {
        cardData = data;
        fullImage.sprite = data.fullCardSprite;

        cardButton.onClick.RemoveAllListeners();
        cardButton.onClick.AddListener(() => ActivateSpell());
    }
    void ActivateSpell()
    {
        Debug.Log($"Card clicked: {cardData.name}");

        // Optional: play sound or animate

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

    

}
