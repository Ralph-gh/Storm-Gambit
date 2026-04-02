using UnityEngine;
using UnityEngine.UI;
using static ChessBoard;

public class ResurrectionButton : MonoBehaviour
{
    private CapturedPieceData data;
    private ResurrectionSpellUI spellUI;

    public UnityEngine.UI.Button button;
    public UnityEngine.UI.Image icon;

    public void Initialize(CapturedPieceData data, ResurrectionSpellUI ui)
    {
        this.data = data;
        spellUI = ui;

        if (button == null)
            button = GetComponent<UnityEngine.UI.Button>();

        if (icon == null)
            icon = GetComponent<Image>(); // <- not GetComponentInChildren, unless you're nesting!

        if (data.pieceSprite != null)
        {
            icon.sprite = data.pieceSprite;
        }
        else
        {
            Debug.LogWarning("[ResurrectionButton] Missing sprite for piece: " + data.pieceType);
        }

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => spellUI.Resurrect(data));
    }

    private void OnClick()
    {
        spellUI.Resurrect(data);
    }
}
