using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CaseDirector : MonoBehaviour
{
    [Header("References")]
    public ProcRoomGen roomGenerator;
    public CaseDefinition caseDefinition;
    public Transform actorRoot;

    [Header("Case Playback")]
    public bool beginOnStart = true;
    public bool lockLayoutDuringCase = true;

    private readonly List<ActorRuntime> activeActors = new List<ActorRuntime>();
    private CaseTimeline timeline;
    private float caseTime;
    private bool isRunning;
    private bool blackoutStarted;
    private bool crimeCommitted;
    private bool blackoutEnded;

    public float CaseTime => caseTime;
    public bool IsRunning => isRunning;
    public bool IsBlackoutActive => blackoutStarted && !blackoutEnded;
    public CaseTimeline Timeline => timeline;

    public event System.Action<CaseDefinition> CaseStarted;
    public event System.Action<CaseDefinition> CaseEnded;

    private void Awake()
    {
        timeline = GetComponent<CaseTimeline>();

        if (timeline == null)
            timeline = gameObject.AddComponent<CaseTimeline>();
    }

    private IEnumerator Start()
    {
        if (!beginOnStart)
            yield break;

        yield return null;
        BeginCase();
    }

    private void Update()
    {
        if (!isRunning)
            return;

        caseTime += Time.deltaTime;

        UpdateCrimeTimeline();

        foreach (ActorRuntime actor in activeActors)
            UpdateActorSchedule(actor);

        if (caseTime >= caseDefinition.durationSeconds)
            CompleteCase();
    }

    public bool BeginCase()
    {
        ClearCase();

        if (roomGenerator == null || caseDefinition == null)
            return false;

        if (!caseDefinition.TryValidate(out string validationError))
        {
            Debug.LogError($"Cannot start case '{caseDefinition.caseTitle}': {validationError}", this);
            return false;
        }

        if (!roomGenerator.HasValidLayout)
            roomGenerator.Generate();

        if (!roomGenerator.HasValidLayout)
            return false;

        if (!TryValidateRouteTiming(out string routeTimingError))
        {
            Debug.LogError($"Cannot start case '{caseDefinition.caseTitle}': {routeTimingError}", this);
            return false;
        }

        if (lockLayoutDuringCase)
            roomGenerator.generateOnKeyPress = false;

        EnsureActorRoot();
        timeline.ResetTimeline();
        caseTime = 0f;
        blackoutStarted = false;
        crimeCommitted = false;
        blackoutEnded = false;
        timeline.Record(caseTime, CaseEventType.CaseStarted);

        foreach (CaseActorSchedule schedule in caseDefinition.actorSchedules)
            SpawnActor(schedule);

        isRunning = true;
        CaseStarted?.Invoke(caseDefinition);
        return true;
    }

    public void StopCase()
    {
        isRunning = false;
    }

    private void SpawnActor(CaseActorSchedule schedule)
    {
        if (schedule.actorPrefab == null || schedule.steps.Count == 0)
            return;

        List<CaseScheduleStep> steps = new List<CaseScheduleStep>(schedule.steps);
        steps.Sort((first, second) => first.startTime.CompareTo(second.startTime));

        if (!CaseRouteFinder.TryFindRoomIndex(
            roomGenerator.Rooms,
            steps[0].roomRole,
            out int startingRoomIndex
        ))
            return;

        GameObject actorObject = Instantiate(schedule.actorPrefab, actorRoot);
        CaseActor actor = actorObject.GetComponent<CaseActor>();

        if (actor == null)
            actor = actorObject.AddComponent<CaseActor>();

        actor.Configure(
            schedule.actorId,
            schedule.displayName,
            schedule.displayColor,
            schedule.movementSpeed
        );
        actor.SetPosition(roomGenerator.ToWorldPosition(roomGenerator.Rooms[startingRoomIndex].Center));

        activeActors.Add(new ActorRuntime(actor, steps, startingRoomIndex));
        timeline.Record(
            caseTime,
            CaseEventType.ActorEnteredRoom,
            schedule.actorId,
            roomGenerator.Rooms[startingRoomIndex].Role
        );
    }

    private void UpdateActorSchedule(ActorRuntime runtime)
    {
        // A schedule must never overwrite a route that is still in progress.
        // The timeline records the movement that actually happened, not an intended teleport.
        if (runtime.Actor.IsMoving)
            return;

        while (runtime.NextStepIndex < runtime.Steps.Count &&
               runtime.Steps[runtime.NextStepIndex].startTime <= caseTime)
        {
            CaseScheduleStep step = runtime.Steps[runtime.NextStepIndex];
            runtime.NextStepIndex++;

            if (!CaseRouteFinder.TryFindRoomIndex(
                roomGenerator.Rooms,
                step.roomRole,
                out int destinationRoomIndex
            ))
                continue;

            if (!CaseRouteFinder.TryBuildRoute(
                roomGenerator,
                runtime.CurrentRoomIndex,
                destinationRoomIndex,
                out List<Vector3> route
            ))
                continue;

            int departureRoomIndex = runtime.CurrentRoomIndex;
            timeline.Record(
                caseTime,
                CaseEventType.ActorLeftRoom,
                runtime.Actor.ActorId,
                roomGenerator.Rooms[departureRoomIndex].Role,
                roomGenerator.Rooms[destinationRoomIndex].Role
            );

            runtime.Actor.FollowRoute(route, () => CompleteActorRoute(
                runtime,
                destinationRoomIndex
            ));
        }
    }

    private void CompleteActorRoute(ActorRuntime runtime, int destinationRoomIndex)
    {
        runtime.CurrentRoomIndex = destinationRoomIndex;
        timeline.Record(
            caseTime,
            CaseEventType.ActorEnteredRoom,
            runtime.Actor.ActorId,
            roomGenerator.Rooms[destinationRoomIndex].Role
        );
    }

    private void EnsureActorRoot()
    {
        if (actorRoot != null)
            return;

        actorRoot = new GameObject("Case Actors").transform;
        actorRoot.SetParent(transform, false);
    }

    private void ClearCase()
    {
        isRunning = false;
        caseTime = 0f;
        activeActors.Clear();

        if (actorRoot == null)
            return;

        for (int index = actorRoot.childCount - 1; index >= 0; index--)
            Destroy(actorRoot.GetChild(index).gameObject);
    }

    private void UpdateCrimeTimeline()
    {
        CaseCrimeDefinition crime = caseDefinition.crime;

        if (!blackoutStarted && caseTime >= crime.blackoutStartTime)
        {
            blackoutStarted = true;
            timeline.Record(caseTime, CaseEventType.BlackoutStarted, roomRole: crime.roomRole);
        }

        if (!crimeCommitted && caseTime >= crime.crimeTime)
        {
            crimeCommitted = true;
            timeline.Record(
                caseTime,
                CaseEventType.CrimeCommitted,
                crime.culpritActorId,
                crime.roomRole
            );
        }

        if (!blackoutEnded && caseTime >= crime.blackoutEndTime)
        {
            blackoutEnded = true;
            timeline.Record(caseTime, CaseEventType.BlackoutEnded, roomRole: crime.roomRole);
        }
    }

    private void CompleteCase()
    {
        if (!isRunning)
            return;

        isRunning = false;
        CaseEnded?.Invoke(caseDefinition);
    }

    private bool TryValidateRouteTiming(out string error)
    {
        foreach (CaseActorSchedule schedule in caseDefinition.actorSchedules)
        {
            List<CaseScheduleStep> steps = new List<CaseScheduleStep>(schedule.steps);
            steps.Sort((first, second) => first.startTime.CompareTo(second.startTime));

            for (int index = 1; index < steps.Count; index++)
            {
                if (!TryGetRouteTravelTime(
                    steps[index - 1].roomRole,
                    steps[index].roomRole,
                    schedule.movementSpeed,
                    out float travelTime
                ))
                {
                    error = $"{schedule.displayName} has a route to an unknown or unreachable room.";
                    return false;
                }

                if (index + 1 < steps.Count &&
                    travelTime > steps[index + 1].startTime - steps[index].startTime)
                {
                    error = $"{schedule.displayName} cannot reach {steps[index].roomRole} before their next move.";
                    return false;
                }
            }
        }

        CaseActorSchedule culprit = caseDefinition.actorSchedules.Find(
            actor => actor.actorId == caseDefinition.crime.culpritActorId
        );
        List<CaseScheduleStep> culpritSteps = new List<CaseScheduleStep>(culprit.steps);
        culpritSteps.Sort((first, second) => first.startTime.CompareTo(second.startTime));
        int crimeStepIndex = culpritSteps.FindLastIndex(
            step => step.startTime <= caseDefinition.crime.crimeTime
        );

        if (crimeStepIndex < 0 ||
            !string.Equals(
                culpritSteps[crimeStepIndex].roomRole,
                caseDefinition.crime.roomRole,
                System.StringComparison.OrdinalIgnoreCase
            ))
        {
            error = "The culprit is not scheduled to be in the crime room.";
            return false;
        }

        float culpritTravelTime = 0f;

        if (crimeStepIndex > 0 &&
            !TryGetRouteTravelTime(
                culpritSteps[crimeStepIndex - 1].roomRole,
                culpritSteps[crimeStepIndex].roomRole,
                culprit.movementSpeed,
                out culpritTravelTime
            ))
        {
            error = "The culprit cannot reach the crime room.";
            return false;
        }

        if (crimeStepIndex > 0 && culpritTravelTime >
            caseDefinition.crime.crimeTime - culpritSteps[crimeStepIndex].startTime)
        {
            error = "The culprit cannot reach the crime room before the crime.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private bool TryGetRouteTravelTime(
        string fromRoomRole,
        string toRoomRole,
        float movementSpeed,
        out float travelTime
    )
    {
        travelTime = 0f;

        if (movementSpeed <= 0f ||
            !CaseRouteFinder.TryFindRoomIndex(roomGenerator.Rooms, fromRoomRole, out int fromRoomIndex) ||
            !CaseRouteFinder.TryFindRoomIndex(roomGenerator.Rooms, toRoomRole, out int toRoomIndex) ||
            !CaseRouteFinder.TryBuildRoute(roomGenerator, fromRoomIndex, toRoomIndex, out List<Vector3> route))
            return false;

        float distance = 0f;

        for (int index = 1; index < route.Count; index++)
            distance += Vector3.Distance(route[index - 1], route[index]);

        travelTime = distance / movementSpeed;
        return true;
    }

    private class ActorRuntime
    {
        public CaseActor Actor { get; }
        public List<CaseScheduleStep> Steps { get; }
        public int CurrentRoomIndex { get; set; }
        public int NextStepIndex { get; set; }

        public ActorRuntime(
            CaseActor actor,
            List<CaseScheduleStep> steps,
            int startingRoomIndex
        )
        {
            Actor = actor;
            Steps = steps;
            CurrentRoomIndex = startingRoomIndex;
            NextStepIndex = 1;
        }
    }
}
