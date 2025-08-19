using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CharacterChoicePanel : MonoBehaviour
{
    [Header("Pool & Prefab")]
    public List<CardData> characterCards;     // ONLY Character CardData assets (cardtype = Character)
    public GameObject cardPrefab;             // UI prefab: RectTransform + CardUI + Button

    [Header("UI")]
    public Transform optionsContainer;        // Horizontal container for the 2 cards
    public CharacterPanelUI characterPanelHUD; // HUD panel that shows the chosen

    void OnEnable()
    {
        Debug.Log("[CharChoice] Panel enabled");
        Clear();
        DrawTwo();
    }

    void DrawTwo()
    {
        if (!optionsContainer || !optionsContainer.gameObject.scene.IsValid())
        {
            Debug.LogError("[CharChoice] optionsContainer is NULL or not a scene object.");
            return;
        }
        if (!cardPrefab)
        {
            Debug.LogError("[CharChoice] cardPrefab is NULL.");
            return;
        }
        if (characterCards == null || characterCards.Count == 0)
        {
            Debug.LogError("[CharChoice] characterCards is empty (drag your Character CardData assets here).");
            return;
        }

        var picks = PickTwo(characterCards);
        Debug.Log($"[CharChoice] Drawing {picks.Count} picks");

        foreach (var data in picks)
        {
            if (!data)
            {
                Debug.LogWarning("[CharChoice] Null CardData in list, skipping.");
                continue;
            }

            var go = Instantiate(cardPrefab, optionsContainer);
            var rt = go.transform as RectTransform;
            if (rt) { rt.localScale = Vector3.one; }

            // Make sure it’s visible size in the layout
            var le = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
            le.preferredWidth = 300f;
            le.preferredHeight = 420f;

            var ui = go.GetComponent<CardUI>();
            if (!ui)
            {
                Debug.LogError("[CharChoice] Card prefab missing CardUI.");
                continue;
            }

            // Selection mode: click reports choice ONLY
            ui.LoadCard(data, true, OnPick);
            Debug.Log($"[CharChoice] Spawned selection card: {data.cardName}");
        }
    }

    void OnPick(CardData chosen)
    {
        Debug.Log("[CharChoice] OnPick -> " + (chosen ? chosen.cardName : "NULL"));

        if (!chosen)
        {
            Debug.LogError("[CharChoice] Chosen CardData is NULL.");
            return;
        }
        if (!characterPanelHUD)
        {
            Debug.LogError("[CharChoice] characterPanelHUD is NULL. Assign your HUD panel (CharacterPanelUI) in the Inspector.");
            return;
        }

        characterPanelHUD.Show(chosen);   // should instantiate HUD card
        gameObject.SetActive(false);      // hide the selection panel
    }

    void Clear()
    {
        if (!optionsContainer) return;
        for (int i = optionsContainer.childCount - 1; i >= 0; i--)
            Destroy(optionsContainer.GetChild(i).gameObject);
    }

    static List<CardData> PickTwo(List<CardData> all)
    {
        var result = new List<CardData>(2);
        if (all.Count == 1) { result.Add(all[0]); return result; }
        int a = Random.Range(0, all.Count);
        int b; do { b = Random.Range(0, all.Count); } while (b == a);
        result.Add(all[a]); result.Add(all[b]);
        return result;
    }
}
