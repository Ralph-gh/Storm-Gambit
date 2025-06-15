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
        DrawCards(startingCardCount);
    }

    void DrawCards(int count)
    {
        for (int i = 0; i < count; i++)
        {
            if (cardPool.Count == 0) return;

            CardData drawn = cardPool[Random.Range(0, cardPool.Count)];
            GameObject cardObj = Instantiate(cardPrefab, handPanel);
            cardObj.GetComponent<CardUI>().LoadCard(drawn);
        }
    }
}
