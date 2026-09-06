using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class EnclosureAmbientBugPresenter : MonoBehaviour
{
    private const int ButterflyFramesPerColour = 3;
    private const int DragonflyFramesPerVariant = 2;
    private const float MaximumBugScale = 0.6f;

    private enum BugKind
    {
        None,
        Butterfly,
        Dragonfly
    }

    [Header("References")]
    [SerializeField] private ShiftDirector shiftDirector;
    [SerializeField] private EnclosureSurveillanceController surveillanceController;

    [Header("Butterfly Frames")]
    [SerializeField] private Sprite[] greenButterflyFrames;
    [SerializeField] private Sprite[] pinkButterflyFrames;
    [SerializeField] private Sprite[] blueButterflyFrames;
    [SerializeField] private Sprite[] orangeButterflyFrames;

    [Header("Dragonfly Frames")]
    [SerializeField] private Sprite[] dragonflyFrames;

    [Header("Rare Occurrence")]
    [SerializeField, Min(0f)] private float minimumTimeBetweenOccurrences = 20f;
    [SerializeField, Range(0f, 1f)] private float chancePerSecond = 0.04f;
    [SerializeField, Range(0f, 1f)] private float dragonflyChance = 0.28f;
    [SerializeField] private Vector2 scaleRange = new Vector2(0.45f, 0.6f);
    [SerializeField] private int sortingOrder = 12;

    [Header("Butterfly Flutter")]
    [SerializeField] private Vector2 butterflyFlightDurationRange = new Vector2(6f, 9f);
    [SerializeField] private Vector2 butterflyFlutterHeightRange = new Vector2(0.14f, 0.3f);
    [SerializeField] private Vector2 butterflyFlutterCyclesRange = new Vector2(5f, 8f);
    [SerializeField, Min(0.1f)] private float butterflyFramesPerSecond = 10f;

    [Header("Dragonfly Dart And Hover")]
    [SerializeField] private Vector2Int dragonflyHoverStopsRange = new Vector2Int(2, 4);
    [SerializeField] private Vector2 dragonflyDartDurationRange = new Vector2(0.22f, 0.48f);
    [SerializeField] private Vector2 dragonflyHoverDurationRange = new Vector2(0.65f, 1.35f);
    [SerializeField, Min(0f)] private float dragonflyHoverRadius = 0.055f;
    [SerializeField, Min(0.1f)] private float dragonflyFramesPerSecond = 14f;

    private SpriteRenderer bugRenderer;
    private System.Random random;
    private Sprite[] activeFrames;
    private EnclosureZone activeZone;
    private BugKind activeKind;
    private float minimumTimeRemaining;
    private float chanceRollAccumulator;
    private float animationElapsed;
    private int activeRunSeed = int.MinValue;

    private Vector3 butterflyStart;
    private Vector3 butterflyEnd;
    private float butterflyDuration;
    private float butterflyElapsed;
    private float butterflyFlutterHeight;
    private float butterflyFlutterCycles;
    private float butterflyPrimaryPhase;
    private float butterflySecondaryPhase;

    private Vector3 dragonflySegmentStart;
    private Vector3 dragonflySegmentTarget;
    private Vector3 dragonflyHoverCentre;
    private float dragonflySegmentElapsed;
    private float dragonflySegmentDuration;
    private int dragonflyStopsRemaining;
    private bool dragonflyHovering;
    private bool dragonflyExiting;

    private bool IsVisible => bugRenderer != null && bugRenderer.enabled;

    private void Awake()
    {
        if (shiftDirector == null)
            shiftDirector = GetComponent<ShiftDirector>();
        if (surveillanceController == null)
            surveillanceController = GetComponent<EnclosureSurveillanceController>();

        EnsureRenderer();
        HideBug();
    }

    private void OnDisable()
    {
        HideBug();
    }

    private void Update()
    {
        if (shiftDirector == null || surveillanceController == null ||
            shiftDirector.Phase != ShiftPhase.Active)
        {
            HideBug();
            return;
        }

        EnsureRunRandom();

        if (IsVisible)
        {
            TickActiveBug(Time.deltaTime);
            return;
        }

        if (minimumTimeRemaining > 0f)
        {
            minimumTimeRemaining = Mathf.Max(0f, minimumTimeRemaining - Time.deltaTime);
            return;
        }

        chanceRollAccumulator += Time.deltaTime;
        while (chanceRollAccumulator >= 1f)
        {
            chanceRollAccumulator -= 1f;
            if (Next01() < chancePerSecond)
            {
                TryBeginVisit();
                return;
            }
        }
    }

    [ContextMenu("Spawn Ambient Bug Now")]
    public void SpawnNow()
    {
        if (shiftDirector == null || surveillanceController == null)
            return;

        EnsureRunRandom();
        TryBeginVisit();
    }

    private void EnsureRunRandom()
    {
        int runSeed = shiftDirector.CurrentRunSeed;
        if (random != null && activeRunSeed == runSeed)
            return;

        activeRunSeed = runSeed;
        random = new System.Random(runSeed ^ 0x2C9277B5);
        HideBug();
        BeginRarityWait();
    }

    private void TryBeginVisit()
    {
        EnclosureZone zone = surveillanceController.SelectedZone;
        if (zone == null)
        {
            minimumTimeRemaining = 3f;
            chanceRollAccumulator = 0f;
            return;
        }

        activeFrames = PickFrames(out activeKind);
        if (activeFrames == null || activeFrames.Length == 0)
        {
            BeginRarityWait();
            return;
        }

        EnsureRenderer();
        activeZone = zone;
        animationElapsed = 0f;
        bugRenderer.sprite = activeFrames[0];
        bugRenderer.enabled = true;
        float scale = Mathf.Min(RandomBetween(scaleRange), MaximumBugScale);
        bugRenderer.transform.localScale = Vector3.one * scale;

        if (activeKind == BugKind.Dragonfly)
            BeginDragonflyVisit(zone);
        else
            BeginButterflyVisit(zone);
    }

    private void BeginButterflyVisit(EnclosureZone zone)
    {
        bool movingRight = Next01() >= 0.5f;
        float halfWidth = zone.Size.x * 0.5f + 0.55f;
        float verticalRange = zone.Size.y * 0.32f;

        butterflyStart = new Vector3(
            zone.Center.x + (movingRight ? -halfWidth : halfWidth),
            zone.Center.y + Mathf.Lerp(-verticalRange, verticalRange, Next01()),
            0f
        );
        butterflyEnd = new Vector3(
            zone.Center.x + (movingRight ? halfWidth : -halfWidth),
            zone.Center.y + Mathf.Lerp(-verticalRange, verticalRange, Next01()),
            0f
        );
        butterflyDuration = RandomBetween(butterflyFlightDurationRange);
        butterflyFlutterHeight = RandomBetween(butterflyFlutterHeightRange);
        butterflyFlutterCycles = RandomBetween(butterflyFlutterCyclesRange);
        butterflyPrimaryPhase = Next01() * Mathf.PI * 2f;
        butterflySecondaryPhase = Next01() * Mathf.PI * 2f;
        butterflyElapsed = 0f;

        bugRenderer.flipX = movingRight;
        bugRenderer.transform.position = butterflyStart;
    }

    private void BeginDragonflyVisit(EnclosureZone zone)
    {
        bool enteringFromLeft = Next01() >= 0.5f;
        float halfWidth = zone.Size.x * 0.5f + 0.55f;
        float verticalRange = zone.Size.y * 0.3f;
        Vector3 start = new Vector3(
            zone.Center.x + (enteringFromLeft ? -halfWidth : halfWidth),
            zone.Center.y + Mathf.Lerp(-verticalRange, verticalRange, Next01()),
            0f
        );

        dragonflyStopsRemaining = RandomInclusive(
            dragonflyHoverStopsRange.x,
            dragonflyHoverStopsRange.y
        );
        dragonflyHovering = false;
        dragonflyExiting = false;
        bugRenderer.transform.position = start;
        BeginDragonflyDart(start, RandomInteriorPoint(zone), false);
    }

    private void TickActiveBug(float deltaTime)
    {
        if (activeZone != surveillanceController.SelectedZone || activeFrames == null || activeFrames.Length == 0)
        {
            FinishVisit();
            return;
        }

        animationElapsed += deltaTime;
        float framesPerSecond = activeKind == BugKind.Dragonfly
            ? dragonflyFramesPerSecond
            : butterflyFramesPerSecond;
        int frame = Mathf.FloorToInt(animationElapsed * framesPerSecond) % activeFrames.Length;
        bugRenderer.sprite = activeFrames[frame];

        if (activeKind == BugKind.Dragonfly)
            TickDragonfly(deltaTime);
        else
            TickButterfly(deltaTime);
    }

    private void TickButterfly(float deltaTime)
    {
        butterflyElapsed += deltaTime;
        float progress = Mathf.Clamp01(butterflyElapsed / Mathf.Max(0.1f, butterflyDuration));
        Vector3 position = Vector3.Lerp(butterflyStart, butterflyEnd, progress);

        float primaryFlutter = Mathf.Sin(
            progress * Mathf.PI * 2f * butterflyFlutterCycles + butterflyPrimaryPhase
        );
        float secondaryFlutter = Mathf.Sin(
            progress * Mathf.PI * 2f * butterflyFlutterCycles * 2.17f + butterflySecondaryPhase
        );
        position.y += (primaryFlutter + secondaryFlutter * 0.35f) * butterflyFlutterHeight;
        bugRenderer.transform.position = position;

        if (progress >= 1f)
            FinishVisit();
    }

    private void TickDragonfly(float deltaTime)
    {
        dragonflySegmentElapsed += deltaTime;

        if (dragonflyHovering)
        {
            float correctionX = Mathf.Sin(animationElapsed * 19f) * dragonflyHoverRadius;
            float correctionY = Mathf.Sin(animationElapsed * 27f + 0.8f) * dragonflyHoverRadius * 0.65f;
            bugRenderer.transform.position = dragonflyHoverCentre + new Vector3(correctionX, correctionY, 0f);

            if (dragonflySegmentElapsed < dragonflySegmentDuration)
                return;

            dragonflyStopsRemaining--;
            if (dragonflyStopsRemaining > 0)
            {
                BeginDragonflyDart(dragonflyHoverCentre, RandomInteriorPoint(activeZone), false);
                return;
            }

            Vector3 exit = RandomExitPoint(activeZone, dragonflyHoverCentre);
            BeginDragonflyDart(dragonflyHoverCentre, exit, true);
            return;
        }

        float progress = Mathf.Clamp01(
            dragonflySegmentElapsed / Mathf.Max(0.05f, dragonflySegmentDuration)
        );
        float easedProgress = Mathf.SmoothStep(0f, 1f, progress);
        bugRenderer.transform.position = Vector3.Lerp(
            dragonflySegmentStart,
            dragonflySegmentTarget,
            easedProgress
        );

        if (progress < 1f)
            return;

        if (dragonflyExiting)
        {
            FinishVisit();
            return;
        }

        dragonflyHoverCentre = dragonflySegmentTarget;
        dragonflyHovering = true;
        dragonflySegmentElapsed = 0f;
        dragonflySegmentDuration = RandomBetween(dragonflyHoverDurationRange);
    }

    private void BeginDragonflyDart(Vector3 start, Vector3 target, bool exiting)
    {
        dragonflySegmentStart = start;
        dragonflySegmentTarget = target;
        dragonflySegmentElapsed = 0f;
        dragonflySegmentDuration = RandomBetween(dragonflyDartDurationRange);
        dragonflyHovering = false;
        dragonflyExiting = exiting;
        bugRenderer.flipX = target.x > start.x;
    }

    private Vector3 RandomInteriorPoint(EnclosureZone zone)
    {
        float horizontalRange = zone.Size.x * 0.34f;
        float verticalRange = zone.Size.y * 0.28f;
        return new Vector3(
            zone.Center.x + Mathf.Lerp(-horizontalRange, horizontalRange, Next01()),
            zone.Center.y + Mathf.Lerp(-verticalRange, verticalRange, Next01()),
            0f
        );
    }

    private Vector3 RandomExitPoint(EnclosureZone zone, Vector3 from)
    {
        float halfWidth = zone.Size.x * 0.5f + 0.55f;
        float exitX = from.x < zone.Center.x
            ? zone.Center.x - halfWidth
            : zone.Center.x + halfWidth;
        float verticalRange = zone.Size.y * 0.3f;
        return new Vector3(
            exitX,
            zone.Center.y + Mathf.Lerp(-verticalRange, verticalRange, Next01()),
            0f
        );
    }

    private Sprite[] PickFrames(out BugKind kind)
    {
        if (dragonflyFrames != null && dragonflyFrames.Length > 0 && Next01() < dragonflyChance)
        {
            kind = BugKind.Dragonfly;
            return PickDragonflyAnimation();
        }

        kind = BugKind.Butterfly;
        List<Sprite> butterflyFrames = new List<Sprite>(12);
        AddUniqueFrames(butterflyFrames, greenButterflyFrames);
        AddUniqueFrames(butterflyFrames, pinkButterflyFrames);
        AddUniqueFrames(butterflyFrames, blueButterflyFrames);
        AddUniqueFrames(butterflyFrames, orangeButterflyFrames);
        butterflyFrames.Sort((first, second) => string.CompareOrdinal(first.name, second.name));

        int colourCount = butterflyFrames.Count / ButterflyFramesPerColour;
        if (colourCount <= 0)
            return null;

        int colourIndex = random.Next(0, colourCount);
        Sprite[] animation = new Sprite[ButterflyFramesPerColour];
        for (int frame = 0; frame < ButterflyFramesPerColour; frame++)
        {
            animation[frame] = butterflyFrames[
                colourIndex * ButterflyFramesPerColour + frame
            ];
        }

        return animation;
    }

    private Sprite[] PickDragonflyAnimation()
    {
        List<Sprite> frames = new List<Sprite>(4);
        AddUniqueFrames(frames, dragonflyFrames);
        frames.Sort((first, second) => string.CompareOrdinal(first.name, second.name));

        int variantCount = frames.Count / DragonflyFramesPerVariant;
        if (variantCount <= 0)
            return null;

        int variantIndex = random.Next(0, variantCount);
        Sprite[] animation = new Sprite[DragonflyFramesPerVariant];
        for (int frame = 0; frame < DragonflyFramesPerVariant; frame++)
        {
            animation[frame] = frames[
                variantIndex * DragonflyFramesPerVariant + frame
            ];
        }

        return animation;
    }

    private static void AddUniqueFrames(List<Sprite> destination, Sprite[] source)
    {
        if (source == null)
            return;

        for (int index = 0; index < source.Length; index++)
        {
            Sprite frame = source[index];
            if (frame != null && !destination.Contains(frame))
                destination.Add(frame);
        }
    }

    private void FinishVisit()
    {
        HideBug();
        BeginRarityWait();
    }

    private void HideBug()
    {
        activeFrames = null;
        activeZone = null;
        activeKind = BugKind.None;
        animationElapsed = 0f;
        butterflyElapsed = 0f;
        dragonflySegmentElapsed = 0f;
        dragonflyHovering = false;
        dragonflyExiting = false;

        if (bugRenderer != null)
        {
            bugRenderer.enabled = false;
            bugRenderer.sprite = null;
        }
    }

    private void BeginRarityWait()
    {
        if (random == null)
            return;

        minimumTimeRemaining = minimumTimeBetweenOccurrences;
        chanceRollAccumulator = 0f;
    }

    private void EnsureRenderer()
    {
        if (bugRenderer != null)
            return;

        GameObject visual = new GameObject("Ambient Bug Visitor");
        visual.transform.SetParent(transform, false);
        bugRenderer = visual.AddComponent<SpriteRenderer>();
        bugRenderer.sortingOrder = sortingOrder;
    }

    private float RandomBetween(Vector2 range)
    {
        return Mathf.Lerp(Mathf.Min(range.x, range.y), Mathf.Max(range.x, range.y), Next01());
    }

    private int RandomInclusive(int first, int second)
    {
        int minimum = Mathf.Min(first, second);
        int maximum = Mathf.Max(first, second);
        return random != null ? random.Next(minimum, maximum + 1) : minimum;
    }

    private float Next01()
    {
        return random != null ? (float)random.NextDouble() : Random.value;
    }
}
