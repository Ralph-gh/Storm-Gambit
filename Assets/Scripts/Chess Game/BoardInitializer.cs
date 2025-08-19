using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;

public class BoardInitializer : MonoBehaviour
{
    [Header("References")]
    public Tilemap tilemap;

    [Header("Prefabs")]
    public GameObject White_Pawn;
    public GameObject White_Rook;
    public GameObject White_Knight;
    public GameObject White_Bishop;
    public GameObject White_Queen;
    public GameObject White_King;

    public GameObject Black_Pawn;
    public GameObject Black_Rook;
    public GameObject Black_Knight;
    public GameObject Black_Bishop;
    public GameObject Black_Queen;
    public GameObject Black_King;

    private Dictionary<string, GameObject> prefabLookup;
    public static BoardInitializer Instance;
    void Awake()
    {
        Instance = this;
    }
    public Vector3 GetWorldPosition(Vector2Int cell)
    {
        return tilemap.GetCellCenterWorld(new Vector3Int(cell.x, cell.y, 0));
    }

    public GameObject GetPromotionPrefab(string type, TeamColor team)
    {
        return (type, team) switch
        {
            ("Queen", TeamColor.White) => White_Queen,
            ("Rook", TeamColor.White) => White_Rook,
            ("Bishop", TeamColor.White) => White_Bishop,
            ("Knight", TeamColor.White) => White_Knight,

            ("Queen", TeamColor.Black) => Black_Queen,
            ("Rook", TeamColor.Black) => Black_Rook,
            ("Bishop", TeamColor.Black) => Black_Bishop,
            ("Knight", TeamColor.Black) => Black_Knight,

            _ => null
        };
    }

    private string[,] boardLayout = new string[8, 8]
    {
        { "Black_Rook", "Black_Knight", "Black_Bishop", "Black_Queen", "Black_King", "Black_Bishop", "Black_Knight", "Black_Rook" },
        { "Black_Pawn", "Black_Pawn", "Black_Pawn", "Black_Pawn", "Black_Pawn", "Black_Pawn", "Black_Pawn", "Black_Pawn" },
        { null, null, null, null, null, null, null, null },
        { null, null, null, null, null, null, null, null },
        { null, null, null, null, null, null, null, null },
        { null, null, null, null, null, null, null, null },
        { "White_Pawn", "White_Pawn", "White_Pawn", "White_Pawn", "White_Pawn", "White_Pawn", "White_Pawn", "White_Pawn" },
        { "White_Rook", "White_Knight", "White_Bishop", "White_Queen", "White_King", "White_Bishop", "White_Knight", "White_Rook" }
    };

    void Start()
    {
        SetupPrefabLookup();
        InitializeBoard();
    }

    void SetupPrefabLookup()
    {
        prefabLookup = new Dictionary<string, GameObject>
        {
            { "White_Pawn", White_Pawn },
            { "White_Rook", White_Rook },
            { "White_Knight", White_Knight },
            { "White_Bishop", White_Bishop },
            { "White_Queen", White_Queen },
            { "White_King", White_King },

            { "Black_Pawn", Black_Pawn },
            { "Black_Rook", Black_Rook },
            { "Black_Knight", Black_Knight },
            { "Black_Bishop", Black_Bishop },
            { "Black_Queen", Black_Queen },
            { "Black_King", Black_King }
        };
    }

    void InitializeBoard()
    {
        for (int y = 0; y < 8; y++)
        {
            for (int x = 0; x < 8; x++)
            {
                string pieceName = boardLayout[7 - y, x];
                if (pieceName != null && prefabLookup.ContainsKey(pieceName))
                {
                    Vector3Int cell = new Vector3Int(x, y, 0);
                    Vector3 worldPos = tilemap.GetCellCenterWorld(cell);
                    GameObject pieceGO = Instantiate(prefabLookup[pieceName], worldPos, Quaternion.identity);
                    ChessPiece cp = pieceGO.GetComponent<ChessPiece>();

                    if (cp != null)
                    {
                        SpriteRenderer sr = pieceGO.GetComponent<SpriteRenderer>();
                        if (sr != null)
                        {
                            cp.pieceSprite = sr.sprite; //  Assign sprite to be used in resurrection UI
                            Debug.Log($"[INIT] Stored sprite for {cp.pieceType}: {sr.sprite?.name}");
                        }
                       
                        cp.SetPosition(new Vector2Int(x, y), worldPos);
                        cp.team = pieceName.StartsWith("White") ? TeamColor.White : TeamColor.Black;
                        cp.startingCell = new Vector2Int(x, y);
                        cp.originalPrefab = prefabLookup[pieceName];
                        cp.pieceSprite = pieceGO.GetComponent<SpriteRenderer>().sprite;

                        ChessBoard.Instance.PlacePiece(cp, new Vector2Int(x, y));
                    }

                    if (cp != null)
                    {
                        cp.SetPosition(new Vector2Int(x, y), worldPos);
                        cp.team = pieceName.StartsWith("White") ? TeamColor.White : TeamColor.Black;
                        cp.startingCell = new Vector2Int(x, y);
                        cp.originalPrefab = prefabLookup[pieceName];

                        // Register the piece on the board
                        ChessBoard.Instance.PlacePiece(cp, new Vector2Int(x, y));

                    }
                }
            }
        }
    }
    public Vector2Int GetStartingSquare(ChessPiece piece)
    {
        // Example: Adjust based on your initialization logic
        if (piece.team == TeamColor.White)
        {
            switch (piece.pieceType)
            {
                case PieceType.Pawn: return new Vector2Int(0, 1); // Update logic for the actual pawn
                case PieceType.Rook: return new Vector2Int(0, 0);
                case PieceType.Knight: return new Vector2Int(1, 0);
                case PieceType.Bishop: return new Vector2Int(2, 0);
                case PieceType.Queen: return new Vector2Int(3, 0);
                case PieceType.King: return new Vector2Int(4, 0);
            }
        }
        else
        {
            switch (piece.pieceType)
            {
                case PieceType.Pawn: return new Vector2Int(0, 6);
                case PieceType.Rook: return new Vector2Int(0, 7);
                case PieceType.Knight: return new Vector2Int(1, 7);
                case PieceType.Bishop: return new Vector2Int(2, 7);
                case PieceType.Queen: return new Vector2Int(3, 7);
                case PieceType.King: return new Vector2Int(4, 7);
            }
        }
        return Vector2Int.zero;
    }

    public Dictionary<PieceType, GameObject> whitePrefabs;
    public Dictionary<PieceType, GameObject> blackPrefabs;

    public GameObject GetPiecePrefab(ChessPiece piece)
    {
        return piece.team == TeamColor.White
            ? whitePrefabs[piece.pieceType]
            : blackPrefabs[piece.pieceType];
    }



}
