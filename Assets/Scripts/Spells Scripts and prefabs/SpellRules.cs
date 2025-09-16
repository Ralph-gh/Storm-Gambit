using UnityEngine;

public static class SpellRules
{
    public static bool IsNet =>
        Unity.Netcode.NetworkManager.Singleton && Unity.Netcode.NetworkManager.Singleton.IsListening;

    // Is it currently this side's turn (local perspective)?
    public static bool IsTurn(TeamColor side)
    {
        if (IsNet)
            return NetPlayer.Local && NetPlayer.Local.Side.Value == side && NetPlayer.Local.CanAct();
        return TurnManager.Instance && TurnManager.Instance.IsPlayersTurn(side);
    }

    // Can this card be cast right now? (turn + free-spell gate, if you added that flag)
    public static bool CanCastNow(TeamColor side)
    {
        if (!IsTurn(side)) return false;
        // If you’re using the “one free spell” flag:
        return TurnManager.Instance == null || TurnManager.Instance.CanCastFreeSpell(side);
    }

    // Enforce “own piece only”
    public static bool IsOwnPiece(ChessPiece piece, TeamColor side)
    {
        return piece && piece.team == side;
    }
}
