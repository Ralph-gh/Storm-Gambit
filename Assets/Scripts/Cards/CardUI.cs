using UnityEngine;
using UnityEngine.UI;

public class CardUI : MonoBehaviour
{
    public Image fullImage;

    public void LoadCard(CardData data)
    {
        fullImage.sprite = data.fullCardSprite;
    }
}
