using TMPro;
using Unity.Collections;
using UnityEngine;
using UnityEngine.UI;

public sealed class MageDuelHUD : MonoBehaviour
{
    private RectTransform overlay, board, playerRect, opponentRect, versusRect;
    private RectTransform localLabelRect, opponentLabelRect;
    private TMP_Text localLabel, opponentLabel;
    private Image portrait;
    private CharacterChoicePanel choices;
    private FixedString128Bytes selected;
    private NetPlayer opponent;
    private bool multiplayer;
    private float nextLookup;
    private readonly Vector3[] corners = new Vector3[4];
    private readonly RectTransform[] controls = new RectTransform[3];
    private readonly Vector2[] originalPositions = new Vector2[3];
    private readonly Vector3[] originalScales = new Vector3[3];

    public void Bind(CardUI card, CardData data, bool online)
    {
        multiplayer = online;
        selected = new FixedString128Bytes(data.cardName);
        var canvas = GetComponentInParent<Canvas>().rootCanvas;
        if (!overlay)
        {
            var root = (RectTransform)canvas.transform;
            board = root.Find("BoardSlot") as RectTransform;
            overlay = Rect("Duel HUD", root);
            if (board) overlay.SetSiblingIndex(board.GetSiblingIndex() + 1);
            overlay.anchorMin = Vector2.zero;
            overlay.anchorMax = Vector2.one;
            overlay.offsetMin = overlay.offsetMax = Vector2.zero;
            opponentRect = Rect("Opponent portrait", overlay);
            portrait = opponentRect.gameObject.AddComponent<Image>();
            portrait.raycastTarget = false;
            portrait.preserveAspect = true;
            opponentLabel = Label(overlay, out opponentLabelRect);
            localLabel = Label(overlay, out localLabelRect);
            var versus = Label(overlay, out versusRect);
            versus.text = "VS";
            versus.color = new Color(1f, 0.78f, 0.4f);
            versusRect.pivot = Vector2.one * 0.5f;
            string[] names = { "TurnCounterUI", "InGameMenuButton", "FlipBoardButton" };
            for (int i = 0; i < names.Length; i++)
            {
                controls[i] = root.Find(names[i]) as RectTransform;
                if (!controls[i]) continue;
                originalPositions[i] = controls[i].anchoredPosition;
                originalScales[i] = controls[i].localScale;
            }
        }
        playerRect = (RectTransform)card.transform;
        playerRect.SetParent(overlay, false);
        playerRect.anchorMin = playerRect.anchorMax = playerRect.pivot = Vector2.one * 0.5f;
        playerRect.localScale = Vector3.one;
        choices = FindFirstObjectByType<CharacterChoicePanel>(FindObjectsInactive.Include);
        if (online) RefreshPlayers();
        else
        {
            portrait.sprite = Resources.Load<Sprite>("StormGambitEnginePortrait");
            portrait.enabled = portrait.sprite != null;
            localLabel.text = Caption(LobbyIdentity.PlayerName, "You", SoloSession.PlayerSide);
            opponentLabel.text = Caption("Stockfish", "Opponent", SoloSession.AISide);
        }
        Layout();
    }

    private static string Caption(string name, string fallback, TeamColor side)
    {
        return (string.IsNullOrWhiteSpace(name) ? fallback : name) + "\n" + side;
    }

    private void Layout()
    {
        Rect safe = Screen.safeArea;
        var canvas = overlay.GetComponentInParent<Canvas>();
        Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(overlay, safe.min, camera, out var min);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(overlay, safe.max, camera, out var max);
        float bottom = Mathf.Lerp(min.y, max.y, 0.76f);
        if (board)
        {
            board.GetWorldCorners(corners);
            bottom = overlay.InverseTransformPoint(corners[1]).y;
        }
        float width = max.x - min.x;
        float height = Mathf.Max(1f, max.y - bottom);
        float center = (min.x + max.x) * 0.5f;
        float labelHeight = Mathf.Min(width * 0.10f, height * 0.20f);
        float cardHeight = Mathf.Min(width * 0.48f, height * 0.94f - labelHeight);
        var size = new Vector2(cardHeight * (2f / 3f), cardHeight);
        float y = bottom + (height + labelHeight) * 0.5f;
        float offset = width * 0.31f;
        playerRect.sizeDelta = opponentRect.sizeDelta = size;
        playerRect.anchoredPosition = new Vector2(center - offset, y);
        opponentRect.anchoredPosition = new Vector2(center + offset, y);
        localLabelRect.sizeDelta = opponentLabelRect.sizeDelta = new Vector2(width * 0.36f, labelHeight);
        localLabelRect.anchoredPosition = playerRect.anchoredPosition - Vector2.up * cardHeight * 0.5f;
        opponentLabelRect.anchoredPosition = opponentRect.anchoredPosition - Vector2.up * cardHeight * 0.5f;
        versusRect.anchoredPosition = new Vector2(center, bottom + height * 0.52f);
        versusRect.sizeDelta = new Vector2(width * 0.18f, height * 0.18f);
        // Keep the original move counter and both buttons in the gap between portraits.
        PlaceControl(0, center + width * 0.015f, bottom + height * 0.83f, width * 0.20f, height * 0.17f);
        PlaceControl(1, center - width * 0.067f, bottom + height * 0.23f, width * 0.125f, height * 0.26f);
        PlaceControl(2, center + width * 0.067f, bottom + height * 0.23f, width * 0.125f, height * 0.26f);
    }

    private void PlaceControl(int index, float x, float y, float width, float height)
    {
        var control = controls[index];
        if (!control) return;
        float scale = Mathf.Min(width / Mathf.Max(1, control.rect.width), height / Mathf.Max(1, control.rect.height));
        control.localScale = new Vector3(scale, scale, 1);
        control.position = overlay.TransformPoint(new Vector3(x, y));
    }

    private void LateUpdate()
    {
        if (!playerRect || !overlay) return;
        Layout();
        if (multiplayer && Time.unscaledTime >= nextLookup)
        {
            nextLookup = Time.unscaledTime + 0.25f;
            RefreshPlayers();
        }
    }

    private void RefreshPlayers()
    {
        var local = NetPlayer.Local;
        if (local && local.IsSpawned)
        {
            if (!local.MageCardName.Value.Equals(selected)) local.MageCardName.Value = selected;
            localLabel.text = Caption(local.PlayerName.Value.ToString(), "You", local.Side.Value);
        }
        if (!opponent || !opponent.IsSpawned)
        {
            opponent = null;
            foreach (var player in FindObjectsByType<NetPlayer>(FindObjectsSortMode.None))
                if (player.IsSpawned && !player.IsOwner) { opponent = player; break; }
        }
        opponentLabel.text = opponent ? Caption(opponent.PlayerName.Value.ToString(), "Opponent", opponent.Side.Value) : "Waiting for opponent";
        Sprite art = null;
        if (opponent && choices && opponent.MageCardName.Value.Length > 0)
        {
            string name = opponent.MageCardName.Value.ToString();
            foreach (var card in choices.characterCards)
                if (card && card.cardName == name) { art = card.fullCardSprite; break; }
        }
        portrait.sprite = art;
        portrait.enabled = art != null;
    }

    private static RectTransform Rect(string name, Transform parent)
    {
        var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
        rect.SetParent(parent, false);
        return rect;
    }

    private static TMP_Text Label(Transform parent, out RectTransform rect)
    {
        rect = Rect("Player name", parent);
        rect.pivot = new Vector2(0.5f, 1);
        var background = rect.gameObject.AddComponent<Image>();
        background.color = new Color(0.05f, 0.06f, 0.10f, 0.92f);
        background.raycastTarget = false;
        var inner = Rect("Name", rect);
        inner.anchorMin = Vector2.zero;
        inner.anchorMax = Vector2.one;
        inner.offsetMin = new Vector2(2, 0);
        inner.offsetMax = new Vector2(-2, 0);
        var label = inner.gameObject.AddComponent<TextMeshProUGUI>();
        label.alignment = TextAlignmentOptions.Center;
        label.richText = false;
        label.enableAutoSizing = true;
        label.fontSizeMin = 8;
        label.fontSizeMax = 36;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.raycastTarget = false;
        return label;
    }

    private void OnEnable() { if (overlay) overlay.gameObject.SetActive(true); }
    private void OnDisable()
    {
        if (overlay) overlay.gameObject.SetActive(false);
        for (int i = 0; i < controls.Length; i++)
        {
            if (!controls[i]) continue;
            controls[i].anchoredPosition = originalPositions[i];
            controls[i].localScale = originalScales[i];
        }
    }
    private void OnDestroy() { if (overlay) Destroy(overlay.gameObject); }
}



