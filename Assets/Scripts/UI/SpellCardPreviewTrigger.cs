using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class SpellCardPreviewTrigger :
    MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler,
    IPointerUpHandler
{
    private Image cardImage;

    private void Awake()
    {
        cardImage = GetComponent<Image>();
    }

    private bool IsPreviewAllowedCard()
    {
        Transform current = transform.parent;

        while (current != null)
        {
            if (current.name == "HandPanelWhite" ||
                current.name == "HandPanelBlack" ||
                current.name == "CharacterPanelHood")
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    private void ShowPreview()
    {
        if (SpellCastState.IsCasting)
        {
            HidePreview();
            return;
        }

        if (!IsPreviewAllowedCard())
            return;

        if (cardImage == null ||
            cardImage.sprite == null ||
            SpellCardPreviewUI.Instance == null)
            return;

        SpellCardPreviewUI.Instance.Show(cardImage.sprite);
    }

    private void HidePreview()
    {
        if (SpellCardPreviewUI.Instance != null)
            SpellCardPreviewUI.Instance.Hide();
    }

    // Mouse hover OR finger entering/dragging onto this card.
    public void OnPointerEnter(PointerEventData eventData)
    {
        ShowPreview();
    }

    // Mouse/finger leaves this specific card.
    public void OnPointerExit(PointerEventData eventData)
    {
        HidePreview();
    }

    // On mobile, remove preview when finger is released.
    public void OnPointerUp(PointerEventData eventData)
    {
        // Touch IDs are normally 0 or greater.
        if (eventData.pointerId >= 0)
        {
            HidePreview();
        }
    }

    private void OnDisable()
    {
        HidePreview();
    }

    private void OnDestroy()
    {
        HidePreview();
    }
}