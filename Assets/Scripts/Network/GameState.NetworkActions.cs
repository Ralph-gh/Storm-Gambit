using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public enum NetworkSpellKind { Teleport, Freeze, Protection, Resurrection, Trap, Lightning }

public partial class GameState
{
    private readonly HashSet<ulong> initializedHands = new();
    private readonly HashSet<ulong> initializedMages = new();
    private readonly HashSet<TeamColor> spellUsedThisTurn = new();
    private readonly Dictionary<int, Action<bool>> pendingSpells = new();
    private int nextSpellRequest;
    private bool localHandSent;

    public void InitializeLocalHand(CardDrawer drawer)
    {
        if (localHandSent || !IsSpawned || NetPlayer.Local == null) return;
        localHandSent = true;
        InitializeHandRpc(drawer.CurrentSpellCount);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void InitializeHandRpc(int count, RpcParams rpc = default)
    {
        NetPlayer player = NetPlayer.FindByClient(rpc.Receive.SenderClientId);
        if (player != null && initializedHands.Add(player.OwnerClientId))
            player.SpellCount.Value = Mathf.Max(0, count);
    }

    public void InitializeMage(NetPlayer player, bool available, int kind)
    {
        if (!IsServer || player == null || !available || !IsMageSpell(kind) ||
            !initializedMages.Add(player.OwnerClientId)) return;
        player.SelectedMageSpell = (NetworkSpellKind)kind;
        player.MageAbilityAvailable.Value = true;
    }

    private static bool IsMageSpell(int kind) =>
        kind == (int)NetworkSpellKind.Teleport || kind == (int)NetworkSpellKind.Freeze ||
        kind == (int)NetworkSpellKind.Lightning;

    private bool CanAcceptAction(NetPlayer player) =>
        IsServer && ChessBoard.Instance != null && !ChessBoard.Instance.gameOver &&
        pendingPromotionPawnId < 0 && player != null && player.Side.Value == CurrentTurn.Value;

    public void RequestSpell(NetworkSpellKind kind, Action<bool> completed,
        int pieceId = -1, int x = 0, int y = 0, bool mageAbility = false,
        PieceType pieceType = PieceType.Pawn)
    {
        if (!IsSpawned || NetPlayer.Local == null || !NetPlayer.Local.CanAct())
        {
            completed?.Invoke(false);
            return;
        }
        int request = ++nextSpellRequest;
        pendingSpells.Add(request, completed);
        RequestSpellRpc(request, kind, pieceId, x, y, mageAbility, pieceType);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RequestSpellRpc(int request, NetworkSpellKind kind, int pieceId,
        int x, int y, bool mageAbility, PieceType pieceType, RpcParams rpc = default)
    {
        ulong sender = rpc.Receive.SenderClientId;
        NetPlayer player = NetPlayer.FindByClient(sender);
        bool accepted = CanAcceptAction(player) && initializedHands.Contains(sender);
        if (accepted)
        {
            accepted = mageAbility
                ? player.MageAbilityAvailable.Value && player.SelectedMageSpell == kind
                : kind != NetworkSpellKind.Lightning && player.SpellCount.Value > 0 &&
                  !spellUsedThisTurn.Contains(player.Side.Value);
        }
        if (accepted)
        {
            switch (kind)
            {
                case NetworkSpellKind.Teleport:
                    accepted = TryTeleportServer(player, pieceId, x, y); break;
                case NetworkSpellKind.Freeze:
                    accepted = TryFreezeServer(pieceId); break;
                case NetworkSpellKind.Protection:
                    accepted = TryProtectionServer(player, pieceId); break;
                case NetworkSpellKind.Resurrection:
                    accepted = TryResurrectServer(player, pieceType, x, y); break;
                case NetworkSpellKind.Trap:
                    accepted = TryPlaceTrapServer(player, x, y); break;
                case NetworkSpellKind.Lightning:
                    accepted = TryLightningServer(pieceId); break;
                default:
                    accepted = false; break;
            }
        }
        if (accepted)
        {
            if (mageAbility) player.MageAbilityAvailable.Value = false;
            else
            {
                player.SpellCount.Value = Mathf.Max(0, player.SpellCount.Value - 1);
                spellUsedThisTurn.Add(player.Side.Value);
            }
        }
        SpellResultClientRpc(request, accepted, mageAbility, TargetClient(sender));
        if (accepted && !ChessBoard.Instance.gameOver)
        {
            TeamColor side = player.Side.Value;
            if (StormEndgameEvaluator.TryResolveCheckmate(side) ||
                StormEndgameEvaluator.TryResolveCheckmate(ChessAttackEvaluator.OppositeTeam(side))) return;
            if (TurnManager.Instance != null && !TurnManager.Instance.HasAnyMovablePiece(side))
            {
                ChessBoard.Instance.ClearEnPassant();
                SetEnPassantClientRpc(-1, -1, -1);
                CurrentTurn.Value = ChessAttackEvaluator.OppositeTeam(side);
            }
        }
    }

    private static ClientRpcParams TargetClient(ulong client) => new ClientRpcParams
    {
        Send = new ClientRpcSendParams { TargetClientIds = new[] { client } }
    };

    [ClientRpc]
    private void SpellResultClientRpc(int request, bool accepted, bool mageAbility,
        ClientRpcParams target = default)
    {
        if (!pendingSpells.TryGetValue(request, out Action<bool> completed)) return;
        pendingSpells.Remove(request);
        if (accepted && !mageAbility) TurnManager.Instance?.RegisterFreeSpellCast();
        completed?.Invoke(accepted);
    }

    [ClientRpc]
    private void MoveResultClientRpc(int pieceId, bool accepted, ClientRpcParams target = default)
    {
        ChessPiece piece = ChessBoard.Instance?.GetPieceById(pieceId);
        if (piece == null) return;
        if (accepted) piece.ConfirmNetworkMove();
        else piece.RejectNetworkMove();
    }

    private void AdvanceMoveNumber(TeamColor movingSide)
    {
        if (movingSide == TeamColor.White)
        {
            if (MoveNumber.Value == 0) MoveNumber.Value = 1;
            return;
        }
        if (MoveNumber.Value > 0) MoveNumber.Value++;
        if (MoveNumber.Value == 0 || MoveNumber.Value % 10 != 0) return;
        foreach (NetPlayer player in FindObjectsByType<NetPlayer>(
                     FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            player.AddSpellServer(1);
        DrawSpellForBothPlayersLocal();
        DrawSpellForBothPlayersClientRpc();
    }

    private void ResetActionState()
    {
        localHandSent = false;
        initializedHands.Clear();
        initializedMages.Clear();
        spellUsedThisTurn.Clear();
        if (!IsServer) return;
        foreach (NetPlayer player in FindObjectsByType<NetPlayer>(
                     FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            player.SpellCount.Value = 0;
            player.MageAbilityAvailable.Value = false;
        }
    }

    private void CancelPendingSpells()
    {
        Action<bool>[] callbacks = new Action<bool>[pendingSpells.Count];
        pendingSpells.Values.CopyTo(callbacks, 0);
        pendingSpells.Clear();
        foreach (Action<bool> callback in callbacks) callback?.Invoke(false);
    }
}
