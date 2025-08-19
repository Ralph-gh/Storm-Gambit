using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CharacterSelectionUI : MonoBehaviour
{
    [Header("Pool & Prefabs")]
    public List<CardData> cardPool;   // Fill with ONLY Character cards (or we’ll filter)
    public GameObject cardPrefab;     // Your existing CardUI prefab

    [Header("UI Refs")]
    public Transform optionsContainer; // HorizontalLayoutGroup parent
    public TMP_Text headerText;        // “Choose your mage”
    public CanvasGroup cg;             // For show/hide & raycasts

    private System.Action<CardData> onChosen;
    private readonly List<GameObject> spawned = new();

    void Awake()
    {
        SetVisible(false);
    }

    public void Show(System.Action<CardData> onCharacterChosen)
    {
        onChosen = onCharacterChosen;
        Clear();

        var pool = (cardPool != null && cardPool.Count > 0)
            ? cardPool.FindAll(c => c.cardtype == CardType.Character)
            : new List<CardData>();

        if (pool.Count == 0)
        {
            Debug.LogWarning("CharacterSelectionUI: No character cards in pool. Check your setup.");
            return;
        }

        var picks = PickTwo(pool);

        foreach (var data in picks)
        {
            var go = Instantiate(cardPrefab, optionsContainer);
            var ui = go.GetComponent<CardUI>();
            ui.LoadCard(data, true, OnPick); // selectionMode = true
            spawned.Add(go);
        }

        if (headerText) headerText.text = "Choose your mage";
        SetVisible(true);
    }

    void OnPick(CardData chosen)
    {
        SetVisible(false);
        Clear();
        onChosen?.Invoke(chosen);
    }

    void SetVisible(bool v)
    {
        if (!cg) { gameObject.SetActive(v); return; }
        cg.alpha = v ? 1f : 0f;
        cg.interactable = v;
        cg.blocksRaycasts = v;
        gameObject.SetActive(v);
    }

    void Clear()
    {
        foreach (var go in spawned) if (go) Destroy(go);
        spawned.Clear();
    }

    static List<CardData> PickTwo(List<CardData> all)
    {
        var result = new List<CardData>(2);
        if (all.Count == 1) { result.Add(all[0]); return result; }

        int a = Random.Range(0, all.Count);
        int b; do { b = Random.Range(0, all.Count); } while (b == a);

        result.Add(all[a]);
        result.Add(all[b]);
        return result;
    }
}
