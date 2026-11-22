using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TextCore.Text;

public class CharacterPanelUI : MonoBehaviour
{
    public GameObject cardPrefab;  // same CardUI prefab
    public Transform slot;         // empty RectTransform child
    public MageAbilityController abilityController;
    private CardUI active;

    public GameObject freezeSpellUIPrefab;
    private CardData currentCharacter;
    public static CharacterPanelUI Instance { get; private set; }

    private bool mageAbilityAvailable;
    public bool HasAvailableMageAbility => mageAbilityAvailable;

    private void Awake()
    {
        Instance = this;
    }
    public void Show(CardData character)
    {
        Clear();

        currentCharacter = character;
        mageAbilityAvailable = true;

        int kind = character.cardName == "Portal Mage" ? (int)NetworkSpellKind.Teleport :
            character.cardName == "Frost Mage" ? (int)NetworkSpellKind.Freeze :
            character.cardName == "Lightning Mage" ? (int)NetworkSpellKind.Lightning : -1;
        mageAbilityAvailable = kind >= 0;
        if (SpellRules.IsNet && NetPlayer.Local != null)
            NetPlayer.Local.SetMageAbilityAvailableServerRpc(mageAbilityAvailable, kind);
        var go = Instantiate(cardPrefab, slot);
        active = go.GetComponent<CardUI>();

        // IMPORTANT: selectionMode=true so click just calls callback (no spell, no destroy)
        active.LoadCard(character, true, OnMageCardClicked);

        // You WANT it clickable now
        active.SetInteractable(true);

        if (SpellRules.IsNet || (SoloSession.IsConfigured &&
            SoloSession.AIEnabled && SoloSession.Opponent == SoloOpponent.Stockfish))
        {
            if (!TryGetComponent<MageDuelHUD>(out var duel)) duel = gameObject.AddComponent<MageDuelHUD>();
            duel.enabled = true;
            duel.Bind(active, character, SpellRules.IsNet);
        }

        gameObject.SetActive(true);
    }


    void OnMageCardClicked(CardData mageCard)
    {
        if (!abilityController)
        {
            Debug.LogError("[CharacterPanelUI] abilityController not assigned.");
            return;
        }

        bool used = abilityController.TryActivateMageAbility(mageCard);

       /*if (used && active != null)
        {
            active.SetInteractable(false);
            Debug.Log("[CharacterPanelUI] Mage card disabled (single-use ability).");
            active.SetSpentVisual(true);
        }*/ //old disable
    }
    public void SetInteractable(bool canClick)
    {
        if (active) active.SetInteractable(canClick);
    }
    private void OnEnable()
    {
        if (abilityController != null)
            abilityController.OnAbilityUsed += HandleAbilityUsed;
    }
    private void OnDisable()
    {
        if (abilityController != null)
            abilityController.OnAbilityUsed -= HandleAbilityUsed;
    }
    private void HandleAbilityUsed()
    {
        mageAbilityAvailable = false;

        if (active != null)
        {
            active.SetInteractable(false);
            active.SetSpentVisual(true);
        }

       // Debug.Log("[CharacterPanelUI] Mage ability successfully consumed.");
    }
    public void Clear()
    {
        if (TryGetComponent<MageDuelHUD>(out var duel)) duel.enabled = false;
        if (active) Destroy(active.gameObject);
        active = null;
    }
}
