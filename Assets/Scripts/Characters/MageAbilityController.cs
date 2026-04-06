using UnityEngine;

public class MageAbilityController : MonoBehaviour
{
    [Header("Mage Names")]
    public string portalMageCardName = "Portal Mage";
    public string frostMageCardName = "Frost Mage";

    [Header("Ability Prefabs")]
    public GameObject teleportationSpellUIPrefab;
    public GameObject freezeSpellUIPrefab;

    private bool abilityUsed = false;

    public bool TryActivateMageAbility(CardData mageCard)
    {
        if (abilityUsed)
        {
            Debug.Log("[MageAbility] Ability already used.");
            return false;
        }

        if (mageCard == null)
        {
            Debug.LogWarning("[MageAbility] mageCard is null.");
            return false;
        }

        var canvas = GameObject.Find("MainCanvas");
        if (canvas == null)
        {
            Debug.LogError("[MageAbility] MainCanvas not found.");
            return false;
        }

        // Portal Mage
        if (mageCard.cardName == portalMageCardName)
        {
            if (teleportationSpellUIPrefab == null)
            {
                Debug.LogError("[MageAbility] teleportationSpellUIPrefab not assigned.");
                return false;
            }

            Instantiate(teleportationSpellUIPrefab, canvas.transform);
            abilityUsed = true;

            Debug.Log("[MageAbility] Portal Mage ability used (Teleport).");
            return true;
        }

        // Frost Mage
        if (mageCard.cardName == frostMageCardName)
        {
            if (freezeSpellUIPrefab == null)
            {
                Debug.LogError("[MageAbility] freezeSpellUIPrefab not assigned.");
                return false;
            }

            Instantiate(freezeSpellUIPrefab, canvas.transform);
            abilityUsed = true;

            Debug.Log("[MageAbility] Frost Mage ability used (Freeze).");
            return true;
        }

        Debug.Log("[MageAbility] This mage has no implemented ability: " + mageCard.cardName);
        return false;
    }

    public bool IsUsed => abilityUsed;
}