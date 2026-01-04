using UnityEngine;

[CreateAssetMenu(menuName = "StormGambit/Mages/Ability Data")]
public class MageAbilityData : ScriptableObject
{
    public MageAbilityId abilityId = MageAbilityId.None;
    public string displayName = "Ability";
    public Sprite icon;
    public MageAbilityTrigger trigger = MageAbilityTrigger.Active;

    [Header("Execution (optional refs)")]
    public GameObject spellUIPrefab; // ex: TeleportationSpellUI prefab
    public bool oncePerTurn = true;  // future-proof flag
}
