using UnityEngine;

[CreateAssetMenu(fileName = "NewCard", menuName = "Chess Mages/SimpleCard")]
public class CardData : ScriptableObject
{
    public Sprite fullCardSprite;  // This sprite includes the full card image with name/art/text
    public CardType type;          // Still useful if you want logic for Surge / Spell / Character
}

