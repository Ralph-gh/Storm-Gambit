using UnityEngine;
using UnityEngine.UI;

public class SpellCardPreviewUI : MonoBehaviour
{
    public static SpellCardPreviewUI Instance { get; private set; }

    [SerializeField] private GameObject previewRoot;
    [SerializeField] private Image previewImage;

    private void Awake()
    {
        Instance = this;
        previewRoot.SetActive(false);
    }

    public void Show(Sprite cardSprite)
    {
        if (cardSprite == null)
            return;

        previewImage.sprite = cardSprite;
        previewImage.preserveAspect = true;

        previewRoot.SetActive(true);
    }

    public void Hide()
    {
        previewRoot.SetActive(false);
    }
}