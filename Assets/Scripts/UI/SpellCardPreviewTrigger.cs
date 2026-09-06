using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class SpellCardPreviewTrigger :
    MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler
{
    private Image cardImage;

    private void Awake()
    {
        cardImage = GetComponent<Image>();
    }

    private bool IsSpellHandCard()
    {
        Transform current = transform.parent;

        while (current != null)
        {
            if (current.name == "HandPanelWhite" ||current.name == "HandPanelBlack" || current.name == "CharacterPanelHood")
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        // Character cards, choice cards, etc. do NOT preview.
        if (!IsSpellHandCard())
            return;

        if (cardImage != null && SpellCardPreviewUI.Instance != null)
            SpellCardPreviewUI.Instance.Show(cardImage.sprite);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (SpellCardPreviewUI.Instance != null)
            SpellCardPreviewUI.Instance.Hide();
    }

    private void OnDisable()
    {
        if (SpellCardPreviewUI.Instance != null)
            SpellCardPreviewUI.Instance.Hide();
    }

    private void OnDestroy()
    {
        if (SpellCardPreviewUI.Instance != null)
            SpellCardPreviewUI.Instance.Hide();
    }
}