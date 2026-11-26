using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.Tilemaps;

public class BugFixRegressionTests
{
    private readonly List<Object> created = new();
    private NetworkManager manager;
    private ChessBoard board;
    private TurnManager turns;
    private BoardInitializer initializer;

    private static object Call(object target, string method, params object[] args) =>
        target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(target, args);

    private static void Set(object target, string field, object value) =>
        target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(target, value);

    private T New<T>(string name) where T : Component
    {
        GameObject go = new GameObject(name);
        created.Add(go);
        return typeof(T) == typeof(Transform) ? go.transform as T : go.AddComponent<T>();
    }

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        turns = New<TurnManager>("Turns");
        turns.enabled = false;
        board = New<ChessBoard>("Board");
        Grid grid = New<Grid>("Grid");
        grid.cellSize = new Vector3(0.5f, 0.5f, 1);
        Tilemap tilemap = New<Tilemap>("Tilemap");
        tilemap.transform.SetParent(grid.transform);
        initializer = New<BoardInitializer>("Initializer");
        initializer.enabled = false;
        initializer.tilemap = tilemap;
        SoloSession.Reset();
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (manager != null) manager.Shutdown();
        yield return null;
        for (int i = created.Count - 1; i >= 0; i--)
            if (created[i] != null) Object.DestroyImmediate(created[i]);
        created.Clear();
        SoloSession.Reset();
        yield return new ExitPlayMode();
    }

    private CardDrawer Hand(int count = 0)
    {
        CardData data = ScriptableObject.CreateInstance<CardData>();
        created.Add(data);
        data.cardName = "Test spell";
        data.cardtype = CardType.Spell;
        CardUI prefab = New<CardUI>("Card template");
        prefab.gameObject.SetActive(false);
        prefab.cardButton = prefab.gameObject.AddComponent<UnityEngine.UI.Button>();
        CardDrawer drawer = New<CardDrawer>("Hand");
        drawer.enabled = false;
        drawer.defaultCardPool = new List<CardData> { data };
        drawer.cardPrefab = prefab.gameObject;
        drawer.handPanel = New<Transform>("Hand panel");

        Call(drawer, "Start");
        if (!SpellRules.IsNet)
        {
            for (int id = 1; id <= count; id++)
            {
                if (id > 1) drawer.DrawOneSpellCard();
                drawer.Choose(id, 0);
            }
        }
        return drawer;
    }

    [Test]
    public void OffersContainUpToThreeDistinctEligibleCards()
    {
        CardDrawer drawer = Hand();
        CardData first = drawer.defaultCardPool[0];
        drawer.defaultCardPool.Add(first);
        drawer.defaultCardPool.Add(null);
        var mage = ScriptableObject.CreateInstance<CardData>();
        created.Add(mage);
        mage.cardtype = CardType.Character;
        drawer.defaultCardPool.Add(mage);
        Assert.AreEqual(1, drawer.SampleOffer().Length);
        for (int i = 0; i < 3; i++)
        {
            var extra = ScriptableObject.CreateInstance<CardData>();
            created.Add(extra);
            extra.cardtype = CardType.Spell;
            drawer.defaultCardPool.Add(extra);
        }
        var unique = new HashSet<CardData>();
        int[] offer = drawer.SampleOffer();
        Assert.AreEqual(3, offer.Length);
        foreach (int index in offer)
        {
            CardData card = drawer.defaultCardPool[index];
            Assert.AreEqual(CardType.Spell, card.cardtype);
            Assert.IsTrue(unique.Add(card));
        }
        Assert.AreEqual(0, drawer.CurrentSpellCount);
        drawer.Choose(1, 0);
        drawer.Choose(1, 0);
        Assert.AreEqual(1, drawer.CurrentSpellCount);
    }

    private ChessPiece Piece(int id, TeamColor side, PieceType type, int x, int y)
    {
        ChessPiece piece = New<ChessPiece>(side + " " + type);
        piece.Id = id;
        piece.team = side;
        piece.pieceType = type;
        piece.SetPosition(new Vector2Int(x, y), initializer.GetWorldPosition(new Vector2Int(x, y)));
        board.PlacePiece(piece, piece.currentCell);
        return piece;
    }

    [Test]
    public void ResurrectedRookCannotCaptureUntilItsOwnersNextTurn()
    {
        ResurrectionSpellUI spell = New<ResurrectionSpellUI>("Resurrection");
        ChessPiece template = New<ChessPiece>("White rook");
        template.team = TeamColor.White;
        template.pieceType = PieceType.Rook;
        template.startingCell = Vector2Int.zero;
        template.originalPrefab = template.gameObject;
        var captured = new ChessBoard.CapturedPieceData(template);
        board.graveyard.GetCapturedByTeam(template.team).Add(captured);
        ChessPiece target = Piece(2, TeamColor.Black, PieceType.Pawn, 0, 2);

        spell.Resurrect(captured);
        ChessPiece rook = board.GetPieceAt(captured.originalPosition);
        Assert.IsNotNull(rook);
        created.Add(rook.gameObject);
        Assert.IsTrue(rook.IsStunned);
        rook.TryMoveFromTap(target.currentCell);
        Assert.AreSame(target, board.GetPieceAt(target.currentCell));
        Assert.AreSame(rook, board.GetPieceAt(captured.originalPosition));

        turns.EndTurn();
        Assert.IsTrue(rook.IsStunned);
        turns.EndTurn();
        Assert.IsFalse(rook.IsStunned);
    }

    [UnityTest]
    public IEnumerator MinorResurrectionOffersOnlyEligiblePiecesAndKeepsTheStun()
    {
        var data = AssetDatabase.LoadAssetAtPath<CardData>(
            "Assets/ScriptableObjects/Cards/MinorResurrection.asset");
        Assert.IsNotNull(data.fullCardSprite);
        Assert.IsNotNull(data.spellUI);
        var canvas = New<Canvas>("MainCanvas");
        var handCard = New<CardUI>("Minor Resurrection");
        handCard.cardButton = handCard.gameObject.AddComponent<UnityEngine.UI.Button>();
        var captured = board.graveyard.GetCapturedByTeam(TeamColor.White);
        foreach (PieceType type in new[] { PieceType.Rook, PieceType.Pawn, PieceType.Knight, PieceType.Bishop })
        {
            var template = New<ChessPiece>(type.ToString());
            template.team = TeamColor.White;
            template.pieceType = type;
            template.startingCell = new Vector2Int(captured.Count, 0);
            template.originalPrefab = template.gameObject;
            captured.Add(new ChessBoard.CapturedPieceData(template));
        }

        handCard.LoadCard(data);
        handCard.cardButton.onClick.Invoke();
        var spell = canvas.GetComponentInChildren<ResurrectionSpellUI>();
        Assert.IsNotNull(spell);
        yield return null;
        Assert.AreEqual(3, spell.whiteContainer.GetComponentsInChildren<ResurrectionButton>().Length);
        spell.Resurrect(captured[0]);
        Assert.AreEqual(4, captured.Count);
        Assert.IsFalse(turns.HasCastFreeSpellThisTurn);
        var knight = captured[2];
        spell.Resurrect(knight);
        var revived = board.GetPieceAt(knight.originalPosition);
        Assert.IsNotNull(revived);
        created.Add(revived.gameObject);
        Assert.AreEqual(PieceType.Knight, revived.pieceType);
        Assert.IsTrue(revived.IsStunned);
        Assert.IsFalse(captured.Contains(knight));
        Assert.IsTrue(turns.HasCastFreeSpellThisTurn);
        yield return null;
        Assert.IsTrue(handCard == null);
    }

    private GameState Host(out NetPlayer player)
    {
        manager = New<NetworkManager>("Test network");
        manager.NetworkConfig = new NetworkConfig();
        manager.NetworkConfig.NetworkTransport = manager.gameObject.AddComponent<UnityTransport>();
        manager.NetworkConfig.EnableSceneManagement = false;
        Assert.IsTrue(manager.StartHost());
        GameObject stateObject = Object.Instantiate(
            AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Scripts/Network/GameState.prefab"));
        created.Add(stateObject);
        GameState state = stateObject.GetComponent<GameState>();
        stateObject.GetComponent<NetworkObject>().Spawn();
        GameObject playerObject = Object.Instantiate(
            AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Scripts/Network/NetPlayer.prefab"));
        created.Add(playerObject);
        player = playerObject.GetComponent<NetPlayer>();
        playerObject.GetComponent<NetworkObject>().SpawnWithOwnership(NetworkManager.ServerClientId);
        CardDrawer.Instance.SyncSpellCountToNetwork();
        state.PickCardRpc(1, 0);
        Assert.AreEqual(0, player.SpellCount.Value, "A player cannot pick before an offer exists.");
        // Host-only fixture: exercise the same draw that begins when both clients are ready.
        Call(state, "BeginDraw");
        CardDrawer.Instance.Choose(1, 0);
        return state;
    }

    [UnityTest]
    public IEnumerator HandCountsStartingCardsDrawsCancellationAndSuccessExactlyOnce()
    {
        CardDrawer drawer = Hand(2);
        Assert.AreEqual(2, drawer.CurrentSpellCount);
        drawer.DrawOneSpellCard();
        Assert.AreEqual(2, drawer.CurrentSpellCount);
        drawer.Choose(3, 0);
        drawer.Choose(3, 0);
        Assert.AreEqual(3, drawer.CurrentSpellCount);
        CardUI card = drawer.handPanel.GetChild(0).GetComponent<CardUI>();
        card.CancelPendingSpellCast();
        Assert.AreEqual(3, drawer.CurrentSpellCount);
        card.ConsumeCardAfterSuccessfulCast();
        card.ConsumeCardAfterSuccessfulCast();
        Assert.AreEqual(2, drawer.CurrentSpellCount);
        yield return null;
        Assert.AreEqual(2, drawer.handPanel.childCount);
    }

    [UnityTest]
    public IEnumerator CheckmateWaitsForTheActualLastSpellToBeConsumed()
    {
        CardDrawer drawer = Hand(2);
        Piece(1, TeamColor.White, PieceType.King, 0, 0);
        Piece(2, TeamColor.Black, PieceType.Queen, 1, 1);
        Piece(3, TeamColor.Black, PieceType.King, 2, 2);
        Assert.IsFalse(StormEndgameEvaluator.TryResolveCheckmate(TeamColor.White));
        drawer.handPanel.GetChild(0).GetComponent<CardUI>().ConsumeCardAfterSuccessfulCast();
        Assert.IsFalse(StormEndgameEvaluator.TryResolveCheckmate(TeamColor.White));
        drawer.handPanel.GetChild(1).GetComponent<CardUI>().ConsumeCardAfterSuccessfulCast();
        Assert.AreEqual(0, drawer.CurrentSpellCount);
        Assert.IsTrue(StormEndgameEvaluator.TryResolveCheckmate(TeamColor.White));
        Assert.IsTrue(board.gameOver);
        yield return null;
    }

    [UnityTest]
    public IEnumerator RejectedMovesDoNotAwardCardsAndRestorePendingPiece()
    {
        CardDrawer drawer = Hand();
        GameState state = Host(out NetPlayer player);
        player.Side.Value = TeamColor.Black;
        state.CurrentTurn.Value = TeamColor.Black;
        state.MoveNumber.Value = 7;
        var schedule = new CardDrawSchedule();
        for (int i = 0; i < 6; i++)
        {
            schedule.Record(TeamColor.White);
            schedule.Record(TeamColor.Black);
        }
        schedule.Record(TeamColor.White);
        Set(state, "drawSchedule", schedule);
        ChessPiece pawn = Piece(10, TeamColor.Black, PieceType.Pawn, 0, 6);
        ChessPiece opponent = Piece(11, TeamColor.White, PieceType.Pawn, 0, 1);
        Set(pawn, "awaitingNetworkMove", true);
        pawn.transform.position = new Vector3(99, 99, 0);
        state.RequestMoveServerRpc(pawn.Id, 99, 99);
        Assert.AreEqual(7, state.MoveNumber.Value);
        Assert.AreEqual(1, drawer.CurrentSpellCount);
        Assert.AreEqual(initializer.GetWorldPosition(pawn.currentCell), pawn.transform.position);
        Assert.IsFalse((bool)typeof(ChessPiece).GetField("awaitingNetworkMove",
            BindingFlags.Instance | BindingFlags.NonPublic).GetValue(pawn));
        state.RequestMoveServerRpc(opponent.Id, 0, 2);
        Assert.AreEqual(new Vector2Int(0, 1), opponent.currentCell);
        Set(state, "pendingPromotionPawnId", pawn.Id);
        state.RequestMoveServerRpc(pawn.Id, 0, 5);
        Assert.AreEqual(new Vector2Int(0, 6), pawn.currentCell);
        Assert.AreEqual(7, state.MoveNumber.Value);
        Set(state, "pendingPromotionPawnId", -1);
        state.RequestMoveServerRpc(pawn.Id, 0, 5);
        Assert.AreEqual(new Vector2Int(0, 5), pawn.currentCell);
        Assert.AreEqual(8, state.MoveNumber.Value);
        Assert.IsTrue(CardDrawer.BlocksPlay);
        state.PickCardRpc(1, 0);
        state.PickCardRpc(2, 99);
        Assert.AreEqual(1, player.SpellCount.Value);
        drawer.Choose(2, 0);
        state.PickCardRpc(2, 0);
        Assert.AreEqual(2, drawer.CurrentSpellCount);
        Assert.AreEqual(2, player.SpellCount.Value);
        yield return null;
    }

    [UnityTest]
    public IEnumerator ServerAcceptsResourcesOnceAndRejectsWrongTargetsAndRepeatSpells()
    {
        CardDrawer drawer = Hand();
        GameState state = Host(out NetPlayer player);
        Piece(1, TeamColor.White, PieceType.King, 7, 0);
        Piece(2, TeamColor.Black, PieceType.King, 7, 7);
        ChessPiece own = Piece(3, TeamColor.White, PieceType.Pawn, 0, 1);
        ChessPiece enemy = Piece(4, TeamColor.Black, PieceType.Pawn, 0, 6);
        bool? accepted = null;
        state.RequestSpell(NetworkSpellKind.Protection, result => accepted = result, enemy.Id);
        Assert.AreEqual(false, accepted);
        Assert.IsFalse(enemy.IsDivinelyProtected);
        Assert.AreEqual(1, player.SpellCount.Value);
        state.RequestSpell(NetworkSpellKind.Protection, result => accepted = result, own.Id);
        Assert.AreEqual(true, accepted);
        Assert.IsTrue(own.IsDivinelyProtected);
        Assert.AreEqual(0, player.SpellCount.Value);
        state.RequestSpell(NetworkSpellKind.Freeze, result => accepted = result, enemy.Id);
        Assert.AreEqual(false, accepted);
        Assert.IsFalse(enemy.IsFrozen);
        Assert.AreEqual(0, player.SpellCount.Value);
        FreezeSpellUI freezeUI = New<FreezeSpellUI>("Rejected freeze UI");
        freezeUI.enabled = false;
        freezeUI.BindSourceCard(drawer.handPanel.GetChild(0).GetComponent<CardUI>());
        Call(freezeUI, "Freeze", enemy);
        Assert.AreEqual(1, drawer.CurrentSpellCount, "A rejected UI request must retain its source card.");
        Assert.AreEqual(1, drawer.handPanel.childCount);
        player.SetMageAbilityAvailableServerRpc(true, (int)NetworkSpellKind.Freeze);
        state.RequestSpell(NetworkSpellKind.Lightning, result => accepted = result,
            enemy.Id, mageAbility: true);
        Assert.AreEqual(false, accepted);
        Assert.IsTrue(player.MageAbilityAvailable.Value);
        state.RequestSpell(NetworkSpellKind.Freeze, result => accepted = result,
            enemy.Id, mageAbility: true);
        Assert.AreEqual(true, accepted);
        Assert.IsTrue(enemy.IsFrozen);
        Assert.IsFalse(player.MageAbilityAvailable.Value);
        Assert.AreEqual(0, player.SpellCount.Value);
        player.SetMageAbilityAvailableServerRpc(true, (int)NetworkSpellKind.Freeze);
        Assert.IsFalse(player.MageAbilityAvailable.Value);
        drawer.SyncSpellCountToNetwork();
        Assert.AreEqual(0, player.SpellCount.Value, "Hand initialization must not overwrite server consumption.");
        board.gameOver = true;
        state.RequestSpell(NetworkSpellKind.Trap, result => accepted = result, x: 2, y: 2);
        Assert.AreEqual(false, accepted);
        Assert.AreEqual(0, player.SpellCount.Value);
        yield return null;
    }

    [UnityTest]
    public IEnumerator NewMatchCanInitializeResourcesWithoutAllowingRepeatRefills()
    {
        CardDrawer drawer = Hand();
        GameState state = Host(out NetPlayer player);
        Assert.AreEqual(1, player.SpellCount.Value);
        player.SpellCount.Value = 1;
        drawer.SyncSpellCountToNetwork();
        Assert.AreEqual(1, player.SpellCount.Value);
        Call(state, "ResetActionState");
        Assert.AreEqual(0, player.SpellCount.Value);
        CardDrawer newHand = Hand();
        Call(state, "BeginDraw");
        newHand.Choose(2, 0);
        Assert.AreEqual(1, player.SpellCount.Value);
        player.SpellCount.Value = 0;
        newHand.SyncSpellCountToNetwork();
        Assert.AreEqual(0, player.SpellCount.Value);
        yield return null;
    }

    [UnityTest]
    public IEnumerator SoloDrawControllerIgnoresNetworkTurnsAndStillDrawsOffline()
    {
        CardDrawer drawer = Hand();
        Host(out _);
        SoloCardDrawController controller = New<SoloCardDrawController>("Solo drawing");
        controller.enabled = false;
        Set(controller, "playerCardDrawer", drawer);
        Call(controller, "Start");
        for (int i = 0; i < 14; i++) turns.CompleteChessMove();
        Assert.IsFalse(CardDrawer.BlocksPlay);
        manager.Shutdown();
        yield return null;
        for (int i = 0; i < 14; i++) turns.NextTurn();
        for (int i = 0; i < 13; i++) turns.CompleteChessMove();
        Assert.IsFalse(CardDrawer.BlocksPlay);
        turns.CompleteChessMove();
        Assert.IsTrue(CardDrawer.BlocksPlay);
        Assert.AreEqual(1, drawer.CurrentSpellCount);
        drawer.Choose(2, 0);
        Assert.AreEqual(2, drawer.CurrentSpellCount);
    }

    [UnityTest]
    public IEnumerator AIFallbackSkipsFrozenPiecesAndCompletesItsTurn()
    {
        SoloSession.ConfigureCampaign(TeamColor.White);
        turns.currentTurn = TeamColor.Black;
        Piece(1, TeamColor.White, PieceType.King, 7, 0);
        Piece(2, TeamColor.Black, PieceType.King, 7, 7);
        ChessPiece frozen = Piece(3, TeamColor.Black, PieceType.Pawn, 0, 6);
        frozen.ApplyFreeze(2);
        Piece(4, TeamColor.Black, PieceType.Rook, 1, 6);
        ComputerPlayer ai = New<ComputerPlayer>("AI");
        Assert.IsTrue((bool)Call(ai, "TryFallbackMove", TeamColor.Black));
        Assert.AreEqual(new Vector2Int(0, 6), frozen.currentCell);
        Assert.AreEqual(TeamColor.White, turns.currentTurn);
        yield return null;
    }
}
