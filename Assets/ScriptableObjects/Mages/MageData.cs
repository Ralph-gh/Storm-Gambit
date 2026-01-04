using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "StormGambit/Mages/Mage Data")]
public class MageData : ScriptableObject
{
    public MageId mageId = MageId.None;
    public string displayName = "Mage";

    [Header("UI")]
    public Sprite cardArt;    // use this in CardUI image
    //public Sprite portrait;   // optional

    [Header("Abilities")]
    public List<MageAbilityData> abilities = new();
}
