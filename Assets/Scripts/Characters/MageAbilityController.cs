using UnityEngine;

public class MageAbilityController : MonoBehaviour
{
    [Header("Mage Names")]
    public string portalMageCardName = "Portal Mage";
    public string frostMageCardName = "Frost Mage";
    public string lightningMageCardName = "Lightning Mage";

    [Header("Ability Prefabs")]
    public GameObject teleportationSpellUIPrefab;
    public GameObject freezeSpellUIPrefab;
    public System.Action OnAbilityUsed;
    [SerializeField]
    private GameObject lightningMageAbilityUIPrefab;

    private SpellPromptPanelUI activePrompt;
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
        // LIGHTNING MAGE
        // =====================================================
        if (mageCard.cardName == "Lightning Mage")
        {
            if (lightningMageAbilityUIPrefab == null)
            {
                Debug.LogError(
                    "[MageAbility] lightningMageAbilityUIPrefab not assigned."
                );

                return false;
            }

            GameObject abilityObject =
                Instantiate(
                    lightningMageAbilityUIPrefab,
                    canvas.transform
                );

            LightningMageAbilityUI lightningUI =
                abilityObject.GetComponent<LightningMageAbilityUI>();

            if (lightningUI == null)
            {
                Debug.LogError(
                    "[MageAbility] Assigned Lightning Mage prefab " +
                    "does not contain LightningMageAbilityUI."
                );

                Destroy(abilityObject);
                return false;
            }

            abilityInProgress = true;

            lightningUI.ConfigureAsMageAbility(
                OnMageAbilitySucceeded,
                OnMageAbilityCancelled
            );

            Debug.Log(
                "[MageAbility] Lightning Mage ability started."
            );

            return true;
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

            GameObject spellObject =
      Instantiate(
          freezeSpellUIPrefab,
          canvas.transform
      );

            FreezeSpellUI freezeUI =
                spellObject.GetComponent<FreezeSpellUI>();

            if (freezeUI == null)
            {
                Debug.LogError(
                    "[MageAbility] Assigned Freeze prefab does not contain FreezeSpellUI."
                );

                Destroy(spellObject);
                return false;
            }

            abilityInProgress = true;

            freezeUI.ConfigureAsMageAbility(
                OnMageAbilitySucceeded,
                OnMageAbilityCancelled
            );

            Debug.Log(
                "[MageAbility] Frost Mage Freeze started."
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

        OnAbilityUsed?.Invoke();

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