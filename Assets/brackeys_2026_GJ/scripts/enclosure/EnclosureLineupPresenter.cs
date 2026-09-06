using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class LineupWitnessCue
{
    public MonkeyActor Speaker { get; }
    public MonkeyActor AccusedMonkey { get; }
    public MonkeyPointDirection PointDirection { get; }
    public bool IsTruthful { get; }

    public LineupWitnessCue(
        MonkeyActor speaker,
        MonkeyActor accusedMonkey,
        MonkeyPointDirection pointDirection,
        bool isTruthful
    )
    {
        Speaker = speaker;
        AccusedMonkey = accusedMonkey;
        PointDirection = pointDirection;
        IsTruthful = isTruthful;
    }
}

[DisallowMultipleComponent]
public class EnclosureLineupPresenter : MonoBehaviour
{
    private sealed class SuspectVisual
    {
        public MonkeyActor Candidate;
        public Transform Portrait;
        public SpriteRenderer Renderer;
        public SpriteRenderer BehindRenderer;
        public SpriteRenderer FaceRenderer;
        public SpriteRenderer FrontAccessoryRenderer;
        public MonkeySpriteAnimator SourceAnimator;
        public MonkeyPointDirection PointDirection;
        public TextMeshPro Label;
        public SpriteRenderer WrongMarker;
        public SpriteRenderer CulpritMarker;
        public TextMeshPro EvidenceMarker;
        public Vector3 Position;
        public float TargetScale;
        public int Index;
    }

    [Header("References")]
    [SerializeField] private ShiftDirector shiftDirector;
    [SerializeField] private EnclosureSurveillanceController surveillanceController;

    [Header("Suspect-board camera")]
    [SerializeField] private Vector2 lineupCenter = new Vector2(0f, -80f);
    [SerializeField, Min(2f)] private float cameraSize = 7f;

    [Header("Artist-replaceable board")]
    [SerializeField] private Sprite boardBackgroundSprite;
    [SerializeField] private Sprite lineupHeaderSprite;
    [SerializeField] private Sprite lineupCrossSprite;
    [SerializeField] private Sprite lineupCircleSprite;
    [SerializeField] private Vector2 lineupHeaderSize = new Vector2(11.5f, 2.38f);
    [SerializeField] private MonkeyLineupArtSet monkeyArtSet;
    [SerializeField] private Vector2 boardWorldSize = new Vector2(24f, 14f);
    [SerializeField] private Color fallbackBackgroundColor = new Color(0.36f, 0.23f, 0.12f, 1f);

    [Header("Artist-adjustable suspect positions")]
    [SerializeField]
    private Vector2[] suspectOffsets =
    {
        new Vector2(-2.3f, 1.35f),
        new Vector2(2.3f, 1.35f),
        new Vector2(-2.3f, -1.45f),
        new Vector2(2.3f, -1.45f)
    };
    [SerializeField, Min(0.1f)] private float portraitHeight = 2.15f;
    [SerializeField, Min(0.1f)] private float lineupCrossSize = 2.4f;
    [SerializeField, Min(0.1f)] private float lineupCircleSize = 2.9f;
    [SerializeField, Min(0f)] private float suspectLabelHorizontalOffset = 4.7f;
    [SerializeField] private Vector2 suspectLabelSize = new Vector2(4.2f, 1.4f);

    [Header("Spotlight overlay")]
    [SerializeField] private Sprite spotlightOverlaySprite;
    [SerializeField, Range(0f, 1f)] private float spotlightAlpha = 0.7f;
    [SerializeField, Min(1f)] private float spotlightCoverage = 1.32f;
    [SerializeField, Min(0f)] private float spotlightMoveSpeed = 18f;
    [SerializeField] private Color spotlightEdgeColor = new Color32(6, 13, 40, 255);
    [SerializeField, Min(0f)] private float spotlightSideExtension = 12f;

    [Header("Interrogation entrance")]
    [SerializeField, Min(0.05f)] private float entranceDuration = 0.64f;
    [SerializeField, Min(0f)] private float entranceStagger = 0.46f;
    [SerializeField, Min(0f)] private float entranceHorizontalOffset = 9.5f;
    [SerializeField, Min(0)] private int runFirstFrame = 60;
    [SerializeField, Min(1)] private int runFrameCount = 4;
    [SerializeField, Min(0.1f)] private float runFramesPerSecond = 9f;
    [SerializeField, Min(0f)] private float spotlightDelayAfterEntrance = 0.18f;
    [SerializeField, Min(0f)] private float spotlightFlickerDuration = 0.32f;

    [Header("Layered pointing frames")]
    [SerializeField, Min(0)] private int pointUpFirstFrame = 40;
    [SerializeField, Min(0)] private int pointUpRightFirstFrame = 52;
    [SerializeField, Min(0)] private int pointRightFirstFrame = 48;
    [SerializeField, Min(0)] private int pointDownRightFirstFrame = 56;
    [SerializeField, Min(0)] private int pointDownFirstFrame = 44;
    [SerializeField, Min(1)] private int pointFrameCount = 4;
    [SerializeField, Min(0.1f)] private float pointFramesPerSecond = 3f;
    [SerializeField, Min(0.1f)] private float reactionFramesPerSecond = 8f;

    [Header("Fallback presentation")]
    [SerializeField] private TMP_FontAsset lineupFont;
    [SerializeField] private Color labelColor = new Color(1f, 0.92f, 0.68f, 1f);
    [SerializeField] private Color headingColor = new Color(1f, 0.85f, 0.25f, 1f);
    [SerializeField] private Color highTrustColor = new Color(0.25f, 0.92f, 0.45f, 1f);
    [SerializeField] private Color lowTrustColor = new Color(0.95f, 0.18f, 0.12f, 1f);
    [SerializeField] private Color wrongColor = new Color(0.95f, 0.18f, 0.12f, 1f);
    [SerializeField] private Color evidenceColor = new Color(1f, 0.82f, 0.16f, 1f);
    [SerializeField] private Color truthRevealColor = new Color(0.25f, 0.92f, 0.45f, 1f);

    private readonly List<GameObject> displayObjects = new List<GameObject>();
    private readonly Dictionary<MonkeyActor, Vector2> portraitPositions =
        new Dictionary<MonkeyActor, Vector2>();
    private readonly List<LineupWitnessCue> witnessCues = new List<LineupWitnessCue>();
    private readonly List<SuspectVisual> suspectVisuals = new List<SuspectVisual>();
    private readonly Dictionary<MonkeyActor, SuspectVisual> suspectVisualByMonkey =
        new Dictionary<MonkeyActor, SuspectVisual>();
    private static Sprite whiteSprite;
    private ShiftPhase previousPhase = ShiftPhase.Preparing;
    private TextMeshPro instructionText;
    private DaycareAudioController audioController;
    private SpriteRenderer spotlightOverlayRenderer;
    private SpriteRenderer spotlightLeftEdgeRenderer;
    private SpriteRenderer spotlightRightEdgeRenderer;
    private float lineupShownAt;
    private int hoveredLineupNumber;
    private bool firstLineupTutorialActive;
    private bool spotlightImpactPlayed;
    private bool lineupSequenceActive;

    public IReadOnlyList<LineupWitnessCue> WitnessCues => witnessCues;
    public bool HasHoveredSuspect => hoveredLineupNumber > 0;
    public bool IsReadyForSelection => lineupSequenceActive && shiftDirector != null &&
        shiftDirector.Phase == ShiftPhase.Lineup &&
        Time.unscaledTime >= GetSpotlightActivationTime() + spotlightFlickerDuration;
    public event Action<IReadOnlyList<LineupWitnessCue>> WitnessCuesReady;

    public bool TryGetLineupNumberAtWorldPoint(Vector2 worldPosition, out int lineupNumber)
    {
        lineupNumber = 0;

        if (!IsReadyForSelection)
            return false;

        foreach (SuspectVisual visual in suspectVisuals)
        {
            if (visual.Renderer == null || !visual.Renderer.enabled)
                continue;

            Bounds bounds = visual.Renderer.bounds;
            float padding = 0.35f;

            if (worldPosition.x < bounds.min.x - padding ||
                worldPosition.x > bounds.max.x + padding ||
                worldPosition.y < bounds.min.y - padding ||
                worldPosition.y > bounds.max.y + padding)
                continue;

            lineupNumber = visual.Index + 1;
            return true;
        }

        return false;
    }

    public void SetHoveredLineupNumber(int lineupNumber)
    {
        hoveredLineupNumber = IsReadyForSelection ? lineupNumber : 0;
    }

    private void Awake()
    {
        if (shiftDirector == null)
            shiftDirector = GetComponent<ShiftDirector>();

        if (surveillanceController == null)
            surveillanceController = GetComponent<EnclosureSurveillanceController>();

        audioController = GetComponent<DaycareAudioController>();
    }

    private void Update()
    {
        if (shiftDirector == null)
            return;

        if (shiftDirector.Phase == ShiftPhase.Lineup && previousPhase != ShiftPhase.Lineup)
            ShowLineup();
        else if (shiftDirector.Phase != ShiftPhase.Lineup &&
            shiftDirector.Phase != ShiftPhase.Failed && displayObjects.Count > 0)
            ClearLineup();

        bool boardVisible = shiftDirector.Phase == ShiftPhase.Lineup ||
            shiftDirector.Phase == ShiftPhase.Failed;

        if (boardVisible && displayObjects.Count > 0)
        {
            AnimatePointingPoses();
            AnimateSuspects();
            RefreshFeedback();
        }

        previousPhase = shiftDirector.Phase;
    }

    private void OnDisable()
    {
        ClearLineup();
    }

    private void OnDestroy()
    {
        ClearLineup();
    }

    private void ShowLineup()
    {
        ClearLineup();
        lineupShownAt = Time.unscaledTime;
        spotlightImpactPlayed = false;
        lineupSequenceActive = true;
        firstLineupTutorialActive = shiftDirector.ResolvedIncidentCount == 0 &&
            shiftDirector.ZoolagSentCount == 0;

        if (surveillanceController != null)
            surveillanceController.FocusLineup(lineupCenter, cameraSize);

        CreateBackdrop();
        CreateLineupHeader();
        CreateTitle();
        CreateZoolagPrompt();
        CreateInstructionText();

        IReadOnlyList<MonkeyActor> candidates = shiftDirector.LineupCandidates;

        for (int index = 0; index < candidates.Count; index++)
            CreateSuspect(candidates[index], index + 1, GetSuspectPosition(index));

        CreateSpotlightOverlay();
        BuildWitnessCues();
        ApplyLineupArt();
        WitnessCuesReady?.Invoke(witnessCues);
        RefreshFeedback();
    }

    private void CreateBackdrop()
    {
        GameObject backdrop = new GameObject("Lineup Background");
        backdrop.transform.position = new Vector3(lineupCenter.x, lineupCenter.y, 2f);

        SpriteRenderer renderer = backdrop.AddComponent<SpriteRenderer>();
        renderer.sprite = boardBackgroundSprite != null ? boardBackgroundSprite : CreateWhiteSprite();
        renderer.color = boardBackgroundSprite != null ? Color.white : fallbackBackgroundColor;
        renderer.sortingOrder = 180;

        Vector2 spriteSize = renderer.sprite.bounds.size;

        float scaleX = boardWorldSize.x / Mathf.Max(0.01f, spriteSize.x);
        float scaleY = boardWorldSize.y / Mathf.Max(0.01f, spriteSize.y);

        float scale = Mathf.Max(scaleX, scaleY);

        backdrop.transform.localScale = new Vector3(scale, scale, 1f);
        displayObjects.Add(backdrop);
    }

    private void CreateSpotlightOverlay()
    {
        if (spotlightOverlaySprite == null)
            return;

        GameObject overlay = new GameObject("Lineup Spotlight Overlay");
        overlay.transform.position = new Vector3(lineupCenter.x, lineupCenter.y, -0.15f);

        spotlightOverlayRenderer = overlay.AddComponent<SpriteRenderer>();
        spotlightOverlayRenderer.sprite = spotlightOverlaySprite;
        spotlightOverlayRenderer.color = new Color(1f, 1f, 1f, spotlightAlpha);
        spotlightOverlayRenderer.sortingOrder = 215;

        Vector2 spriteSize = spotlightOverlaySprite.bounds.size;
        overlay.transform.localScale = new Vector3(
            boardWorldSize.x * spotlightCoverage / Mathf.Max(0.01f, spriteSize.x),
            boardWorldSize.y * spotlightCoverage / Mathf.Max(0.01f, spriteSize.y),
            1f
        );
        displayObjects.Add(overlay);

        spotlightLeftEdgeRenderer = CreateSpotlightEdge("Lineup Spotlight Left Extension");
        spotlightRightEdgeRenderer = CreateSpotlightEdge("Lineup Spotlight Right Extension");
        UpdateSpotlightEdgeCoverage();
        SetSpotlightVisible(false);
    }

    private SpriteRenderer CreateSpotlightEdge(string objectName)
    {
        GameObject edge = new GameObject(objectName);
        SpriteRenderer renderer = edge.AddComponent<SpriteRenderer>();
        renderer.sprite = CreateWhiteSprite();
        renderer.sortingOrder = 214;
        displayObjects.Add(edge);
        return renderer;
    }

    private void UpdateSpotlightEdgeCoverage()
    {
        if (spotlightOverlayRenderer == null || spotlightLeftEdgeRenderer == null ||
            spotlightRightEdgeRenderer == null)
            return;

        Bounds overlayBounds = spotlightOverlayRenderer.bounds;
        float extension = Mathf.Max(0.1f, spotlightSideExtension);
        float seamOverlap = 0.05f;
        Vector2 edgeSize = new Vector2(extension + seamOverlap, overlayBounds.size.y);
        Color edgeColor = spotlightEdgeColor;
        edgeColor.a *= spotlightAlpha;

        PositionSpotlightEdge(
            spotlightLeftEdgeRenderer,
            new Vector3(
                overlayBounds.min.x - extension * 0.5f + seamOverlap * 0.5f,
                overlayBounds.center.y,
                -0.15f
            ),
            edgeSize,
            edgeColor
        );
        PositionSpotlightEdge(
            spotlightRightEdgeRenderer,
            new Vector3(
                overlayBounds.max.x + extension * 0.5f - seamOverlap * 0.5f,
                overlayBounds.center.y,
                -0.15f
            ),
            edgeSize,
            edgeColor
        );
    }

    private static void PositionSpotlightEdge(
        SpriteRenderer renderer,
        Vector3 position,
        Vector2 worldSize,
        Color color
    )
    {
        renderer.transform.position = position;
        Vector2 spriteSize = renderer.sprite.bounds.size;
        renderer.transform.localScale = new Vector3(
            worldSize.x / Mathf.Max(0.01f, spriteSize.x),
            worldSize.y / Mathf.Max(0.01f, spriteSize.y),
            1f
        );
        renderer.color = color;
    }

    private void CreateLineupHeader()
    {
        if (lineupHeaderSprite == null)
            return;

        GameObject header = new GameObject("Lineup Header");
        header.transform.position = new Vector3(lineupCenter.x, lineupCenter.y + 4.9f, -0.1f);

        SpriteRenderer renderer = header.AddComponent<SpriteRenderer>();
        renderer.sprite = lineupHeaderSprite;
        renderer.color = Color.white;
        renderer.sortingOrder = 216;

        Vector2 spriteSize = lineupHeaderSprite.bounds.size;
        header.transform.localScale = new Vector3(
            lineupHeaderSize.x / Mathf.Max(0.01f, spriteSize.x),
            lineupHeaderSize.y / Mathf.Max(0.01f, spriteSize.y),
            1f
        );
        displayObjects.Add(header);
    }

    private void CreateTitle()
    {
        TextMeshPro title = CreateWorldText(
            "Suspect Board Title",
            GetIncidentQuestion(),
            new Vector3(lineupCenter.x, lineupCenter.y + 4.9f, -0.1f),
            5.2f,
            headingColor,
            220,
            new Vector2(20f, 1.5f)
        );
        title.fontStyle = FontStyles.Bold;
        title.enableAutoSizing = true;
        title.fontSizeMin = 3.5f;
        title.fontSizeMax = 5.3f;
    }

    private string GetIncidentQuestion()
    {
        DaycareIncident incident = shiftDirector.CurrentIncident;
        if (incident == null)
            return "WHO DID IT?";

        switch (incident.Definition.type)
        {
            case DaycareIncidentType.AuxCord:
                return "WHO STOLE THE AUX CORD?";
            case DaycareIncidentType.Generator:
                return "WHO UNPLUGGED THE GENERATOR?";
            case DaycareIncidentType.SurveillanceConsole:
                return "WHO MESSED WITH THE CAMERAS?";
            case DaycareIncidentType.BananaStash:
                return "WHO RAIDED THE BANANAS?";
            case DaycareIncidentType.GiantPoo:
                return "WHO MADE THE BIG MESS?";
            default:
                return $"WHO TOOK THE {incident.Definition.objectName.ToUpperInvariant()}?";
        }
    }

    private void CreateZoolagPrompt()
    {
        TextMeshPro prompt = CreateWorldText(
            "Zoolag Selection Prompt",
            firstLineupTutorialActive
                ? "POINTING = ACCUSATION   •   HIGHER TRUST = MORE RELIABLE"
                : "SELECT A MONKEY TO SEND TO THE ZOOLAG!",
            new Vector3(lineupCenter.x, lineupCenter.y + 4.45f, -0.1f),
            2.8f,
            headingColor,
            220,
            new Vector2(20f, 0.9f)
        );
        prompt.fontStyle = FontStyles.Bold;
    }

    private void CreateInstructionText()
    {
        instructionText = CreateWorldText(
            "Suspect Board Instructions",
            string.Empty,
            new Vector3(lineupCenter.x, lineupCenter.y - 3.25f, -0.1f),
            3.1f,
            labelColor,
            220,
            new Vector2(20f, 1.1f)
        );
        instructionText.fontStyle = FontStyles.Bold;
    }

    private void CreateSuspect(MonkeyActor candidate, int lineupNumber, Vector2 position)
    {
        if (candidate == null)
            return;

        MonkeySpriteAnimator animator = candidate.GetComponent<MonkeySpriteAnimator>();
        SpriteRenderer source = animator != null
            ? animator.BodyRenderer
            : candidate.GetComponent<SpriteRenderer>();
        if (source == null || source.sprite == null)
            return;

        Sprite portraitSprite = animator != null && animator.PortraitSprite != null
            ? animator.PortraitSprite
            : source.sprite;

        GameObject portrait = new GameObject($"Suspect {lineupNumber} - {candidate.DisplayName}");
        portrait.transform.position = new Vector3(position.x, position.y, 0f);

        SpriteRenderer renderer = portrait.AddComponent<SpriteRenderer>();
        renderer.sprite = portraitSprite;
        renderer.color = candidate.DisplayColor;
        renderer.sharedMaterial = source.sharedMaterial;
        renderer.flipX = source.flipX;
        renderer.flipY = source.flipY;
        renderer.sortingOrder = 200;

        SpriteRenderer behindRenderer = animator != null
            ? CreatePortraitLayer(
                portrait.transform,
                "Behind Accessory",
                animator.PortraitBehindSprite,
                animator.BehindRenderer,
                Color.white,
                199
            )
            : null;
        SpriteRenderer faceRenderer = animator != null
            ? CreatePortraitLayer(
                portrait.transform,
                "Face",
                animator.PortraitFaceSprite,
                animator.FaceRenderer,
                Color.white,
                201
            )
            : null;
        SpriteRenderer frontAccessoryRenderer = animator != null
            ? CreatePortraitLayer(
                portrait.transform,
                "Front Accessory",
                animator.PortraitFrontAccessorySprite,
                animator.FrontAccessoryRenderer,
                animator.PortraitFrontAccessoryColor,
                202
            )
            : null;

        float spriteHeight = Mathf.Max(0.01f, portraitSprite.bounds.size.y);
        float targetScale = portraitHeight / spriteHeight;
        portrait.transform.localScale = Vector3.zero;
        displayObjects.Add(portrait);
        portraitPositions[candidate] = position;

        bool leftColumn = (lineupNumber - 1) % 2 == 0;
        float labelDirection = leftColumn ? -1f : 1f;
        TextMeshPro label = CreateWorldText(
            $"Suspect {lineupNumber} Label",
            $"{candidate.DisplayName.ToUpperInvariant()}\n<size=75%>TRUST - {GetTrustValueText(candidate.Trust)}</size>",
            new Vector3(
                position.x + suspectLabelHorizontalOffset * labelDirection,
                position.y,
                -0.1f
            ),
            4.5f,
            labelColor,
            217,
            suspectLabelSize
        );
        label.fontStyle = FontStyles.Bold;
        label.alignment = leftColumn ? TextAlignmentOptions.Right : TextAlignmentOptions.Left;

        SpriteRenderer wrongMarker = CreateResultMarker(
            $"Suspect {lineupNumber} Wrong Marker",
            lineupCrossSprite,
            position,
            lineupCrossSize,
            231
        );
        SpriteRenderer culpritMarker = CreateResultMarker(
            $"Suspect {lineupNumber} Culprit Marker",
            lineupCircleSprite,
            position,
            lineupCircleSize,
            185
        );

        TextMeshPro evidenceMarker = CreateWorldText(
            $"Suspect {lineupNumber} Camera Evidence",
            "CAMERA EVIDENCE",
            new Vector3(position.x, position.y + 1.45f, -0.2f),
            2.6f,
            evidenceColor,
            225,
            new Vector2(4.2f, 0.7f)
        );
        evidenceMarker.fontStyle = FontStyles.Bold;

        evidenceMarker.fontMaterial.EnableKeyword("UNDERLAY_ON");
        evidenceMarker.fontMaterial.SetColor("_UnderlayColor", Color.black);
        evidenceMarker.fontMaterial.SetFloat("_UnderlayOffsetX", 0f);
        evidenceMarker.fontMaterial.SetFloat("_UnderlayOffsetY", -3.5f);
        evidenceMarker.fontMaterial.SetFloat("_UnderlayDilate", 0.08f);
        evidenceMarker.fontMaterial.SetFloat("_UnderlaySoftness", 0f);

        evidenceMarker.enabled = false;

        SuspectVisual visual = new SuspectVisual
        {
            Candidate = candidate,
            Portrait = portrait.transform,
            Renderer = renderer,
            BehindRenderer = behindRenderer,
            FaceRenderer = faceRenderer,
            FrontAccessoryRenderer = frontAccessoryRenderer,
            SourceAnimator = animator,
            PointDirection = MonkeyPointDirection.None,
            Label = label,
            WrongMarker = wrongMarker,
            CulpritMarker = culpritMarker,
            EvidenceMarker = evidenceMarker,
            Position = new Vector3(position.x, position.y, 0f),
            TargetScale = targetScale,
            Index = lineupNumber - 1
        };
        suspectVisuals.Add(visual);
        suspectVisualByMonkey[candidate] = visual;
    }

    private SpriteRenderer CreateResultMarker(
        string objectName,
        Sprite sprite,
        Vector2 position,
        float worldSize,
        int sortingOrder
    )
    {
        if (sprite == null)
            return null;

        GameObject markerObject = new GameObject(objectName);
        markerObject.transform.position = new Vector3(position.x, position.y, -0.2f);
        float spriteSize = Mathf.Max(0.01f, sprite.bounds.size.x, sprite.bounds.size.y);
        markerObject.transform.localScale = Vector3.one * worldSize / spriteSize;

        SpriteRenderer marker = markerObject.AddComponent<SpriteRenderer>();
        marker.sprite = sprite;
        marker.sortingOrder = sortingOrder;
        marker.enabled = false;
        displayObjects.Add(markerObject);
        return marker;
    }

    private static SpriteRenderer CreatePortraitLayer(
        Transform parent,
        string objectName,
        Sprite sprite,
        SpriteRenderer source,
        Color color,
        int sortingOrder
    )
    {
        if (sprite == null)
            return null;

        GameObject layer = new GameObject(objectName);
        layer.transform.SetParent(parent, false);

        SpriteRenderer renderer = layer.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = color;
        renderer.sharedMaterial = source != null ? source.sharedMaterial : null;
        renderer.flipX = source != null && source.flipX;
        renderer.flipY = source != null && source.flipY;
        renderer.sortingOrder = sortingOrder;
        return renderer;
    }

    private TextMeshPro CreateWorldText(
        string objectName,
        string content,
        Vector3 position,
        float fontSize,
        Color color,
        int sortingOrder,
        Vector2 size
    )
    {
        GameObject label = new GameObject(objectName);
        label.transform.position = position;

        TextMeshPro text = label.AddComponent<TextMeshPro>();
        if (lineupFont != null)
            text.font = lineupFont;
        text.text = content;
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = fontSize;
        text.color = color;
        text.sortingOrder = sortingOrder;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.rectTransform.sizeDelta = size;
        displayObjects.Add(label);
        return text;
    }

    private void AnimateSuspects()
    {
        float elapsed = Time.unscaledTime - lineupShownAt;

        foreach (SuspectVisual visual in suspectVisuals)
        {
            float entranceTime = Mathf.Max(0.01f, entranceDuration);
            float entranceStartedAt = visual.Index * entranceStagger;
            float progress = Mathf.Clamp01(
                (elapsed - entranceStartedAt) / entranceTime
            );
            float eased = Mathf.SmoothStep(0f, 1f, progress);
            bool resultReveal = shiftDirector.Phase == ShiftPhase.Failed;
            bool highlighted = !resultReveal && ShouldHighlight(visual.Candidate);
            bool hovered = !resultReveal && visual.Index + 1 == hoveredLineupNumber;
            float pulse = highlighted
                ? 1f + (Mathf.Sin(Time.unscaledTime * 5f) + 1f) * 0.025f
                : 1f;
            float hoverScale = hovered ? 1.08f : 1f;

            bool entersFromLeft = visual.Index % 2 == 0;
            float entranceDirection = entersFromLeft ? -1f : 1f;
            Vector3 startPosition = visual.Position +
                Vector3.right * entranceHorizontalOffset * entranceDirection;

            if (progress <= 0f)
            {
                visual.Portrait.localScale = Vector3.zero;
                visual.Portrait.position = startPosition;
                visual.Label.enabled = false;
                continue;
            }

            visual.Portrait.localScale = Vector3.one * visual.TargetScale * pulse * hoverScale;
            visual.Portrait.position = Vector3.Lerp(startPosition, visual.Position, eased);
            visual.Label.enabled = progress >= 1f;

            if (progress < 1f)
            {
                TryApplyLayeredAnimation(
                    visual,
                    runFirstFrame,
                    !entersFromLeft,
                    runFramesPerSecond,
                    runFrameCount,
                    Mathf.Max(0f, elapsed - entranceStartedAt)
                );
            }
        }

        AnimateSpotlight();
    }

    private void AnimateSpotlight()
    {
        if (shiftDirector.Phase == ShiftPhase.Failed)
        {
            SetSpotlightVisible(false);
            return;
        }

        float activationTime = GetSpotlightActivationTime();
        float now = Time.unscaledTime;

        if (now < activationTime)
        {
            SetSpotlightVisible(false);
            return;
        }

        bool impactStartedThisFrame = !spotlightImpactPlayed;
        if (impactStartedThisFrame)
        {
            spotlightImpactPlayed = true;
            audioController?.PlayLineupThunk();
        }

        if (spotlightOverlayRenderer == null)
            return;

        Vector3 target = GetSpotlightTargetPosition();

        if (impactStartedThisFrame)
            spotlightOverlayRenderer.transform.position = target;

        float flickerAge = now - activationTime;
        if (flickerAge < spotlightFlickerDuration)
        {
            float normalized = flickerAge / Mathf.Max(0.01f, spotlightFlickerDuration);
            bool flickerOn = normalized < 0.22f ||
                (normalized >= 0.4f && normalized < 0.62f) ||
                normalized >= 0.8f;
            SetSpotlightVisible(flickerOn);
            return;
        }

        SetSpotlightVisible(true);

        target = GetSpotlightTargetPosition();

        float blend = spotlightMoveSpeed <= 0f
            ? 1f
            : 1f - Mathf.Exp(-spotlightMoveSpeed * Time.unscaledDeltaTime);
        spotlightOverlayRenderer.transform.position = Vector3.Lerp(
            spotlightOverlayRenderer.transform.position,
            target,
            blend
        );
        spotlightOverlayRenderer.color = new Color(1f, 1f, 1f, spotlightAlpha);
        UpdateSpotlightEdgeCoverage();
    }

    private float GetSpotlightActivationTime()
    {
        int suspectCount = Mathf.Max(1, suspectVisuals.Count);
        float finalEntranceEnd = (suspectCount - 1) * entranceStagger + entranceDuration;
        return lineupShownAt + finalEntranceEnd + spotlightDelayAfterEntrance;
    }

    private Vector3 GetSpotlightTargetPosition()
    {
        SuspectVisual focusedVisual = GetSpotlightTarget();
        return focusedVisual != null
            ? new Vector3(focusedVisual.Position.x, focusedVisual.Position.y, -0.15f)
            : new Vector3(lineupCenter.x, lineupCenter.y, -0.15f);
    }

    private void SetSpotlightVisible(bool visible)
    {
        if (spotlightOverlayRenderer != null)
            spotlightOverlayRenderer.enabled = visible;
        if (spotlightLeftEdgeRenderer != null)
            spotlightLeftEdgeRenderer.enabled = visible;
        if (spotlightRightEdgeRenderer != null)
            spotlightRightEdgeRenderer.enabled = visible;
    }

    private SuspectVisual GetSpotlightTarget()
    {
        int hoveredIndex = hoveredLineupNumber - 1;
        if (hoveredIndex >= 0 && hoveredIndex < suspectVisuals.Count)
            return suspectVisuals[hoveredIndex];

        DaycareIncident incident = shiftDirector.CurrentIncident;
        if (incident != null &&
            (incident.WasCaughtOnCamera || incident.HasFallbackEvidence) &&
            suspectVisualByMonkey.TryGetValue(incident.Culprit, out SuspectVisual culpritVisual))
            return culpritVisual;

        return null;
    }

    private string GetTrustValueText(float trust)
    {
        Color valueColor = trust >= 90f
            ? highTrustColor
            : trust <= 10f
                ? lowTrustColor
                : labelColor;
        return $"<color=#{ColorUtility.ToHtmlStringRGB(valueColor)}>{trust:0}%</color>";
    }

    private void RefreshFeedback()
    {
        DaycareIncident incident = shiftDirector.CurrentIncident;
        bool hasDirectEvidence = incident != null &&
            (incident.WasCaughtOnCamera || incident.HasFallbackEvidence);
        bool incidentResolved = incident != null && shiftDirector.Phase == ShiftPhase.Failed;
        LineupWitnessCue hoveredCue = firstLineupTutorialActive
            ? GetHoveredWitnessCue()
            : null;

        foreach (SuspectVisual visual in suspectVisuals)
        {
            bool directEvidence = hasDirectEvidence && incident.Culprit == visual.Candidate;
            bool revealedCulprit = incidentResolved && incident.Culprit == visual.Candidate;
            bool wasWrong = visual.Candidate == shiftDirector.LastAccusedMonkey && !revealedCulprit;
            MonkeyWitnessStatement statement = incidentResolved
                ? FindWitnessStatement(incident, visual.Candidate)
                : null;

            if (visual.WrongMarker != null)
                visual.WrongMarker.enabled = incidentResolved && !revealedCulprit;
            if (visual.CulpritMarker != null)
                visual.CulpritMarker.enabled = revealedCulprit;
            visual.EvidenceMarker.enabled = directEvidence || revealedCulprit || statement != null;
            visual.EvidenceMarker.text = revealedCulprit
                ? "CULPRIT  •  LIED"
                : statement != null
                    ? statement.IsTruthful ? "TOLD TRUTH" : "LIED"
                    : incident.HasFallbackEvidence ? "CLUE FOUND" : "CAMERA EVIDENCE";
            visual.EvidenceMarker.color = statement != null && statement.IsTruthful
                ? truthRevealColor
                : revealedCulprit || statement != null
                    ? wrongColor
                    : evidenceColor;
            visual.Label.color = wasWrong
                ? wrongColor
                : directEvidence || revealedCulprit
                    ? evidenceColor
                    : visual.Index + 1 == hoveredLineupNumber
                        ? Color.white
                        : hoveredCue != null && hoveredCue.AccusedMonkey == visual.Candidate
                            ? evidenceColor
                            : labelColor;
        }

        if (instructionText == null)
            return;

        if (shiftDirector.Phase == ShiftPhase.Failed && incident != null)
            instructionText.text = $"WRONG MONKEY   •   {incident.Culprit.DisplayName.ToUpperInvariant()} DID IT   •   CLICK OR PRESS ENTER TO CONTINUE";
        else if (firstLineupTutorialActive && !hasDirectEvidence)
            instructionText.text = hoveredCue != null
                ? $"{hoveredCue.Speaker.DisplayName.ToUpperInvariant()} ACCUSES {hoveredCue.AccusedMonkey.DisplayName.ToUpperInvariant()}   •   TRUST {hoveredCue.Speaker.Trust:0}%   •   CLICK A SUSPECT WHEN READY"
                : "HOVER OVER EACH MONKEY TO READ THEIR ACCUSATION   •   HIGHER TRUST = MORE RELIABLE";
        else
            instructionText.text = hasDirectEvidence
                ? "DIRECT EVIDENCE FOUND   -   SELECT A MONKEY TO SEND TO THE ZOOLAG"
                : "SELECT A MONKEY TO SEND TO THE ZOOLAG   -   CLICK OR PRESS 1-4";

        instructionText.color = incidentResolved || hasDirectEvidence ? evidenceColor : labelColor;
    }

    private LineupWitnessCue GetHoveredWitnessCue()
    {
        int hoveredIndex = hoveredLineupNumber - 1;
        if (hoveredIndex < 0 || hoveredIndex >= suspectVisuals.Count)
            return null;

        MonkeyActor hoveredMonkey = suspectVisuals[hoveredIndex].Candidate;

        foreach (LineupWitnessCue cue in witnessCues)
        {
            if (cue.Speaker == hoveredMonkey)
                return cue;
        }

        return null;
    }

    private static MonkeyWitnessStatement FindWitnessStatement(
        DaycareIncident incident,
        MonkeyActor speaker
    )
    {
        foreach (MonkeyWitnessStatement statement in incident.WitnessStatements)
        {
            if (statement.Speaker == speaker)
                return statement;
        }

        return null;
    }

    private bool ShouldHighlight(MonkeyActor candidate)
    {
        DaycareIncident incident = shiftDirector.CurrentIncident;
        return incident != null && incident.Culprit == candidate &&
            (incident.WasCaughtOnCamera || incident.HasFallbackEvidence);
    }

    private Vector2 GetSuspectPosition(int index)
    {
        if (suspectOffsets != null && index >= 0 && index < suspectOffsets.Length)
            return lineupCenter + suspectOffsets[index];

        return lineupCenter + new Vector2((index - 1.5f) * 3f, 0f);
    }

    private void BuildWitnessCues()
    {
        witnessCues.Clear();

        DaycareIncident incident = shiftDirector.CurrentIncident;
        if (incident == null)
            return;

        foreach (MonkeyWitnessStatement statement in incident.WitnessStatements)
        {
            if (statement.AccusedMonkey == null ||
                !portraitPositions.TryGetValue(statement.Speaker, out Vector2 speakerPosition) ||
                !portraitPositions.TryGetValue(statement.AccusedMonkey, out Vector2 accusedPosition))
                continue;

            witnessCues.Add(new LineupWitnessCue(
                statement.Speaker,
                statement.AccusedMonkey,
                ResolvePointDirection(accusedPosition - speakerPosition),
                statement.IsTruthful
            ));
        }
    }

    private void ApplyLineupArt()
    {
        foreach (SuspectVisual visual in suspectVisuals)
            ApplyPose(visual, MonkeyPointDirection.None);

        foreach (LineupWitnessCue cue in witnessCues)
        {
            if (suspectVisualByMonkey.TryGetValue(cue.Speaker, out SuspectVisual visual))
                ApplyPose(visual, cue.PointDirection);
        }
    }

    private void ApplyPose(SuspectVisual visual, MonkeyPointDirection direction)
    {
        visual.PointDirection = direction;

        if (TryApplyLayeredPose(visual, direction))
            return;

        if (monkeyArtSet == null ||
            !monkeyArtSet.TryResolve(direction, out Sprite sprite, out bool flipX))
            return;

        visual.Renderer.sprite = sprite;
        visual.Renderer.flipX = flipX;
        visual.TargetScale = portraitHeight / Mathf.Max(0.01f, sprite.bounds.size.y);
        SetRendererEnabled(visual.BehindRenderer, false);
        SetRendererEnabled(visual.FaceRenderer, false);
        SetRendererEnabled(visual.FrontAccessoryRenderer, false);
    }

    private void AnimatePointingPoses()
    {
        if (shiftDirector.Phase == ShiftPhase.Failed)
        {
            ApplyFailedLineupReactions();
            return;
        }

        foreach (SuspectVisual visual in suspectVisuals)
        {
            if (!HasFinishedEntrance(visual))
                continue;

            if (visual.PointDirection != MonkeyPointDirection.None)
                TryApplyLayeredPose(visual, visual.PointDirection);
        }
    }

    private void ApplyFailedLineupReactions()
    {
        DaycareIncident incident = shiftDirector.CurrentIncident;
        if (incident == null)
            return;

        foreach (SuspectVisual visual in suspectVisuals)
        {
            int firstFrame = visual.Candidate == incident.Culprit
                ? 0
                : visual.Candidate == shiftDirector.LastAccusedMonkey
                    ? 20
                    : 36;
            TryApplyLayeredAnimation(visual, firstFrame, false, reactionFramesPerSecond);
        }
    }

    private bool TryApplyLayeredPose(
        SuspectVisual visual,
        MonkeyPointDirection direction
    )
    {
        if (visual.SourceAnimator == null ||
            !TryResolvePointFrame(direction, out int firstFrame, out bool flipX))
            return false;

        return TryApplyLayeredAnimation(
            visual,
            firstFrame,
            flipX,
            pointFramesPerSecond
        );
    }

    private bool TryApplyLayeredAnimation(
        SuspectVisual visual,
        int firstFrame,
        bool flipX,
        float framesPerSecond,
        int frameCount = -1,
        float animationTime = -1f
    )
    {
        if (visual.SourceAnimator == null)
            return false;

        int resolvedFrameCount = frameCount > 0 ? frameCount : pointFrameCount;
        float resolvedAnimationTime = animationTime >= 0f
            ? animationTime
            : Time.unscaledTime - lineupShownAt;
        int animationFrame = firstFrame + Mathf.FloorToInt(
                resolvedAnimationTime * framesPerSecond
            ) % Mathf.Max(1, resolvedFrameCount);

        if (!visual.SourceAnimator.TryGetLayerFrame(
                animationFrame,
                out Sprite body,
                out Sprite face,
                out Sprite behindAccessory,
                out Sprite frontAccessory
            ))
            return false;

        visual.Renderer.sprite = body;
        visual.Renderer.flipX = flipX;
        visual.TargetScale = portraitHeight / Mathf.Max(0.01f, body.bounds.size.y);
        SetPortraitLayer(visual.BehindRenderer, behindAccessory, flipX);
        SetPortraitLayer(visual.FaceRenderer, face, flipX);
        SetPortraitLayer(visual.FrontAccessoryRenderer, frontAccessory, flipX);
        return true;
    }

    private bool HasFinishedEntrance(SuspectVisual visual)
    {
        float entranceEnd = visual.Index * entranceStagger + entranceDuration;
        return Time.unscaledTime - lineupShownAt >= entranceEnd;
    }

    private bool TryResolvePointFrame(
        MonkeyPointDirection direction,
        out int firstFrame,
        out bool flipX
    )
    {
        flipX = false;

        switch (direction)
        {
            case MonkeyPointDirection.Up:
                firstFrame = pointUpFirstFrame;
                break;
            case MonkeyPointDirection.UpRight:
                firstFrame = pointUpRightFirstFrame;
                flipX = true;
                break;
            case MonkeyPointDirection.Right:
                firstFrame = pointRightFirstFrame;
                flipX = true;
                break;
            case MonkeyPointDirection.DownRight:
                firstFrame = pointDownRightFirstFrame;
                flipX = true;
                break;
            case MonkeyPointDirection.Down:
                firstFrame = pointDownFirstFrame;
                break;
            case MonkeyPointDirection.DownLeft:
                firstFrame = pointDownRightFirstFrame;
                break;
            case MonkeyPointDirection.Left:
                firstFrame = pointRightFirstFrame;
                break;
            case MonkeyPointDirection.UpLeft:
                firstFrame = pointUpRightFirstFrame;
                break;
            default:
                firstFrame = 0;
                break;
        }

        return firstFrame >= 0;
    }

    private static void SetPortraitLayer(
        SpriteRenderer renderer,
        Sprite sprite,
        bool flipX
    )
    {
        if (renderer == null)
            return;

        renderer.sprite = sprite;
        renderer.flipX = flipX;
        renderer.enabled = sprite != null;
    }

    private static void SetRendererEnabled(SpriteRenderer renderer, bool enabled)
    {
        if (renderer != null)
            renderer.enabled = enabled;
    }

    public static MonkeyPointDirection ResolvePointDirection(Vector2 offset)
    {
        return WitnessDeductionModel.ResolvePointDirection(offset.x, offset.y);
    }

    private void ClearLineup()
    {
        foreach (GameObject displayObject in displayObjects)
        {
            if (displayObject != null)
                Destroy(displayObject);
        }

        displayObjects.Clear();
        portraitPositions.Clear();
        witnessCues.Clear();
        suspectVisuals.Clear();
        suspectVisualByMonkey.Clear();
        instructionText = null;
        spotlightOverlayRenderer = null;
        spotlightLeftEdgeRenderer = null;
        spotlightRightEdgeRenderer = null;
        hoveredLineupNumber = 0;
        firstLineupTutorialActive = false;
        spotlightImpactPlayed = false;
        lineupSequenceActive = false;
    }

    private static Sprite CreateWhiteSprite()
    {
        if (whiteSprite == null)
        {
            whiteSprite = Sprite.Create(
                Texture2D.whiteTexture,
                new Rect(0f, 0f, 1f, 1f),
                new Vector2(0.5f, 0.5f)
            );
        }

        return whiteSprite;
    }
}
