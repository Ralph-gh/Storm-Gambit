using UnityEngine;

public class SpellUIManager : MonoBehaviour
{
    public static SpellUIManager Instance;
    public TMPro.TextMeshProUGUI promptText;
    public GameObject confirmPanel;

    private System.Action onConfirm;

    private void Awake() => Instance = this;

    public void ShowMessage(string msg)
    {
        promptText.text = msg;
    }

    public void ShowTeleportConfirm(System.Action confirmAction)
    {
        confirmPanel.SetActive(true);
        onConfirm = confirmAction;
    }

    public void OnConfirmPressed()
    {
        confirmPanel.SetActive(false);
        onConfirm?.Invoke();
    }

    public void OnCancelPressed()
    {
        confirmPanel.SetActive(false);
        ShowMessage("Teleport cancelled.");
    }
}
