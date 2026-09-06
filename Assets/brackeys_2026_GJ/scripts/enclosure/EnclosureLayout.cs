using System.Collections.Generic;
using UnityEngine;

public class EnclosureZone
{
    public const float PooAlertMessLevel = 72f;

    public EnclosureZoneDefinition Definition { get; }
    public float MessLevel { get; private set; }
    public bool HasCrater { get; private set; }

    public string Id => Definition.zoneId;
    public string DisplayName => Definition.displayName;
    public Vector2 Center => Definition.center;
    public Vector2 Size => Definition.size;
    private readonly float hangoutHorizontalInset;
    private readonly float hangoutBottomInset;
    private readonly float hangoutTopInset;

    public bool NeedsCleaning => MessLevel >= PooAlertMessLevel;
    public EnclosureZone(
        EnclosureZoneDefinition definition,
        float horizontalInset,
        float bottomInset,
        float topInset
    )
    {
        Definition = definition;
        hangoutHorizontalInset = Mathf.Max(0f, horizontalInset);
        hangoutBottomInset = Mathf.Max(0f, bottomInset);
        hangoutTopInset = Mathf.Max(0f, topInset);
    }

    public Vector3 GetWanderPoint(System.Random random)
    {
        float halfWidth = Size.x * 0.5f;
        float halfHeight = Size.y * 0.5f;
        float horizontalExtent = Mathf.Max(0f, halfWidth - hangoutHorizontalInset);
        float minX = Center.x - horizontalExtent;
        float maxX = Center.x + horizontalExtent;
        float minY = Center.y - Mathf.Max(0f, halfHeight - hangoutBottomInset);
        float maxY = Center.y + Mathf.Max(0f, halfHeight - hangoutTopInset);
        float x = Mathf.Lerp(minX, maxX, (float)random.NextDouble());
        float y = Mathf.Lerp(minY, maxY, (float)random.NextDouble());

        return new Vector3(
            x,
            y,
            0f
        );
    }

    public bool Contains(Vector2 point)
    {
        Vector2 halfSize = Size * 0.5f;
        return Mathf.Abs(point.x - Center.x) <= halfSize.x &&
            Mathf.Abs(point.y - Center.y) <= halfSize.y;
    }

    public void AddMess(float amount)
    {
        MessLevel = Mathf.Clamp(MessLevel + amount, 0f, 100f);
    }

    public void Clean(float amount)
    {
        MessLevel = Mathf.Clamp(MessLevel - amount, 0f, 100f);
    }
    public void MarkCrater()
    {
        HasCrater = true;
    }
}

public class EnclosureHall
{
    public EnclosureHallDefinition Definition { get; }

    public Vector2[] Points => Definition.points;
    public int Width => Definition.width;

    public EnclosureHall(EnclosureHallDefinition definition)
    {
        Definition = definition;
    }
}

[DisallowMultipleComponent]
public class EnclosureLayout : MonoBehaviour
{
    [Header("Temporary Zone Visuals")]
    [SerializeField] private GameObject zoneVisualPrefab;
    [SerializeField] private GameObject hallVisualPrefab;
    [Range(0f, 1f)]
    [SerializeField] private float placeholderAlpha = 0.14f;

    [Header("Jungle Environment Art")]
    [SerializeField] private Sprite groundSprite;
    [SerializeField] private Sprite[] groundVariants;
    [SerializeField] private Sprite[] topWallSprites;
    [SerializeField] private Sprite[] bottomWallSprites;
    [SerializeField] private Sprite[] leftWallSprites;
    [SerializeField] private Sprite[] rightWallSprites;

    [Header("Floor Details")]
    [SerializeField] private bool showFloorDetails = true;
    [SerializeField] private Sprite[] floorDetailSprites;
    [SerializeField, Min(0)] private int minimumFloorDetailsPerZone = 4;
    [SerializeField, Min(0)] private int maximumFloorDetailsPerZone = 7;
    [SerializeField, Min(0f)] private float floorDetailEdgePadding = 0.55f;
    [SerializeField, Min(0.1f)] private float minimumFloorDetailScale = 0.9f;
    [SerializeField, Min(0.1f)] private float maximumFloorDetailScale = 1.1f;
    [SerializeField] private int floorDetailSortingOrder = -10;

    [SerializeField, Min(0f)] private float cameraFramePadding = 1f;
    [SerializeField, Min(0.1f)] private float wallThickness = 1.2f;
    [SerializeField, Min(0f)] private float entranceGap = 0.9f;
    [SerializeField, Min(0f)] private float edgeBleed = 0.16f;
    [SerializeField, Min(0f)] private float bottomWallRise = 0.35f;

    [Header("Jungle Environment Depth")]
    [SerializeField] private int topWallSortingOrder = 5;
    [SerializeField] private int sideWallSortingOrder = 30;
    [SerializeField] private int bottomWallSortingOrder = 60;

    [Header("Zone Hangout Area")]
    [SerializeField, Min(0f)] private float hangoutHorizontalInset = 0.35f;
    [SerializeField, Min(0f)] private float hangoutBottomInset = 0.65f;
    [SerializeField, Min(0f)] private float hangoutTopInset = 0.8f;

    private readonly List<EnclosureZone> zones = new List<EnclosureZone>();
    private readonly List<EnclosureHall> hallways = new List<EnclosureHall>();
    private readonly Dictionary<string, GameObject> zoneEnvironments =
        new Dictionary<string, GameObject>();
    private readonly Dictionary<string, Vector2> zoneFrameSizes =
        new Dictionary<string, Vector2>();
    private float sharedCameraOrthographicSize = 3f;

    [SerializeField] private EnclosurePathfinder pathfinder;

    public EnclosurePathfinder Pathfinder => pathfinder;
    private Transform zoneRoot;
    private Transform hallwayRoot;

    public IReadOnlyList<EnclosureZone> Zones => zones;
    public IReadOnlyList<EnclosureHall> Hallways => hallways;
    public float SharedCameraOrthographicSize => sharedCameraOrthographicSize;

    public bool IsBuilt { get; private set; }

    public void Build(EnclosureShiftDefinition definition, int runSeed = 0)
    {
        Clear();

        zoneRoot = new GameObject("Jungle Enclosure Zones").transform;
        zoneRoot.SetParent(transform, false);

        hallwayRoot = new GameObject("Jungle Enclosure Hallways").transform;
        hallwayRoot.SetParent(transform, false);

        for (int i = 0; i < definition.zones.Count; i++)
        {
            EnclosureZoneDefinition zoneDefinition = definition.zones[i];
            EnclosureZone zone = new EnclosureZone(
                zoneDefinition,
                hangoutHorizontalInset,
                hangoutBottomInset,
                hangoutTopInset
            );

            zones.Add(zone);
            BuildPlaceholder(zone);
        }

        for (int i = 0; i < definition.hallway.Count; i++)
        {
            EnclosureHall hallway = new EnclosureHall(definition.hallway[i]);

            hallways.Add(hallway);
            BuildHallPlaceholder(hallway, i);
        }

        float cameraAspect = Camera.main != null ? Camera.main.aspect : 16f / 9f;
        sharedCameraOrthographicSize = CalculateSharedCameraSize(cameraAspect);

        if (HasGroundArt())
        {
            System.Random backgroundRandom = new System.Random(runSeed ^ 0x31A5B7D9);
            System.Random floorDetailRandom = new System.Random(runSeed ^ 0x05EEDB17);
            for (int i = 0; i < zones.Count; i++)
            {
                BuildZoneEnvironment(
                    zones[i],
                    i,
                    PickGroundSprite(backgroundRandom),
                    floorDetailRandom
                );
            }
        }

        Physics2D.SyncTransforms();
        BuildPathfindingGrid();
        ShowZoneEnvironment(zones.Count > 0 ? zones[0].Id : null);

        IsBuilt = true;
    }
    private void BuildPathfindingGrid()
    {
        if (pathfinder == null)
            return;

        Bounds bounds = new Bounds();

        bool hasBounds = false;

        foreach (EnclosureZone zone in zones)
        {
            Bounds zoneBounds = new Bounds(
                zone.Center,
                zone.Size
            );

            if (!hasBounds)
            {
                bounds = zoneBounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(zoneBounds);
            }
        }

        foreach (EnclosureHall hall in hallways)
        {
            Vector2[] points = hall.Points;

            foreach (Vector2 point in points)
            {
                Vector3 point3D = point;

                if (!hasBounds)
                {
                    bounds = new Bounds(point3D, Vector3.zero);
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(point3D);
                }
            }
        }

        if (!hasBounds)
            return;

        bounds.Expand(2f);

        pathfinder.Build(bounds);
    }
    public EnclosureZone FindZone(string zoneId)
    {
        return zones.Find(zone => zone.Id == zoneId);
    }

    public void ShowZoneEnvironment(string zoneId)
    {
        foreach (KeyValuePair<string, GameObject> entry in zoneEnvironments)
            entry.Value.SetActive(entry.Key == zoneId);
    }

    private void BuildPlaceholder(EnclosureZone zone)
    {
        if (zoneVisualPrefab == null)
            return;

        GameObject room = Instantiate(zoneVisualPrefab, zoneRoot);

        room.name = zone.DisplayName;
        room.transform.position = zone.Center;
        room.transform.localScale = new Vector3(
            zone.Size.x,
            zone.Size.y,
            1f
        );

        if (HasGroundArt())
        {
            foreach (SpriteRenderer renderer in room.GetComponentsInChildren<SpriteRenderer>())
                renderer.enabled = false;
        }
        else
        {
            foreach (SpriteRenderer renderer in room.GetComponentsInChildren<SpriteRenderer>())
            {
                Color color = zone.Definition.placeholderColor;
                color.a = placeholderAlpha;
                renderer.color = color;
            }
        }
    }

    private void BuildZoneEnvironment(
        EnclosureZone zone,
        int variation,
        Sprite zoneGroundSprite,
        System.Random floorDetailRandom
    )
    {
        Transform artRoot = new GameObject($"{zone.DisplayName} Environment").transform;
        artRoot.SetParent(zoneRoot, false);
        artRoot.position = zone.Center;
        zoneEnvironments[zone.Id] = artRoot.gameObject;

        float cameraAspect = Camera.main != null ? Camera.main.aspect : 16f / 9f;
        Vector2 frameSize = new Vector2(
            sharedCameraOrthographicSize * 2f * cameraAspect,
            sharedCameraOrthographicSize * 2f
        );
        zoneFrameSizes[zone.Id] = frameSize;
        CreateVisual(artRoot, "Dirt Ground", zoneGroundSprite, Vector2.zero, frameSize, -20);
        BuildFloorDetails(artRoot, frameSize, floorDetailRandom);
        BuildHorizontalEdge(artRoot, zone, "Top", topWallSprites, variation * 3, frameSize, true);
        BuildHorizontalEdge(artRoot, zone, "Bottom", bottomWallSprites, variation * 3, frameSize, false);
        BuildVerticalEdge(artRoot, zone, "Left", leftWallSprites, variation * 3, frameSize, true);
        BuildVerticalEdge(artRoot, zone, "Right", rightWallSprites, variation * 3, frameSize, false);
    }

    private void BuildFloorDetails(
        Transform parent,
        Vector2 frameSize,
        System.Random random
    )
    {
        if (!showFloorDetails || floorDetailSprites == null ||
            floorDetailSprites.Length == 0 || random == null)
            return;

        List<Sprite> availableSprites = new List<Sprite>(floorDetailSprites.Length);

        foreach (Sprite sprite in floorDetailSprites)
        {
            if (sprite != null)
                availableSprites.Add(sprite);
        }

        if (availableSprites.Count == 0)
            return;

        for (int index = availableSprites.Count - 1; index > 0; index--)
        {
            int swapIndex = random.Next(index + 1);
            (availableSprites[index], availableSprites[swapIndex]) =
                (availableSprites[swapIndex], availableSprites[index]);
        }

        int minimumCount = Mathf.Clamp(
            minimumFloorDetailsPerZone,
            0,
            availableSprites.Count
        );
        int maximumCount = Mathf.Clamp(
            Mathf.Max(minimumCount, maximumFloorDetailsPerZone),
            minimumCount,
            availableSprites.Count
        );
        int detailCount = random.Next(minimumCount, maximumCount + 1);
        float halfWidth = Mathf.Max(
            0f,
            frameSize.x * 0.5f - wallThickness - floorDetailEdgePadding
        );
        float halfHeight = Mathf.Max(
            0f,
            frameSize.y * 0.5f - wallThickness - floorDetailEdgePadding
        );
        float minimumScale = Mathf.Max(0.1f, minimumFloorDetailScale);
        float maximumScale = Mathf.Max(minimumScale, maximumFloorDetailScale);

        for (int index = 0; index < detailCount; index++)
        {
            Sprite sprite = availableSprites[index];
            Vector2 position = new Vector2(
                Mathf.Lerp(-halfWidth, halfWidth, (float)random.NextDouble()),
                Mathf.Lerp(-halfHeight, halfHeight, (float)random.NextDouble())
            );
            float scale = Mathf.Lerp(
                minimumScale,
                maximumScale,
                (float)random.NextDouble()
            );

            GameObject detail = new GameObject($"Floor Detail {index + 1:00}");
            detail.transform.SetParent(parent, false);
            detail.transform.localPosition = new Vector3(position.x, position.y, 0f);
            detail.transform.localScale = Vector3.one * scale;

            SpriteRenderer renderer = detail.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = floorDetailSortingOrder;
            renderer.flipX = random.NextDouble() < 0.5d;
        }
    }

    private void BuildHorizontalEdge(
        Transform parent,
        EnclosureZone zone,
        string edgeName,
        Sprite[] sprites,
        int variation,
        Vector2 frameSize,
        bool top
    )
    {
        float y = (frameSize.y - wallThickness) * 0.5f * (top ? 1f : -1f);
        if (!top)
            y += bottomWallRise;
        List<Vector2> openings = FindEdgeOpenings(zone, true, top, frameSize.x);
        BuildEdgeSegments(
            parent,
            edgeName,
            sprites,
            variation,
            frameSize.x,
            y,
            true,
            openings,
            top ? topWallSortingOrder : bottomWallSortingOrder
        );
    }

    private void BuildVerticalEdge(
        Transform parent,
        EnclosureZone zone,
        string edgeName,
        Sprite[] sprites,
        int variation,
        Vector2 frameSize,
        bool left
    )
    {
        float x = (frameSize.x - wallThickness) * 0.5f * (left ? -1f : 1f);
        List<Vector2> openings = FindEdgeOpenings(zone, false, !left, frameSize.y);
        BuildEdgeSegments(
            parent,
            edgeName,
            sprites,
            variation,
            frameSize.y,
            x,
            false,
            openings,
            sideWallSortingOrder
        );
    }

    private List<Vector2> FindEdgeOpenings(
        EnclosureZone zone,
        bool horizontal,
        bool positiveEdge,
        float visualLength
    )
    {
        List<Vector2> openings = new List<Vector2>();
        float perpendicularCenter = horizontal ? zone.Center.y : zone.Center.x;
        float perpendicularSize = horizontal ? zone.Size.y : zone.Size.x;
        float edge = perpendicularCenter + perpendicularSize * 0.5f * (positiveEdge ? 1f : -1f);
        float alongCenter = horizontal ? zone.Center.x : zone.Center.y;
        float alongSize = horizontal ? zone.Size.x : zone.Size.y;

        foreach (EnclosureHall hall in hallways)
        {
            float tolerance = Mathf.Max(0.05f, hall.Width * 0.1f);
            Vector2[] points = hall.Points;

            if (points == null || points.Length == 0)
                continue;

            for (int endpointIndex = 0; endpointIndex < 2; endpointIndex++)
            {
                Vector2 point = points[endpointIndex == 0 ? 0 : points.Length - 1];
                float perpendicular = horizontal ? point.y : point.x;
                float along = horizontal ? point.x : point.y;

                if (Mathf.Abs(perpendicular - edge) > tolerance ||
                    Mathf.Abs(along - alongCenter) > alongSize * 0.5f + tolerance)
                    continue;

                float visualCenter = Mathf.Clamp(
                    along - alongCenter,
                    -visualLength * 0.5f,
                    visualLength * 0.5f
                );
                float visualWidth = Mathf.Max(entranceGap, hall.Width);
                bool duplicate = false;

                for (int index = 0; index < openings.Count; index++)
                {
                    if (Mathf.Abs(openings[index].x - visualCenter) > visualWidth * 0.25f)
                        continue;

                    Vector2 existing = openings[index];
                    existing.y = Mathf.Max(existing.y, visualWidth);
                    openings[index] = existing;
                    duplicate = true;
                    break;
                }

                if (!duplicate)
                    openings.Add(new Vector2(visualCenter, visualWidth));
            }
        }

        openings.Sort((first, second) => first.x.CompareTo(second.x));
        return openings;
    }

    private void BuildEdgeSegments(
        Transform parent,
        string edgeName,
        Sprite[] sprites,
        int variation,
        float length,
        float fixedPosition,
        bool horizontal,
        List<Vector2> openings,
        int sortingOrder
    )
    {
        if (sprites == null || sprites.Length == 0)
            return;

        float halfLength = length * 0.5f;
        float cursor = -halfLength;
        List<Vector2> solidSpans = new List<Vector2>();

        foreach (Vector2 opening in openings)
        {
            float openingStart = Mathf.Clamp(opening.x - opening.y * 0.5f, -halfLength, halfLength);
            float openingEnd = Mathf.Clamp(opening.x + opening.y * 0.5f, -halfLength, halfLength);

            if (openingStart > cursor + 0.05f)
                solidSpans.Add(new Vector2(cursor, openingStart));

            cursor = Mathf.Max(cursor, openingEnd);
        }

        if (cursor < halfLength - 0.05f)
            solidSpans.Add(new Vector2(cursor, halfLength));

        int spriteIndex = variation;

        foreach (Vector2 span in solidSpans)
        {
            float spanLength = span.y - span.x;
            int tileCount = Mathf.Max(
                1,
                Mathf.CeilToInt(spanLength / length * sprites.Length)
            );
            float tileLength = spanLength / tileCount;
            float overlap = Mathf.Max(edgeBleed, tileLength * 0.28f);

            for (int tileIndex = 0; tileIndex < tileCount; tileIndex++)
            {
                float start = span.x + tileLength * tileIndex;
                float end = start + tileLength;

                if (tileIndex > 0)
                    start -= overlap * 0.5f;
                if (tileIndex < tileCount - 1)
                    end += overlap * 0.5f;

                CreateEdgeSegment(
                    parent,
                    edgeName,
                    sprites,
                    spriteIndex,
                    start,
                    end,
                    fixedPosition,
                    horizontal,
                    sortingOrder
                );
                spriteIndex++;
            }
        }
    }

    private void CreateEdgeSegment(
        Transform parent,
        string edgeName,
        Sprite[] sprites,
        int variation,
        float start,
        float end,
        float fixedPosition,
        bool horizontal,
        int sortingOrder
    )
    {
        float segmentLength = end - start;
        float center = (start + end) * 0.5f;
        Vector2 position = horizontal
            ? new Vector2(center, fixedPosition)
            : new Vector2(fixedPosition, center);
        Vector2 size = horizontal
            ? new Vector2(segmentLength, wallThickness)
            : new Vector2(wallThickness, segmentLength);

        CreateVisual(
            parent,
            $"{edgeName} Wall Segment",
            PickSprite(sprites, variation),
            position,
            size,
            sortingOrder
        );
    }

    private static SpriteRenderer CreateVisual(
        Transform parent,
        string name,
        Sprite sprite,
        Vector2 localPosition,
        Vector2 size,
        int sortingOrder
    )
    {
        if (sprite == null)
            return null;

        GameObject visual = new GameObject(name);
        visual.transform.SetParent(parent, false);
        visual.transform.localPosition = new Vector3(localPosition.x, localPosition.y, 0f);
        Vector2 spriteSize = sprite.bounds.size;
        visual.transform.localScale = new Vector3(
            spriteSize.x > 0f ? size.x / spriteSize.x : 1f,
            spriteSize.y > 0f ? size.y / spriteSize.y : 1f,
            1f
        );

        SpriteRenderer renderer = visual.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = sortingOrder;
        return renderer;
    }

    private static Sprite PickSprite(Sprite[] sprites, int index)
    {
        if (sprites == null || sprites.Length == 0)
            return null;

        return sprites[(index % sprites.Length + sprites.Length) % sprites.Length];
    }

    private bool HasGroundArt()
    {
        return groundSprite != null || (groundVariants != null && groundVariants.Length > 0);
    }

    private Sprite PickGroundSprite(System.Random random)
    {
        if (groundVariants != null && groundVariants.Length > 0)
            return groundVariants[random.Next(groundVariants.Length)];

        return groundSprite;
    }

    private float CalculateSharedCameraSize(float cameraAspect)
    {
        float safeAspect = Mathf.Max(0.1f, cameraAspect);
        float cameraSize = 1f;

        foreach (EnclosureZone zone in zones)
        {
            float halfWidth = zone.Size.x * 0.5f + cameraFramePadding;
            float halfHeight = zone.Size.y * 0.5f + cameraFramePadding;
            cameraSize = Mathf.Max(cameraSize, halfHeight, halfWidth / safeAspect);
        }

        return cameraSize;
    }

    private void BuildHallPlaceholder(EnclosureHall hall, int hallnumber)
    {
        if (hallVisualPrefab == null)
            return;

        int width = hall.Width;
        Vector2[] points = hall.Points;
        GameObject hallobj = new GameObject("Hall" + hallnumber);
        hallobj.transform.parent = hallwayRoot;
        for (int i = 1; i < points.Length; i++)
        {
            Vector2 pointA = points[i - 1];
            Vector2 pointB = points[i];

            Vector2 difference = pointB - pointA;

            bool horizontal = Mathf.Abs(difference.x) > 0.001f;

            float length = horizontal
                ? Mathf.Abs(difference.x) + 1f
                : Mathf.Abs(difference.y) + 1f;

            Vector2 center = (pointA + pointB) * 0.5f;

            GameObject visual = Instantiate(hallVisualPrefab, hallobj.transform);

            visual.name = "Hall Segment " + (i - 1);
            visual.transform.position = center;

            visual.transform.localScale = horizontal
                ? new Vector3(length, width, 1f)
                : new Vector3(width, length, 1f);

            if (HasGroundArt())
            {
                foreach (SpriteRenderer renderer in visual.GetComponentsInChildren<SpriteRenderer>())
                    renderer.enabled = false;
            }
        }
    }
    public GameObject GetZoneEnvironment(string zoneId)
    {
        if (string.IsNullOrEmpty(zoneId))
            return null;

        zoneEnvironments.TryGetValue(zoneId, out GameObject environment);
        return environment;
    }

    public bool TryGetZoneFrameSize(string zoneId, out Vector2 frameSize)
    {
        if (string.IsNullOrEmpty(zoneId))
        {
            frameSize = Vector2.zero;
            return false;
        }

        return zoneFrameSizes.TryGetValue(zoneId, out frameSize);
    }

    private void Clear()
    {
        zones.Clear();
        hallways.Clear();
        zoneEnvironments.Clear();
        zoneFrameSizes.Clear();
        sharedCameraOrthographicSize = 3f;
        IsBuilt = false;

        if (zoneRoot != null)
        {
            if (Application.isPlaying)
                Destroy(zoneRoot.gameObject);
            else
                DestroyImmediate(zoneRoot.gameObject);

            zoneRoot = null;
        }

        if (hallwayRoot != null)
        {
            if (Application.isPlaying)
                Destroy(hallwayRoot.gameObject);
            else
                DestroyImmediate(hallwayRoot.gameObject);

            hallwayRoot = null;
        }
    }
}
