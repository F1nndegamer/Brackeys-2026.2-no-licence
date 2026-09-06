using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Surveillance Case/Case Definition")]
public class CaseDefinition : ScriptableObject
{
    public string caseTitle = "Case";
    [TextArea] public string objectiveText = "Who Done Did IT!.";
    [Min(1f)] public float durationSeconds = 60f;
    [Min(0)] public int reviewTokenCount = 1;
    public CaseCrimeDefinition crime = new CaseCrimeDefinition();
    public List<CaseActorSchedule> actorSchedules = new List<CaseActorSchedule>();
    public List<CaseStatement> statements = new List<CaseStatement>();
    public List<CaseEvidence> evidence = new List<CaseEvidence>();

    public bool TryValidate(out string error)
    {
        if (crime == null)
        {
            error = "This case has no crime definition.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(crime.culpritActorId) ||
            actorSchedules.FindIndex(actor => actor.actorId == crime.culpritActorId) < 0)
        {
            error = "The culprit must be one of the scheduled actors.";
            return false;
        }

        if (crime.blackoutStartTime > crime.crimeTime ||
            crime.crimeTime > crime.blackoutEndTime ||
            crime.blackoutEndTime > durationSeconds)
        {
            error = "The crime must occur during a blackout inside the case duration.";
            return false;
        }

        int lieCount = 0;
        string lieStatementId = string.Empty;

        foreach (CaseStatement statement in statements)
        {
            if (string.IsNullOrWhiteSpace(statement.statementId) ||
                string.IsNullOrWhiteSpace(statement.speakerActorId) ||
                string.IsNullOrWhiteSpace(statement.text))
            {
                error = "Every statement needs an id, speaker, and text.";
                return false;
            }

            if (!statement.isCulpritLie)
                continue;

            lieCount++;
            lieStatementId = statement.statementId;

            if (statement.speakerActorId != crime.culpritActorId)
            {
                error = "Only the culprit may have the false statement.";
                return false;
            }
        }

        if (lieCount != 1)
        {
            error = "A case needs exactly one false culprit statement.";
            return false;
        }

        foreach (CaseEvidence item in evidence)
        {
            if (string.IsNullOrWhiteSpace(item.evidenceId) ||
                string.IsNullOrWhiteSpace(item.text))
            {
                error = "Every evidence item needs an id and text.";
                return false;
            }

            if (item.disprovesStatementId == lieStatementId)
            {
                error = string.Empty;
                return true;
            }
        }

        error = "The false statement needs at least one matching evidence item.";
        return false;
    }
}

[Serializable]
public class CaseCrimeDefinition
{
    public string itemName = "Banana";
    public string crimeVerb = "was stolen";
    public string roomRole = "Storage";
    [Min(0f)] public float blackoutStartTime = 38f;
    [Min(0f)] public float crimeTime = 40f;
    [Min(0f)] public float blackoutEndTime = 42f;
    public string culpritActorId = "03";
}

[Serializable]
public class CaseStatement
{
    public string statementId = "statement_id";
    public string speakerActorId = "01";
    [TextArea] public string text;
    public bool isCulpritLie;
}

[Serializable]
public class CaseEvidence
{
    public string evidenceId = "evidence_id";
    [TextArea] public string text;
    public string roomRole;
    [Min(0f)] public float time;
    public string disprovesStatementId;
    [Min(0)] public int reviewTokenCost = 1;
    public bool unlockedAtStart;
}

[Serializable]
public class CaseActorSchedule
{
    public string actorId = "01";
    public string displayName = "Monkey 01";
    public GameObject actorPrefab;
    public Color displayColor = Color.white;
    [Min(0.1f)] public float movementSpeed = 2f;
    public List<CaseScheduleStep> steps = new List<CaseScheduleStep>();
}

[Serializable]
public class CaseScheduleStep
{
    [Min(0f)] public float startTime;
    public string roomRole;
}

[DisallowMultipleComponent]
public class CaseActor : MonoBehaviour
{
    [SerializeField] private string actorId;

    private readonly List<Vector3> route = new List<Vector3>();
    private int routeIndex;
    private float movementSpeed;
    private Action routeCompleted;

    public string ActorId => actorId;
    public bool IsMoving => routeIndex < route.Count;

    public void Configure(
        string newActorId,
        string newDisplayName,
        Color displayColor,
        float newMovementSpeed
    )
    {
        actorId = newActorId;
        movementSpeed = newMovementSpeed;
        gameObject.name = string.IsNullOrWhiteSpace(newDisplayName)
            ? "Actor " + actorId
            : newDisplayName;

        SpriteRenderer spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        if (spriteRenderer != null)
            spriteRenderer.color = displayColor;
    }

    public void SetPosition(Vector3 position)
    {
        route.Clear();
        routeIndex = 0;
        routeCompleted = null;
        transform.position = position;
    }

    public void FollowRoute(List<Vector3> newRoute, Action onRouteCompleted)
    {
        route.Clear();
        route.AddRange(newRoute);
        routeIndex = 0;
        routeCompleted = onRouteCompleted;

        if (route.Count == 0)
            CompleteRoute();
    }

    private void Update()
    {
        if (!IsMoving)
            return;

        transform.position = Vector3.MoveTowards(
            transform.position,
            route[routeIndex],
            movementSpeed * Time.deltaTime
        );

        if (Vector3.Distance(transform.position, route[routeIndex]) <= 0.001f)
        {
            routeIndex++;

            if (!IsMoving)
                CompleteRoute();
        }
    }

    private void CompleteRoute()
    {
        Action completed = routeCompleted;
        routeCompleted = null;
        completed?.Invoke();
    }
}
