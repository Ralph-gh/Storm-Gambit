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

    // --- Small helpers so we don’t repeat logic everywhere ---

    private static bool IsNetActive =>
        Unity.Netcode.NetworkManager.Singleton && Unity.Netcode.NetworkManager.Singleton.IsListening;

    private static TeamColor ResolveMySide()
    {
        if (IsNetActive && NetPlayer.Local)
            return NetPlayer.Local.Side.Value;               // who I am (local player)
        return TurnManager.Instance ? TurnManager.Instance.currentTurn : TeamColor.White; // offline fallback
    }

    private static bool ResolveIsMyTurn(TeamColor mySide)
    {
        if (IsNetActive && GameState.Instance != null)
            return GameState.Instance.CurrentTurn.Value == mySide; // server-authoritative turn
        return TurnManager.Instance && TurnManager.Instance.IsPlayersTurn(mySide); // offline fallback
    }

    private static bool FreeSpellAvailable(bool isMyTurn)
    {
        if (!isMyTurn) return false;
        return TurnManager.Instance && !TurnManager.Instance.HasCastFreeSpellThisTurn;
    }

    // ----------------------------------------------------------------

    private void Start()
    {
        // Only warn when it's a spell card missing a UI prefab
        if (!isSelectionMode && cardData != null && cardData.cardtype != CardType.Character)
        {
            if (cardData.spellUI == null)
                Debug.LogError("Spell UI is not assigned in CardData: " + cardData.name);
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

        TeamColor mySide = ResolveMySide();
        bool isMyTurn = ResolveIsMyTurn(mySide);
        bool freeSpellAvailable = FreeSpellAvailable(isMyTurn);

        // Resurrection’s target availability is rechecked in RefreshInteractable()
        cardButton.interactable = freeSpellAvailable;
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
            cardButton.onClick.AddListener(() => onSelectedCallback?.Invoke(cardData));
        else
            cardButton.onClick.AddListener(ActivateSpell);

        // No graveyard listener here; OnEnable handles it (prevents duplicates)
        RefreshInteractable();
    }

    private void HandleTurnChanged(TeamColor side) => RefreshInteractable();

    private void RefreshInteractable()
    {
        if (cardButton == null || cardData == null || TurnManager.Instance == null) return;

        // Characters are selection-only
        if (cardData.cardtype == CardType.Character)
        {
            SetInteractable(isSelectionMode);
            return;
        }

        TeamColor mySide = ResolveMySide();
        bool isMyTurn = ResolveIsMyTurn(mySide);
        bool freeSpellAvailable = FreeSpellAvailable(isMyTurn);

        if (cardData.spellType == SpellType.Resurrect)
        {
            bool hasTargets = false;
            if (ChessBoard.Instance != null)
            {
                var captured = ChessBoard.Instance.graveyard.GetCapturedByTeam(mySide); // <-- mySide (not currentTurn)
                hasTargets = (captured != null && captured.Count > 0);
            }
            SetInteractable(freeSpellAvailable && hasTargets);
        }
        else
        {
            // Generic spells: just gate by turn + free-spell
            SetInteractable(freeSpellAvailable /* && add per-spell conditions here if needed */);
        }
    }

    private void UpdateResurrectInteractable() => RefreshInteractable();

    private void ActivateSpell()
    {
        if (cardData == null) return;

        TeamColor mySide = ResolveMySide();
        bool isMyTurn = ResolveIsMyTurn(mySide);
        bool freeSpellAvailable = FreeSpellAvailable(isMyTurn);

        // Gate by my turn + free-spell
        if (!freeSpellAvailable)
        {
            Debug.Log("Not your turn or free spell already used.");
            return;
        }

        if (cardData.cardtype == CardType.Character)
        {
            Debug.Log($"Character card clicked (ignored for spell): {cardData.name}");
            return;
        }

        // Extra guard for Resurrection: block if no targets (using mySide)
        if (cardData.spellType == SpellType.Resurrect && ChessBoard.Instance != null)
        {
            var captured = ChessBoard.Instance.graveyard.GetCapturedByTeam(mySide);
            if (captured == null || captured.Count == 0)
            {
                Debug.Log("No captured pieces - resurrection not available.");
                RefreshInteractable();
                return;
            }
        }

        // Spawn the spell UI or resolve immediately
        var canvas = GameObject.Find("MainCanvas");
        if (cardData.spellUI != null)
        {
            if (canvas == null)
            {
                Debug.LogError("MainCanvas not found. Cannot spawn spell UI.");
                return;
            }
            Instantiate(cardData.spellUI, canvas.transform);
        }
        else
        {
            // If you have instant spells without UI, resolve here,
            // BUT do not consume free spell unless the effect succeeds.
            // SpellManager.Instance.ResolveSpell(cardData);
        }

        // Do NOT consume the free spell here.
        // Consume it ONLY after the effect succeeds inside the specific Spell UI.
        Destroy(gameObject); // remove the card from the hand after play
    }

    public void SetInteractable(bool value)
    {
        if (cardButton) cardButton.interactable = value;
    }
}
