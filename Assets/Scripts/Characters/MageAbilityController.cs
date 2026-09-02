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
    private bool abilityInProgress = false;

    public bool TryActivateMageAbility(CardData mageCard)
    {
        if (abilityUsed)
        {
            Debug.Log("[MageAbility] Ability already used.");
            return false;
        }

        if (abilityInProgress)
        {
            Debug.Log("[MageAbility] An ability is already being resolved.");
            return false;
        }

        if (mageCard == null)
        {
            Debug.LogWarning("[MageAbility] mageCard is null.");
            return false;
        }

        GameObject canvas = GameObject.Find("MainCanvas");

        if (canvas == null)
        {
            Debug.LogError("[MageAbility] MainCanvas not found.");
            return false;
        }

        // =====================================================
        // PORTAL MAGE
        // =====================================================

        if (mageCard.cardName == portalMageCardName)
        {
            if (teleportationSpellUIPrefab == null)
            {
                Debug.LogError(
                    "[MageAbility] teleportationSpellUIPrefab not assigned."
                );

                return false;
            }

            GameObject spellObject =
                Instantiate(
                    teleportationSpellUIPrefab,
                    canvas.transform
                );

            TeleportationSpellUI teleportUI =
                spellObject.GetComponent<TeleportationSpellUI>();

            if (teleportUI == null)
            {
                Debug.LogError(
                    "[MageAbility] Assigned Teleport prefab does not contain TeleportationSpellUI."
                );

                Destroy(spellObject);
                return false;
            }

            abilityInProgress = true;

            teleportUI.ConfigureAsMageAbility(
                OnMageAbilitySucceeded,
                OnMageAbilityCancelled
            );

            Debug.Log(
                "[MageAbility] Portal Mage Teleport started."
            );

            return true;
        }

        // =====================================================
        // FROST MAGE
        // =====================================================

        if (mageCard.cardName == frostMageCardName)
        {
            if (freezeSpellUIPrefab == null)
            {
                Debug.LogError(
                    "[MageAbility] freezeSpellUIPrefab not assigned."
                );

                return false;
            }

            Instantiate(
                freezeSpellUIPrefab,
                canvas.transform
            );

            // Freeze still uses old behavior for now.
            abilityUsed = true;

            Debug.Log(
                "[MageAbility] Frost Mage ability used (Freeze)."
            );

            return true;
        }

        Debug.Log(
            "[MageAbility] This mage has no implemented ability: "
            + mageCard.cardName
        );

        return false;
    }

    private void OnMageAbilitySucceeded()
    {
        abilityInProgress = false;
        abilityUsed = true;

        Debug.Log(
            "[MageAbility] Mage ability completed successfully."
        );
    }

    private void OnMageAbilityCancelled()
    {
        abilityInProgress = false;

        Debug.Log(
            "[MageAbility] Mage ability cancelled. Ability remains available."
        );
    }

    public bool IsUsed => abilityUsed;
    public bool IsInProgress => abilityInProgress;
}