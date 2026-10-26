using Unity.Netcode;
using UnityEngine;

public static class StormEndgameEvaluator
{
    public static bool TryResolveCheckmate(TeamColor defendingTeam)
    {
        if (ChessBoard.Instance == null || ChessBoard.Instance.gameOver) return false;

        // Multiplayer: only the server decides game over.
        if (IsNetworkGame() && (GameState.Instance == null || !GameState.Instance.IsServer)) return false;

        if (!ChessAttackEvaluator.IsKingInCheck(defendingTeam)) return false;
        if (HasAnyClassicalEscape(defendingTeam)) return false;

        bool hasSpells = HasSpells(defendingTeam);
        bool hasMageAbility = HasMageAbility(defendingTeam);

        Debug.Log($"[CHECKMATE] {defendingTeam} has no classical escape. Spells={hasSpells}, MageAbility={hasMageAbility}");

        // Storm Gambit rule:
        // classical checkmate is NOT game over while magic remains.
        if (hasSpells || hasMageAbility)
        {
            Debug.Log($"[CHECKMATE] {defendingTeam} survives because magic is still available.");
            return false;
        }

        TeamColor winner = ChessAttackEvaluator.OppositeTeam(defendingTeam);
        string winnerText = winner == TeamColor.White ? "White Wins!" : "Black Wins!";

        ChessBoard.Instance.gameOver = true;
        Debug.Log($"Game Over — {winnerText} (Checkmate — no magic remaining)");

        if (IsNetworkGame())
        {
            GameState.Instance.ShowVictoryClientRpc(winnerText);
        }
        else
        {
            ShowSoloResult(winnerText);
        }

        return true;
    }

    private static bool HasAnyClassicalEscape(TeamColor team)
    {
        for (int x = 0; x < 8; x++)
        {
            for (int y = 0; y < 8; y++)
            {
                ChessPiece piece = ChessBoard.Instance.GetPieceAt(new Vector2Int(x, y));

                if (piece == null || piece.team != team) continue;
                if (piece.IsFrozen || piece.IsStunned || piece.IsDivinelyProtected) continue;

                for (int targetX = 0; targetX < 8; targetX++)
                {
                    for (int targetY = 0; targetY < 8; targetY++)
                    {
                        Vector2Int destination = new Vector2Int(targetX, targetY);

                        if (destination == piece.currentCell) continue;

                        ChessPiece target = ChessBoard.Instance.GetPieceAt(destination);

                        if (target != null && target.team == team) continue;

                        if (target != null && target.team != team &&
                            (target.IsDivinelyProtected || target.IsFrozen))
                        {
                            continue;
                        }

                        ChessMoveValidationResult result = ChessMoveValidator.Validate(piece, destination);

                        if (result.IsLegal) return true;
                    }
                }
            }
        }

        return false;
    }

    private static bool HasSpells(TeamColor team)
    {
        if (IsNetworkGame())
        {
            NetPlayer player = NetPlayer.FindBySide(team);
            return player != null && player.SpellCount.Value > 0;
        }

        if (SoloSession.IsConfigured && SoloSession.IsAISide(team)) return false;

        return CardDrawer.Instance != null && CardDrawer.Instance.CurrentSpellCount > 0;
    }

    private static bool HasMageAbility(TeamColor team)
    {
        if (IsNetworkGame())
        {
            NetPlayer player = NetPlayer.FindBySide(team);
            return player != null && player.MageAbilityAvailable.Value;
        }

        if (SoloSession.IsConfigured && SoloSession.IsAISide(team)) return false;

        return CharacterPanelUI.Instance != null && CharacterPanelUI.Instance.HasAvailableMageAbility;
    }

    private static bool IsNetworkGame()
    {
        return NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
    }

    private static void ShowSoloResult(string winnerText)
    {
        ChessBoard board = ChessBoard.Instance;

        if (board.victoryScreen != null) board.victoryScreen.SetActive(true);
        if (board.victoryText != null) board.victoryText.text = winnerText;
        if (board.audioSource != null && board.victoryClip != null) board.audioSource.PlayOneShot(board.victoryClip);
    }
}