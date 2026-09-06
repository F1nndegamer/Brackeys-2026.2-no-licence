using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class ProcRoomGen : MonoBehaviour
{
    [System.Serializable]
    public class RoomSize
    {
        public int width = 6;
        public int height = 5;

        public RoomSize(int width, int height)
        {
            this.width = width;
            this.height = height;
        }
    }

    [Header("Rooms")]
    public List<RoomSize> roomSizes = new List<RoomSize>
    {
        new RoomSize(6, 5),
        new RoomSize(10, 5),
        new RoomSize(5, 7),
        new RoomSize(8, 4)
    };

    [Tooltip("Room names are assigned in the same order as Room Sizes.")]
    public List<string> roomRoles = new List<string>
    {
        "Security",
        "Archives",
        "Storage",
        "Laboratory"
    };

    [Header("Generation")]
    public int seed = 0;
    public bool randomSeed = true;
    [Min(1)] public int layoutAttempts = 25;
    [Min(1)] public int placementAttempts = 100;
    [Min(0)] public int roomPadding = 2;
    [Min(0)] public int roomSpread = 20;

    [Header("Hallways")]
    public bool generateHallways = true;
    [Min(0)] public int extraHallwayCount = 1;
    [Min(1)] public int maxConnectionsPerRoom = 3;
    [Range(1, 10)] public int hallwayWidth = 2;

    [Header("Visuals")]
    public GameObject roomPrefab;
    public GameObject hallwayPrefab;
    public float cellSize = 1f;

    [Header("Debug")]
    public bool generateOnStart = true;
    public bool showGizmos = true;
    public bool generateOnKeyPress = true;

    private RoomLayout layout = new RoomLayout();
    private Transform generatedRoot;
    private bool hasValidLayout;

    public IReadOnlyList<GeneratedRoom> Rooms => layout.Rooms;
    public IReadOnlyList<RoomConnection> Connections => layout.Connections;
    public bool HasValidLayout => hasValidLayout;

    private void Start()
    {
        if (generateOnStart)
            Generate();
    }

    private void Update()
    {
        bool rPressed =
            Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame;

        if (rPressed && generateOnKeyPress)
            Generate();
    }

    public void Generate()
    {
        ClearGeneration();

        int attempts = randomSeed ? Mathf.Max(1, layoutAttempts) : 1;

        for (int attempt = 0; attempt < attempts; attempt++)
        {
            if (randomSeed)
                seed = Random.Range(int.MinValue, int.MaxValue);

            List<Vector2Int> sizes = new List<Vector2Int>();

            foreach (RoomSize roomSize in roomSizes)
                sizes.Add(new Vector2Int(roomSize.width, roomSize.height));

            RoomLayoutGenerator generator = new RoomLayoutGenerator(
                sizes,
                roomRoles,
                new System.Random(seed),
                placementAttempts,
                roomPadding,
                roomSpread,
                hallwayWidth,
                generateHallways,
                extraHallwayCount,
                maxConnectionsPerRoom
            );

            if (!generator.TryGenerate(out RoomLayout generatedLayout))
                continue;

            layout = generatedLayout;
            BuildVisuals();
            hasValidLayout = true;
            return;
        }
    }

    private void BuildVisuals()
    {
        generatedRoot = new GameObject("Generated Layout").transform;
        generatedRoot.SetParent(transform, false);

        if (roomPrefab != null)
        {
            foreach (GeneratedRoom room in layout.Rooms)
            {
                GameObject roomObject = Instantiate(roomPrefab, generatedRoot);
                roomObject.name = room.Role;
                roomObject.transform.position = ToWorldPosition(room.Center);
                roomObject.transform.localScale = new Vector3(
                    room.Bounds.width * cellSize,
                    room.Bounds.height * cellSize,
                    1
                );
            }
        }

        if (hallwayPrefab != null)
        {
            foreach (RectInt hallway in layout.Hallways)
            {
                GameObject hallwayObject = Instantiate(hallwayPrefab, generatedRoot);
                hallwayObject.name = "Hallway";
                hallwayObject.transform.position = ToWorldPosition(hallway.center);
                hallwayObject.transform.localScale = new Vector3(
                    hallway.width * cellSize,
                    hallway.height * cellSize,
                    1
                );
            }
        }
    }

    public Vector3 ToWorldPosition(Vector2 gridPosition)
    {
        return new Vector3(gridPosition.x * cellSize, gridPosition.y * cellSize, 0);
    }

    private void ClearGeneration()
    {
        layout = new RoomLayout();
        hasValidLayout = false;

        if (generatedRoot == null)
            return;

        if (Application.isPlaying)
            Destroy(generatedRoot.gameObject);
        else
            DestroyImmediate(generatedRoot.gameObject);

        generatedRoot = null;
    }

    private void OnDrawGizmos()
    {
        if (!showGizmos)
            return;

        Gizmos.color = Color.green;

        foreach (GeneratedRoom room in layout.Rooms)
        {
            Gizmos.DrawWireCube(
                ToWorldPosition(room.Center),
                new Vector3(
                    room.Bounds.width * cellSize,
                    room.Bounds.height * cellSize,
                    0.1f
                )
            );
        }

        Gizmos.color = Color.yellow;

        foreach (RectInt hallway in layout.Hallways)
        {
            Gizmos.DrawWireCube(
                ToWorldPosition(hallway.center),
                new Vector3(
                    hallway.width * cellSize,
                    hallway.height * cellSize,
                    0.1f
                )
            );
        }
    }
}
