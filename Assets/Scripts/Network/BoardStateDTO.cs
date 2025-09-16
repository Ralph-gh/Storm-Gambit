// BoardStateDTO.cs
using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public struct PieceDTO
{
    public int id;                 // stable unique id per piece (store on ChessPiece)
    public int type;               // cast PieceType to int
    public int team;               // cast TeamColor to int
    public Vector2Int cell;        // board coords
    public bool alive;             // true if on board
    public bool hasMoved;          // for castling/pawn double step
    public bool divineProtected;   // status flag
}

[Serializable]
public struct BoardSnapshot
{
    public int turn;                   // whose turn (TeamColor as int)
    public List<PieceDTO> pieces;      // all pieces (alive+captured)
    public int hash;                   // quick change detection
}
