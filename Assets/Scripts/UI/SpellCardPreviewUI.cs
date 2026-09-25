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

    private void OnEnable()
    {
        SpellCastState.CastingChanged += HandleCastingChanged;
    }

    private void OnDisable()
    {
        SpellCastState.CastingChanged -= HandleCastingChanged;
    }

    private void HandleCastingChanged(bool isCasting)
    {
        // As soon as a spell starts casting,
        // immediately remove any enlarged card preview.
        if (isCasting)
        {
            Hide();
        }
    }

    public void Show(Sprite cardSprite)
    {
        // Never show the enlarged preview while casting.
        if (SpellCastState.IsCasting)
        {
            Hide();
            return;
        }

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