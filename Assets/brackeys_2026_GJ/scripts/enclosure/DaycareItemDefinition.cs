using UnityEngine;

public enum DaycareItemKind
{
    Banana,
    ManyBanana,
    Poo,
    RotBanana,
    Berries,
    Battery
}

[CreateAssetMenu(menuName = "Jungle Enclosure/Item Definition")]
public class DaycareItemDefinition : ScriptableObject
{
    public DaycareItemKind kind;
    public string displayName = "Banana";
    public Sprite sprite;

    // Ground visuals sit at 0 (rooms) and -1 (halls), monkeys at 10.
    public int sortingOrder = 5;

    [Header("Eating")]
    // Signed, matching MonkeyActor.Feed. Negative hunger feeds, positive anger annoys.
    public float hungerDelta = -48f;
    public float angerDelta = -14f;
    public float naughtinessDelta;
    [Min(0.1f)] public float snackDurationSeconds = 4.5f;

    [Header("Spoiling")]
    [Tooltip("Seconds before this item turns into rotsInto. 0 means it never spoils.")]
    [Min(0f)] public float rotAfterSeconds;
    public DaycareItemDefinition rotsInto;

    public bool CanSpoil => rotAfterSeconds > 0f && rotsInto != null;
}
