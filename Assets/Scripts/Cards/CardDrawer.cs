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
    public int startingCardCount = 3;

    void Start()
    {
        DrawFixedCard("Resurrection Stone");
        DrawFixedCard("Teleportation");

        DrawCards(startingCardCount - 2); // Draw the rest randomly
    }

    void DrawCards(int count)
    {
        for (int i = 0; i < count; i++)
        {
            if (cardPool.Count == 0) return;

            CardData drawn = cardPool[Random.Range(0, cardPool.Count)];

            GameObject cardObj = Instantiate(cardPrefab, handPanel);
            CardUI cardUI = cardObj.GetComponent<CardUI>();

            if (cardUI != null)
            {
                cardUI.LoadCard(drawn, TeamColor.White);
                Debug.Log($"Drew card: {drawn.cardName}");
            }
            else
            {
                Debug.LogError("Card prefab missing CardUI script!");
            }
        }
    }

    void DrawFixedCard(string cardName)
    {
        CardData fixedCard = cardPool.Find(c => c.cardName == cardName);
        if (fixedCard == null)
        {
            Debug.LogWarning($"Card '{cardName}' not found in card pool!");
            return;
        }

        GameObject cardObj = Instantiate(cardPrefab, handPanel);
        cardObj.GetComponent<CardUI>().LoadCard(fixedCard, TeamColor.White); // Assuming White by default
    }

}
