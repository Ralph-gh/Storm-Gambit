using UnityEngine;

public class MageAbilityController : MonoBehaviour
{
    [Header("Mage Identity")]
    public string portalMageCardName = "Portal Mage";
    // Later we’ll replace this with MageData / MageId (solid multiplayer-friendly)

    [Header("Teleport Ability")]
    public GameObject teleportationSpellUIPrefab; // your TeleportationSpellUI prefab

    public void TryActivateMageAbility(CardData mageCard)
    {
        if (!mageCard)
        {
            Debug.LogWarning("[MageAbility] mageCard is null.");
            return;
        }

        // Only Portal Mage for now
        if (mageCard.cardName != portalMageCardName)
        {
            Debug.Log("[MageAbility] No active ability for this mage: " + mageCard.cardName);
            return;
        }

        if (!teleportationSpellUIPrefab)
        {
            Debug.LogError("[MageAbility] teleportationSpellUIPrefab not assigned.");
            return;
        }

        // Same flow as teleport spell: open the TeleportationSpellUI
        var canvas = GameObject.Find("MainCanvas");
        if (!canvas)
        {
            Debug.LogError("[MageAbility] MainCanvas not found.");
            return;
        }

        Instantiate(teleportationSpellUIPrefab, canvas.transform);
        Debug.Log("[MageAbility] Portal Mage activated Teleport.");
    }
}
