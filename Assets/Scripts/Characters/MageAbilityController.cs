using UnityEngine;

public class MageAbilityController : MonoBehaviour
{
    [Header("Teleport (Portal Mage)")]
    public string portalMageCardName = "Portal Mage";
    public GameObject teleportationSpellUIPrefab;

    private bool abilityUsed = false;

    public bool TryActivateMageAbility(CardData mageCard)
    {
        if (abilityUsed)
        {
            Debug.Log("[MageAbility] Ability already used.");
            return false;
        }

        if (!mageCard)
        {
            Debug.LogWarning("[MageAbility] mageCard is null.");
            return false;
        }

        // Only Portal Mage has an ability for now
        if (mageCard.cardName != portalMageCardName)
        {
            Debug.Log("[MageAbility] This mage has no ability: " + mageCard.cardName);
            return false;
        }

        if (!teleportationSpellUIPrefab)
        {
            Debug.LogError("[MageAbility] teleportationSpellUIPrefab not assigned.");
            return false;
        }

        var canvas = GameObject.Find("MainCanvas");
        if (!canvas)
        {
            Debug.LogError("[MageAbility] MainCanvas not found.");
            return false;
        }

        Instantiate(teleportationSpellUIPrefab, canvas.transform);

        // Mark ability as consumed
        abilityUsed = true;
        Debug.Log("[MageAbility] Portal Mage ability used (Teleport).");
        return true;
    }

    public bool IsUsed => abilityUsed;
}
