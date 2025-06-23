using UnityEngine;

public class SpellInputManager : MonoBehaviour
{
    public static SpellInputManager Instance;

    private System.Action<ChessPiece> onPieceSelected;
    private System.Action<Vector2Int> onCellSelected;

    private void Awake() => Instance = this;

    public void EnablePieceSelection(System.Action<ChessPiece> callback)
    {
        onPieceSelected = callback;
    }

    public void EnableSquareSelection(System.Action<Vector2Int> callback)
    {
        onCellSelected = callback;
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            Vector2Int cell = WorldToCell(mousePos);

            if (onCellSelected != null)
            {
                onCellSelected.Invoke(cell);
                onCellSelected = null;
            }
            else
            {
                RaycastHit2D hit = Physics2D.Raycast(mousePos, Vector2.zero);
                if (hit.collider != null && hit.collider.TryGetComponent(out ChessPiece piece))
                {
                    onPieceSelected?.Invoke(piece);
                    onPieceSelected = null;
                }
            }
        }
    }

    Vector2Int WorldToCell(Vector3 worldPos)
    {
        float size = 0.5f;
        int x = Mathf.FloorToInt(worldPos.x / size);
        int y = Mathf.FloorToInt(worldPos.y / size);
        return new Vector2Int(x, y);
    }
}
