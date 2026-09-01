using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SpellPromptPanelUI : MonoBehaviour
{
    [SerializeField] private TMP_Text messageText;

    [SerializeField]
    private UnityEngine.UI.Button okButton;

    [SerializeField]
    private UnityEngine.UI.Button cancelButton;

    private Action onOk;
    private Action onCancel;

    public void Setup(
        string message,
        bool showOk,
        bool showCancel,
        Action okAction = null,
        Action cancelAction = null)
    {
        if (messageText != null)
            messageText.text = message;

        onOk = okAction;
        onCancel = cancelAction;

        if (okButton != null)
        {
            okButton.gameObject.SetActive(showOk);
            okButton.onClick.RemoveAllListeners();
            okButton.onClick.AddListener(HandleOk);
        }

        if (cancelButton != null)
        {
            cancelButton.gameObject.SetActive(showCancel);
            cancelButton.onClick.RemoveAllListeners();
            cancelButton.onClick.AddListener(HandleCancel);
        }
    }

    private void HandleOk()
    {
        onOk?.Invoke();
        Destroy(gameObject);
    }

    private void HandleCancel()
    {
        onCancel?.Invoke();
        Destroy(gameObject);
    }

    public void Close()
    {
        Destroy(gameObject);
    }
}