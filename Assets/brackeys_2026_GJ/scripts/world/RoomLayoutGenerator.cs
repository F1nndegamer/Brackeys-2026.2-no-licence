using System.Collections.Generic;
using UnityEngine;

public class RoomLayoutGenerator
{
    private readonly IList<Vector2Int> roomSizes;
    private readonly IList<string> roomRoles;
    private readonly System.Random random;
    private readonly int placementAttempts;
    private readonly int roomPadding;
    private readonly int roomSpread;
    private readonly int hallwayWidth;
    private readonly bool generateHallways;
    private readonly int extraHallwayCount;
    private readonly int maxConnectionsPerRoom;

    public RoomLayoutGenerator(
        IList<Vector2Int> roomSizes,
        IList<string> roomRoles,
        System.Random random,
        int placementAttempts,
        int roomPadding,
        int roomSpread,
        int hallwayWidth,
        bool generateHallways,
        int extraHallwayCount,
        int maxConnectionsPerRoom
    )
    {
        this.roomSizes = roomSizes;
        this.roomRoles = roomRoles;
        this.random = random;
        this.placementAttempts = Mathf.Max(1, placementAttempts);
        this.roomPadding = Mathf.Max(0, roomPadding);
        this.roomSpread = Mathf.Max(0, roomSpread);
        this.hallwayWidth = Mathf.Max(1, hallwayWidth);
        this.generateHallways = generateHallways;
        this.extraHallwayCount = Mathf.Max(0, extraHallwayCount);
        this.maxConnectionsPerRoom = Mathf.Max(1, maxConnectionsPerRoom);
    }

    public bool TryGenerate(out RoomLayout layout)
    {
        layout = new RoomLayout();

        if (!TryGenerateRooms(layout))
            return false;

        return !generateHallways || TryGenerateHallways(layout);
    }

    private bool TryGenerateRooms(RoomLayout layout)
    {
        if (roomSizes.Count == 0)
            return false;

        Vector2Int firstSize = roomSizes[0];
        AddRoom(
            layout,
            new RectInt(
                -firstSize.x / 2,
                -firstSize.y / 2,
                firstSize.x,
                firstSize.y
            )
        );

        for (int roomIndex = 1; roomIndex < roomSizes.Count; roomIndex++)
        {
            if (!TryPlaceRoom(layout, roomSizes[roomIndex], out RectInt room))
                return false;

            AddRoom(layout, room);
        }

        return true;
    }

    private bool TryPlaceRoom(
        RoomLayout layout,
        Vector2Int size,
        out RectInt room
    )
    {
        int totalAttempts = placementAttempts * 6;

        for (int attempt = 0; attempt < totalAttempts; attempt++)
        {
            RectInt anchor = layout.Rooms[random.Next(layout.Rooms.Count)].Bounds;
            room = CreateCandidateRoom(anchor, size);

            if (CanPlaceRoom(layout, room))
                return true;
        }

        room = default;
        return false;
    }

    private RectInt CreateCandidateRoom(RectInt anchor, Vector2Int size)
    {
        int side = random.Next(4);
        int x;
        int y;

        switch (side)
        {
            case 0:
                x = random.Next(anchor.xMin - size.x - roomSpread, anchor.xMax + roomSpread);
                y = anchor.yMax + random.Next(0, roomSpread + 1);
                break;
            case 1:
                x = random.Next(anchor.xMin - size.x - roomSpread, anchor.xMax + roomSpread);
                y = anchor.yMin - size.y - random.Next(0, roomSpread + 1);
                break;
            case 2:
                x = anchor.xMax + random.Next(0, roomSpread + 1);
                y = random.Next(anchor.yMin - size.y - roomSpread, anchor.yMax + roomSpread);
                break;
            default:
                x = anchor.xMin - size.x - random.Next(0, roomSpread + 1);
                y = random.Next(anchor.yMin - size.y - roomSpread, anchor.yMax + roomSpread);
                break;
        }

        return new RectInt(x, y, size.x, size.y);
    }

    private bool CanPlaceRoom(RoomLayout layout, RectInt room)
    {
        RectInt paddedRoom = new RectInt(
            room.xMin - roomPadding,
            room.yMin - roomPadding,
            room.width + roomPadding * 2,
            room.height + roomPadding * 2
        );

        foreach (GeneratedRoom existingRoom in layout.Rooms)
        {
            if (paddedRoom.Overlaps(existingRoom.Bounds))
                return false;
        }

        return true;
    }

    private void AddRoom(RoomLayout layout, RectInt room)
    {
        int roomIndex = layout.Rooms.Count;
        layout.Rooms.Add(new GeneratedRoom(GetRoomRole(roomIndex), room));
    }

    private string GetRoomRole(int roomIndex)
    {
        if (roomIndex < roomRoles.Count && !string.IsNullOrWhiteSpace(roomRoles[roomIndex]))
            return roomRoles[roomIndex];

        return "Room " + (roomIndex + 1);
    }

    private bool TryGenerateHallways(RoomLayout layout)
    {
        if (layout.Rooms.Count < 2)
            return true;

        List<int> connected = new List<int> { 0 };
        List<int> unconnected = new List<int>();
        List<int> connectionCounts = new List<int>();

        for (int roomIndex = 0; roomIndex < layout.Rooms.Count; roomIndex++)
        {
            connectionCounts.Add(0);

            if (roomIndex > 0)
                unconnected.Add(roomIndex);
        }

        while (unconnected.Count > 0)
        {
            if (!TryFindNearestConnection(
                layout,
                connected,
                unconnected,
                connectionCounts,
                out int connectedIndex,
                out int unconnectedIndex
            ))
                return false;

            if (!TryCreateConnection(
                layout,
                layout.Rooms[connectedIndex].Bounds,
                layout.Rooms[unconnectedIndex].Bounds,
                out List<Vector2Int> route
            ))
                return false;

            AddConnection(
                layout,
                connectionCounts,
                connectedIndex,
                unconnectedIndex,
                route
            );
            connected.Add(unconnectedIndex);
            unconnected.Remove(unconnectedIndex);
        }

        TryAddExtraHallways(layout, connectionCounts);
        return true;
    }

    private bool TryFindNearestConnection(
        RoomLayout layout,
        List<int> connected,
        List<int> unconnected,
        List<int> connectionCounts,
        out int bestConnected,
        out int bestUnconnected
    )
    {
        float shortestDistance = float.MaxValue;
        bestConnected = -1;
        bestUnconnected = -1;

        foreach (int connectedIndex in connected)
        {
            if (connectionCounts[connectedIndex] >= maxConnectionsPerRoom)
                continue;

            foreach (int unconnectedIndex in unconnected)
            {
                if (connectionCounts[unconnectedIndex] >= maxConnectionsPerRoom)
                    continue;

                float distance = GetRoomDistance(
                    layout.Rooms[connectedIndex].Bounds,
                    layout.Rooms[unconnectedIndex].Bounds
                );

                if (distance < shortestDistance)
                {
                    shortestDistance = distance;
                    bestConnected = connectedIndex;
                    bestUnconnected = unconnectedIndex;
                }
            }
        }

        return bestConnected >= 0;
    }

    private void TryAddExtraHallways(RoomLayout layout, List<int> connectionCounts)
    {
        if (extraHallwayCount == 0)
            return;

        List<RoomPair> candidates = new List<RoomPair>();

        for (int first = 0; first < layout.Rooms.Count; first++)
        {
            for (int second = first + 1; second < layout.Rooms.Count; second++)
            {
                if (!AreConnected(layout, first, second))
                    candidates.Add(new RoomPair(first, second));
            }
        }

        Shuffle(candidates);

        int extraHallwaysAdded = 0;

        foreach (RoomPair candidate in candidates)
        {
            if (extraHallwaysAdded >= extraHallwayCount)
                return;

            if (connectionCounts[candidate.First] >= maxConnectionsPerRoom ||
                connectionCounts[candidate.Second] >= maxConnectionsPerRoom)
                continue;

            if (!TryCreateConnection(
                layout,
                layout.Rooms[candidate.First].Bounds,
                layout.Rooms[candidate.Second].Bounds,
                out List<Vector2Int> route
            ))
                continue;

            AddConnection(
                layout,
                connectionCounts,
                candidate.First,
                candidate.Second,
                route
            );
            extraHallwaysAdded++;
        }
    }

    private bool AreConnected(RoomLayout layout, int first, int second)
    {
        foreach (RoomConnection connection in layout.Connections)
        {
            bool matchesForward =
                connection.FirstRoomIndex == first && connection.SecondRoomIndex == second;
            bool matchesBackward =
                connection.FirstRoomIndex == second && connection.SecondRoomIndex == first;

            if (matchesForward || matchesBackward)
                return true;
        }

        return false;
    }

    private void AddConnection(
        RoomLayout layout,
        List<int> connectionCounts,
        int firstRoomIndex,
        int secondRoomIndex,
        List<Vector2Int> route
    )
    {
        CreatePath(layout, route);
        layout.Connections.Add(new RoomConnection(firstRoomIndex, secondRoomIndex, route));
        connectionCounts[firstRoomIndex]++;
        connectionCounts[secondRoomIndex]++;
    }

    private void Shuffle<T>(List<T> values)
    {
        for (int index = values.Count - 1; index > 0; index--)
        {
            int replacementIndex = random.Next(index + 1);
            T value = values[index];
            values[index] = values[replacementIndex];
            values[replacementIndex] = value;
        }
    }

    private float GetRoomDistance(RectInt first, RectInt second)
    {
        return Mathf.Abs(first.center.x - second.center.x) +
               Mathf.Abs(first.center.y - second.center.y);
    }

    private bool TryCreateConnection(
        RoomLayout layout,
        RectInt roomA,
        RectInt roomB,
        out List<Vector2Int> route
    )
    {
        GetConnectionPoints(roomA, roomB, out Vector2Int start, out Vector2Int end);

        List<Vector2Int> directPath = new List<Vector2Int> { start, end };

        if ((start.x == end.x || start.y == end.y) &&
            IsPathClear(layout, directPath, roomA, roomB))
        {
            route = directPath;
            return true;
        }

        List<Vector2Int> horizontalFirst = new List<Vector2Int>
        {
            start,
            new Vector2Int(end.x, start.y),
            end
        };
        List<Vector2Int> verticalFirst = new List<Vector2Int>
        {
            start,
            new Vector2Int(start.x, end.y),
            end
        };

        bool horizontalValid = IsPathClear(layout, horizontalFirst, roomA, roomB);
        bool verticalValid = IsPathClear(layout, verticalFirst, roomA, roomB);

        if (horizontalValid && verticalValid)
        {
            route = random.Next(2) == 0 ? horizontalFirst : verticalFirst;
            return true;
        }

        if (horizontalValid)
        {
            route = horizontalFirst;
            return true;
        }

        if (verticalValid)
        {
            route = verticalFirst;
            return true;
        }

        return TryCreateFallbackPath(layout, start, end, roomA, roomB, out route);
    }

    private void GetConnectionPoints(
        RectInt roomA,
        RectInt roomB,
        out Vector2Int start,
        out Vector2Int end
    )
    {
        Vector2 direction = roomB.center - roomA.center;

        if (Mathf.Abs(direction.x) >= Mathf.Abs(direction.y))
        {
            int yA = Mathf.Clamp(
                Mathf.RoundToInt(roomB.center.y),
                roomA.yMin + hallwayWidth / 2,
                roomA.yMax - 1 - hallwayWidth / 2
            );
            int yB = Mathf.Clamp(
                Mathf.RoundToInt(roomA.center.y),
                roomB.yMin + hallwayWidth / 2,
                roomB.yMax - 1 - hallwayWidth / 2
            );

            if (direction.x >= 0)
            {
                start = new Vector2Int(roomA.xMax - 1, yA);
                end = new Vector2Int(roomB.xMin, yB);
            }
            else
            {
                start = new Vector2Int(roomA.xMin, yA);
                end = new Vector2Int(roomB.xMax - 1, yB);
            }
        }
        else
        {
            int xA = Mathf.Clamp(
                Mathf.RoundToInt(roomB.center.x),
                roomA.xMin + hallwayWidth / 2,
                roomA.xMax - 1 - hallwayWidth / 2
            );
            int xB = Mathf.Clamp(
                Mathf.RoundToInt(roomA.center.x),
                roomB.xMin + hallwayWidth / 2,
                roomB.xMax - 1 - hallwayWidth / 2
            );

            if (direction.y >= 0)
            {
                start = new Vector2Int(xA, roomA.yMax - 1);
                end = new Vector2Int(xB, roomB.yMin);
            }
            else
            {
                start = new Vector2Int(xA, roomA.yMin);
                end = new Vector2Int(xB, roomB.yMax - 1);
            }
        }
    }

    private bool IsPathClear(
        RoomLayout layout,
        List<Vector2Int> points,
        RectInt roomA,
        RectInt roomB
    )
    {
        for (int index = 0; index < points.Count - 1; index++)
        {
            RectInt hallway = GetHallwayRect(points[index], points[index + 1]);

            foreach (GeneratedRoom room in layout.Rooms)
            {
                if (room.Bounds == roomA || room.Bounds == roomB)
                    continue;

                if (hallway.Overlaps(room.Bounds))
                    return false;
            }
        }

        return true;
    }

    private bool TryCreateFallbackPath(
        RoomLayout layout,
        Vector2Int start,
        Vector2Int end,
        RectInt roomA,
        RectInt roomB,
        out List<Vector2Int> route
    )
    {
        Vector2Int bestCorner = Vector2Int.zero;
        float bestDistance = float.MaxValue;
        bool found = false;

        for (int x = Mathf.Min(start.x, end.x) - 20; x <= Mathf.Max(start.x, end.x) + 20; x++)
        {
            TryUseFallbackCorner(layout, start, end, roomA, roomB, new Vector2Int(x, start.y), ref bestCorner, ref bestDistance, ref found);
            TryUseFallbackCorner(layout, start, end, roomA, roomB, new Vector2Int(x, end.y), ref bestCorner, ref bestDistance, ref found);
        }

        for (int y = Mathf.Min(start.y, end.y) - 20; y <= Mathf.Max(start.y, end.y) + 20; y++)
        {
            TryUseFallbackCorner(layout, start, end, roomA, roomB, new Vector2Int(start.x, y), ref bestCorner, ref bestDistance, ref found);
            TryUseFallbackCorner(layout, start, end, roomA, roomB, new Vector2Int(end.x, y), ref bestCorner, ref bestDistance, ref found);
        }

        if (!found)
        {
            route = null;
            return false;
        }

        route = new List<Vector2Int> { start, bestCorner, end };
        return true;
    }

    private void TryUseFallbackCorner(
        RoomLayout layout,
        Vector2Int start,
        Vector2Int end,
        RectInt roomA,
        RectInt roomB,
        Vector2Int corner,
        ref Vector2Int bestCorner,
        ref float bestDistance,
        ref bool found
    )
    {
        List<Vector2Int> path = new List<Vector2Int> { start, corner, end };

        if (!IsPathClear(layout, path, roomA, roomB))
            return;

        float distance = Vector2Int.Distance(start, corner) + Vector2Int.Distance(corner, end);

        if (distance >= bestDistance)
            return;

        bestDistance = distance;
        bestCorner = corner;
        found = true;
    }

    private void CreatePath(RoomLayout layout, List<Vector2Int> points)
    {
        for (int index = 0; index < points.Count - 1; index++)
            layout.Hallways.Add(GetHallwayRect(points[index], points[index + 1]));
    }

    private RectInt GetHallwayRect(Vector2Int start, Vector2Int end)
    {
        if (start.x == end.x)
        {
            int minY = Mathf.Min(start.y, end.y);
            int maxY = Mathf.Max(start.y, end.y);
            return new RectInt(
                start.x - hallwayWidth / 2,
                minY,
                hallwayWidth,
                Mathf.Max(1, maxY - minY + 1)
            );
        }

        int minX = Mathf.Min(start.x, end.x);
        int maxX = Mathf.Max(start.x, end.x);
        return new RectInt(
            minX,
            start.y - hallwayWidth / 2,
            Mathf.Max(1, maxX - minX + 1),
            hallwayWidth
        );
    }

    private struct RoomPair
    {
        public int First { get; }
        public int Second { get; }

        public RoomPair(int first, int second)
        {
            First = first;
            Second = second;
        }
    }
}
