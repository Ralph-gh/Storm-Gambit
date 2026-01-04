using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CardUI : MonoBehaviour
{
    public Image fullImage;
    public Button cardButton; // assign on prefab
    public CardData cardData;
    private System.Action<MageData> onMageSelected;
    private bool isMageSelection;

    private System.Action<CardData> onSelectedCallback;
    private bool isSelectionMode;
    
    //Keeping the spell until completed
    private bool isCastingSpell;
    private GameObject activeSpellUI;

    // --- Small helpers so we don’t repeat logic everywhere ---

    private static bool IsNetActive =>
        Unity.Netcode.NetworkManager.Singleton && Unity.Netcode.NetworkManager.Singleton.IsListening;
    public void CancelPendingSpellCast()
    {
        isCastingSpell = false;
        activeSpellUI = null;
        RefreshInteractable();
    }

    public void ConsumeCardAfterSuccessfulCast()
    {
        isCastingSpell = false;
        activeSpellUI = null;
        Destroy(gameObject);
    }

    private void Update()
    {
        // Safety: if UI is destroyed without calling back, restore the card.
        if (isCastingSpell && activeSpellUI == null)
            CancelPendingSpellCast();
    }
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
        if (isCastingSpell)
        {
            SetInteractable(false);
            return;
        }

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

        // Safety: don't open two spell UIs for the same card.
        if (isCastingSpell) return;

        var canvas = GameObject.Find("MainCanvas");
        if (cardData.spellUI != null)
        {
            if (canvas == null)
            {
                Debug.LogError("MainCanvas not found. Cannot spawn spell UI.");
                return;
            }

            isCastingSpell = true;
            SetInteractable(false);

            activeSpellUI = Instantiate(cardData.spellUI, canvas.transform);

            // Allows spell UI script to live on root OR children
            activeSpellUI.BroadcastMessage("BindSourceCard", this, SendMessageOptions.DontRequireReceiver);
        }
        else
        {
            // Instant spells (if any): consume immediately only after success
            ConsumeCardAfterSuccessfulCast();
        }
    }

    public void SetInteractable(bool value)
    {
        if (cardButton) cardButton.interactable = value;
    }
    public void LoadMage(MageData mage, bool selectionMode, System.Action<MageData> onSelected)
    {
        isMageSelection = selectionMode;
        onMageSelected = onSelected;

        if (fullImage) fullImage.sprite = mage.cardArt;

        if (!cardButton) cardButton = GetComponent<Button>();
        if (!cardButton)
        {
            Debug.LogError("[CardUI] Missing Button component.");
            return;
        }

        cardButton.onClick.RemoveAllListeners();

        if (selectionMode)
        {
            cardButton.onClick.AddListener(() => onMageSelected?.Invoke(mage));
        }
        else
        {
            cardButton.interactable = false; // display-only
        }
    }
}
