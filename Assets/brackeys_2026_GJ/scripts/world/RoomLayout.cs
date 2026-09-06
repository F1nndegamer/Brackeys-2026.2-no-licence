using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class GeneratedRoom
{
    [SerializeField] private string role;
    [SerializeField] private RectInt bounds;

    public string Role => role;
    public RectInt Bounds => bounds;
    public Vector2 Center => bounds.center;

    public GeneratedRoom(string role, RectInt bounds)
    {
        this.role = role;
        this.bounds = bounds;
    }
}

[Serializable]
public class RoomConnection
{
    [SerializeField] private int firstRoomIndex;
    [SerializeField] private int secondRoomIndex;
    [SerializeField] private List<Vector2Int> routePoints;

    public int FirstRoomIndex => firstRoomIndex;
    public int SecondRoomIndex => secondRoomIndex;
    public IReadOnlyList<Vector2Int> RoutePoints => routePoints;

    public RoomConnection(
        int firstRoomIndex,
        int secondRoomIndex,
        List<Vector2Int> routePoints
    )
    {
        this.firstRoomIndex = firstRoomIndex;
        this.secondRoomIndex = secondRoomIndex;
        this.routePoints = routePoints;
    }
}

public class RoomLayout
{
    public List<GeneratedRoom> Rooms { get; } = new List<GeneratedRoom>();
    public List<RectInt> Hallways { get; } = new List<RectInt>();
    public List<RoomConnection> Connections { get; } = new List<RoomConnection>();
}
