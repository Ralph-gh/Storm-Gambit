using UnityEngine;

public enum SpellType { None, Teleport, Resurrect, Damage }
[CreateAssetMenu(fileName = "NewCard", menuName = "Chess Mages/SimpleCard")]

public class CardData : ScriptableObject
{
    public string cardName;         
    public Sprite fullCardSprite;  // This sprite includes the full card image with name/art/text
    public CardType cardtype;          // Still useful if you want logic for Surge / Spell / Character

    [Header("Spell Logic")]
    public SpellType spellType;
    public GameObject spellUI; // Assign a prefab if this spell opens a UI
}

