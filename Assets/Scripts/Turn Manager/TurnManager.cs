using UnityEngine;

public class TurnManager : MonoBehaviour
{
    public static TurnManager Instance;

    public TeamColor currentTurn = TeamColor.White;

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
    }

    public bool IsPlayersTurn(TeamColor team)
    {
        return currentTurn == team;
    }
}