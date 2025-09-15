using System;
using UnityEngine;

public class TurnManager : MonoBehaviour
{
    public static TurnManager Instance;

    public TeamColor currentTurn = TeamColor.White;
    public int turnNumber = 1;
    public void NextTurn() => EndTurn();

    // Single UI event to subscribe to for refreshes
    public event Action<TeamColor> OnTurnChanged;

    // One free spell per turn
    public bool HasCastFreeSpellThisTurn { get; private set; }

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
    }

    void Start()
    {
        // Ensure we start in a consistent state
        BeginTurn(currentTurn, /*raiseEvent*/ true);
    }

    // --- Public API ---

    public bool IsPlayersTurn(TeamColor team) => currentTurn == team;

    public bool CanCastFreeSpell(TeamColor caster)
    {
        return caster == currentTurn && !HasCastFreeSpellThisTurn;
    }

    public void RegisterFreeSpellCast()
    {
        HasCastFreeSpellThisTurn = true; // Do NOT end the turn here
        OnTurnChanged?.Invoke(currentTurn);
    }

    public void EndTurn()
    {
        // Flip side and advance the turn counter
        currentTurn = (currentTurn == TeamColor.White) ? TeamColor.Black : TeamColor.White;
        turnNumber++;
        BeginTurn(currentTurn, /*raiseEvent*/ true);
    }

    // For networking syncs or reconciling state from server/host
    public void SyncTurn(TeamColor newTurn)
    {
        currentTurn = newTurn;
        BeginTurn(currentTurn, /*raiseEvent*/ true);
    }

    // --- Internals ---

    private void BeginTurn(TeamColor side, bool raiseEvent)
    {
        // Reset per-turn flags here
        HasCastFreeSpellThisTurn = false;

        if (raiseEvent)
            OnTurnChanged?.Invoke(side); // UI re-enables spell cards/buttons here
    }
}
