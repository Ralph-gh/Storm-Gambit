using System;
using UnityEngine;
using UnityEngine.UI;

public class CardChoicePanel : MonoBehaviour
{
    private Text heading;
    private RectTransform row;

    private RectTransform hand;

    public static CardChoicePanel Create(Transform handPanel)
    {
        var root = Region(handPanel.parent, "Private card choice", 0, 0, 1, 1);
        root.SetSiblingIndex(handPanel.GetSiblingIndex() + 1);
        var panel = root.gameObject.AddComponent<CardChoicePanel>();
        panel.hand = handPanel as RectTransform;
        // Cover held cards only inside the hand while an offer is open.
        root.gameObject.AddComponent<Image>().color = new Color(.055f, .065f, .10f, 1f);
        panel.heading = Label(root, "Choose a spell", 40);
        panel.row = Region(root, "Cards", .015f, .025f, .985f, .82f);
        if (SpellRules.IsNet)
        {
            var menu = FindFirstObjectByType<InGameMenuUI>(FindObjectsInactive.Include);
            if (menu)
            {
                var leave = Region(root, "Leave match", .78f, .84f, .99f, 1f);
                var label = Label(leave, "Leave match", 25);
                label.rectTransform.anchorMin = Vector2.zero;
                label.rectTransform.anchorMax = Vector2.one;
                label.raycastTarget = true;
                var button = leave.gameObject.AddComponent<UnityEngine.UI.Button>();
                button.targetGraphic = label;
                button.onClick.AddListener(menu.GoToMainMenu);
            }
        }
        root.gameObject.SetActive(false);
        return panel;
    }

    private void FitHand()
    {
        if (!hand || !(hand.parent is RectTransform parent)) return;
        var canvas = hand.GetComponentInParent<Canvas>();
        if (!canvas) return;
        var camera = canvas.rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.rootCanvas.worldCamera;
        var safe = Screen.safeArea;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, safe.min, camera, out var safeMin);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, safe.max, camera, out var safeMax);
        Vector2 min = parent.InverseTransformPoint(hand.TransformPoint(hand.rect.min));
        Vector2 max = parent.InverseTransformPoint(hand.TransformPoint(hand.rect.max));
        var rect = (RectTransform)transform;
        rect.anchorMin = rect.anchorMax = parent.pivot;
        rect.offsetMin = Vector2.Max(min, safeMin);
        rect.offsetMax = Vector2.Max(rect.offsetMin, Vector2.Min(max, safeMax));
    }

    private void LateUpdate() => FitHand();

    private static RectTransform Region(Transform parent, string name, float x, float y, float right, float top)
    {
        var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(x, y);
        rect.anchorMax = new Vector2(right, top);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return rect;
    }

    private static Text Label(Transform parent, string value, int size)
    {
        var text = Region(parent, value, 0.05f, 0.83f, 0.95f, 1f).gameObject.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = size;
        text.resizeTextForBestFit = true;
        text.resizeTextMaxSize = size;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = new Color(1f, .93f, .76f);
        text.raycastTarget = false;
        text.text = value;
        return text;
    }

    public void ShowMessage(string message)
    {
        gameObject.SetActive(true);
        FitHand();
        heading.text = message;
        for (int i = row.childCount - 1; i >= 0; i--)
        {
            GameObject child = row.GetChild(i).gameObject;
            child.SetActive(false);
            Destroy(child);
        }
    }

    public void Show(CardData[] cards, Action<int> pick)
    {
        ShowMessage("Choose a spell");
        float width = 1f / Mathf.Max(3, cards.Length);
        float left = (1f - cards.Length * width) / 2;
        for (int i = 0; i < cards.Length; i++)
        {
            int slot = i;
            var rect = Region(row, cards[i].cardName, left + width * i + .008f, 0,
                left + width * (i + 1) - .008f, 1);
            var art = rect.gameObject.AddComponent<Image>();
            art.sprite = cards[i].fullCardSprite;
            art.preserveAspect = true;
            if (!art.sprite)
            {
                art.color = new Color(.14f, .17f, .25f);
                Label(rect, cards[i].cardName, 28);
            }
            var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = art;
            button.onClick.AddListener(() => pick(slot));
        }
    }
}
