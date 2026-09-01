using UnityEngine;

public class BoardFlipController : MonoBehaviour
{
    public static BoardFlipController Instance { get; private set; }

    [Header("Camera")]
    [SerializeField] private Camera boardCamera;

    private Quaternion normalCameraRotation;

    public bool IsFlipped { get; private set; }

    private void Awake()
    {
        // Singleton
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (boardCamera == null)
            boardCamera = Camera.main;

        if (boardCamera == null)
        {
            Debug.LogError("BoardFlipController: No camera found.");
            return;
        }

        normalCameraRotation = boardCamera.transform.rotation;
    }

    public void FlipBoard()
    {
        SetFlipped(!IsFlipped);
    }

    public void SetFlipped(bool flipped)
    {
        if (boardCamera == null)
            return;

        IsFlipped = flipped;

        // Rotate camera
        boardCamera.transform.rotation = IsFlipped
            ? normalCameraRotation * Quaternion.Euler(0f, 0f, 180f)
            : normalCameraRotation;

        // Fix every piece currently on the board
        RefreshAllPieces();
    }

    public void RefreshAllPieces()
    {
        ChessPiece[] pieces = FindObjectsByType<ChessPiece>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None
        );

        foreach (ChessPiece piece in pieces)
        {
            ApplyOrientation(piece);
        }
    }

    public void ApplyOrientation(ChessPiece piece)
    {
        if (piece == null)
            return;

        piece.transform.rotation = IsFlipped
            ? Quaternion.Euler(0f, 0f, 180f)
            : Quaternion.identity;
    }
}