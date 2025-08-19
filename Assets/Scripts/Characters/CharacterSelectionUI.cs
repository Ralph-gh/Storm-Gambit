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

        // --- Force the panel fully visible & interactive up front ---
        if (!cg) cg = GetComponent<CanvasGroup>();
        if (cg)
        {
            cg.ignoreParentGroups = true;   // <- bypass any hidden parent CanvasGroup
            cg.alpha = 1f;
            cg.interactable = true;
            cg.blocksRaycasts = true;
        }
        gameObject.SetActive(true);

        // Bring to top of sibling order so it’s not behind other UI
        transform.SetAsLastSibling();

        // Make sure the rect fills the screen (common reason it "shows" but off-screen)
        EnsureFullScreenRect(transform as RectTransform);

        // Validate the options container is a SCENE object
        if (optionsContainer == null || !optionsContainer.gameObject.scene.IsValid())
        {
            Debug.LogError("[CharSelUI] optionsContainer is NULL or not a scene object. Assign from Hierarchy.");
            return;
        }

        // Build character pool
        var pool = (cardPool != null && cardPool.Count > 0)
            ? cardPool.FindAll(c => c.cardtype == CardType.Character)
            : new List<CardData>();

        if (pool.Count == 0)
        {
            Debug.LogError("[CharSelUI] No Character cards in cardPool (or none flagged as Character).");
            return;
        }

        var picks = PickTwo(pool);

        // Spawn cards using SAFE parent pattern
        foreach (var data in picks)
        {
            var go = Instantiate(cardPrefab);                 // instantiate first
            go.transform.SetParent(optionsContainer, false);  // then parent to scene container

            var ui = go.GetComponent<CardUI>();
            if (!ui)
            {
                Debug.LogError("[CharSelUI] Card prefab is missing CardUI.");
                continue;
            }

            ui.LoadCard(data, true, OnPick);  // selection mode = true
            spawned.Add(go);
        }

        if (headerText) headerText.text = "Choose your mage";
        Debug.Log("[CharSelUI] Panel ON, cards spawned: " + spawned.Count);
    }

    // Helper: ensure full-screen rect so it's not off-screen or size (0,0)
    private void EnsureFullScreenRect(RectTransform rt)
    {
        if (!rt) return;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
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
