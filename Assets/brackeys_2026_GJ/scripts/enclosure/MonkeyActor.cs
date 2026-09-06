using System;
using System.Collections.Generic;
using UnityEngine;

public enum MonkeyActivity
{
    Wandering,
    Snacking,
    Searching,
    ShowingOff,
    Socialising,
    Sulking,
    TimeOut,
    HidingLoot,
    Pooping,
    ApproachingFight,
    Fighting,
    Napping,
    Waving,
    Fleeing,
    // Walking to a specific world point to collect an item.
    Fetching,
    Running,
    Angry,
    Panicking
}

[DisallowMultipleComponent]
public class MonkeyActor : MonoBehaviour
{

    [SerializeField, Range(0.25f, 1f)] private float movementSpeedMultiplier = 0.35f;
    [SerializeField, Range(0f, 100f)] private float hungryThreshold = 100f;
    // A scuffle winds a monkey up further, so it needs a cool-off window afterwards.
    // Without one an angry monkey would re-enter Fighting the instant a fight ends.    
    private const float FightCooldownSeconds = 8f;
    private MonkeyProfile profile;
    private EnclosureLayout enclosureLayout;
    private EnclosureZone currentZone;
    private EnclosureZone destinationZone;
    private EnclosureZone routeOriginZone;
    private System.Random random;
    private Vector3 targetPosition;
    private float decisionTimer;
    private float timeoutRemaining;
    private float fightRemaining;
    private float fightCooldown;
    private float fullHungerHoldRemaining;
    private float fullHungerHoldSeconds = 20f;
    private float recentTroubleAge = float.PositiveInfinity;
    private float settledInCurrentZoneSeconds;
    private float baseMovementSpeed;
    private float movementSpeed;
    private SpriteRenderer spriteRenderer;
    private MonkeySpriteAnimator spriteAnimator;
    private Rigidbody2D body;
    private EnclosureSurveillanceController surveillanceController; public string MonkeyId => profile.monkeyId;
    public string DisplayName => profile.displayName;
    public MonkeyDisposition Disposition => profile.disposition;
    public Color DisplayColor => profile != null ? profile.displayColor : Color.white;
    public EnclosureZone CurrentZone => currentZone;
    public float Hunger { get; private set; }
    public float Fullness => 100f - Hunger;
    public bool IsFull => Hunger <= 0.5f;
    public bool NeedsAngerIntervention => Fullness <= 0.5f && Naughtiness > 50f;
    public float Anger { get; private set; }
    public float Trust { get; private set; }
    public float Naughtiness { get; private set; }
    public MonkeyActivity Activity { get; private set; }
    public int TimeoutCount { get; private set; }
    public int FightCount { get; private set; }
    public int IncidentCount { get; private set; }
    public int MessCount { get; private set; }
    public string RecentTrouble { get; private set; } = "None";
    public float TruthfulnessScore => Trust / 100f;
    public float NaughtinessScore => Naughtiness / 100f;
    public bool IsInTimeOut => timeoutRemaining > 0f;
    public bool IsFighting => fightRemaining > 0f;
    public bool IsMoving { get; private set; }
    public bool IsRunning => runningRoute || Activity == MonkeyActivity.Running;
    public bool HasReachedDestination => !routePending && !IsMoving;
    public bool IsChangingZones => routePending && routeOriginZone != null;
    public float SettledInCurrentZoneSeconds => settledInCurrentZoneSeconds;
    public float FacingDirectionX { get; private set; } = 1f;
    public Vector2 WorldPosition => GetPosition();
    public event Action<MonkeyActor, MonkeyActivity> ActivityChanged;
    public event Action<MonkeyActor> PoopingCompleted;
    public event Action<MonkeyActor, float> TrustChanged;
    private EnclosurePathfinder pathfinder;

    private List<Vector2> path;
    private int pathIndex;
    private Vector2 requestedDestination;
    private float repathTimer;
    private bool routePending;
    private bool runningRoute;
    private bool completedCurrentPoop;

    public bool CanAppearInCameraFeed(EnclosureZone zone)
    {
        if (zone == null)
            return false;

        if (routePending && routeOriginZone != null)
        {
            if (zone != routeOriginZone && zone != destinationZone)
                return false;

            if (enclosureLayout != null &&
                enclosureLayout.TryGetZoneFrameSize(zone.Id, out Vector2 frameSize))
            {
                Vector2 halfFrame = frameSize * 0.5f;
                Vector2 offset = WorldPosition - zone.Center;
                return Mathf.Abs(offset.x) <= halfFrame.x &&
                    Mathf.Abs(offset.y) <= halfFrame.y;
            }

            return zone.Contains(WorldPosition);
        }

        return zone == currentZone;
    }

    public void Configure(
     MonkeyProfile newProfile,
     EnclosureZone startingZone,
     EnclosureLayout newEnclosureLayout,
     float newFullHungerHoldSeconds,
     int seed
 )
    {
        profile = newProfile;
        enclosureLayout = newEnclosureLayout;
        pathfinder = newEnclosureLayout.Pathfinder;

        currentZone = startingZone;
        destinationZone = startingZone;
        routeOriginZone = null;

        random = new System.Random(seed);
        fullHungerHoldSeconds = Mathf.Max(0f, newFullHungerHoldSeconds);
        fullHungerHoldRemaining = 0f;

        baseMovementSpeed = newProfile.movementSpeed * movementSpeedMultiplier;
        movementSpeed = baseMovementSpeed;

        Hunger = newProfile.startingHunger;
        fullHungerHoldRemaining = Hunger <= 0.5f ? fullHungerHoldSeconds : 0f;
        Anger = newProfile.startingAnger;
        Trust = newProfile.startingTrust;
        Naughtiness = newProfile.startingNaughtiness;
        TimeoutCount = 0;
        FightCount = 0;
        IncidentCount = 0;
        MessCount = 0;
        RecentTrouble = "None";
        recentTroubleAge = float.PositiveInfinity;
        settledInCurrentZoneSeconds = 0f;

        spriteRenderer = GetComponent<SpriteRenderer>();
        spriteAnimator = GetComponent<MonkeySpriteAnimator>();
        body = GetComponent<Rigidbody2D>();
        surveillanceController = GetComponentInParent<EnclosureSurveillanceController>();

        if (spriteAnimator == null && spriteRenderer != null)
            spriteAnimator = gameObject.AddComponent<MonkeySpriteAnimator>();

        if (spriteAnimator != null)
            spriteAnimator.Configure(newProfile);
        else if (spriteRenderer != null)
            spriteRenderer.color = newProfile.displayColor;

        gameObject.name = newProfile.displayName;

        Vector2 spawnPosition = currentZone.GetWanderPoint(random);

        if (body != null)
            body.position = spawnPosition;
        else
            transform.position = spawnPosition;

        ChooseNextActivity(true);
    }
    public void TickState(
        float deltaTime,
        float naughtinessPerSecond = 0f,
        float populationPressure = 1f
    )
    {
        if (profile == null)
            return;

        if (!IsChangingZones && currentZone != null && currentZone.Contains(WorldPosition))
            settledInCurrentZoneSeconds += deltaTime;
        else
            settledInCurrentZoneSeconds = 0f;

        if (fullHungerHoldRemaining > 0f)
        {
            fullHungerHoldRemaining = Mathf.Max(0f, fullHungerHoldRemaining - deltaTime);
            Hunger = 0f;
        }
        else
        {
            Hunger = Mathf.Clamp(Hunger + deltaTime * 0.72f, 0f, 100f);
        }

        UpdateHungerObject();
        float angerGain = Hunger > 65f ? 0.38f : -0.12f;
        angerGain += currentZone.MessLevel > 55f ? 0.18f : 0f;
        Anger = Mathf.Clamp(Anger + angerGain * deltaTime, 0f, 100f);
        Naughtiness = Mathf.Clamp(
            Naughtiness + naughtinessPerSecond * Mathf.Max(1f, populationPressure) * deltaTime,
            0f,
            100f
        );
        UpdateNaughtinessAlert();
        recentTroubleAge += deltaTime;
        float routeMultiplier = runningRoute ? 1.85f : 1f;
        movementSpeed = baseMovementSpeed * Mathf.Lerp(1f, 1.35f, NaughtinessScore) * routeMultiplier;

        if (timeoutRemaining > 0f)
        {
            timeoutRemaining = Mathf.Max(0f, timeoutRemaining - deltaTime);

            if (timeoutRemaining <= 0f)
            {
                if (spriteAnimator != null)
                    spriteAnimator.HideTimeoutBox();

                ChooseNextActivity(true);
            }
        }

        if (fightCooldown > 0f)
            fightCooldown = Mathf.Max(0f, fightCooldown - deltaTime);

        if (fightRemaining > 0f)
        {
            fightRemaining = Mathf.Max(0f, fightRemaining - deltaTime);
            Anger = Mathf.Clamp(Anger + 12f * deltaTime, 0f, 100f);
            Naughtiness = Mathf.Clamp(Naughtiness + 0.45f * deltaTime, 0f, 100f);

            if (fightRemaining <= 0f)
            {
                fightCooldown = FightCooldownSeconds;
                ChooseNextActivity(true);
            }
        }

        if (NeedsAngerIntervention && CanEnterAngryState() && Activity != MonkeyActivity.Angry)
            SetActivity(MonkeyActivity.Angry, 2.5f);
        else if (!NeedsAngerIntervention && Activity == MonkeyActivity.Angry)
            ChooseNextActivity(true);

        if (!routePending)
            decisionTimer -= deltaTime;

        if (!routePending && decisionTimer <= 0f && !IsInTimeOut &&
            Activity != MonkeyActivity.HidingLoot)
        {
            if (Activity == MonkeyActivity.Pooping)
            {
                if (spriteAnimator == null)
                    NotifyPoopingAnimationCompleted();

                if (!completedCurrentPoop)
                    return;
            }

            ChooseNextActivity(false);
        }

    }
    private void UpdateNaughtinessAlert()
    {
        if (spriteAnimator == null)
            return;

        spriteAnimator.SetNaughtyAlertActive(Naughtiness >= 100f);
    }
    public void SetRoomPooAlertActive(bool active)
    {
        if (spriteAnimator != null)
            spriteAnimator.SetPooAlertActive(active);
    }

    public void NotifyPoopingAnimationCompleted()
    {
        if (Activity != MonkeyActivity.Pooping || completedCurrentPoop)
            return;

        completedCurrentPoop = true;
        MessCount++;
        Anger = Mathf.Max(0f, Anger - 8f);

        if (currentZone != null)
            currentZone.AddMess(18f);

        PoopingCompleted?.Invoke(this);
    }

    // Signed deltas so a rotten banana can raise anger where a fresh one lowers it.
    public void Feed(
        float hungerDelta = -48f,
        float angerDelta = -14f,
        float snackDuration = 4.5f,
        float naughtinessDelta = -12f,
        float trustDelta = 10f,
        bool rewardedRecentNaughtiness = false
    )
    {
        bool becameFull = Hunger > 0.5f && Hunger + hungerDelta <= 0.5f;
        Hunger = Mathf.Clamp(Hunger + hungerDelta, 0f, 100f);
        Anger = Mathf.Clamp(Anger + angerDelta, 0f, 100f);
        if (rewardedRecentNaughtiness)
            Naughtiness = 100f;
        else
            AdjustNaughtiness(naughtinessDelta);
        AdjustTrust(trustDelta);

        if (becameFull)
            fullHungerHoldRemaining = fullHungerHoldSeconds;

        SetActivity(MonkeyActivity.Snacking, snackDuration);
    }

    public void CalmFromCleaning()
    {
        Anger = Mathf.Max(0f, Anger - 10f);
    }

    public void SendToTimeOut(
        float duration,
        bool justified,
        float justifiedNaughtiness = 2f,
        float justifiedTrustPenalty = 10f,
        float unfairTrustPenalty = 30f
    )
    {
        TimeoutCount++;
        RecentTrouble = justified ? "Timed out after trouble" : "Unfair time-out";
        if (justified)
            Naughtiness = Mathf.Min(Naughtiness, Mathf.Clamp(justifiedNaughtiness, 0f, 10f));
        else
            AdjustNaughtiness(10f);

        AdjustTrust(-Mathf.Abs(justified
            ? justifiedTrustPenalty
            : unfairTrustPenalty));

        timeoutRemaining = duration;
        fightRemaining = 0f;
        fightCooldown = FightCooldownSeconds;
        Anger = Mathf.Max(0f, Anger - 50f);
        SetActivity(MonkeyActivity.Sulking, duration);

        if (spriteAnimator != null)
        {
            spriteAnimator.PlayTimeoutBoxDrop(() =>
            {
                if (surveillanceController != null)
                    surveillanceController.PlayTimeoutLanding(this);
            });
        }
    }

    public void StartFighting(float duration)
    {
        if (!IsFighting)
        {
            FightCount++;
            RecordNaughtyAction("Started a fight", 4f);
        }

        fightRemaining = duration;
        SetActivity(MonkeyActivity.Fighting, duration);
    }

    public bool BeginFightApproach(Vector2 position)
    {
        if (profile == null || IsInTimeOut || currentZone == null)
            return false;

        fightRemaining = 0f;
        runningRoute = false;
        Activity = MonkeyActivity.ApproachingFight;
        decisionTimer = 15f;
        destinationZone = currentZone;
        PlanRoute(position);
        ActivityChanged?.Invoke(this, Activity);
        return true;
    }

    public void SetFightPose(Vector2 position, float facingDirectionX)
    {
        destinationZone = currentZone;
        StopMovement();
        SetPosition(position);
        targetPosition = position;
        FacingDirectionX = facingDirectionX < 0f ? -1f : 1f;
    }

    public void StopFighting()
    {
        fightRemaining = 0f;
        fightCooldown = FightCooldownSeconds;
        ChooseNextActivity(true);
    }

    // Forces a move to a specific zone. Deliberately bypasses SetActivity, whose
    // SelectActivityZone would discard the caller's zone and roll its own.
    public void FleeTo(EnclosureZone zone)
    {
        if (profile == null || zone == null)
            return;

        fightRemaining = 0f;
        Activity = MonkeyActivity.Fleeing;
        decisionTimer = 12f + (float)random.NextDouble() * 4f;
        runningRoute = true;

        destinationZone = zone;

        PlanRoute(zone.GetWanderPoint(random));

        ActivityChanged?.Invoke(this, Activity);
    }

    public void RunTo(EnclosureZone zone)
    {
        FleeTo(zone);
    }

    public void Panic(float duration)
    {
        if (profile == null || IsInTimeOut || Activity == MonkeyActivity.HidingLoot ||
            IsFightActivity(Activity))
            return;

        SetActivity(MonkeyActivity.Panicking, Mathf.Max(0.1f, duration));
    }

    public void RefuseFood(float duration)
    {
        if (profile == null || IsInTimeOut || Activity == MonkeyActivity.HidingLoot ||
            IsFightActivity(Activity))
            return;

        SetActivity(MonkeyActivity.Panicking, Mathf.Max(0.1f, duration));
    }
    public bool GoToPoint(
        EnclosureZone owningZone,
        Vector2 point,
        MonkeyActivity travelActivity,
        float commitSeconds
    )
    {
        if (profile == null || owningZone == null || IsInTimeOut)
            return false;

        fightRemaining = 0f;
        runningRoute = false;
        Activity = travelActivity;
        decisionTimer = Mathf.Max(1f, commitSeconds);

        destinationZone = owningZone;

        PlanRoute(point);

        ActivityChanged?.Invoke(this, Activity);
        return true;
    }

    public void HideLoot()
    {
        SetActivity(MonkeyActivity.HidingLoot, 999f);
    }

    public void ResumeRoutine()
    {
        if (profile != null && Activity == MonkeyActivity.HidingLoot)
            ChooseNextActivity(true);
    }

    public void ResumeRoutineAfterGreeting()
    {
        if (profile != null && Activity == MonkeyActivity.Waving)
            ChooseNextActivity(true);
    }

    public void BeginGreeting(EnclosureZone zone, Vector2 position, float duration)
    {
        if (profile == null || zone == null)
            return;

        fightRemaining = 0f;
        timeoutRemaining = 0f;
        if (spriteAnimator != null)
            spriteAnimator.HideTimeoutBox();

        currentZone = zone;
        destinationZone = zone;
        StopMovement();
        SetPosition(position);
        targetPosition = position;
        FacingDirectionX = 1f;
        SetActivity(MonkeyActivity.Waving, Mathf.Max(0.1f, duration));
    }

    public void PlaceForScriptedEvent(EnclosureZone zone, Vector2 position)
    {
        if (profile == null || zone == null || IsInTimeOut)
            return;

        fightRemaining = 0f;
        currentZone = zone;
        destinationZone = zone;
        StopMovement();
        SetPosition(position);
        targetPosition = position;
    }

    public void RecordIncident(string objectName)
    {
        IncidentCount++;
        RecordNaughtyAction(string.IsNullOrWhiteSpace(objectName)
            ? "Caused an incident"
            : $"Took the {objectName}", 8f);
    }

    public void AdjustTrust(float amount)
    {
        float previousTrust = Trust;
        Trust = Mathf.Clamp(Trust + amount, 0f, 99f);
        float appliedChange = Trust - previousTrust;

        if (!Mathf.Approximately(appliedChange, 0f))
            TrustChanged?.Invoke(this, appliedChange);
    }

    public void AdjustNaughtiness(float amount)
    {
        Naughtiness = Mathf.Clamp(Naughtiness + amount, 0f, 100f);
    }

    public void RecordNaughtyAction(string description, float naughtinessIncrease = 0f)
    {
        RecentTrouble = description;
        recentTroubleAge = 0f;
        AdjustNaughtiness(naughtinessIncrease);
    }

    public bool HasRecentNaughtyAction(float withinSeconds)
    {
        return recentTroubleAge <= Mathf.Max(0f, withinSeconds);
    }

    public void BecomeRestless(float hungerIncrease, float angerIncrease)
    {
        Hunger = Mathf.Clamp(Hunger + hungerIncrease, 0f, 100f);
        Anger = Mathf.Clamp(Anger + angerIncrease, 0f, 100f);
    }

    private void FixedUpdate()
    {
        if (profile == null)
            return;

        if (!CanMoveDuringActivity(Activity))
        {
            IsMoving = false;
            return;
        }

        MoveAlongPath(Time.fixedDeltaTime);
    }
    private void MoveAlongPath(float deltaTime)
    {
        if (path == null || path.Count == 0)
        {
            IsMoving = false;

            if (!routePending)
                return;

            if (pathfinder != null)
            {
                repathTimer -= deltaTime;

                if (repathTimer <= 0f)
                    RefreshPath();

                return;
            }

            MoveDirectlyToTarget(deltaTime);
            return;
        }

        Vector2 currentPosition = GetPosition();
        Vector2 target = path[pathIndex];

        Vector2 nextPosition = Vector2.MoveTowards(
            currentPosition,
            target,
            movementSpeed * deltaTime
        );

        UpdateMovementState(currentPosition, nextPosition);
        SetPosition(nextPosition);

        if (destinationZone != null && destinationZone.Contains(nextPosition))
            currentZone = destinationZone;

        if (Vector2.Distance(nextPosition, target) < 0.05f)
        {
            pathIndex++;

            if (pathIndex >= path.Count)
            {
                path = null;
                routePending = false;
                routeOriginZone = null;
                IsMoving = false;

                if (destinationZone != null)
                    currentZone = destinationZone;

                FinishRunningActivity();

                return;
            }

            targetPosition = path[pathIndex];
        }
    }

    private void MoveDirectlyToTarget(float deltaTime)
    {
        Vector2 currentPosition = GetPosition();
        Vector2 nextPosition = Vector2.MoveTowards(
            currentPosition,
            targetPosition,
            movementSpeed * deltaTime
        );
        UpdateMovementState(currentPosition, nextPosition);
        SetPosition(nextPosition);

        if (destinationZone != null && destinationZone.Contains(nextPosition))
            currentZone = destinationZone;

        if (destinationZone != null &&
            Vector2.SqrMagnitude(nextPosition - (Vector2)targetPosition) < 0.0025f)
        {
            currentZone = destinationZone;
            routePending = false;
            routeOriginZone = null;
            IsMoving = false;
            FinishRunningActivity();
        }
    }

    private void FinishRunningActivity()
    {
        if (Activity != MonkeyActivity.Running && Activity != MonkeyActivity.Fleeing)
            return;

        runningRoute = false;
        ChooseNextActivity(true);
    }

    private void PlanRoute(Vector2 destination)
    {
        requestedDestination = destination;
        routeOriginZone = destinationZone != null && destinationZone != currentZone
            ? currentZone
            : null;
        routePending = true;
        RefreshPath();
    }

    private void RefreshPath()
    {
        repathTimer = 0.25f;
        path = pathfinder != null && pathfinder.IsReady
            ? pathfinder.FindPath(GetPosition(), requestedDestination)
            : null;
        pathIndex = 0;

        if (path != null && path.Count > 0)
        {
            if (Vector2.SqrMagnitude(path[path.Count - 1] - requestedDestination) > 0.0025f)
                path.Add(requestedDestination);

            targetPosition = path[0];
        }
        else if (destinationZone == currentZone)
            targetPosition = requestedDestination;
    }

    private Vector2 GetPosition()
    {
        return body != null ? body.position : (Vector2)transform.position;
    }

    private void SetPosition(Vector2 position)
    {
        if (body != null)
            body.MovePosition(position);
        else
            transform.position = new Vector3(position.x, position.y, transform.position.z);
    }

    private void UpdateMovementState(Vector2 currentPosition, Vector2 nextPosition)
    {
        Vector2 movement = nextPosition - currentPosition;
        IsMoving = movement.sqrMagnitude > 0.0000001f;

        if (Mathf.Abs(movement.x) > 0.00001f)
            FacingDirectionX = Mathf.Sign(movement.x);
    }

    private void StopMovement()
    {
        path = null;
        pathIndex = 0;
        routePending = false;
        routeOriginZone = null;
        runningRoute = false;
        targetPosition = GetPosition();
        IsMoving = false;
    }

    private static bool CanMoveDuringActivity(MonkeyActivity activity)
    {
        return activity == MonkeyActivity.Wandering ||
            activity == MonkeyActivity.Searching ||
            activity == MonkeyActivity.Socialising ||
            activity == MonkeyActivity.Fetching ||
            activity == MonkeyActivity.ApproachingFight ||
            activity == MonkeyActivity.Fleeing ||
            activity == MonkeyActivity.Running;
    }

    private static bool IsFightActivity(MonkeyActivity activity)
    {
        return activity == MonkeyActivity.ApproachingFight ||
            activity == MonkeyActivity.Fighting;
    }

    private bool CanEnterAngryState()
    {
        return !IsInTimeOut &&
            !IsFighting &&
            Activity != MonkeyActivity.ApproachingFight &&
            Activity != MonkeyActivity.Fetching &&
            Activity != MonkeyActivity.HidingLoot &&
            Activity != MonkeyActivity.Panicking &&
            Activity != MonkeyActivity.Snacking &&
            Activity != MonkeyActivity.Waving;
    }

    private void ChooseNextActivity(bool immediate)
    {
        MonkeyActivity nextActivity;

        if (NeedsAngerIntervention)
            nextActivity = MonkeyActivity.Angry;
        else if (Hunger > 72f)
            nextActivity = MonkeyActivity.Searching;
        else
        {
            float fightChance = Mathf.InverseLerp(25f, 100f, Naughtiness) *
                (Disposition == MonkeyDisposition.Bully ? 0.48f : 0.28f);
            float showingOffChance = Mathf.InverseLerp(10f, 100f, Naughtiness) *
                (Disposition == MonkeyDisposition.Busybody ? 0.4f : 0.22f);
            float sulkingChance = Mathf.InverseLerp(20f, 0f, Trust) *
                (Disposition == MonkeyDisposition.Sleepy || Disposition == MonkeyDisposition.Dreamer
                    ? 0.46f
                    : 0.28f);
            double roll = random.NextDouble();

            if (fightCooldown <= 0f && roll < fightChance)
                nextActivity = MonkeyActivity.Fighting;
            else if (roll < fightChance + showingOffChance)
                nextActivity = MonkeyActivity.ShowingOff;
            else if (roll < fightChance + showingOffChance + sulkingChance)
                nextActivity = MonkeyActivity.Sulking;
            else if (Trust > 15f && random.NextDouble() < 0.08d)
                nextActivity = MonkeyActivity.Snacking;
            else
                nextActivity = ChooseDispositionActivity();
        }

        SetActivity(nextActivity, immediate ? 0.5f : (float)(2.5d + random.NextDouble() * 4d));
    }

    private MonkeyActivity ChooseDispositionActivity()
    {
        if (random.NextDouble() < 0.08d)
            return MonkeyActivity.Running;

        int roll = random.Next(100);

        switch (Disposition)
        {
            case MonkeyDisposition.Scavenger:
                return roll < 35 ? MonkeyActivity.Searching :
                    roll < 55 ? MonkeyActivity.Wandering :
                    roll < 75 ? MonkeyActivity.Socialising :
                    roll < 92 ? MonkeyActivity.Pooping : MonkeyActivity.Napping;

            case MonkeyDisposition.Bully:
                return roll < 34 ? MonkeyActivity.Socialising :
                    roll < 60 ? MonkeyActivity.Wandering :
                    roll < 78 ? MonkeyActivity.Searching :
                    roll < 94 ? MonkeyActivity.Pooping : MonkeyActivity.Napping;

            case MonkeyDisposition.Clinger:
                return roll < 44 ? MonkeyActivity.Socialising :
                    roll < 60 ? MonkeyActivity.Wandering :
                    roll < 76 ? MonkeyActivity.Searching :
                    roll < 90 ? MonkeyActivity.Napping : MonkeyActivity.Pooping;

            case MonkeyDisposition.Dreamer:
                return roll < 34 ? MonkeyActivity.Wandering :
                    roll < 55 ? MonkeyActivity.Napping :
                    roll < 70 ? MonkeyActivity.Socialising :
                    roll < 84 ? MonkeyActivity.Searching : MonkeyActivity.Pooping;

            case MonkeyDisposition.Busybody:
                return roll < 30 ? MonkeyActivity.Searching :
                    roll < 55 ? MonkeyActivity.Socialising :
                    roll < 75 ? MonkeyActivity.Wandering :
                    roll < 92 ? MonkeyActivity.Pooping : MonkeyActivity.Napping;

            default:
                return roll < 36 ? MonkeyActivity.Napping :
                    roll < 60 ? MonkeyActivity.Wandering :
                    roll < 75 ? MonkeyActivity.Socialising :
                    roll < 88 ? MonkeyActivity.Searching : MonkeyActivity.Pooping;
        }
    }

    private void SetActivity(MonkeyActivity nextActivity, float duration)
    {
        if (nextActivity != MonkeyActivity.Fighting)
            fightRemaining = 0f;

        if (nextActivity == MonkeyActivity.Pooping && spriteAnimator != null)
            duration = Mathf.Max(duration, spriteAnimator.PoopingAnimationDuration);

        Activity = nextActivity;
        decisionTimer = duration;
        completedCurrentPoop = false;
        runningRoute = nextActivity == MonkeyActivity.Running ||
            nextActivity == MonkeyActivity.Fleeing;

        if (!CanMoveDuringActivity(nextActivity))
        {
            destinationZone = currentZone;
            StopMovement();

            ActivityChanged?.Invoke(this, nextActivity);
            return;
        }

        destinationZone = SelectActivityZone(nextActivity);

        PlanRoute(destinationZone.GetWanderPoint(random));

        ActivityChanged?.Invoke(this, nextActivity);
    }

    private EnclosureZone SelectActivityZone(MonkeyActivity nextActivity)
    {
        if (enclosureLayout == null || enclosureLayout.Zones.Count == 0 ||
            nextActivity == MonkeyActivity.TimeOut || nextActivity == MonkeyActivity.HidingLoot ||
            nextActivity == MonkeyActivity.Pooping || nextActivity == MonkeyActivity.Fighting)
            return currentZone;

        double travelChance = nextActivity switch
        {
            MonkeyActivity.Searching => 0.38d,
            MonkeyActivity.Socialising => 0.24d,
            MonkeyActivity.Wandering => 0.08d,
            MonkeyActivity.Running => 0.8d,
            MonkeyActivity.ShowingOff => 0.05d,
            _ => 0.03d
        };

        if (random.NextDouble() >= travelChance)
            return currentZone;

        EnclosureZone chosenZone = currentZone;

        for (int attempt = 0; attempt < 4 && chosenZone == currentZone; attempt++)
            chosenZone = enclosureLayout.Zones[random.Next(enclosureLayout.Zones.Count)];

        return chosenZone;
    }
    private void UpdateHungerObject()
    {
        if (spriteAnimator == null)
            return;

        spriteAnimator.ShowHunger(Hunger >= hungryThreshold);
    }
}
