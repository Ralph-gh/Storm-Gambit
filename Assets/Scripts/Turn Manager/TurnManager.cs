using System;
using UnityEngine;

public class TurnManager : MonoBehaviour
{
    public static TurnManager Instance;

    public TeamColor currentTurn = TeamColor.White;

    public int turnNumber = 0;
    public event Action<TeamColor> OnTurnChanged;
    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    public void NextTurn()
    {
        currentTurn = (currentTurn == TeamColor.White) ? TeamColor.Black : TeamColor.White;
        turnNumber++;
        OnTurnChanged?.Invoke(currentTurn); // NEW: notify listeners
    }

    public bool IsPlayersTurn(TeamColor team)
    {
        return currentTurn == team;
    }

    public void SyncTurn(TeamColor newTurn)
    {
        currentTurn = newTurn;
        OnTurnChanged?.Invoke(newTurn);
    }
}