using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum ShiftPhase
{
    Preparing,
    Active,
    IncidentWindow,
    Lineup,
    Zoolag,
    Welcome,
    Failed
}

public class ShiftCameraObservation
{
    public float StartTime { get; }
    public float EndTime { get; private set; }
    public string ZoneId { get; }

    public ShiftCameraObservation(float startTime, string zoneId)
    {
        StartTime = startTime;
        EndTime = startTime;
        ZoneId = zoneId;
    }

    public void EndAt(float endTime)
    {
        EndTime = Mathf.Max(StartTime, endTime);
    }
}

public class MonkeyWitnessStatement
{
    public MonkeyActor Speaker { get; }
    public MonkeyActor AccusedMonkey { get; }
    public bool IsTruthful { get; }
    public string Text { get; }

    public MonkeyWitnessStatement(
        MonkeyActor speaker,
        MonkeyActor accusedMonkey,
        bool isTruthful,
        string text
    )
    {
        Speaker = speaker;
        AccusedMonkey = accusedMonkey;
        IsTruthful = isTruthful;
        Text = text;
    }
}

public class DaycareIncident
{
    public DaycareIncidentDefinition Definition { get; }
    public float Time { get; }
    public EnclosureZone Zone { get; }
    public MonkeyActor Culprit { get; }
    public bool WasCaughtOnCamera { get; }
    public bool HasFallbackEvidence { get; }
    public IReadOnlyList<MonkeyWitnessStatement> WitnessStatements { get; }
    public double DeductionLikelihoodRatio { get; }

    public DaycareIncident(
        DaycareIncidentDefinition definition,
        float time,
        EnclosureZone zone,
        MonkeyActor culprit,
        bool wasCaughtOnCamera,
        bool hasFallbackEvidence,
        List<MonkeyWitnessStatement> witnessStatements,
        double deductionLikelihoodRatio
    )
    {
        Definition = definition;
        Time = time;
        Zone = zone;
        Culprit = culprit;
        WasCaughtOnCamera = wasCaughtOnCamera;
        HasFallbackEvidence = hasFallbackEvidence;
        WitnessStatements = witnessStatements;
        DeductionLikelihoodRatio = deductionLikelihoodRatio;
    }

}

[DisallowMultipleComponent]
public class ShiftDirector : MonoBehaviour
{
    private const float CulpritArrivalGraceSeconds = 2f;
    private const float FightDurationSeconds = 6f;

    [Header("References")]
    [SerializeField] private EnclosureLayout enclosureLayout;
    [SerializeField] private EnclosureShiftDefinition shiftDefinition;
    [SerializeField] private Transform monkeyRoot;
    [SerializeField] private DaycareItemManager itemManager;
    [SerializeField] private bool startAutomatically = true;
    [SerializeField, Min(0.4f)] private float fightSpacing = 1.15f;

    private readonly List<MonkeyActor> monkeys = new List<MonkeyActor>();
    private readonly List<MonkeyActor> latestNewcomers = new List<MonkeyActor>();
    private readonly List<MonkeyActor> lineupCandidates = new List<MonkeyActor>();
    private readonly HashSet<MonkeyActor> accusedMonkeys = new HashSet<MonkeyActor>();
    private MonkeyActor lastAccusedMonkey;
    private readonly List<ShiftCameraObservation> cameraObservations = new List<ShiftCameraObservation>();
    private readonly Dictionary<MonkeyActor, MonkeyActor> fightPartners = new Dictionary<MonkeyActor, MonkeyActor>();
    private readonly List<MonkeyActor> fightCleanup = new List<MonkeyActor>();
    private readonly List<Color> newcomerColorPool = new List<Color>();
    private readonly List<MonkeyProfile> runMonkeyProfiles = new List<MonkeyProfile>();
    private readonly List<string> runNewcomerNames = new List<string>();
    private readonly List<DaycareIncidentDefinition> incidentBag = new List<DaycareIncidentDefinition>();
    private System.Random incidentRandom;
    private System.Random newcomerRandom;
    private ShiftCameraObservation currentObservation;
    private DaycareIncident currentIncident;
    private DaycareIncidentDefinition previousIncidentDefinition;
    private float shiftTime;
    private float messTimer;
    private float nextIncidentTime;
    private int remainingGuesses;
    private int resolvedIncidentCount;
    private int zoolagSentCount;
    private int totalMonkeysSpawned;
    private int archivedFightCount;
    private int archivedTimeoutCount;
    private int archivedMessCount;
    private int runSeed;
    private float incidentWindowRemaining;
    private float nextFeedAvailableTime;
    private bool tutorialMode;
    private float currentIncidentWindowDuration;
    private float welcomeRemaining;

    public ShiftPhase Phase { get; private set; } = ShiftPhase.Preparing;
    public float ShiftTime => shiftTime;
    public int RemainingGuesses => remainingGuesses;
    public int ResolvedIncidentCount => resolvedIncidentCount;
    public int ZoolagSentCount => zoolagSentCount;
    public int CurrentRunSeed => runSeed;
    public int MonkeyCount => monkeys.Count;
    public int TotalFightCount => archivedFightCount + SumMonkeyStat(monkey => monkey.FightCount);
    public int TotalTimeoutCount => archivedTimeoutCount + SumMonkeyStat(monkey => monkey.TimeoutCount);
    public int TotalMessCount => archivedMessCount + SumMonkeyStat(monkey => monkey.MessCount);
    public int ActiveTimeOutCount => CountMonkeysInTimeOut();
    public int TimeOutCapacity
    {
        get
        {
            if (shiftDefinition == null)
                return 0;

            int safeCapacity = Mathf.Max(0, monkeys.Count - shiftDefinition.lineupSize);
            int configuredStartingCapacity = Mathf.Min(
                shiftDefinition.maxMonkeysInTimeOut,
                Mathf.Max(0, shiftDefinition.startingMonkeyCount - shiftDefinition.lineupSize)
            );
            int newcomers = Mathf.Max(0, monkeys.Count - shiftDefinition.startingMonkeyCount);
            return Mathf.Min(safeCapacity, configuredStartingCapacity + newcomers);
        }
    }
    public float FeedCooldownRemaining => Mathf.Max(0f, nextFeedAvailableTime - shiftTime);
    public float NextIncidentTime => nextIncidentTime;
    public float IncidentWindowRemaining => incidentWindowRemaining;
    public float WelcomeRemaining => welcomeRemaining;
    public float IncidentWindowDuration =>
        currentIncidentWindowDuration > 0f
            ? currentIncidentWindowDuration
            : shiftDefinition != null ? shiftDefinition.incidentWindowSeconds : 0f;
    public bool IsTutorialMode => tutorialMode;
    public string StatusMessage { get; private set; } = "Preparing the enclosure.";
    public DaycareIncident CurrentIncident => currentIncident;
    public EnclosureShiftDefinition Definition => shiftDefinition;
    public IReadOnlyList<MonkeyActor> Monkeys => monkeys;
    public IReadOnlyList<MonkeyActor> LatestNewcomers => latestNewcomers;
    public IReadOnlyList<MonkeyActor> LineupCandidates => lineupCandidates;
    public MonkeyActor LastAccusedMonkey => lastAccusedMonkey;
    public IReadOnlyList<ShiftCameraObservation> CameraObservations => cameraObservations;

    public bool CanCleanZone(EnclosureZone zone)
    {
        return Phase == ShiftPhase.Active && itemManager != null &&
            itemManager.HasItemsInZone(zone, DaycareItemKind.Poo, DaycareItemKind.RotBanana);
    }

    public bool CanTimeOutMonkey(MonkeyActor target)
    {
        return Phase == ShiftPhase.Active && shiftDefinition != null && target != null && monkeys.Contains(target) &&
            !target.IsInTimeOut && CountMonkeysInTimeOut() < TimeOutCapacity;
    }

    public bool CanFeedMonkey(MonkeyActor target)
    {
        return Phase == ShiftPhase.Active && itemManager != null &&
            target != null && monkeys.Contains(target) &&
            !target.IsInTimeOut && FeedCooldownRemaining <= 0f &&
            !itemManager.HasActiveClaim(target);
    }

    public bool HasRecentNaughtyAction(MonkeyActor target)
    {
        return target != null && shiftDefinition != null &&
            target.HasRecentNaughtyAction(shiftDefinition.recentTroubleWindowSeconds);
    }

    public bool StartTutorialFight(
        EnclosureZone zone,
        MonkeyActor preferredFighter,
        out MonkeyActor firstFighter,
        out MonkeyActor secondFighter
    )
    {
        firstFighter = null;
        secondFighter = null;

        if (!tutorialMode || Phase != ShiftPhase.Active || zone == null)
            return false;

        if (IsAvailableTutorialFighter(preferredFighter, zone))
            firstFighter = preferredFighter;

        foreach (MonkeyActor monkey in monkeys)
        {
            if (!IsAvailableTutorialFighter(monkey, zone) || monkey == firstFighter)
                continue;

            if (firstFighter == null)
                firstFighter = monkey;
            else
            {
                secondFighter = monkey;
                break;
            }
        }

        if (firstFighter == null || secondFighter == null)
        {
            foreach (MonkeyActor monkey in monkeys)
            {
                if (monkey == firstFighter || monkey == secondFighter || monkey.IsInTimeOut ||
                    monkey.Activity == MonkeyActivity.HidingLoot)
                    continue;

                if (firstFighter == null)
                    firstFighter = monkey;
                else
                {
                    secondFighter = monkey;
                    break;
                }
            }
        }

        if (firstFighter == null || secondFighter == null)
            return false;

        DetachTutorialFighter(firstFighter);
        DetachTutorialFighter(secondFighter);

        if (itemManager != null)
        {
            itemManager.ReleaseMonkey(firstFighter);
            itemManager.ReleaseMonkey(secondFighter);
        }

        Vector2 center = zone.Center;
        float halfSpacing = Mathf.Min(
            fightSpacing * 0.5f,
            Mathf.Max(0.2f, zone.Size.x * 0.5f - 0.75f)
        );
        float verticalInset = Mathf.Max(0f, zone.Size.y * 0.5f - 0.9f);
        center.y = Mathf.Clamp(
            (firstFighter.WorldPosition.y + secondFighter.WorldPosition.y) * 0.5f,
            zone.Center.y - verticalInset,
            zone.Center.y + verticalInset
        );

        firstFighter.PlaceForScriptedEvent(zone, center + Vector2.left * halfSpacing);
        secondFighter.PlaceForScriptedEvent(zone, center + Vector2.right * halfSpacing);
        firstFighter.SetFightPose(firstFighter.WorldPosition, 1f);
        secondFighter.SetFightPose(secondFighter.WorldPosition, -1f);
        firstFighter.StartFighting(45f);
        secondFighter.StartFighting(45f);
        fightPartners[firstFighter] = secondFighter;
        fightPartners[secondFighter] = firstFighter;
        return true;
    }

    private void DetachTutorialFighter(MonkeyActor fighter)
    {
        if (fighter == null || !fightPartners.TryGetValue(fighter, out MonkeyActor oldPartner))
            return;

        fightPartners.Remove(fighter);

        if (oldPartner == null)
            return;

        fightPartners.Remove(oldPartner);

        if (IsFightActivity(oldPartner.Activity))
            oldPartner.StopFighting();
    }

    private static bool IsAvailableTutorialFighter(MonkeyActor monkey, EnclosureZone zone)
    {
        return monkey != null && monkey.CurrentZone == zone && !monkey.IsInTimeOut &&
            monkey.Activity != MonkeyActivity.HidingLoot;
    }

    public event Action<DaycareIncident> IncidentOccurred;
    public event Action CareActionCompleted;
    public event Action<bool> AccusationResolved;

    private IEnumerator Start()
    {
        yield return null;

        if (startAutomatically)
            BeginShift();
    }

    private void Update()
    {
        if (Phase == ShiftPhase.Active)
            TickActive();
        else if (Phase == ShiftPhase.IncidentWindow)
            TickIncidentWindow();
        else if (Phase == ShiftPhase.Welcome)
            TickWelcome();
    }

    private void TickActive()
    {
        shiftTime += Time.deltaTime;
        messTimer += Time.deltaTime;
        TickMonkeys(Time.deltaTime);

        UpdateFights();

        if (itemManager != null)
            itemManager.Tick(Time.deltaTime);

        if (messTimer >= 5f)
        {
            messTimer = 0f;

            foreach (EnclosureZone zone in enclosureLayout.Zones)
                zone.AddMess(2f);
        }

        if (!tutorialMode && currentIncident == null && shiftTime >= nextIncidentTime)
            TriggerIncident();
    }
    private void TickIncidentWindow()
    {
        shiftTime += Time.deltaTime;

        TickMonkeys(Time.deltaTime);

        UpdateFights();

        // Items keep ticking here on purpose. MoveAlongPath is not phase-gated, so a
        // monkey already walking to a banana arrives during the window - without this
        // it would stand on uneaten food until the lineup starts.
        if (itemManager != null)
            itemManager.Tick(Time.deltaTime);

        // Real-time during the alarm punch, but frozen while the game is paused.
        if (!tutorialMode)
        {
            float incidentDeltaTime = Time.timeScale <= 0f ? 0f : Time.unscaledDeltaTime;
            incidentWindowRemaining = Mathf.Max(
                0f,
                incidentWindowRemaining - incidentDeltaTime
            );
        }

        if (incidentWindowRemaining <= 0f)
            EnterLineup();
    }

    public void SkipIncidentWindow()
    {
        if (Phase == ShiftPhase.IncidentWindow &&
            (!tutorialMode || currentIncident != null && currentIncident.WasCaughtOnCamera))
            EnterLineup();
    }

    private void EnterLineup()
    {
        incidentWindowRemaining = 0f;

        if (itemManager != null)
            itemManager.DespawnIncidentProp();

        Phase = ShiftPhase.Lineup;
        EndCameraObservation();
        StatusMessage = $"{currentIncident.Definition.alertTitle}. Pick the culprit.";
    }

    public bool BeginShift()
    {
        return BeginShift(false);
    }

    public bool BeginTutorialShift()
    {
        return BeginShift(true);
    }

    private bool BeginShift(bool startInTutorialMode)
    {
        ClearShift();

        if (enclosureLayout == null)
            enclosureLayout = GetComponent<EnclosureLayout>();

        if (itemManager == null)
            itemManager = GetComponent<DaycareItemManager>();

        string validationError = string.Empty;

        if (shiftDefinition == null || enclosureLayout == null ||
            !shiftDefinition.TryValidate(out validationError))
        {
            StatusMessage = string.IsNullOrWhiteSpace(validationError)
                ? "Shift data is missing."
                : validationError;
            Debug.LogError(StatusMessage, this);
            return false;
        }

        runSeed = shiftDefinition.randomizeSeedEachRun
            ? Guid.NewGuid().GetHashCode()
            : shiftDefinition.fixedSeed;
        tutorialMode = startInTutorialMode;
        incidentRandom = new System.Random(DeriveSeed(runSeed, 0));
        newcomerRandom = new System.Random(DeriveSeed(runSeed, 11));
        PrepareRunRoster(new System.Random(DeriveSeed(runSeed, 12)));
        RefillNewcomerColorPool();

        if (itemManager != null)
            itemManager.SetRunSeed(DeriveSeed(runSeed, 1));

        enclosureLayout.Build(shiftDefinition, runSeed);
        EnsureMonkeyRoot();
        totalMonkeysSpawned = 0;

        for (int index = 0; index < shiftDefinition.startingMonkeyCount; index++)
            SpawnMonkey(runMonkeyProfiles[index]);

        remainingGuesses = shiftDefinition.guessesPerIncident;
        shiftTime = 0f;
        messTimer = 0f;
        nextIncidentTime = shiftDefinition.firstIncidentTimeSeconds;
        nextFeedAvailableTime = 0f;
        resolvedIncidentCount = 0;
        Phase = ShiftPhase.Active;
        StatusMessage = "Daycare opened. Keep the monkeys under control.";
        return true;
    }

    private void TickMonkeys(float deltaTime)
    {
        int extraMonkeys = Mathf.Max(0, monkeys.Count - shiftDefinition.startingMonkeyCount);
        float populationPressure = 1f +
            extraMonkeys * shiftDefinition.populationPressurePerExtraMonkey;

        foreach (MonkeyActor monkey in monkeys)
        {
            monkey.TickState(
                deltaTime,
                shiftDefinition.baseNaughtinessPerSecond,
                populationPressure
            );
        }
    }

    public void BeginCameraObservation(string zoneId)
    {
        EndCameraObservation();

        if ((Phase != ShiftPhase.Active && Phase != ShiftPhase.IncidentWindow &&
            Phase != ShiftPhase.Welcome) ||
            enclosureLayout.FindZone(zoneId) == null)
            return;

        currentObservation = new ShiftCameraObservation(shiftTime, zoneId);
        cameraObservations.Add(currentObservation);
    }

    public void EndCameraObservation()
    {
        if (currentObservation == null)
            return;

        currentObservation.EndAt(shiftTime);
        currentObservation = null;
    }

    // A MonkeyActor cannot see its neighbours, so pairing happens here. Polling beats
    // subscribing to ActivityChanged: StartFighting would re-raise that event and
    // recurse straight back into this pairing pass.
    private void UpdateFights()
    {
        foreach (MonkeyActor monkey in monkeys)
        {
            if (monkey.Activity != MonkeyActivity.Fighting || fightPartners.ContainsKey(monkey))
                continue;

            MonkeyActor partner = FindFightPartner(monkey);

            if (partner == null)
            {
                monkey.StopFighting();
                continue;
            }

            if (!BeginFightApproach(monkey, partner))
            {
                monkey.StopFighting();
                continue;
            }

            fightPartners[monkey] = partner;
            fightPartners[partner] = monkey;
            ScatterGentleBystanders(monkey.CurrentZone, monkey, partner);
        }

        fightCleanup.Clear();

        foreach (KeyValuePair<MonkeyActor, MonkeyActor> pair in fightPartners)
        {
            MonkeyActor first = pair.Key;
            MonkeyActor second = pair.Value;

            if (first == null || second == null)
            {
                fightCleanup.Add(first);
                continue;
            }

            if (first.GetInstanceID() > second.GetInstanceID())
                continue;

            bool firstParticipating = IsFightActivity(first.Activity);
            bool secondParticipating = IsFightActivity(second.Activity);

            if (!firstParticipating || !secondParticipating)
            {
                fightCleanup.Add(first);
                fightCleanup.Add(second);
                continue;
            }

            if (first.Activity == MonkeyActivity.ApproachingFight &&
                second.Activity == MonkeyActivity.ApproachingFight &&
                first.HasReachedDestination && second.HasReachedDestination)
            {
                ArrangeFightPair(first, second);
                first.StartFighting(FightDurationSeconds);
                second.StartFighting(FightDurationSeconds);
            }

            if (first.Activity != second.Activity &&
                (first.Activity == MonkeyActivity.Fighting || second.Activity == MonkeyActivity.Fighting))
            {
                fightCleanup.Add(pair.Key);
                fightCleanup.Add(pair.Value);
            }
        }

        foreach (MonkeyActor finished in fightCleanup)
        {
            if (finished == null)
                continue;

            if (fightPartners.TryGetValue(finished, out MonkeyActor partner) && partner != null)
            {
                if (IsFightActivity(partner.Activity))
                    partner.StopFighting();

                fightPartners.Remove(partner);
            }

            fightPartners.Remove(finished);
        }
    }

    private MonkeyActor FindFightPartner(MonkeyActor instigator)
    {
        return FindMonkeyInZone(
            instigator.CurrentZone.Id,
            monkey => monkey != instigator && !monkey.IsInTimeOut &&
                monkey.Activity != MonkeyActivity.HidingLoot &&
                !IsFightActivity(monkey.Activity),
            (best, candidate) => candidate.Anger > best.Anger
        );
    }

    private bool BeginFightApproach(MonkeyActor first, MonkeyActor second)
    {
        if (!TryGetFightPositions(first, second, out MonkeyActor left, out MonkeyActor right,
            out Vector2 leftPosition, out Vector2 rightPosition))
            return false;

        return left.BeginFightApproach(leftPosition) && right.BeginFightApproach(rightPosition);
    }

    private static bool IsFightActivity(MonkeyActivity activity)
    {
        return activity == MonkeyActivity.ApproachingFight || activity == MonkeyActivity.Fighting;
    }

    private void ScatterGentleBystanders(
        EnclosureZone zone,
        MonkeyActor firstFighter,
        MonkeyActor secondFighter
    )
    {
        if (zone == null || enclosureLayout == null || enclosureLayout.Zones.Count < 2)
            return;

        foreach (MonkeyActor monkey in monkeys)
        {
            if (monkey == firstFighter || monkey == secondFighter || monkey.CurrentZone != zone ||
                monkey.IsInTimeOut || monkey.Activity == MonkeyActivity.HidingLoot)
                continue;

            float fleeChance = monkey.Disposition switch
            {
                MonkeyDisposition.Bully => 0f,
                MonkeyDisposition.Busybody => 0.2f,
                MonkeyDisposition.Scavenger => 0.45f,
                MonkeyDisposition.Clinger => 0.8f,
                MonkeyDisposition.Dreamer => 0.72f,
                _ => 0.85f
            };

            if (incidentRandom.NextDouble() > fleeChance)
                continue;

            List<EnclosureZone> destinations = new List<EnclosureZone>();

            foreach (EnclosureZone candidate in enclosureLayout.Zones)
            {
                if (candidate != zone)
                    destinations.Add(candidate);
            }

            if (destinations.Count > 0)
                monkey.FleeTo(destinations[incidentRandom.Next(destinations.Count)]);
        }
    }

    private void TickWelcome()
    {
        float deltaTime = Time.timeScale <= 0f ? 0f : Time.unscaledDeltaTime;
        shiftTime += deltaTime;
        TickMonkeys(deltaTime);
        welcomeRemaining = Mathf.Max(0f, welcomeRemaining - deltaTime);

        if (welcomeRemaining > 0f)
            return;

        DisperseAfterWelcome();
        nextIncidentTime = shiftTime + shiftDefinition.incidentIntervalSeconds;
        Phase = ShiftPhase.Active;
        StatusMessage = "The monkeys have dispersed. The next round has begun.";
    }

    private void ArrangeFightPair(MonkeyActor first, MonkeyActor second)
    {
        if (!TryGetFightPositions(first, second, out MonkeyActor left, out MonkeyActor right,
            out Vector2 leftPosition, out Vector2 rightPosition))
            return;

        left.SetFightPose(leftPosition, 1f);
        right.SetFightPose(rightPosition, -1f);
    }

    private bool TryGetFightPositions(
        MonkeyActor first,
        MonkeyActor second,
        out MonkeyActor left,
        out MonkeyActor right,
        out Vector2 leftPosition,
        out Vector2 rightPosition
    )
    {
        left = null;
        right = null;
        leftPosition = default;
        rightPosition = default;

        EnclosureZone zone = first != null ? first.CurrentZone : null;

        if (zone == null || second == null || second.CurrentZone != zone)
            return false;

        Vector2 midpoint = (first.WorldPosition + second.WorldPosition) * 0.5f;
        float usableHalfWidth = Mathf.Max(0.2f, zone.Size.x * 0.5f - 0.75f);
        float usableHalfHeight = Mathf.Max(0f, zone.Size.y * 0.5f - 0.9f);
        float halfSpacing = Mathf.Min(fightSpacing * 0.5f, usableHalfWidth);
        float minimumCenterX = zone.Center.x - usableHalfWidth + halfSpacing;
        float maximumCenterX = zone.Center.x + usableHalfWidth - halfSpacing;

        midpoint.x = Mathf.Clamp(midpoint.x, minimumCenterX, maximumCenterX);
        midpoint.y = Mathf.Clamp(
            midpoint.y,
            zone.Center.y - usableHalfHeight,
            zone.Center.y + usableHalfHeight);

        left = first.WorldPosition.x <= second.WorldPosition.x ? first : second;
        right = left == first ? second : first;

        leftPosition = midpoint + Vector2.left * halfSpacing;
        rightPosition = midpoint + Vector2.right * halfSpacing;
        return true;
    }

    public string FeedHungriestMonkey(string zoneId)
    {
        MonkeyActor target = FindMonkeyInZone(
            zoneId,
            monkey => !monkey.IsInTimeOut && !monkey.IsFull,
            (best, candidate) => candidate.Hunger > best.Hunger
        );

        if (target == null)
            return "All monkeys here are full.";

        return FeedMonkey(target);
    }

    public string FeedMonkey(MonkeyActor target)
    {
        if (Phase != ShiftPhase.Active || target == null || !monkeys.Contains(target))
            return "That monkey cannot be fed right now.";

        if (FeedCooldownRemaining > 0f)
            return $"Bananas ready in {Mathf.CeilToInt(FeedCooldownRemaining)} seconds.";

        if (target.IsInTimeOut)
            return $"{target.DisplayName} is currently in time-out.";

        if (target.IsFull)
        {
            target.RefuseFood(1f);
            StatusMessage = $"{target.DisplayName} is full and refuses the banana.";
            CareActionCompleted?.Invoke();
            return StatusMessage;
        }

        if (itemManager == null)
        {
            StatusMessage = "Banana dispenser is unavailable.";
            return StatusMessage;
        }

        if (itemManager.HasActiveClaim(target))
            return $"{target.DisplayName} is already heading for a banana.";

        // Feeding is physical now: the banana lands on the ground and the monkey walks
        // to it. Hunger only drops on arrival, in DaycareItemManager.ResolveClaims.
        if (itemManager.SpawnBananaFor(target) != null)
        {
            nextFeedAvailableTime = shiftTime + shiftDefinition.feedCooldownSeconds;
            StatusMessage = $"Dropped a banana for {target.DisplayName}.";
            CareActionCompleted?.Invoke();
            return StatusMessage;
        }

        StatusMessage = "The banana dispenser could not find a safe landing spot.";
        return StatusMessage;
    }

    // Called by DaycareItemManager the moment a monkey actually reaches its food.
    public void ReportFed(MonkeyActor target)
    {
        if (target == null)
            return;

        StatusMessage = $"Fed {target.DisplayName}. Hunger is now {target.Hunger:0}.";
    }

    public string CleanZone(string zoneId)
    {
        EnclosureZone zone = enclosureLayout.FindZone(zoneId);

        if (zone == null)
            return "That enclosure zone is unavailable.";

        if (!CanCleanZone(zone))
            return "There is nothing here to clean.";

        zone.Clean(65f);

        if (itemManager != null)
            itemManager.RemoveItemsInZone(zone, DaycareItemKind.Poo, DaycareItemKind.RotBanana);

        foreach (MonkeyActor monkey in monkeys)
        {
            if (monkey.CurrentZone == zone)
                monkey.CalmFromCleaning();
        }

        StatusMessage = $"Cleaned {zone.DisplayName}. The monkeys settle down a little.";
        CareActionCompleted?.Invoke();
        return StatusMessage;
    }

    public string TimeOutAngriestMonkey(string zoneId)
    {
        MonkeyActor target = FindMonkeyInZone(zoneId, monkey => !monkey.IsInTimeOut, (best, candidate) => candidate.Anger > best.Anger);

        if (target == null)
            return "No monkey here needs time-out.";

        return TimeOutMonkey(target);
    }

    public string TimeOutMonkey(MonkeyActor target)
    {
        if (Phase != ShiftPhase.Active || target == null || !monkeys.Contains(target))
            return "That monkey cannot be sent to time-out right now.";

        if (target.IsInTimeOut)
            return $"{target.DisplayName} is already in time-out.";

        if (CountMonkeysInTimeOut() >= TimeOutCapacity)
            return $"Time-out is full. Only {TimeOutCapacity} monkeys fit right now.";

        bool justified = target.NeedsAngerIntervention || target.HasRecentNaughtyAction(
            shiftDefinition.recentTroubleWindowSeconds
        );
        target.SendToTimeOut(
            shiftDefinition.timeoutDurationSeconds,
            justified,
            shiftDefinition.justifiedTimeoutNaughtiness,
            shiftDefinition.justifiedTimeoutTrustPenalty,
            shiftDefinition.unfairTimeoutTrustPenalty
        );
        StatusMessage = justified
            ? $"{target.DisplayName} was caught in time and is calming down."
            : $"{target.DisplayName} did nothing wrong and is now naughtier.";
        CareActionCompleted?.Invoke();
        return StatusMessage;
    }

    private int CountMonkeysInTimeOut()
    {
        int count = 0;

        foreach (MonkeyActor monkey in monkeys)
        {
            if (monkey != null && monkey.IsInTimeOut)
                count++;
        }

        return count;
    }

    public bool TryAccuse(int lineupNumber, out string result)
    {
        result = string.Empty;

        if (Phase != ShiftPhase.Lineup || currentIncident == null ||
            lineupNumber < 1 || lineupNumber > lineupCandidates.Count)
            return false;

        MonkeyActor selectedMonkey = lineupCandidates[lineupNumber - 1];

        if (accusedMonkeys.Contains(selectedMonkey))
        {
            result = $"You already accused {selectedMonkey.DisplayName}. Choose another monkey.";
            StatusMessage = result;
            return false;
        }

        accusedMonkeys.Add(selectedMonkey);
        lastAccusedMonkey = selectedMonkey;
        zoolagSentCount++;

        if (selectedMonkey == currentIncident.Culprit)
        {
            string culpritName = selectedMonkey.DisplayName;
            resolvedIncidentCount++;
            RemoveMonkeyFromDaycare(selectedMonkey);
            ApplySolvedIncidentPressure();
            latestNewcomers.Clear();

            for (int index = 0; index < shiftDefinition.monkeysAddedPerSolvedIncident; index++)
            {
                MonkeyActor newcomer = AddNewMonkey();

                if (newcomer != null)
                    latestNewcomers.Add(newcomer);
            }

            currentIncident = null;
            ClearLineup();
            remainingGuesses = shiftDefinition.guessesPerIncident;
            Phase = ShiftPhase.Zoolag;
            result = latestNewcomers.Count > 0
                ? $"Correct. {culpritName} was sent to the Zoolag. {latestNewcomers.Count} new monkeys joined. Daycare population: {monkeys.Count}."
                : $"Correct. {culpritName} was sent to the Zoolag.";

            StatusMessage = result;
            AccusationResolved?.Invoke(true);
            return true;
        }

        if (tutorialMode)
        {
            accusedMonkeys.Remove(selectedMonkey);
            lastAccusedMonkey = null;
            result = "Not quite. Follow the direct evidence and try again.";
            StatusMessage = result;
            AccusationResolved?.Invoke(false);
            return false;
        }

        remainingGuesses--;
        if (remainingGuesses <= 0)
        {
            Phase = ShiftPhase.Failed;
            result = $"Wrong. It was {currentIncident.Culprit.DisplayName}. The daycare is in chaos.";
        }
        else
        {
            result = $"Wrong. {remainingGuesses} guesses remain.";
        }

        StatusMessage = result;
        AccusationResolved?.Invoke(false);
        return false;
    }

    public bool TriggerTutorialIncident()
    {
        if (!tutorialMode || Phase != ShiftPhase.Active || currentIncident != null ||
            shiftDefinition == null)
            return false;

        DaycareIncidentDefinition tutorialIncident = shiftDefinition.incidentPool.Find(
            incident => incident != null && !incident.blocksCameraEvidence
        );
        EnclosureZone tutorialZone = currentObservation != null && enclosureLayout != null
            ? enclosureLayout.FindZone(currentObservation.ZoneId)
            : null;
        MonkeyActor tutorialCulprit = monkeys.Find(
            monkey => monkey != null && !monkey.IsInTimeOut && tutorialZone != null &&
                monkey.CurrentZone == tutorialZone
        );

        if (tutorialCulprit == null)
        {
            tutorialCulprit = monkeys.Find(monkey => monkey != null && !monkey.IsInTimeOut);

            if (tutorialCulprit != null && tutorialZone != null)
                tutorialCulprit.PlaceForScriptedEvent(tutorialZone, tutorialZone.Center);
        }

        if (itemManager != null)
            itemManager.ReleaseMonkey(tutorialCulprit);

        TriggerIncident(tutorialIncident, tutorialCulprit);
        return Phase == ShiftPhase.IncidentWindow;
    }

    private void TriggerIncident(
        DaycareIncidentDefinition forcedDefinition = null,
        MonkeyActor forcedCulprit = null
    )
    {
        DaycareIncidentDefinition incidentDefinition = forcedDefinition ?? SelectIncidentDefinition();
        MonkeyActor culprit = forcedCulprit ?? SelectCulprit();

        if (incidentDefinition == null || culprit == null)
        {
            nextIncidentTime = shiftTime + 1f;
            return;
        }

        EnclosureZone incidentZone = culprit.CurrentZone;
        bool caughtOnCamera = !incidentDefinition.blocksCameraEvidence &&
            WasZoneObservedAt(incidentZone.Id, shiftTime);
        culprit.RecordIncident(incidentDefinition.objectName);
        culprit.HideLoot();
        List<MonkeyWitnessStatement> witnessStatements = null;
        double deductionLikelihoodRatio = 0d;
        bool fairWitnessPattern = false;
        int lineupAttempts = Mathf.Max(8, monkeys.Count * 2);

        for (int attempt = 0; attempt < lineupAttempts; attempt++)
        {
            PrepareLineup(culprit);
            witnessStatements = BuildWitnessStatements(
                culprit,
                out deductionLikelihoodRatio,
                out fairWitnessPattern
            );

            if (fairWitnessPattern)
                break;
        }

        currentIncident = new DaycareIncident(
            incidentDefinition,
            shiftTime,
            incidentZone,
            culprit,
            caughtOnCamera,
            !fairWitnessPattern,
            witnessStatements,
            deductionLikelihoodRatio
        );

        switch (incidentDefinition.type)
        {
            case DaycareIncidentType.BananaStash:
                foreach (MonkeyActor monkey in monkeys)
                {
                    if (monkey != culprit)
                        monkey.BecomeRestless(14f, 8f);
                }

                break;
            case DaycareIncidentType.GiantPoo:
                incidentZone.AddMess(60f);
                break;
        }

        Phase = ShiftPhase.IncidentWindow;
        currentIncidentWindowDuration = tutorialMode
            ? Mathf.Max(20f, shiftDefinition.incidentWindowSeconds)
            : shiftDefinition.incidentWindowSeconds;
        incidentWindowRemaining = currentIncidentWindowDuration;
        StatusMessage = caughtOnCamera
            ? $"{incidentDefinition.alertTitle}. You caught it on camera."
            : $"{incidentDefinition.alertTitle}. You missed the crime! - Do you trust the monkeys?";

        foreach (MonkeyActor monkey in monkeys)
        {
            if (monkey != null && monkey != culprit)
                monkey.Panic(incidentWindowRemaining);
        }

        IncidentOccurred?.Invoke(currentIncident);

        // Full-room incident artwork already contains the damaged or stolen object.
        if (itemManager != null && incidentDefinition.IncidentSprite == null)
            itemManager.SpawnIncidentProp(incidentDefinition.type, incidentZone);
    }

    private void PrepareLineup(MonkeyActor culprit)
    {
        lineupCandidates.Clear();
        accusedMonkeys.Clear();
        lastAccusedMonkey = null;
        lineupCandidates.Add(culprit);

        List<MonkeyActor> available = monkeys.FindAll(
            monkey => monkey != null && monkey != culprit && !monkey.IsInTimeOut
        );

        for (int index = available.Count - 1; index > 0; index--)
        {
            int swapIndex = incidentRandom.Next(index + 1);
            MonkeyActor swap = available[index];
            available[index] = available[swapIndex];
            available[swapIndex] = swap;
        }

        int targetCount = Mathf.Min(shiftDefinition.lineupSize, monkeys.Count);

        for (int index = 0; index < available.Count && lineupCandidates.Count < targetCount; index++)
            lineupCandidates.Add(available[index]);

        for (int index = lineupCandidates.Count - 1; index > 0; index--)
        {
            int swapIndex = incidentRandom.Next(index + 1);
            MonkeyActor swap = lineupCandidates[index];
            lineupCandidates[index] = lineupCandidates[swapIndex];
            lineupCandidates[swapIndex] = swap;
        }
    }

    public bool WasLineupMonkeyAccused(MonkeyActor monkey)
    {
        return monkey != null && accusedMonkeys.Contains(monkey);
    }

    private void ClearLineup()
    {
        lineupCandidates.Clear();
        accusedMonkeys.Clear();
        lastAccusedMonkey = null;
    }

    private DaycareIncidentDefinition SelectIncidentDefinition()
    {
        if (incidentBag.Count == 0)
            RefillIncidentBag();

        if (incidentBag.Count == 0)
            return null;

        int lastIndex = incidentBag.Count - 1;
        DaycareIncidentDefinition selected = incidentBag[lastIndex];
        incidentBag.RemoveAt(lastIndex);
        previousIncidentDefinition = selected;
        return selected;
    }

    private void RefillIncidentBag()
    {
        incidentBag.Clear();

        foreach (DaycareIncidentDefinition definition in shiftDefinition.incidentPool)
        {
            if (definition != null)
                incidentBag.Add(definition);
        }

        Shuffle(incidentBag, incidentRandom);

        if (incidentBag.Count <= 1 || previousIncidentDefinition == null ||
            incidentBag[incidentBag.Count - 1] != previousIncidentDefinition)
            return;

        int replacementIndex = incidentBag.FindIndex(definition =>
            definition != previousIncidentDefinition);

        if (replacementIndex < 0)
            return;

        int lastIndex = incidentBag.Count - 1;
        (incidentBag[replacementIndex], incidentBag[lastIndex]) =
            (incidentBag[lastIndex], incidentBag[replacementIndex]);
    }

    private MonkeyActor SelectCulprit()
    {
        float totalWeight = 0f;
        List<float> weights = new List<float>();

        foreach (MonkeyActor monkey in monkeys)
        {
            if (!IsEligibleIncidentCulprit(monkey))
            {
                weights.Add(0f);
                continue;
            }

            float dispositionWeight = monkey.Disposition == MonkeyDisposition.Bully ? 12f :
                monkey.Disposition == MonkeyDisposition.Scavenger ? 6f : 0f;
            float weight = 1f + monkey.NaughtinessScore * monkey.NaughtinessScore * 100f +
                dispositionWeight;
            weights.Add(weight);
            totalWeight += weight;
        }

        double roll = incidentRandom.NextDouble() * totalWeight;

        for (int index = 0; index < monkeys.Count; index++)
        {
            if (weights[index] <= 0f)
                continue;

            roll -= weights[index];

            if (roll <= 0d)
                return monkeys[index];
        }

        return monkeys.FindLast(IsEligibleIncidentCulprit);
    }

    private static bool IsEligibleIncidentCulprit(MonkeyActor monkey)
    {
        return monkey != null &&
            !monkey.IsInTimeOut &&
            monkey.CurrentZone != null &&
            !monkey.IsChangingZones &&
            monkey.SettledInCurrentZoneSeconds >= CulpritArrivalGraceSeconds &&
            monkey.CurrentZone.Contains(monkey.WorldPosition);
    }

    private List<MonkeyWitnessStatement> BuildWitnessStatements(
        MonkeyActor thief,
        out double likelihoodRatio,
        out bool fairWitnessPattern
    )
    {
        List<MonkeyWitnessStatement> statements = new List<MonkeyWitnessStatement>();
        int culpritIndex = lineupCandidates.IndexOf(thief);
        float[] trustPercentages = new float[lineupCandidates.Count];

        for (int index = 0; index < lineupCandidates.Count; index++)
            trustPercentages[index] = lineupCandidates[index].Trust;

        if (lineupCandidates.Count < 3)
        {
            likelihoodRatio = 0d;
            fairWitnessPattern = false;

            foreach (MonkeyActor speaker in lineupCandidates)
            {
                statements.Add(new MonkeyWitnessStatement(
                    speaker,
                    null,
                    false,
                    $"{speaker.DisplayName} shrugs."
                ));
            }

            return statements;
        }

        fairWitnessPattern = WitnessDeductionModel.TryGenerateFairCues(
            incidentRandom,
            trustPercentages,
            culpritIndex,
            monkeys.Count >= 10,
            out IReadOnlyList<WitnessDeductionCue> cues,
            out likelihoodRatio
        );

        foreach (WitnessDeductionCue cue in cues)
        {
            MonkeyActor speaker = lineupCandidates[cue.SpeakerIndex];

            if (cue.IsShrug)
            {
                statements.Add(new MonkeyWitnessStatement(
                    speaker,
                    null,
                    false,
                    $"{speaker.DisplayName} shrugs."
                ));
                continue;
            }

            MonkeyActor accused = lineupCandidates[cue.AccusedIndex];
            string truthLabel = cue.IsTruthful ? "points at" : "blames";
            statements.Add(new MonkeyWitnessStatement(
                speaker,
                accused,
                cue.IsTruthful,
                $"{speaker.DisplayName} {truthLabel} {accused.DisplayName}."
            ));
        }

        return statements;
    }

    private bool WasZoneObservedAt(string zoneId, float time)
    {
        if (currentObservation != null && currentObservation.ZoneId == zoneId &&
            currentObservation.StartTime <= time)
            return true;

        foreach (ShiftCameraObservation observation in cameraObservations)
        {
            if (observation.ZoneId == zoneId && observation.StartTime <= time && observation.EndTime >= time)
                return true;
        }

        return false;
    }

    private MonkeyActor FindMonkeyInZone(
        string zoneId,
        Predicate<MonkeyActor> filter,
        Func<MonkeyActor, MonkeyActor, bool> shouldReplace
    )
    {
        MonkeyActor best = null;

        foreach (MonkeyActor monkey in monkeys)
        {
            if (monkey.CurrentZone.Id != zoneId || !filter(monkey))
                continue;

            if (best == null || shouldReplace(best, monkey))
                best = monkey;
        }

        return best;
    }

    private MonkeyActor AddNewMonkey()
    {
        if (runMonkeyProfiles.Count == 0)
            return null;

        int newcomerIndex = Mathf.Max(0, totalMonkeysSpawned - shiftDefinition.startingMonkeyCount);
        bool hasAuthoredProfile = totalMonkeysSpawned < runMonkeyProfiles.Count;
        int fallbackNameIndex = Mathf.Max(0, totalMonkeysSpawned - runMonkeyProfiles.Count);
        MonkeyProfile template = runMonkeyProfiles[totalMonkeysSpawned % runMonkeyProfiles.Count];
        MonkeyProfile profile = CreateNewcomerProfile(
            template,
            newcomerIndex,
            hasAuthoredProfile,
            fallbackNameIndex
        );
        return SpawnMonkey(profile);
    }


    public bool BeginPostZoolagWave()
    {
        if (Phase != ShiftPhase.Zoolag || enclosureLayout == null ||
            enclosureLayout.Zones.Count == 0)
            return false;

        EnclosureZone firstZone = enclosureLayout.Zones[0];
        float duration = shiftDefinition != null
            ? Mathf.Max(0.1f, shiftDefinition.postZoolagWaveSeconds)
            : 2f;
        ArrangeWelcomeLineup(firstZone, duration);
        welcomeRemaining = duration;
        Phase = ShiftPhase.Welcome;
        StatusMessage = "Everyone is back in the first enclosure to wave hello.";
        return true;
    }

    private void ArrangeWelcomeLineup(EnclosureZone zone, float duration)
    {
        int count = monkeys.Count;

        if (count == 0)
            return;

        int columns = Mathf.CeilToInt(Mathf.Sqrt(count * 1.5f));
        int rows = Mathf.CeilToInt(count / (float)columns);
        float usableWidth = Mathf.Max(0.5f, zone.Size.x - 1.8f);
        float usableHeight = Mathf.Max(0.5f, zone.Size.y - 2.1f);
        float xStep = columns > 1 ? usableWidth / (columns - 1) : 0f;
        float yStep = rows > 1 ? usableHeight / (rows - 1) : 0f;
        Vector2 origin = zone.Center + new Vector2(-usableWidth * 0.5f, usableHeight * 0.5f);

        List<MonkeyActor> greetingOrder = new List<MonkeyActor>(monkeys);

        for (int index = greetingOrder.Count - 1; index > 0; index--)
        {
            int swapIndex = incidentRandom.Next(index + 1);
            (greetingOrder[index], greetingOrder[swapIndex]) =
                (greetingOrder[swapIndex], greetingOrder[index]);
        }

        float jitterX = columns > 1 ? Mathf.Min(0.22f, xStep * 0.12f) : 0.16f;
        float jitterY = rows > 1 ? Mathf.Min(0.16f, yStep * 0.1f) : 0.12f;

        for (int index = 0; index < count; index++)
        {
            int row = index / columns;
            int column = index % columns;
            int entriesInRow = Mathf.Min(columns, count - row * columns);
            float rowWidth = Mathf.Max(0, entriesInRow - 1) * xStep;
            Vector2 position = new Vector2(
                zone.Center.x - rowWidth * 0.5f + column * xStep,
                origin.y - row * yStep
            );
            position += new Vector2(
                ((float)incidentRandom.NextDouble() * 2f - 1f) * jitterX,
                ((float)incidentRandom.NextDouble() * 2f - 1f) * jitterY
            );
            greetingOrder[index].BeginGreeting(zone, position, duration);
        }
    }

    private void DisperseAfterWelcome()
    {
        if (enclosureLayout == null || enclosureLayout.Zones.Count == 0)
            return;

        EnclosureZone welcomeZone = enclosureLayout.Zones[0];
        List<EnclosureZone> destinations = new List<EnclosureZone>();

        foreach (EnclosureZone zone in enclosureLayout.Zones)
        {
            if (zone != welcomeZone)
                destinations.Add(zone);
        }

        foreach (MonkeyActor monkey in monkeys)
        {
            if (destinations.Count > 0 && incidentRandom.NextDouble() < 0.85d)
                monkey.RunTo(destinations[incidentRandom.Next(destinations.Count)]);
            else
                monkey.ResumeRoutineAfterGreeting();
        }
    }

    private void RemoveMonkeyFromDaycare(MonkeyActor monkey)
    {
        if (monkey == null)
            return;

        archivedFightCount += monkey.FightCount;
        archivedTimeoutCount += monkey.TimeoutCount;
        archivedMessCount += monkey.MessCount;

        if (fightPartners.TryGetValue(monkey, out MonkeyActor partner))
        {
            fightPartners.Remove(monkey);

            if (partner != null)
            {
                fightPartners.Remove(partner);

                if (partner.Activity == MonkeyActivity.Fighting)
                    partner.StopFighting();
            }
        }

        fightCleanup.Remove(monkey);

        if (itemManager != null)
            itemManager.ReleaseMonkey(monkey);

        monkey.ActivityChanged -= HandleMonkeyActivityChanged;
        monkeys.Remove(monkey);
        Destroy(monkey.gameObject);
    }

    private MonkeyProfile CreateNewcomerProfile(
        MonkeyProfile template,
        int newcomerIndex,
        bool hasAuthoredProfile,
        int fallbackNameIndex
    )
    {
        string newcomerName = hasAuthoredProfile
            ? template.displayName
            : fallbackNameIndex < runNewcomerNames.Count &&
            !string.IsNullOrWhiteSpace(runNewcomerNames[fallbackNameIndex])
                ? runNewcomerNames[fallbackNameIndex]
                : $"{template.displayName} {newcomerIndex + 2}";

        return new MonkeyProfile
        {
            monkeyId = $"newcomer_{newcomerIndex + 1}",
            displayName = newcomerName,
            actorPrefab = template.actorPrefab,
            disposition = template.disposition,
            accessory = template.accessory,
            displayColor = ResolveNewcomerColor(template.displayColor),
            startingZoneId = template.startingZoneId,
            startingHunger = template.startingHunger,
            startingAnger = template.startingAnger,
            startingTrust = Mathf.Clamp(
                template.startingTrust -
                resolvedIncidentCount * shiftDefinition.solvedIncidentTrustPenalty,
                0f,
                99f
            ),
            startingNaughtiness = Mathf.Clamp(
                template.startingNaughtiness +
                resolvedIncidentCount * shiftDefinition.solvedIncidentNaughtinessBoost,
                0f,
                100f
            ),
            movementSpeed = template.movementSpeed
        };
    }

    private Color ResolveNewcomerColor(Color fallback)
    {
        if (newcomerColorPool.Count == 0)
            RefillNewcomerColorPool();

        if (newcomerColorPool.Count == 0)
            return fallback;

        int lastIndex = newcomerColorPool.Count - 1;
        Color color = newcomerColorPool[lastIndex];
        newcomerColorPool.RemoveAt(lastIndex);
        return color;
    }

    private void RefillNewcomerColorPool()
    {
        newcomerColorPool.Clear();

        if (shiftDefinition == null || shiftDefinition.newcomerHexColors == null)
            return;

        foreach (string configuredHex in shiftDefinition.newcomerHexColors)
        {
            string hex = configuredHex;

            if (string.IsNullOrWhiteSpace(hex))
                continue;

            hex = hex.Trim();

            if (!hex.StartsWith("#"))
                hex = $"#{hex}";

            if (ColorUtility.TryParseHtmlString(hex, out Color color))
                newcomerColorPool.Add(color);
        }

        if (newcomerRandom == null)
            newcomerRandom = new System.Random();

        for (int index = newcomerColorPool.Count - 1; index > 0; index--)
        {
            int swapIndex = newcomerRandom.Next(index + 1);
            Color swap = newcomerColorPool[index];
            newcomerColorPool[index] = newcomerColorPool[swapIndex];
            newcomerColorPool[swapIndex] = swap;
        }
    }

    private void PrepareRunRoster(System.Random rosterRandom)
    {
        runMonkeyProfiles.Clear();
        runNewcomerNames.Clear();

        if (shiftDefinition.monkeyProfiles != null)
            runMonkeyProfiles.AddRange(shiftDefinition.monkeyProfiles);

        if (shiftDefinition.fallbackNewcomerNames != null)
            runNewcomerNames.AddRange(shiftDefinition.fallbackNewcomerNames);

        Shuffle(runMonkeyProfiles, rosterRandom);
        Shuffle(runNewcomerNames, rosterRandom);
    }

    private static void Shuffle<T>(List<T> values, System.Random random)
    {
        for (int index = values.Count - 1; index > 0; index--)
        {
            int swapIndex = random.Next(index + 1);
            T swap = values[index];
            values[index] = values[swapIndex];
            values[swapIndex] = swap;
        }
    }

    private int SumMonkeyStat(Func<MonkeyActor, int> selector)
    {
        int total = 0;

        foreach (MonkeyActor monkey in monkeys)
        {
            if (monkey != null)
                total += selector(monkey);
        }

        return total;
    }

    private void ApplySolvedIncidentPressure()
    {
        foreach (MonkeyActor monkey in monkeys)
        {
            monkey.AdjustTrust(-shiftDefinition.solvedIncidentTrustPenalty);
            monkey.AdjustNaughtiness(shiftDefinition.solvedIncidentNaughtinessBoost);
        }
    }

    private void HandleMonkeyActivityChanged(MonkeyActor source, MonkeyActivity activity)
    {
        if (source == null || activity != MonkeyActivity.ShowingOff)
            return;

        source.RecordNaughtyAction("Showed off");

        foreach (MonkeyActor monkey in monkeys)
        {
            if (monkey != null && monkey.CurrentZone == source.CurrentZone)
                monkey.AdjustNaughtiness(5f);
        }
    }

    private MonkeyActor SpawnMonkey(MonkeyProfile profile)
    {
        EnclosureZone startingZone = enclosureLayout.FindZone(profile.startingZoneId);

        if (profile.actorPrefab == null || startingZone == null)
            return null;

        GameObject monkeyObject = Instantiate(profile.actorPrefab, monkeyRoot);
        MonkeyActor monkey = monkeyObject.GetComponent<MonkeyActor>();

        if (monkey == null)
            monkey = monkeyObject.AddComponent<MonkeyActor>();

        monkey.Configure(
            profile,
            startingZone,
            enclosureLayout,
            shiftDefinition != null ? shiftDefinition.fullHungerHoldSeconds : 20f,
            DeriveSeed(runSeed, totalMonkeysSpawned + 2)
        );
        if (itemManager != null)
            itemManager.RegisterMonkey(monkey);
        monkey.ActivityChanged += HandleMonkeyActivityChanged;
        monkeys.Add(monkey);
        totalMonkeysSpawned++;
        return monkey;
    }

    private static int DeriveSeed(int sourceSeed, int streamIndex)
    {
        return unchecked(sourceSeed * 397 ^ streamIndex * 7919);
    }

    private void EnsureMonkeyRoot()
    {
        if (monkeyRoot != null)
            return;

        monkeyRoot = new GameObject("Daycare Monkeys").transform;
        monkeyRoot.SetParent(transform, false);
    }

    private void ClearShift()
    {
        Phase = ShiftPhase.Preparing;
        shiftTime = 0f;
        currentIncident = null;

        if (itemManager != null)
            itemManager.Clear();

        ClearLineup();
        nextIncidentTime = 0f;
        incidentWindowRemaining = 0f;
        currentIncidentWindowDuration = 0f;
        welcomeRemaining = 0f;
        resolvedIncidentCount = 0;
        zoolagSentCount = 0;
        totalMonkeysSpawned = 0;
        latestNewcomers.Clear();
        nextFeedAvailableTime = 0f;
        cameraObservations.Clear();
        currentObservation = null;
        fightPartners.Clear();
        fightCleanup.Clear();
        newcomerColorPool.Clear();
        runMonkeyProfiles.Clear();
        runNewcomerNames.Clear();
        incidentBag.Clear();
        previousIncidentDefinition = null;
        archivedFightCount = 0;
        archivedTimeoutCount = 0;
        archivedMessCount = 0;
        tutorialMode = false;
        foreach (MonkeyActor monkey in monkeys)
        {
            if (monkey != null)
                monkey.ActivityChanged -= HandleMonkeyActivityChanged;
        }

        monkeys.Clear();

        if (monkeyRoot == null)
            return;

        for (int index = monkeyRoot.childCount - 1; index >= 0; index--)
            Destroy(monkeyRoot.GetChild(index).gameObject);
    }

    public void PrepareForTitle()
    {
        ClearShift();
        StatusMessage = "Ready to open the daycare.";
    }
}
