using System;
using TMPro;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using UnityEngine.UI;
using UIButton = UnityEngine.UI.Button;

public class LobbyRoomRow : MonoBehaviour
{
    public TMP_Text roomName;
    public TMP_Text hostName;
    public TMP_Text access;
    public UIButton joinButton;

    public void Bind(Lobby room, Action<Lobby> join)
    {
        roomName.text = room.Name;
        string host = room.Data != null && room.Data.TryGetValue(LobbyMatchmaking.NameKey, out var name)
            ? name.Value : "Player";
        hostName.text = "Hosted by " + host;
        access.text = (room.HasPassword ? "PASSWORD" : "OPEN") + "   ·   " +
            (room.MaxPlayers - room.AvailableSlots) + "/" + room.MaxPlayers;
        joinButton.onClick.AddListener(() => join(room));
    }
}

