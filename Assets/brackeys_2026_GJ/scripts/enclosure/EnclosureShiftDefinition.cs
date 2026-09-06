using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public enum MonkeyDisposition
{
    Scavenger,
    Bully,
    Clinger,
    Dreamer,
    Busybody,
    Sleepy
}
public enum MonkeyAccessory
{
    None,
    Bowtie,
    Hat,
    Hair,
    Hair2,
    Hair3,
    Sunglasses,
    Shine,
    HairBow,
    BlueHat,
    Earring
}
public enum DaycareIncidentType
{
    AuxCord,
    Generator,
    SurveillanceConsole,
    BananaStash,
    Grenade,
    GiantPoo
}

[CreateAssetMenu(menuName = "Jungle Enclosure/Shift Definition")]
public class EnclosureShiftDefinition : ScriptableObject
{
    public string shiftTitle = "TRUST NO ONE: FIRST SHIFT";
    [TextArea] public string briefing = "Keep the enclosure calm. Something will go wrong.";
    [Min(1)] public int startingMonkeyCount = 6;
    [Min(5f)] public float firstIncidentTimeSeconds = 60f;
    [Min(5f)] public float incidentIntervalSeconds = 60f;
    [Range(2, 6)] public int lineupSize = 4;
    [Range(1, 3)] public int guessesPerIncident = 1;
    [Min(1f)] public float incidentWindowSeconds = 5f;
    [Min(0f)] public float feedCooldownSeconds = 15f;
    [Min(0f)] public float fullHungerHoldSeconds = 20f;
    [Min(1f)] public float timeoutDurationSeconds = 30f;
    [Range(0f, 10f)] public float justifiedTimeoutNaughtiness = 2f;
    [Range(0f, 99f)] public float justifiedTimeoutTrustPenalty = 10f;
    [Range(0f, 99f)] public float unfairTimeoutTrustPenalty = 30f;
    [Tooltip("Starting time-out capacity. It grows with the population while always leaving enough monkeys for the lineup.")]
    [Min(1)] public int maxMonkeysInTimeOut = 2;
    [Min(1f)] public float recentTroubleWindowSeconds = 10f;
    [Min(0f)] public float baseNaughtinessPerSecond = 0.06f;
    [Min(0f)] public float populationPressurePerExtraMonkey = 0.12f;
    [Min(0f)] public float solvedIncidentTrustPenalty = 3f;
    [Min(0f)] public float solvedIncidentNaughtinessBoost = 5f;
    [Min(1)] public int monkeysAddedPerSolvedIncident = 2;
    [Min(0.1f)] public float postZoolagWaveSeconds = 2f;
    [Header("Randomness")]
    public bool randomizeSeedEachRun = true;
    [FormerlySerializedAs("incidentSeed")] public int fixedSeed = 1729;
    public List<DaycareIncidentDefinition> incidentPool = new List<DaycareIncidentDefinition>();
    public List<EnclosureZoneDefinition> zones = new List<EnclosureZoneDefinition>();
    public List<EnclosureHallDefinition> hallway = new List<EnclosureHallDefinition>();
    [FormerlySerializedAs("monkeys")]
    public List<MonkeyProfile> monkeyProfiles = new List<MonkeyProfile>();
    [FormerlySerializedAs("newcomerNames")]
    public List<string> fallbackNewcomerNames = new List<string>();
    [Tooltip("Hex colours used by newcomers in order. Both #RRGGBB and RRGGBB are accepted.")]
    public List<string> newcomerHexColors = new List<string>();

    public bool TryValidate(out string error)
    {
        if (zones == null || zones.Count < 2)
        {
            error = "A shift needs at least two enclosure zones.";
            return false;
        }

        if (monkeyProfiles == null || monkeyProfiles.Count < startingMonkeyCount)
        {
            error = "The daycare needs enough monkey profiles for its starting population.";
            return false;
        }

        if (lineupSize > startingMonkeyCount)
        {
            error = "The lineup cannot be larger than the starting population.";
            return false;
        }

        if (monkeysAddedPerSolvedIncident < 1)
        {
            error = "At least one monkey must arrive after a solved incident.";
            return false;
        }

        if (incidentPool == null || incidentPool.Count == 0)
        {
            error = "A shift needs at least one possible incident.";
            return false;
        }

        if (incidentIntervalSeconds <= 0f)
        {
            error = "The incident interval must be positive.";
            return false;
        }

        if (incidentWindowSeconds >= incidentIntervalSeconds)
        {
            error = "The incident window must be shorter than the incident interval.";
            return false;
        }

        HashSet<string> zoneIds = new HashSet<string>();

        foreach (EnclosureZoneDefinition zone in zones)
        {
            if (zone == null || string.IsNullOrWhiteSpace(zone.zoneId) ||
                !zoneIds.Add(zone.zoneId) || zone.size.x <= 0f || zone.size.y <= 0f)
            {
                error = "Every enclosure zone needs a unique id and a positive size.";
                return false;
            }
        }

        if (hallway != null)
        {
            foreach (EnclosureHallDefinition hall in hallway)
            {
                if (hall == null || hall.points == null || hall.points.Length < 2 || hall.width <= 0)
                {
                    error = "Every hallway needs at least two points and a positive width.";
                    return false;
                }

                for (int index = 1; index < hall.points.Length; index++)
                {
                    Vector2 difference = hall.points[index] - hall.points[index - 1];
                    bool horizontal = Mathf.Abs(difference.x) > 0.001f && Mathf.Abs(difference.y) <= 0.001f;
                    bool vertical = Mathf.Abs(difference.y) > 0.001f && Mathf.Abs(difference.x) <= 0.001f;

                    if (!horizontal && !vertical)
                    {
                        error = "Hallway segments must be straight and axis-aligned.";
                        return false;
                    }
                }
            }
        }

        foreach (DaycareIncidentDefinition incident in incidentPool)
        {
            if (incident == null)
            {
                error = "The incident pool contains an empty incident.";
                return false;
            }
        }

        HashSet<string> monkeyIds = new HashSet<string>();

        foreach (MonkeyProfile monkey in monkeyProfiles)
        {
            if (monkey == null || string.IsNullOrWhiteSpace(monkey.monkeyId) ||
                !monkeyIds.Add(monkey.monkeyId) || monkey.actorPrefab == null ||
                string.IsNullOrWhiteSpace(monkey.startingZoneId) ||
                !zoneIds.Contains(monkey.startingZoneId))
            {
                error = "Every monkey needs a unique id, an actor prefab, and a valid starting zone.";
                return false;
            }
        }

        error = string.Empty;
        return true;
    }
}

[Serializable]
public class DaycareIncidentDefinition
{
    public DaycareIncidentType type;
    public string alertTitle = "INCIDENT ALERT";
    [TextArea] public string description = "Something has gone missing.";
    public string objectName = "item";
    [TextArea] public string effectDescription = "The enclosure has changed.";
    public bool blocksCameraEvidence;
    [SerializeField] private Sprite incidentSprite;
    [SerializeField] private Vector2 incidentOffsetMinimum;
    [SerializeField] private Vector2 incidentOffsetMaximum;

    public Sprite IncidentSprite => incidentSprite;
    public Vector2 IncidentOffsetMinimum => incidentOffsetMinimum;
    public Vector2 IncidentOffsetMaximum => incidentOffsetMaximum;
}

[Serializable]
public class EnclosureZoneDefinition
{
    public string zoneId = "banana_grove";
    public string displayName = "Banana Grove";
    public Vector2 center;
    public Vector2 size = new Vector2(5f, 3f);
    public Color placeholderColor = new Color(0.3f, 0.7f, 0.25f, 1f);
}
[Serializable]
public class EnclosureHallDefinition
{
    public Vector2[] points = new Vector2[2];
    public int width = 2;
    public Color placeholderColor = new Color(0.3f, 0.7f, 0.25f, 1f);
}


[Serializable]
public class MonkeyProfile
{
    public string monkeyId = "monkey_01";
    public string displayName = "Ruckus";
    public GameObject actorPrefab;
    public MonkeyDisposition disposition;
    public MonkeyAccessory accessory;
    public Color displayColor = Color.white;
    public string startingZoneId = "banana_grove";
    [Range(0f, 100f)] public float startingHunger = 35f;
    [Range(0f, 100f)] public float startingAnger = 20f;
    [Range(0f, 99f)] public float startingTrust = 50f;
    [Range(0f, 100f)] public float startingNaughtiness = 15f;
    [Min(0.25f)] public float movementSpeed = 2f;
}
