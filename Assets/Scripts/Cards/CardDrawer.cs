using System.Collections.Generic;
using UnityEngine;

public class CardDrawer : MonoBehaviour
{
    [Header("Card Data")]
    public List<CardData> cardPool;         // Assign ScriptableObjects here

    [Header("UI References")]
    public GameObject cardPrefab;           // Your Card UI prefab
    public Transform handPanel;             // Your HandPanel (UI container)

    [Header("Draw Settings")]
    public int startingCardCount = 2;

    [Header("Card Pools")]
    public List<CardData> defaultCardPool;
    public List<CardData> stockfishCampaignPool;

    private List<CardData> activeCardPool;

    void Start()
    {
        ConfigureCardPool();
        DrawCards(startingCardCount);
    }

    void DrawCards(int count)
    {
        for (int i = 0; i < count; i++)
        {
            if (activeCardPool == null || activeCardPool.Count == 0)
                return;

            CardData drawn =
                activeCardPool[Random.Range(0, activeCardPool.Count)];
            GameObject cardObj = Instantiate(cardPrefab, handPanel);
            cardObj.GetComponent<CardUI>().LoadCard(drawn);
        }
    }
    public void DrawOneSpellCard()
    {
        if (cardPool == null || cardPool.Count == 0 || cardPrefab == null || handPanel == null)
            return;

        // Filter to spells only (exclude Character cards)
        var spells = new List<CardData>();
        foreach (var c in cardPool)
            if (c != null && c.cardtype != CardType.Character)
                spells.Add(c);

        if (spells.Count == 0) return;

        var drawn = spells[Random.Range(0, spells.Count)];
        var cardObj = Instantiate(cardPrefab, handPanel);
        cardObj.GetComponent<CardUI>().LoadCard(drawn);
    }
    private void ConfigureCardPool()
    {
        bool stockfishCampaign =
            SoloSession.IsConfigured &&
            SoloSession.Mode == SoloGameMode.Campaign &&
            SoloSession.AIEnabled &&
            SoloSession.Opponent == SoloOpponent.Stockfish;

        activeCardPool = stockfishCampaign
            ? stockfishCampaignPool
            : defaultCardPool;

        Debug.Log(
            stockfishCampaign
                ? "[CARDS] Using Stockfish Campaign deck."
                : "[CARDS] Using default deck."
        );
    }
}
