using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CardUI : MonoBehaviour
{
    public CardData cardData;                 // Drag this in from inspector or set via code
    public TeamColor ownerTeam;              // Set this when the card is spawned
    public TextMeshProUGUI titleText;        // Link UI components in Inspector
    public Image artworkImage;


    public void LoadCard(CardData data, TeamColor team)
    {
        cardData = data;
        ownerTeam = team;

        titleText.text = data.cardName;

        if (artworkImage == null)
        {
            Debug.LogError("Artwork image is NULL on card prefab!");
            return;
        }

        if (data.fullCardSprite == null)
        {
            Debug.LogError($"CardData sprite is missing for: {data.cardName}");
        }
        else
        {
            artworkImage.sprite = data.fullCardSprite;
            artworkImage.color = Color.white;
            Debug.Log($"[CardUI] Assigned sprite: {data.fullCardSprite.name}");
        }
    }


    public void OnCardClicked()
    {
        if (cardData == null || cardData.spellPrefab == null) return;

        GameObject spellGO = Instantiate(cardData.spellPrefab); //  this is a logic prefab only
        ISpell spell = spellGO.GetComponent<ISpell>();

        if (spell == null)
        {
            Debug.LogError("Assigned spell prefab does not implement ISpell.");
            return;
        }

        // Pass this card to the spell for destruction after cast
        if (spell is MonoBehaviour mb)
        {
            var field = mb.GetType().GetField("cardObject");
            if (field != null)
                field.SetValue(mb, this.gameObject);
        }

        if (spell.CanCast(ownerTeam))
            spell.Cast(ownerTeam);
        else
            Destroy(spellGO);
    }

}
