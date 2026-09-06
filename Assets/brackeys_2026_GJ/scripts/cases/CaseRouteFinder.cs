using System;
using System.Collections.Generic;
using UnityEngine;

public static class CaseRouteFinder
{
    public static bool TryFindRoomIndex(
        IReadOnlyList<GeneratedRoom> rooms,
        string roomRole,
        out int roomIndex
    )
    {
        for (int index = 0; index < rooms.Count; index++)
        {
            if (string.Equals(rooms[index].Role, roomRole, StringComparison.OrdinalIgnoreCase))
            {
                roomIndex = index;
                return true;
            }
        }

        roomIndex = -1;
        return false;
    }

    public static bool TryBuildRoute(
        ProcRoomGen roomGenerator,
        int startRoomIndex,
        int destinationRoomIndex,
        out List<Vector3> route
    )
    {
        route = new List<Vector3>();

        if (startRoomIndex == destinationRoomIndex)
            return true;

        IReadOnlyList<GeneratedRoom> rooms = roomGenerator.Rooms;
        IReadOnlyList<RoomConnection> connections = roomGenerator.Connections;
        Queue<int> searchQueue = new Queue<int>();
        Dictionary<int, PreviousRoom> previousRooms = new Dictionary<int, PreviousRoom>();

        searchQueue.Enqueue(startRoomIndex);
        previousRooms.Add(startRoomIndex, null);

        while (searchQueue.Count > 0)
        {
            int currentRoomIndex = searchQueue.Dequeue();

            if (currentRoomIndex == destinationRoomIndex)
                break;

            foreach (RoomConnection connection in connections)
            {
                if (!TryGetNeighbour(connection, currentRoomIndex, out int neighbourIndex))
                    continue;

                if (previousRooms.ContainsKey(neighbourIndex))
                    continue;

                previousRooms.Add(neighbourIndex, new PreviousRoom(currentRoomIndex, connection));
                searchQueue.Enqueue(neighbourIndex);
            }
        }

        if (!previousRooms.ContainsKey(destinationRoomIndex))
            return false;

        List<RoomSegment> segments = new List<RoomSegment>();
        int roomIndex = destinationRoomIndex;

        while (roomIndex != startRoomIndex)
        {
            PreviousRoom previousRoom = previousRooms[roomIndex];
            segments.Add(new RoomSegment(previousRoom.RoomIndex, roomIndex, previousRoom.Connection));
            roomIndex = previousRoom.RoomIndex;
        }

        segments.Reverse();
        AddPoint(route, roomGenerator.ToWorldPosition(rooms[startRoomIndex].Center));

        foreach (RoomSegment segment in segments)
        {
            AddConnectionRoute(roomGenerator, route, segment);
            AddPoint(route, roomGenerator.ToWorldPosition(rooms[segment.ToRoomIndex].Center));
        }

        return true;
    }

    private static bool TryGetNeighbour(
        RoomConnection connection,
        int roomIndex,
        out int neighbourIndex
    )
    {
        if (connection.FirstRoomIndex == roomIndex)
        {
            neighbourIndex = connection.SecondRoomIndex;
            return true;
        }

        if (connection.SecondRoomIndex == roomIndex)
        {
            neighbourIndex = connection.FirstRoomIndex;
            return true;
        }

        neighbourIndex = -1;
        return false;
    }

    private static void AddConnectionRoute(
        ProcRoomGen roomGenerator,
        List<Vector3> route,
        RoomSegment segment
    )
    {
        bool forward = segment.Connection.FirstRoomIndex == segment.FromRoomIndex;
        IReadOnlyList<Vector2Int> routePoints = segment.Connection.RoutePoints;

        if (forward)
        {
            foreach (Vector2Int point in routePoints)
                AddPoint(route, roomGenerator.ToWorldPosition(point));

            return;
        }

        for (int index = routePoints.Count - 1; index >= 0; index--)
            AddPoint(route, roomGenerator.ToWorldPosition(routePoints[index]));
    }

    private static void AddPoint(List<Vector3> route, Vector3 point)
    {
        if (route.Count == 0 || route[route.Count - 1] != point)
            route.Add(point);
    }

    private class PreviousRoom
    {
        public int RoomIndex { get; }
        public RoomConnection Connection { get; }

        public PreviousRoom(int roomIndex, RoomConnection connection)
        {
            RoomIndex = roomIndex;
            Connection = connection;
        }
    }

    private struct RoomSegment
    {
        public int FromRoomIndex { get; }
        public int ToRoomIndex { get; }
        public RoomConnection Connection { get; }

        public RoomSegment(int fromRoomIndex, int toRoomIndex, RoomConnection connection)
        {
            FromRoomIndex = fromRoomIndex;
            ToRoomIndex = toRoomIndex;
            Connection = connection;
        }
    }
}
