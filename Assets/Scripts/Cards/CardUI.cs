using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CardUI : MonoBehaviour
{
    public Image fullImage;
    public Button cardButton; // assign on prefab
    public CardData cardData;

    private System.Action<CardData> onSelectedCallback;
    private bool isSelectionMode;

    private void Start()
    {
        // Only warn when it's a spell card missing a UI prefab
        if (!isSelectionMode && cardData != null && cardData.cardtype != CardType.Character)
        {
            if (cardData.spellUI == null)
            {
                Debug.LogError("Spell UI is not assigned in CardData: " + cardData.name);
            }
        }
        RefreshInteractable(); // initial state if loaded via inspector
    }

    private void OnEnable()
    {
        // Subscribe to turn changes (to re-enable/disable per-turn free spell)
        if (TurnManager.Instance != null)
            TurnManager.Instance.OnTurnChanged += HandleTurnChanged;

        // For Resurrection, also listen to graveyard changes
        if (cardData != null && cardData.spellType == SpellType.Resurrect && ChessBoard.Instance != null)
            ChessBoard.Instance.OnGraveyardChanged += UpdateResurrectInteractable;

        RefreshInteractable();
    }

    private void OnDisable()
    {
        if (TurnManager.Instance != null)
            TurnManager.Instance.OnTurnChanged -= HandleTurnChanged;

        if (cardData != null && cardData.spellType == SpellType.Resurrect && ChessBoard.Instance != null)
            ChessBoard.Instance.OnGraveyardChanged -= UpdateResurrectInteractable;
    }

    private void OnDestroy()
    {
        // extra safety
        if (TurnManager.Instance != null)
            TurnManager.Instance.OnTurnChanged -= HandleTurnChanged;

        if (cardData != null && cardData.spellType == SpellType.Resurrect && ChessBoard.Instance != null)
            ChessBoard.Instance.OnGraveyardChanged -= UpdateResurrectInteractable;
    }

    public void LoadCard(CardData data)
    {
        LoadCard(data, false, null);
    }

    public void LoadCard(CardData data, bool selectionMode, System.Action<CardData> onSelected)
    {
        cardData = data;
        isSelectionMode = selectionMode;
        onSelectedCallback = onSelected;

        if (fullImage) fullImage.sprite = data.fullCardSprite;

        if (cardButton == null)
        {
            Debug.LogError($"CardUI on {name} is missing cardButton reference.");
            return;
        }

        cardButton.onClick.RemoveAllListeners();

        if (isSelectionMode)
        {
            cardButton.onClick.AddListener(() => onSelectedCallback?.Invoke(cardData));
        }
        else
        {
            cardButton.onClick.AddListener(ActivateSpell);
        }

        // Ensure graveyard listener is set correctly for Resurrection
        if (cardData.spellType == SpellType.Resurrect && ChessBoard.Instance != null)
        {
            ChessBoard.Instance.OnGraveyardChanged -= UpdateResurrectInteractable; // avoid double-sub
            ChessBoard.Instance.OnGraveyardChanged += UpdateResurrectInteractable;
        }

        RefreshInteractable();
    }

    private void HandleTurnChanged(TeamColor side)
    {
        RefreshInteractable();
    }

    private void RefreshInteractable()
    {
        if (cardButton == null || cardData == null || TurnManager.Instance == null) return;

        // Characters are not spells; keep default (usually clickable for selection decks, ignored otherwise)
        if (cardData.cardtype == CardType.Character)
        {
            SetInteractable(isSelectionMode); // clickable only in selection mode
            return;
        }

        TeamColor myTeam = TurnManager.Instance.currentTurn; // cards in hand are for current player’s side
        bool canCastFreeNow = TurnManager.Instance.CanCastFreeSpell(myTeam);

        if (cardData.spellType == SpellType.Resurrect)
        {
            // Requires both: free-spell available AND at least one captured piece for current player
            bool hasTargets = false;
            if (ChessBoard.Instance != null)
            {
                List<ChessBoard.CapturedPieceData> captured =
                    ChessBoard.Instance.graveyard.GetCapturedByTeam(myTeam);
                hasTargets = captured != null && captured.Count > 0;
            }
            SetInteractable(canCastFreeNow && hasTargets);
        }
        else
        {
            // Generic spells: gate by free-spell availability and any future extra conditions
            SetInteractable(canCastFreeNow /* && AdditionalSpellConditions() */);
        }
    }

    private void UpdateResurrectInteractable()
    {
        // Only relevant for Resurrection; reuse central refresh
        RefreshInteractable();
    }

    private void ActivateSpell()
    {
        if (cardData == null) return;

        // Characters should not trigger spell logic
        if (cardData.cardtype == CardType.Character)
        {
            Debug.Log($"Character card clicked (ignored for spell): {cardData.name}");
            return;
        }

        // Safety gate: re-check before activating (prevents race with UI state)
        TeamColor myTeam = TurnManager.Instance.currentTurn;
        if (!TurnManager.Instance.CanCastFreeSpell(myTeam))
        {
            Debug.Log("Free spell already used this turn.");
            return;
        }

        // Extra guard for Resurrection: block if no targets
        if (cardData.spellType == SpellType.Resurrect && ChessBoard.Instance != null)
        {
            var captured = ChessBoard.Instance.graveyard.GetCapturedByTeam(myTeam);
            if (captured == null || captured.Count == 0)
            {
                Debug.Log("No captured pieces - resurrection not available.");
                RefreshInteractable();
                return;
            }
        }

        // Spawn the spell UI or resolve immediately
        if (cardData.spellUI != null)
        {
            var canvas = GameObject.Find("MainCanvas");
            if (canvas == null)
            {
                Debug.LogError("MainCanvas not found. Cannot spawn spell UI.");
                return;
            }
            Instantiate(cardData.spellUI, canvas.transform);
        }
        else
        {
            // If you have instant spells without UI:
            // SpellManager.Instance.ResolveSpell(cardData);
        }

        // Consume the one free spell for this turn (do NOT end turn)
        TurnManager.Instance.RegisterFreeSpellCast();

        // Optionally refresh other cards in hand (they should all gray out now)
        // If your hand manager rebuilds UI, this may be redundant.
        // Broadcast through the same event to keep it simple:
        //TurnManager.Instance.OnTurnChanged?.Invoke(TurnManager.Instance.currentTurn);

        // Remove this card from hand after play
        TurnManager.Instance.RegisterFreeSpellCast(); // TurnManager will raise the event
        Destroy(gameObject); // or keep card and just gray it out if you prefer
    }

    public void SetInteractable(bool value)
    {
        if (cardButton) cardButton.interactable = value;
    }
}
