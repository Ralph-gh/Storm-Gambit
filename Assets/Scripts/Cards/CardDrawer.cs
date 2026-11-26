using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class CardDrawer : MonoBehaviour
{
    public static CardDrawer Instance { get; private set; }
    public static event Action ChoiceFinished;
    public static bool BlocksPlay => Instance && Instance.blocked;

    public int CurrentSpellCount { get; private set; }
    public bool HandInitialized { get; private set; }
    public List<CardData> cardPool;
    public GameObject cardPrefab;
    public Transform handPanel;
    public List<CardData> defaultCardPool;
    public List<CardData> stockfishCampaignPool;

    private List<CardData> pool;
    private CardChoicePanel panel;
    private bool blocked = true;
    private int[] choices = Array.Empty<int>();
    private int offerId;
    private bool submitted;
    private NetworkManager session;
    private CharacterChoicePanel magePicker;

    void Awake() => Instance = this;

    void Start()
    {
        bool campaign = !SpellRules.IsNet && SoloSession.IsConfigured &&
            SoloSession.Mode == SoloGameMode.Campaign && SoloSession.AIEnabled &&
            SoloSession.Opponent == SoloOpponent.Stockfish;
        pool = campaign ? stockfishCampaignPool : defaultCardPool;
        panel = CardChoicePanel.Create(handPanel);
        magePicker = FindFirstObjectByType<CharacterChoicePanel>();
        if (magePicker) magePicker.MageChosen += OnMageChosen;
        if (SpellRules.IsNet)
        {
            session = NetworkManager.Singleton;
            session.OnClientDisconnectCallback += HandleDisconnect;
            if (!magePicker) panel.ShowMessage("Preparing your cards…");
            SyncSpellCountToNetwork();
        }
        else if (!magePicker) DrawOneSpellCard();
    }

    private void OnMageChosen()
    {
        magePicker.MageChosen -= OnMageChosen;
        magePicker = null;
        if (submitted) return;
        if (!session) DrawOneSpellCard();
        else if (choices.Length > 0) DisplayOffer();
        else panel.ShowMessage("Preparing your cards.");
    }

    void OnDestroy()
    {
        if (magePicker) magePicker.MageChosen -= OnMageChosen;
        if (session) session.OnClientDisconnectCallback -= HandleDisconnect;
        if (panel) Destroy(panel.gameObject);
        if (Instance == this) Instance = null;
    }

    private void HandleDisconnect(ulong clientId)
    {
        if (!blocked || !panel) return;
        submitted = true;
        choices = Array.Empty<int>();
        panel.ShowMessage("Match disconnected");
    }

    void Update()
    {
        if (SpellRules.IsNet && !HandInitialized) SyncSpellCountToNetwork();
    }

    public void SyncSpellCountToNetwork()
    {
        if (panel && SpellRules.IsNet && NetPlayer.Local && NetPlayer.Local.IsSpawned &&
            GameState.Instance && GameState.Instance.IsSpawned)
            GameState.Instance.InitializeLocalHand();
    }

    public int[] SampleOffer()
    {
        var indices = new List<int>();
        var unique = new HashSet<CardData>();
        if (pool != null)
            for (int i = 0; i < pool.Count; i++)
                if (pool[i] && pool[i].cardtype != CardType.Character && unique.Add(pool[i]))
                    indices.Add(i);

        int count = Mathf.Min(3, indices.Count);
        for (int i = 0; i < count; i++)
        {
            int pick = UnityEngine.Random.Range(i, indices.Count);
            (indices[i], indices[pick]) = (indices[pick], indices[i]);
        }
        return indices.GetRange(0, count).ToArray();
    }

    public void DrawOneSpellCard()
    {
        if (SpellRules.IsNet) return;
        int[] indices = SampleOffer();
        if (indices.Length == 0)
        {
            Debug.LogWarning("[Cards] No eligible cards in the active pool.");
            FinishChoice();
            return;
        }
        ShowOffer(++offerId, indices);
    }

    public void ShowOffer(int id, int[] indices)
    {
        blocked = true;
        offerId = id;
        submitted = false;
        choices = indices;
        if (!magePicker) DisplayOffer();
    }

    private void DisplayOffer()
    {
        int id = offerId;
        var cards = Array.ConvertAll(choices, index => pool[index]);
        panel.Show(cards, slot => Choose(id, slot));
    }

    public void Choose(int id, int slot)
    {
        if (magePicker || !blocked || submitted || id != offerId || slot < 0 || slot >= choices.Length) return;
        submitted = true;
        if (session || SpellRules.IsNet)
        {
            if (!SpellRules.IsNet || !GameState.Instance || !GameState.Instance.IsSpawned)
            {
                panel.ShowMessage("Match disconnected");
                return;
            }
            panel.ShowMessage("Confirming card…");
            GameState.Instance.PickCardRpc(id, choices[slot]);
            return;
        }
        AddToHand(choices[slot]);
        FinishChoice();
    }

    public void AddToHand(int index)
    {
        var card = Instantiate(cardPrefab, handPanel).GetComponent<CardUI>();
        card.LoadCard(pool[index]);
        CurrentSpellCount++;
    }

    public void WaitForOpponent()
    {
        blocked = true;
        choices = Array.Empty<int>();
        panel.ShowMessage("Waiting for opponent…");
    }

    public void FinishChoice()
    {
        blocked = false;
        choices = Array.Empty<int>();
        bool openingHand = !HandInitialized;
        HandInitialized = true;
        panel.gameObject.SetActive(false);
        if (!openingHand && !SpellRules.IsNet && TurnManager.Instance)
            StormEndgameEvaluator.TryResolveCheckmate(TurnManager.Instance.currentTurn);
        ChoiceFinished?.Invoke();
    }

    public void NotifySpellConsumed() => CurrentSpellCount = Mathf.Max(0, CurrentSpellCount - 1);
}
