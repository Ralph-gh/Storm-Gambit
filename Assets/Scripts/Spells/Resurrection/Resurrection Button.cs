using UnityEngine;
using UnityEngine.UI;

public class ResurrectionButton : MonoBehaviour
{
    private ChessPiece piece;
    private ResurrectionSpellUI spellUI;
    public Button button;
    public Image icon;

    public void Initialize(ChessPiece pieceToResurrect, ResurrectionSpellUI ui)
    {
        piece = pieceToResurrect;
        spellUI = ui;

        if (button == null)
            button = GetComponent<Button>();

        if (icon != null && pieceToResurrect != null)
            icon.sprite = pieceToResurrect.GetComponent<SpriteRenderer>().sprite;

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(OnClick);
    }

    void OnClick()
    {
        spellUI.Resurrect(piece);
    }
}
