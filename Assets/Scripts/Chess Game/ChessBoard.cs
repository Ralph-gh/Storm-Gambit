using System.Collections.Generic;
using System.Reflection;
using Unity.Netcode;
using UnityEngine;

public class ChessBoard : NetworkBehaviour
{
    private int _nextId = 10000; // start above any initial IDs


    public static ChessBoard Instance { get; private set; }
    public List<ChessPiece> capturedPieces = new List<ChessPiece>();// List to track captured pieces
    private ChessPiece[,] board = new ChessPiece[8, 8];
    public bool resurrectionAllowed = false;
    public bool gameOver = false;
    public GameObject victoryScreen;
    public TMPro.TextMeshProUGUI victoryText; // or UnityEngine.UI.Text
    public AudioClip victoryClip;
    public AudioClip captureClip;
    public AudioSource audioSource;
    //string winner = "Me"; for winner testing
    public GameObject promotionUI;
    public ChessPiece pawnToPromote;
    public BoardInitializer initializer;
    public PromotionSelector promotionSelector;
    public Graveyard graveyard = new();
    private readonly Dictionary<int, ChessPiece> _byId = new();
    private Dictionary<int, ChessPiece> idLookup = new Dictionary<int, ChessPiece>();//Dictionnary lookup for networking
    public void RegisterPiece(ChessPiece p) { if (p) idLookup[p.Id] = p; }
    public void UnregisterPiece(ChessPiece p) { if (p) idLookup.Remove(p.Id); }
    public event System.Action OnGraveyardChanged;
    public void RaiseGraveyardChanged() => OnGraveyardChanged?.Invoke(); //graveyard on network play
      private int _idCounter = 1000;

    public void TriggerPromotion(ChessPiece pawn)
    {
        pawnToPromote = pawn;
        if (promotionSelector != null)
        {
            promotionSelector.Show(pawn.transform.position, pawn.currentCell, pawn.team);
        }
        else
        {
            Debug.LogWarning("Promotion UI is not assigned. Auto-promoting to Queen.");
        }
    }

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    public void PlacePiece(ChessPiece piece, Vector2Int position)
    {
        board[position.x, position.y] = piece;
        idLookup[piece.Id] = piece;
    }

    public void MovePiece(Vector2Int from, Vector2Int to)
    {
        var p = board[from.x, from.y];
        board[to.x, to.y] = p;
        board[from.x, from.y] = null;
    }

    public ChessPiece GetPieceAt(Vector2Int position)
    {
        if (position.x < 0 || position.x >= 8 || position.y < 0 || position.y >= 8)
            return null;

        return board[position.x, position.y];
    }

    public void CapturePiece(Vector2Int targetPosition)
    {
        if (gameOver) return; // prevent multiple endings

        ChessPiece target = GetPieceAt(targetPosition);
        
        if (target != null) 
        {
            // Clear from board array
            board[targetPosition.x, targetPosition.y] = null;
            UnregisterPiece(target); // <<< For network game
            SpriteRenderer sr = target.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                target.pieceSprite = sr.sprite;
                Debug.Log($"[CAPTURE] Stored sprite: {target.pieceSprite.name}");
            }
            else
            {
                Debug.LogWarning("[CAPTURE]  SpriteRenderer missing on target.");
            }
            if (target.pieceType == PieceType.King && !resurrectionAllowed)
            {
                gameOver = true;
                string winner = target.team == TeamColor.White ? "Black Wins!" : "White Wins!";
                Debug.Log($"Game Over — {winner}");

                if (GameState.Instance && GameState.Instance.IsServer)
                    GameState.Instance.ShowVictoryClientRpc(winner);
                 // the ClientRpc above runs on host and clients.

                /*if (victoryScreen != null)                            //moved to network
                {
                    victoryScreen.SetActive(true);
                    victoryText.text = winner;
                    if (victoryClip != null && audioSource != null)
                        audioSource.PlayOneShot(victoryClip);
                }*/
            }
            // Backup the sprite before destruction
            Sprite savedSprite = target.pieceSprite;
            target.pieceSprite = savedSprite; // Redundant in theory, but keeps the reference alive
                                              // Create and add captured record
            CapturedPieceData data = new CapturedPieceData(target);
            data.originalPosition = target.startingCell;

            // Use wrapper (it raises OnGraveyardChanged)
            AddCapturedPiece(data);

            Debug.Log($"[CAPTURE] Captured {target.pieceType}, sprite: {target.pieceSprite?.name}");

            if (audioSource != null && captureClip != null) audioSource.PlayOneShot(captureClip);

            // No need to invoke OnGraveyardChanged() again here – AddCapturedPiece already did it.
            GameObject.Destroy(target.gameObject);
        }
    }

    public List<ChessPiece> GetCapturedPieces(TeamColor team)
    {
        return capturedPieces.FindAll(p => p.team == team);
    }

    public bool HasCapturedPieces(TeamColor team)
    {
        return GetCapturedPieces(team).Count > 0;
    }

    public void RemoveCapturedPiece(TeamColor team, ChessPiece piece)
    {
        capturedPieces.Remove(piece);
        OnGraveyardChanged?.Invoke();
    }

    public Vector2Int FindNearestAvailableSquare(Vector2Int origin)
    {
        Vector2Int[] directions = {
        Vector2Int.zero,
        Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right,
        new Vector2Int(1,1), new Vector2Int(-1,1), new Vector2Int(1,-1), new Vector2Int(-1,-1)
    };

        foreach (var dir in directions)
        {
            Vector2Int candidate = origin + dir;
            if (IsInsideBoard(candidate) && GetPieceAt(candidate) == null)
                return candidate;
        }

        return new Vector2Int(-1, -1); // No valid square
    }
    public bool IsInsideBoard(Vector2Int cell)
    {
        return cell.x >= 0 && cell.x < 8 && cell.y >= 0 && cell.y < 8;
    }
    public class CapturedPieceData
    {
        public PieceType pieceType;
        public TeamColor team;
        public GameObject originalPrefab;
        public Vector2Int originalPosition;
        public Sprite pieceSprite;

        public CapturedPieceData(ChessPiece piece)
        {
            pieceType = piece.pieceType;
            team = piece.team;
            originalPrefab = piece.originalPrefab;
            pieceSprite = piece.pieceSprite;
            originalPosition = piece.startingCell;
        }
    }

    public class Graveyard
    {
        public List<CapturedPieceData> whiteCaptured = new();
        public List<CapturedPieceData> blackCaptured = new();
        public void AddCapturedPiece(CapturedPieceData data)
        {
            if (data.team == TeamColor.White) whiteCaptured.Add(data);
            else blackCaptured.Add(data);
        }

        // Add by ChessPiece
        public void AddCapturedPiece(ChessPiece piece)
        {
            AddCapturedPiece(new CapturedPieceData(piece));
        }

        // Remove by exact data
        public bool RemoveCapturedPiece(CapturedPieceData data)
        {
            return (data.team == TeamColor.White)
                ? whiteCaptured.Remove(data)
                : blackCaptured.Remove(data);
        }

        // Remove first match by (type, team)
        public bool RemoveCapturedPieceByTypeAndTeam(PieceType type, TeamColor team)
        {
            var list = (team == TeamColor.White) ? whiteCaptured : blackCaptured;
            int idx = list.FindIndex(d => d.pieceType == type && d.team == team);
            if (idx >= 0) { list.RemoveAt(idx); return true; }
            return false;
        }

        public List<CapturedPieceData> GetCapturedByTeam(TeamColor team)
        {
            return team == TeamColor.White ? whiteCaptured : blackCaptured;
        }
    }

    // Authoritative server-side apply (no input here)
    public void ExecuteMoveServer(ChessPiece piece, Vector2Int to)
    {
        // capture bookkeeping (server-side)
        var capture = GetPieceAt(to);
        if (capture != null && capture.team != piece.team)
        {
            board[to.x, to.y] = null;
        }

        // authoritative write – DO NOT read from board[from]
        Vector2Int from = piece.currentCell;
        board[from.x, from.y] = null;   // clear whatever might be there
        board[to.x, to.y] = piece;    // write the mover directly
        piece.currentCell = to;
        piece.hasMoved = true;
    }

    // Client-side visuals only (called by RPC)
    // === NEW: rules check wrapper (reuses your existing validators) ===
    public bool IsLegalMove(ChessPiece piece, Vector2Int to)
    {
        var victim = GetPieceAt(to);
        switch (piece.pieceType)
        {
            case PieceType.Pawn:
                return Pawn.IsValidMove(
                    piece.currentCell,
                    to,
                    piece.team,
                    piece.hasMoved,
                    GetPieceAt,
                    victim != null
                );
            case PieceType.Knight:
                return Knight.IsValidMove(piece.currentCell, to, piece.team, GetPieceAt);
            case PieceType.Bishop:
                return Bishop.IsValidMove(piece.currentCell, to, piece.team, GetPieceAt);
            case PieceType.Rook:
                return Rook.IsValidMove(piece.currentCell, to, piece.team, GetPieceAt);
            case PieceType.Queen:
                return Queen.IsValidMove(piece.currentCell, to, piece.team, GetPieceAt);
            case PieceType.King:
                return King.IsValidMove(piece.currentCell, to, piece.team, GetPieceAt);
            default:
                return false;
        }
    }



    // === NEW: local visual+board apply (used by ClientRpc) ===
    public void MovePieceLocal(ChessPiece piece, Vector2Int to)
    {
        var from = piece.currentCell;
        if (board[to.x, to.y] != null && board[to.x, to.y] != piece)        //safety for removing stray piece during multiplayer
        {
            var stray = board[to.x, to.y];
            UnregisterPiece(stray);
            idLookup.Remove(stray.Id);
            Destroy(stray.gameObject);
        }

       
        board[to.x, to.y] = piece;
        board[from.x, from.y] = null;
        piece.currentCell = to;
        piece.hasMoved = true;
        piece.transform.position = BoardInitializer.Instance
            ? BoardInitializer.Instance.GetWorldPosition(to)
            : new Vector3((to.x + 0.5f) * 0.5f, (to.y + 0.5f) * 0.5f, 0f); // fallback
    }


    // === NEW: remove on clients (used when ApplyMoveClientRpc says a capture happened) ===
    public void RemovePieceLocal(ChessPiece piece)
    {
        board[piece.currentCell.x, piece.currentCell.y] = null;
        idLookup.Remove(piece.Id);
        if (piece) Destroy(piece.gameObject);
    }


    
    public ChessPiece GetPieceById(int id)
    {
        ChessPiece p;
        return idLookup.TryGetValue(id, out p) ? p : null;
    }

    public void RebuildIndexFromScene()
    {
        idLookup.Clear();
        var all = FindObjectsByType<ChessPiece>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (var p in all) idLookup[p.Id] = p;
    }

    public void RebuildBoardAndIndexFromScene()
    {
        // clear board + index
        for (int x = 0; x < 8; x++) for (int y = 0; y < 8; y++) board[x, y] = null;
        idLookup.Clear();

        var all = FindObjectsByType<ChessPiece>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (var p in all)
        {
            if (IsInsideBoard(p.currentCell))
            {
                board[p.currentCell.x, p.currentCell.y] = p;
                idLookup[p.Id] = p;
            }
        }
        
    }

    public void AddCapturedPiece(ChessPiece victim)
    {
        graveyard.AddCapturedPiece(victim);
        RaiseGraveyardChanged();
    }

    public void AddCapturedPiece(CapturedPieceData data)
    {
        graveyard.AddCapturedPiece(data);
        RaiseGraveyardChanged();
    }

    public void RemoveCapturedPiece(CapturedPieceData data)
    {
        if (graveyard.RemoveCapturedPiece(data))
            RaiseGraveyardChanged();
    }

    public void RemoveCapturedPieceByTypeAndTeam(PieceType type, TeamColor team)
    {
        if (graveyard.RemoveCapturedPieceByTypeAndTeam(type, team))
            RaiseGraveyardChanged();
    }
    public int AllocatePieceId() //Id allocation for network play
    {
        // optional: ensure monotonic if you ever rebuild
        while (idLookup.ContainsKey(_nextId)) _nextId++;
        return _nextId++;
    }



}
