using UnityEngine;

public enum SoloGameMode
{
    None,
    Campaign,
    Practice
}

public enum SoloOpponent
{
    None,
    Stockfish
}

public static class SoloSession
{
    public static bool IsConfigured { get; private set; }

    public static SoloGameMode Mode { get; private set; } = SoloGameMode.None;
    public static SoloOpponent Opponent { get; private set; } = SoloOpponent.None;

    public static TeamColor PlayerSide { get; private set; } = TeamColor.White;
    public static TeamColor AISide { get; private set; } = TeamColor.Black;

    public static bool AIEnabled { get; private set; }

    public static void ConfigureCampaign(TeamColor playerSide)
    {
        IsConfigured = true;
        Mode = SoloGameMode.Campaign;
        Opponent = SoloOpponent.Stockfish;

        PlayerSide = playerSide;
        AISide = Opposite(playerSide);
        AIEnabled = true;

        Debug.Log(
            $"[SOLO] Campaign configured | Player={PlayerSide} | " +
            $"AI={AISide} | Opponent={Opponent}"
        );
    }

    public static void ConfigurePractice(
        TeamColor playerSide,
        bool enableAI)
    {
        IsConfigured = true;
        Mode = SoloGameMode.Practice;

        PlayerSide = playerSide;
        AISide = Opposite(playerSide);

        AIEnabled = enableAI;
        Opponent = enableAI
            ? SoloOpponent.Stockfish
            : SoloOpponent.None;
    }

    public static bool IsHumanSide(TeamColor side)
    {
        return !IsConfigured || side == PlayerSide;
    }

    public static bool IsAISide(TeamColor side)
    {
        return IsConfigured &&
               AIEnabled &&
               side == AISide;
    }

    public static void Reset()
    {
        IsConfigured = false;
        Mode = SoloGameMode.None;
        Opponent = SoloOpponent.None;

        PlayerSide = TeamColor.White;
        AISide = TeamColor.Black;
        AIEnabled = false;
    }

    private static TeamColor Opposite(TeamColor side)
    {
        return side == TeamColor.White
            ? TeamColor.Black
            : TeamColor.White;
    }
}
