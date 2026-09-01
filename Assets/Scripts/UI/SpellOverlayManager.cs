using UnityEngine;

public class SpellOverlayManager : MonoBehaviour
{
    public static SpellOverlayManager Instance { get; private set; }

    [Header("Prefabs")]
    [SerializeField] private GameObject actionPromptPrefab;
    [SerializeField] private GameObject notificationPopupPrefab;

    [Header("Spawn Root")]
    [SerializeField] private Transform overlayRoot;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (overlayRoot == null)
            overlayRoot = transform;
    }

    public SpellPromptPanelUI ShowActionPrompt(
        string message,
        System.Action cancelAction = null)
    {
        if (actionPromptPrefab == null)
        {
            Debug.LogError("[SpellOverlayManager] Action Prompt Prefab is missing.");
            return null;
        }

        GameObject go = Instantiate(actionPromptPrefab, overlayRoot);
        SpellPromptPanelUI ui = go.GetComponent<SpellPromptPanelUI>();

        if (ui != null)
            ui.Setup(message, showOk: false, showCancel: true, cancelAction: cancelAction);

        return ui;
    }

    public SpellPromptPanelUI ShowNotificationPopup(
        string message,
        System.Action okAction = null)
    {
        if (notificationPopupPrefab == null)
        {
            Debug.LogError("[SpellOverlayManager] Notification Popup Prefab is missing.");
            return null;
        }

        GameObject go = Instantiate(notificationPopupPrefab, overlayRoot);
        SpellPromptPanelUI ui = go.GetComponent<SpellPromptPanelUI>();

        if (ui != null)
            ui.Setup(message, showOk: true, showCancel: false, okAction: okAction);

        return ui;
    }
}