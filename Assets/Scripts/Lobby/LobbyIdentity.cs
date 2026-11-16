using System;
using UnityEngine;

// Session display names only. Authentication remains anonymous until profiles are introduced.
public static class LobbyIdentity
{
    public const int MaxNameLength = 24;
    public static string PlayerName => PlayerPrefs.GetString("StormGambit.PlayerName", "");

    public static string ValidateName(string value)
    {
        value = (value ?? "").Trim();
        if (value.Length < 2 || value.Length > MaxNameLength)
            throw new ArgumentException("Enter a player name with 2–24 characters.");
        foreach (char c in value)
            if (char.IsControl(c) || c == '<' || c == '>')
                throw new ArgumentException("Use a player name without line breaks or < > symbols.");
        return value;
    }

    public static void SaveName(string value)
    {
        PlayerPrefs.SetString("StormGambit.PlayerName", ValidateName(value));
        PlayerPrefs.Save();
    }

    public static string ValidateRoomName(string value, string playerName)
    {
        value = (value ?? "").Trim();
        if (value.Length == 0) value = playerName + "'s game";
        if (value.Length > 40) throw new ArgumentException("Game names can contain up to 40 characters.");
        foreach (char c in value)
            if (char.IsControl(c) || c == '<' || c == '>')
                throw new ArgumentException("Use a game name without line breaks or < > symbols.");
        return value;
    }

    public static string ValidatePassword(string value, bool required)
    {
        if (!required) return null;
        if (string.IsNullOrEmpty(value) || value.Length < 8 || value.Length > 64)
            throw new ArgumentException("Passwords must contain 8–64 characters.");
        return value;
    }
}
