using System;
using System.Collections.Generic;
using Unity.Netcode;

public partial class GameState
{
    private readonly Dictionary<ulong, int[]> offers = new();
    private CardDrawSchedule drawSchedule;
    private int drawId;
    private bool choosingCards;
    private bool completingMove;
    private TeamColor nextAfterDraw;

    public void InitializeLocalHand()
    {
        if (localHandSent || !IsSpawned || !NetPlayer.Local) return;
        localHandSent = true;
        ReadyForHandRpc();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void ReadyForHandRpc(RpcParams rpc = default)
    {
        ulong owner = rpc.Receive.SenderClientId;
        if (!NetPlayer.FindByClient(owner) || !initializedHands.Add(owner)) return;
        if (initializedHands.Count == 2) BeginDraw();
    }

    private void BeginDraw()
    {
        choosingCards = true;
        drawId++;
        foreach (ulong owner in initializedHands)
        {
            int[] options = CardDrawer.Instance.SampleOffer();
            if (options.Length > 0)
            {
                offers.Add(owner, options);
                OfferClientRpc(drawId, options, TargetClient(owner));
            }
        }
        FinishDrawIfReady();
    }

    [ClientRpc]
    private void OfferClientRpc(int id, int[] options, ClientRpcParams target = default)
        => CardDrawer.Instance.ShowOffer(id, options);

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void PickCardRpc(int id, int cardIndex, RpcParams rpc = default)
    {
        ulong owner = rpc.Receive.SenderClientId;
        if (!choosingCards || drawId != id || !offers.TryGetValue(owner, out var options) ||
            Array.IndexOf(options, cardIndex) < 0) return;
        NetPlayer player = NetPlayer.FindByClient(owner);
        if (!player) return;
        offers.Remove(owner);
        player.AddSpellServer(1);
        GrantCardClientRpc(cardIndex, TargetClient(owner));
        FinishDrawIfReady();
    }

    [ClientRpc]
    private void GrantCardClientRpc(int index, ClientRpcParams target = default)
    {
        CardDrawer.Instance.AddToHand(index);
        CardDrawer.Instance.WaitForOpponent();
    }

    private void FinishDrawIfReady()
    {
        if (offers.Count != 0) return;
        choosingCards = false;
        if (completingMove)
        {
            completingMove = false;
            if (!StormEndgameEvaluator.TryResolveCheckmate(nextAfterDraw))
                CurrentTurn.Value = nextAfterDraw;
        }
        FinishDrawClientRpc();
    }

    [ClientRpc]
    private void FinishDrawClientRpc() => CardDrawer.Instance.FinishChoice();

    private void CompleteNetworkMove(TeamColor side, TeamColor next)
    {
        if (side == TeamColor.White && MoveNumber.Value == 0) MoveNumber.Value = 1;
        if (side == TeamColor.Black) MoveNumber.Value++;
        if (ChessBoard.Instance.gameOver) return;
        if (drawSchedule.Record(side))
        {
            completingMove = true;
            nextAfterDraw = next;
            BeginDraw();
        }
        else if (!StormEndgameEvaluator.TryResolveCheckmate(next))
            CurrentTurn.Value = next;
    }
}
