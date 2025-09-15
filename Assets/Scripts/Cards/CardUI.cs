using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public class CardUI : MonoBehaviour
{
    public Image fullImage;
    public Button cardButton; // Add this in the prefab
    public CardData cardData;

    private System.Action<CardData> onSelectedCallback;
    private bool isSelectionMode;
    private void Start()
    {
        // Don't error if this is a Character or selection mode
        if (!isSelectionMode && cardData != null && cardData.cardtype != CardType.Character)
        {
            if (cardData.spellUI == null)
            {
                Debug.LogError("Spell UI is not assigned in CardData: " + cardData.name);
                return;
            }
        }
    }
    public void LoadCard(CardData data)
    {
        LoadCard(data, false, null);
        if (cardData.spellType == SpellType.Resurrect)
        {
            TeamColor currentTeam = TurnManager.Instance.currentTurn;
            var captured = ChessBoard.Instance.graveyard.GetCapturedByTeam(currentTeam);
            SetInteractable(captured.Count > 0);
        }
    }

    private void OnEnable()
    {
        // If card already loaded and it's Resurrect, ensure we’re listening
        if (cardData != null && cardData.spellType == SpellType.Resurrect && ChessBoard.Instance != null)
        {
            ChessBoard.Instance.OnGraveyardChanged += UpdateResurrectInteractable;
            UpdateResurrectInteractable(); // initial state
        }
    }

    private void OnDisable()
    {
        if (cardData != null && cardData.spellType == SpellType.Resurrect && ChessBoard.Instance != null)
            ChessBoard.Instance.OnGraveyardChanged -= UpdateResurrectInteractable;
    }

    public void LoadCard(CardData data, bool selectionMode, System.Action<CardData> onSelected)
    {
        cardData = data;
        isSelectionMode = selectionMode;
        onSelectedCallback = onSelected;

        if (fullImage) fullImage.sprite = data.fullCardSprite;
        cardButton.onClick.RemoveAllListeners();

        if (cardData.spellType == SpellType.Resurrect && ChessBoard.Instance != null)
        {
            ChessBoard.Instance.OnGraveyardChanged -= UpdateResurrectInteractable; // avoid double-sub
            ChessBoard.Instance.OnGraveyardChanged += UpdateResurrectInteractable;
            UpdateResurrectInteractable(); // set initial interactable
        }

        if (isSelectionMode)
            cardButton.onClick.AddListener(() => onSelectedCallback?.Invoke(cardData));
        else
            cardButton.onClick.AddListener(() => ActivateSpell());
    }
    void ActivateSpell()
    {
        Debug.Log($"Card clicked: {cardData.name}");
        if (cardData == null) return;
        // Optional: play sound or animate

        // Characters should not trigger spell logic
        if (cardData.cardtype == CardType.Character)
        {
            Debug.Log($"Character card clicked (ignored for spell): {cardData.name}");
            return;
        }
        /*if (cardData.spellType == SpellType.Resurrect)
        {
            TeamColor currentTeam = TurnManager.Instance.currentTurn;
            var captured = ChessBoard.Instance.graveyard.GetCapturedByTeam(currentTeam);
            if (captured.Count == 0) { Debug.Log("No captured pieces - resurrection not available."); }
            SetInteractable(false); // disable this card
            return; //don't open UI
        } */
        if (cardData.spellUI != null)
        {
            Instantiate(cardData.spellUI, GameObject.Find("MainCanvas").transform);
        }

        else
        {
            // Immediate spells can resolve directly
            //SpellManager.Instance.ResolveSpell(cardData );
        }


        Destroy(gameObject); //  Remove the card from the hand after play
    }
    public void SetInteractable(bool value)
    {
        if (cardButton) cardButton.interactable = value;
    }
    private void UpdateResurrectInteractable()
    {
        if (cardButton == null || ChessBoard.Instance == null) return;

        TeamColor currentTeam = TurnManager.Instance.currentTurn;
        List<ChessBoard.CapturedPieceData> captured =
            ChessBoard.Instance.graveyard.GetCapturedByTeam(currentTeam); // <— declared here

        // Enable only if there is at least 1 captured piece for the current player
        SetInteractable(captured != null && captured.Count > 0);
    }

   /* private void OnDestroy()
    {
        if (cardData != null && cardData.spellType == SpellType.Resurrect && ChessBoard.Instance != null)
            ChessBoard.Instance.OnGraveyardChanged -= UpdateResurrectInteractable;
    }*/

}
