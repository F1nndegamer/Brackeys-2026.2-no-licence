using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class DaycareItemManager : MonoBehaviour
{
    public event System.Action<DaycareItem> PooCleaned;

    [SerializeField] private ShiftDirector shiftDirector;
    [SerializeField] private GameObject itemPrefab;

    [Header("Definitions")]
    [SerializeField] private DaycareItemDefinition bananaDefinition;
    [SerializeField] private DaycareItemDefinition manyBananaDefinition;
    [SerializeField] private DaycareItemDefinition pooDefinition;
    [SerializeField] private DaycareItemDefinition berriesDefinition;
    [SerializeField] private DaycareItemDefinition batteryDefinition;

    [Header("Drop")]
    [SerializeField, Min(0.1f)] private float dropHeight = 4f;
    [SerializeField, Min(0.05f)] private float dropDuration = 0.35f;
    [SerializeField, Min(0f)] private float minimumFoodDropDistance = 1.2f;
    [SerializeField, Range(1, 20)] private int foodDropPointAttempts = 8;

    [Header("Fetching")]
    [SerializeField, Min(0.05f)] private float itemClaimRadius = 0.35f;
    [SerializeField, Min(1f)] private float fetchAbandonSeconds = 12f;

    [Header("Clutter")]
    [SerializeField, Min(1)] private int maxUneatenItemsPerZone = 6;

    [Header("Poo Alert")]
    [SerializeField, Min(0)] private int pooThoughtBubbleThreshold = 10;

    private readonly List<DaycareItem> items = new List<DaycareItem>();
    private readonly Dictionary<MonkeyActor, DaycareItem> claims =
        new Dictionary<MonkeyActor, DaycareItem>();
    private readonly HashSet<MonkeyActor> trackedMonkeys = new HashSet<MonkeyActor>();
    private readonly HashSet<MonkeyActor> rewardedAfterTrouble = new HashSet<MonkeyActor>();
    private readonly List<MonkeyActor> releaseBuffer = new List<MonkeyActor>();
    // Only one incident runs at a time, so one prop slot covers every incident type.
    private DaycareItem incidentPropItem;
    private System.Random random;
    private Transform itemRoot;

    private void Awake()
    {
        if (shiftDirector == null)
            shiftDirector = GetComponent<ShiftDirector>();

        SetRunSeed(System.Guid.NewGuid().GetHashCode());
    }

    public void SetRunSeed(int seed)
    {
        random = new System.Random(seed);
    }

    public bool HasActiveClaim(MonkeyActor monkey)
    {
        return monkey != null && claims.ContainsKey(monkey);
    }

    public void ReleaseMonkey(MonkeyActor monkey)
    {
        if (monkey == null)
            return;

        if (claims.TryGetValue(monkey, out DaycareItem claimedItem) && claimedItem != null)
            claimedItem.ReleaseClaim();

        claims.Remove(monkey);
        if (trackedMonkeys.Remove(monkey))
            monkey.PoopingCompleted -= HandlePoopingCompleted;
        monkey.SetRoomPooAlertActive(false);
        rewardedAfterTrouble.Remove(monkey);
    }

    public void RegisterMonkey(MonkeyActor monkey)
    {
        if (monkey == null || !trackedMonkeys.Add(monkey))
            return;

        monkey.PoopingCompleted += HandlePoopingCompleted;
        UpdateRoomPooAlert(monkey);
    }

    public DaycareItem SpawnBananaFor(MonkeyActor target)
    {
        DaycareItem item = SpawnFoodFor(target, bananaDefinition, true);

        if (item != null && HasActiveClaim(target) && shiftDirector != null &&
            shiftDirector.HasRecentNaughtyAction(target))
            rewardedAfterTrouble.Add(target);

        return item;
    }

    public DaycareItem SpawnBerriesFor(MonkeyActor target)
    {
        return SpawnFoodFor(target, berriesDefinition, false);
    }

    // Drops food inside the target's own zone and sends it walking.
    private DaycareItem SpawnFoodFor(
        MonkeyActor target,
        DaycareItemDefinition definition,
        bool ignoreClutterLimit)
    {
        if (definition == null || itemPrefab == null || target == null)
            return null;

        EnclosureZone zone = target.CurrentZone;

        if (zone == null)
            return null;

        Vector3 dropPoint = ChooseFoodDropPoint(target, zone);

        DaycareItem item = Spawn(
            definition,
            zone,
            dropPoint,
            true,
            ignoreClutterLimit);

        if (item == null)
            return null;

        item.TryClaim(target);
        claims[target] = item;

        if (!target.GoToPoint(zone, dropPoint, MonkeyActivity.Fetching, fetchAbandonSeconds))
        {
            // The monkey refused (time-out). Leave the food on the ground unclaimed
            // rather than stranding a claim nobody will ever resolve.
            claims.Remove(target);
            item.ReleaseClaim();
        }

        return item;
    }

    private Vector3 ChooseFoodDropPoint(MonkeyActor target, EnclosureZone zone)
    {
        Vector3 bestPoint = zone.GetWanderPoint(random);
        float bestDistance = Vector2.Distance(target.WorldPosition, bestPoint);
        int attempts = Mathf.Max(1, foodDropPointAttempts);

        for (int attempt = 1; attempt < attempts; attempt++)
        {
            Vector3 candidate = zone.GetWanderPoint(random);
            float distance = Vector2.Distance(target.WorldPosition, candidate);

            if (distance > bestDistance)
            {
                bestPoint = candidate;
                bestDistance = distance;
            }

            if (distance >= minimumFoodDropDistance)
                return candidate;
        }

        return bestPoint;
    }

    // Leaves the object an incident happened to at the scene of the crime.
    public DaycareItem SpawnIncidentProp(DaycareIncidentType type, EnclosureZone zone)
    {
        DaycareItemDefinition definition = GetIncidentPropDefinition(type);

        if (definition == null || itemPrefab == null || zone == null)
            return null;

        DespawnIncidentProp();

        // No drop animation - the prop should read as something that was already
        // sitting there and has just been messed with.
        incidentPropItem = Spawn(definition, zone, zone.Center, false, true);
        return incidentPropItem;
    }

    public void DespawnIncidentProp()
    {
        if (incidentPropItem == null)
            return;

        Despawn(incidentPropItem);
        incidentPropItem = null;
    }

    private DaycareItemDefinition GetIncidentPropDefinition(DaycareIncidentType type)
    {
        switch (type)
        {
            case DaycareIncidentType.BananaStash: return manyBananaDefinition;
            case DaycareIncidentType.Generator: return batteryDefinition;
            default: return null;
        }
    }

    public void RemoveItemsInZone(EnclosureZone zone, params DaycareItemKind[] kinds)
    {
        if (zone == null || kinds == null || kinds.Length == 0)
            return;

        for (int index = items.Count - 1; index >= 0; index--)
        {
            DaycareItem item = items[index];

            if (item == null || item.Zone != zone || item == incidentPropItem)
                continue;

            if (System.Array.IndexOf(kinds, item.Kind) < 0)
                continue;

            ReleaseClaimsOn(item);
            Despawn(item);
        }
    }

    public bool HasItemsInZone(EnclosureZone zone, params DaycareItemKind[] kinds)
    {
        if (zone == null || kinds == null || kinds.Length == 0)
            return false;

        foreach (DaycareItem item in items)
        {
            if (item != null && item.Zone == zone && item != incidentPropItem &&
                System.Array.IndexOf(kinds, item.Kind) >= 0)
                return true;
        }

        return false;
    }

    public void Tick(float deltaTime)
    {
        for (int index = items.Count - 1; index >= 0; index--)
        {
            if (items[index] == null)
            {
                items.RemoveAt(index);
                continue;
            }

            items[index].Tick(deltaTime);
        }

        ResolveClaims();
        UpdateRoomPooAlerts();
    }

    public void Clear()
    {
        items.Clear();
        claims.Clear();
        foreach (MonkeyActor monkey in trackedMonkeys)
        {
            if (monkey != null)
            {
                monkey.PoopingCompleted -= HandlePoopingCompleted;
                monkey.SetRoomPooAlertActive(false);
            }
        }

        trackedMonkeys.Clear();
        rewardedAfterTrouble.Clear();
        incidentPropItem = null;

        if (itemRoot == null)
            return;

        for (int index = itemRoot.childCount - 1; index >= 0; index--)
            Destroy(itemRoot.GetChild(index).gameObject);
    }

    private void ResolveClaims()
    {
        releaseBuffer.Clear();

        foreach (KeyValuePair<MonkeyActor, DaycareItem> claim in claims)
        {
            MonkeyActor monkey = claim.Key;
            DaycareItem item = claim.Value;

            if (monkey == null || item == null)
            {
                releaseBuffer.Add(monkey);
                continue;
            }

            if (item.IsDropping)
                continue;

            if (Vector2.Distance(monkey.WorldPosition, item.Position) <= itemClaimRadius)
            {
                bool punishedForReward = rewardedAfterTrouble.Remove(monkey);
                monkey.Feed(
                    item.Definition.hungerDelta,
                    item.Definition.angerDelta,
                    item.Definition.snackDurationSeconds,
                    item.Definition.naughtinessDelta,
                    10f,
                    punishedForReward
                );

                Despawn(item);
                releaseBuffer.Add(monkey);

                if (shiftDirector != null)
                    shiftDirector.ReportFed(monkey);

                continue;
            }

            // Gave up, got sent to time-out, or was dragged into a fight.
            if (monkey.Activity != MonkeyActivity.Fetching)
                releaseBuffer.Add(monkey);
        }

        foreach (MonkeyActor monkey in releaseBuffer)
        {
            if (monkey != null && claims.TryGetValue(monkey, out DaycareItem abandoned) && abandoned != null)
                abandoned.ReleaseClaim();

            claims.Remove(monkey);
            rewardedAfterTrouble.Remove(monkey);
        }
    }

    private void HandlePoopingCompleted(MonkeyActor monkey)
    {
        if (monkey == null || pooDefinition == null || monkey.CurrentZone == null)
            return;

        Spawn(pooDefinition, monkey.CurrentZone, monkey.transform.position, false, true);
    }

    public DaycareItem SpawnTutorialPoo(EnclosureZone zone)
    {
        if (zone == null)
            return null;

        Vector3 point = zone.Center + new Vector2(0.65f, -0.35f);
        return Spawn(pooDefinition, zone, point, false, true);
    }

    public void CleanItem(DaycareItem item)
    {
        if (!IsCleanable(item))
            return;

        if (item.Kind == DaycareItemKind.Poo)
            PooCleaned?.Invoke(item);

        ReleaseClaimsOn(item);
        Despawn(item);
    }

    public void CleanPoo(DaycareItem poo)
    {
        if (poo != null && poo.Kind == DaycareItemKind.Poo)
            CleanItem(poo);
    }

    public DaycareItem TryGetCleanableAtWorldPoint(Vector2 worldPosition)
    {
        foreach (DaycareItem item in items)
        {
            if (!IsCleanable(item))
                continue;

            Collider2D collider = item.GetComponent<Collider2D>();

            if (collider != null && collider.OverlapPoint(worldPosition))
                return item;
        }

        return null;
    }

    public DaycareItem TryGetPooAtWorldPoint(Vector2 worldPosition)
    {
        foreach (DaycareItem item in items)
        {
            if (item == null || item.Kind != DaycareItemKind.Poo)
                continue;

            Collider2D collider = item.GetComponent<Collider2D>();

            if (collider != null && collider.OverlapPoint(worldPosition))
                return item;
        }

        return null;
    }

    private static bool IsCleanable(DaycareItem item)
    {
        return item != null &&
            (item.Kind == DaycareItemKind.Poo || item.Kind == DaycareItemKind.RotBanana);
    }

    private DaycareItem Spawn(
        DaycareItemDefinition definition,
        EnclosureZone zone,
        Vector3 point,
        bool animateDrop,
        bool ignoreClutterLimit = false
    )
    {
        if (definition == null || itemPrefab == null || zone == null)
            return null;

        if (!ignoreClutterLimit && CountItemsInZone(zone) >= maxUneatenItemsPerZone)
            return null;

        EnsureItemRoot();

        GameObject itemObject = Instantiate(itemPrefab, itemRoot);
        DaycareItem item = itemObject.GetComponent<DaycareItem>();

        if (item == null)
            item = itemObject.AddComponent<DaycareItem>();

        item.Configure(definition, zone, point, animateDrop, dropHeight, dropDuration);
        items.Add(item);
        return item;
    }

    private int CountItemsInZone(EnclosureZone zone)
    {
        int count = 0;

        foreach (DaycareItem item in items)
            if (item != null && item.Zone == zone)
                count++;

        return count;
    }

    private int CountItemsInZone(EnclosureZone zone, DaycareItemKind kind)
    {
        int count = 0;

        foreach (DaycareItem item in items)
        {
            if (item != null && item.Zone == zone && item.Kind == kind &&
                item != incidentPropItem)
                count++;
        }

        return count;
    }

    private void UpdateRoomPooAlerts()
    {
        foreach (MonkeyActor monkey in trackedMonkeys)
            UpdateRoomPooAlert(monkey);
    }

    private void UpdateRoomPooAlert(MonkeyActor monkey)
    {
        if (monkey == null)
            return;

        EnclosureZone zone = monkey.CurrentZone;
        bool hasPooProblem = zone != null &&
            CountItemsInZone(zone, DaycareItemKind.Poo) > pooThoughtBubbleThreshold;
        monkey.SetRoomPooAlertActive(hasPooProblem);
    }

    private void ReleaseClaimsOn(DaycareItem item)
    {
        releaseBuffer.Clear();

        foreach (KeyValuePair<MonkeyActor, DaycareItem> claim in claims)
            if (claim.Value == item)
                releaseBuffer.Add(claim.Key);

        foreach (MonkeyActor monkey in releaseBuffer)
        {
            claims.Remove(monkey);
            rewardedAfterTrouble.Remove(monkey);
        }
    }

    private void Despawn(DaycareItem item)
    {
        if (item == null)
            return;

        items.Remove(item);

        if (item == incidentPropItem)
            incidentPropItem = null;

        Destroy(item.gameObject);
    }

    private void EnsureItemRoot()
    {
        if (itemRoot != null)
            return;

        itemRoot = new GameObject("Daycare Items").transform;
        itemRoot.SetParent(transform, false);
    }
}
