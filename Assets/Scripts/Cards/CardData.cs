using System.Collections.Generic;
using UnityEngine;


public enum SpellType { None, Teleport, Resurrect, ExplosiveTrap, MinorResurrect }
[CreateAssetMenu(fileName = "NewCard", menuName = "Chess Mages/SimpleCard")]

public class CardData : ScriptableObject
{
    public bool IsResurrection => spellType == SpellType.Resurrect || spellType == SpellType.MinorResurrect;

    public static bool CanMinorResurrect(PieceType type) =>
        type == PieceType.Pawn || type == PieceType.Bishop || type == PieceType.Knight;

    public bool CanResurrect(PieceType type) => IsResurrection &&
        (spellType != SpellType.MinorResurrect || CanMinorResurrect(type));
    
    public string cardName;         
    public Sprite fullCardSprite;  // This sprite includes the full card image with name/art/text
    public CardType cardtype;          // Still useful if you want logic for Surge / Spell / Character

    [Header("Spell Logic")]
    public SpellType spellType;
    public GameObject spellUI; // Assign a prefab if this spell opens a UI

    [Header("Character (Mage) Data")]
   //public MageId mageId = MageId.None;
    public List<AbilityId> abilities = new List<AbilityId>();
}

public enum AbilityId
{
    None,
    Teleport,        // Portal Mage active
    // later: FreezeTile, IgnitePiece, Heal, TimeShift, etc.
}
