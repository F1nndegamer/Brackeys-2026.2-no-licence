using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[DisallowMultipleComponent]
public class EnclosureSurveillanceController : MonoBehaviour
{
    public Camera SurveillanceCamera => surveillanceCamera;
    public bool HasHoveredMonkey => hoveredMonkey != null;
    public bool HasHoveredCleanableItem { get; private set; }

    [Header("References")]
    [SerializeField] private EnclosureLayout enclosureLayout;
    [SerializeField] private ShiftDirector shiftDirector;
    [SerializeField] private Camera surveillanceCamera;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private EnclosureMonkeyRosterPresenter monkeyRoster;
    [SerializeField] private EnclosureLineupPresenter lineupPresenter;
    [SerializeField] private DaycareGameFlowPresenter gameFlowPresenter;

    [Header("Selection Indicator")]
    [SerializeField] private Sprite selectionIndicatorSprite;
    [SerializeField, Min(0.01f)] private float selectionIndicatorScale = 0.4f;
    [SerializeField, Min(0f)] private float selectionIndicatorHeight = 0.12f;
    [SerializeField, Min(0f)] private float selectionIndicatorBobHeight = 0.08f;
    [SerializeField, Min(0f)] private float selectionIndicatorBobSpeed = 2.25f;

    [Header("Camera")]
    [SerializeField] private float cameraDepth = -10f;
    [SerializeField, Range(0f, 0.25f)] private float horizontalMonkeyViewportPadding = 0.015f;
    [SerializeField, Range(0f, 0.5f)] private float bottomMonkeyViewportPadding = 0.245f;
    [SerializeField, Range(0f, 0.25f)] private float topMonkeyViewportPadding = 0.015f;

    [Header("Camera Switch Static")]
    [SerializeField] private Sprite[] switchStaticFrames;
    [SerializeField, Min(0.02f)] private float switchStaticDuration = 0.2f;
    [SerializeField, Range(0f, 1f)] private float switchStaticAlpha = 0.7f;
    [SerializeField] private int switchStaticSortingOrder = 900;
    [SerializeField] private AudioClip switchStaticSound;
    [SerializeField, Range(0f, 1f)] private float switchStaticVolume = 0.7f;
    [Header("Camera Disturbance")]
    [SerializeField, Min(0f)] private float impactShakeAmount = 0.08f;
    [SerializeField, Min(0f)] private float timeoutLandingShakeAmount = 0.11f;
    [SerializeField, Min(0.01f)] private float timeoutLandingShakeDuration = 0.18f;
    [SerializeField, Min(0.01f)] private float glitchTransitionDuration = 0.25f;
    [SerializeField, Min(0f)] private float glitchShakeAmount = 0.018f;
    [SerializeField, Min(0f)] private float glitchZoomAmount = 0.006f;
    [Header("Post Processing")]
    [SerializeField] private Volume cctvVolume;
    [SerializeField] private VolumeProfile cctvProfile;

    [SerializeField] private float normalContrast = 20f;
    [SerializeField] private float normalSaturation = -35f;
    [SerializeField] private float normalGrain = 1f;
    [SerializeField] private float normalChromaticAberration = 0.05f;
    [SerializeField] private float normalLensDistortion = -0.3f;

    private ColorAdjustments colorAdjustments;
    private FilmGrain filmGrain;
    private ChromaticAberration chromaticAberration;
    private LensDistortion lensDistortion;
    private bool switchingCamera;
    private Coroutine switchRoutine;
    private SpriteRenderer switchStaticRenderer;
    private AudioSource switchStaticAudioSource;
    private readonly StringBuilder display = new StringBuilder();
    private int selectedFeedIndex = -1;
    private string lastActionMessage;
    private bool cameraGlitching;
    private float cameraGlitchRemaining;
    private float shakeRemaining;
    private float shakeDuration;
    private float shakeAmount;
    private bool cameraWasDisturbed;
    private ShiftPhase previousPhase = ShiftPhase.Preparing;
    private Vector3 stableCameraPosition;
    private float stableCameraSize;
    private MonkeyActor selectedMonkey;
    private MonkeyActor hoveredMonkey;

    private SpriteRenderer selectionMarker;
    private static readonly List<RaycastResult> UiRaycastResults = new List<RaycastResult>();
    [SerializeField] private DaycareItemManager itemManager;
    public TMP_Text StatusText => statusText;
    public MonkeyActor SelectedMonkey => selectedMonkey;
    public event Action<MonkeyActor> SelectedMonkeyChanged;
    public event Action<MonkeyActor> TimeoutMonkeyClicked;
    public event Action<EnclosureZone> CameraChanged;

    public EnclosureZone SelectedZone =>
        selectedFeedIndex >= 0 && selectedFeedIndex < enclosureLayout.Zones.Count
            ? enclosureLayout.Zones[selectedFeedIndex]
            : null;

    private IEnumerator Start()
    {
        if (enclosureLayout == null)
            enclosureLayout = GetComponent<EnclosureLayout>();

        if (shiftDirector == null)
            shiftDirector = GetComponent<ShiftDirector>();

        if (surveillanceCamera == null)
            surveillanceCamera = Camera.main;

        if (cctvVolume == null && cctvProfile != null)
        {
            cctvVolume = gameObject.AddComponent<Volume>();
            cctvVolume.isGlobal = true;
            cctvVolume.priority = 10f;
            cctvVolume.sharedProfile = cctvProfile;
        }

        if (cctvVolume != null && cctvVolume.profile != null)
        {
            cctvVolume.profile.TryGet(out colorAdjustments);
            cctvVolume.profile.TryGet(out filmGrain);
            cctvVolume.profile.TryGet(out chromaticAberration);
            cctvVolume.profile.TryGet(out lensDistortion);

            SetNormalCctvEffects();
        }
        if (statusText == null)
            statusText = FindStatusText();

        if (monkeyRoster == null)
            monkeyRoster = GetComponent<EnclosureMonkeyRosterPresenter>();

        if (lineupPresenter == null)
            lineupPresenter = GetComponent<EnclosureLineupPresenter>();
        if (gameFlowPresenter == null)
            gameFlowPresenter = GetComponentInChildren<DaycareGameFlowPresenter>(true);

        while (enclosureLayout != null && !enclosureLayout.IsBuilt)
            yield return null;

        SelectFeed(0, true);
    }

    private void Update()
    {
        if (shiftDirector == null || enclosureLayout == null)
            return;

        ResumeObservationOnReturnToShift();
        ValidateSelectedMonkey();
        UpdateFeedVisibility();
        UpdateHoveredMonkey();
        UpdateSelectionMarker();

        if (gameFlowPresenter != null && gameFlowPresenter.IsMenuBlockingInput)
        {
            SetHoveredMonkey(null);
            RenderStatus();
            UpdateCameraGlitch();
            return;
        }

        if (monkeyRoster != null && monkeyRoster.IsBlockingInput)
        {
            RenderStatus();
            UpdateCameraGlitch();
            return;
        }

        if (shiftDirector.Phase == ShiftPhase.Active)
            HandleShiftInput();
        else if (shiftDirector.Phase == ShiftPhase.IncidentWindow)
            HandleIncidentWindowInput();
        else if (shiftDirector.Phase == ShiftPhase.Lineup)
            HandleLineupInput();

        RenderStatus();
        UpdateCameraGlitch();
    }

    private void OnDisable()
    {
        CancelCameraSwitch();
        SetHoveredMonkey(null);

        if (shiftDirector != null)
            shiftDirector.EndCameraObservation();
    }

    private void ResumeObservationOnReturnToShift()
    {
        ShiftPhase phase = shiftDirector.Phase;
        bool enteredLineup = phase == ShiftPhase.Lineup && previousPhase != ShiftPhase.Lineup;
        bool returnedToShift = (phase == ShiftPhase.Active || phase == ShiftPhase.Welcome) &&
            phase != previousPhase;
        bool wasCameraPhase = previousPhase == ShiftPhase.Active ||
            previousPhase == ShiftPhase.IncidentWindow || previousPhase == ShiftPhase.Welcome;
        bool isCameraPhase = phase == ShiftPhase.Active ||
            phase == ShiftPhase.IncidentWindow || phase == ShiftPhase.Welcome;
        previousPhase = phase;

        if (wasCameraPhase && !isCameraPhase)
            CancelCameraSwitch();

        if (enteredLineup)
            StopCameraDisturbance();

        if (phase != ShiftPhase.Active)
            SetSelectedMonkey(null);

        if (returnedToShift && SelectedZone != null)
            SelectFeed(selectedFeedIndex, true);
    }

    private void HandleShiftInput()
    {
        HandleMonkeyMouseInput();

        if (Keyboard.current != null &&
            (Keyboard.current.leftArrowKey.wasPressedThisFrame ||
                Keyboard.current.aKey.wasPressedThisFrame))
            SelectFeed(selectedFeedIndex - 1, false);

        if (Keyboard.current != null &&
            (Keyboard.current.rightArrowKey.wasPressedThisFrame ||
                Keyboard.current.dKey.wasPressedThisFrame))
            SelectFeed(selectedFeedIndex + 1, false);

        if (SelectedZone == null)
            return;

        if (Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame)
            FeedSelectedMonkey();

        if (Keyboard.current != null && Keyboard.current.cKey.wasPressedThisFrame)
            CleanSelectedZone();

        if (Keyboard.current != null && Keyboard.current.tKey.wasPressedThisFrame)
            TimeOutSelectedMonkey();
    }

    private void HandleIncidentWindowInput()
    {
        if (Keyboard.current == null)
            return;

        if (Keyboard.current.leftArrowKey.wasPressedThisFrame ||
            Keyboard.current.aKey.wasPressedThisFrame)
            SelectFeed(selectedFeedIndex - 1, false);

        if (Keyboard.current.rightArrowKey.wasPressedThisFrame ||
            Keyboard.current.dKey.wasPressedThisFrame)
            SelectFeed(selectedFeedIndex + 1, false);

        if (Keyboard.current.spaceKey.wasPressedThisFrame)
            shiftDirector.SkipIncidentWindow();
    }

    private void HandleLineupInput()
    {
        if (gameFlowPresenter != null && gameFlowPresenter.IsAccusationFlowOpen)
            return;

        if (lineupPresenter != null && !lineupPresenter.IsReadyForSelection)
        {
            lineupPresenter.SetHoveredLineupNumber(0);
            return;
        }

        if (Mouse.current != null && lineupPresenter != null)
        {
            bool pointerBlocked = IsPointerOverBlockingUi();
            int hoveredLineupNumber = 0;

            if (!pointerBlocked)
            {
                Vector2 worldPosition = GetMouseWorldPosition();
                lineupPresenter.TryGetLineupNumberAtWorldPoint(
                    worldPosition,
                    out hoveredLineupNumber
                );
            }

            lineupPresenter.SetHoveredLineupNumber(hoveredLineupNumber);

            if (!pointerBlocked && hoveredLineupNumber > 0 &&
                Mouse.current.leftButton.wasPressedThisFrame)
            {
                RequestAccusation(hoveredLineupNumber);
                return;
            }
        }

        if (Keyboard.current == null)
            return;

        for (int index = 1; index <= shiftDirector.LineupCandidates.Count; index++)
        {
            Key key = (Key)((int)Key.Digit1 + index - 1);

            if (!Keyboard.current[key].wasPressedThisFrame)
                continue;

            RequestAccusation(index);
            return;
        }
    }

    private void RequestAccusation(int lineupNumber)
    {
        if (gameFlowPresenter != null)
        {
            gameFlowPresenter.RequestAccusation(lineupNumber, out lastActionMessage);
            return;
        }

        shiftDirector.TryAccuse(lineupNumber, out lastActionMessage);
    }

    private void SelectFeed(int requestedIndex, bool bypassStatic)
    {
        if (enclosureLayout.Zones.Count == 0 || surveillanceCamera == null)
            return;

        if (switchingCamera)
            return;

        int count = enclosureLayout.Zones.Count;
        int targetIndex = (requestedIndex % count + count) % count;

        if (bypassStatic || selectedFeedIndex < 0)
        {
            ApplyFeed(targetIndex);
            return;
        }

        switchRoutine = StartCoroutine(SwitchFeedRoutine(targetIndex));
    }
    private void ApplyFeed(int feedIndex)
    {
        selectedFeedIndex = feedIndex;

        EnclosureZone zone = SelectedZone;

        SetSelectedMonkey(null);
        enclosureLayout.ShowZoneEnvironment(zone.Id);

        surveillanceCamera.transform.position = new Vector3(
            zone.Center.x,
            zone.Center.y,
            cameraDepth
        );

        stableCameraPosition = surveillanceCamera.transform.position;

        if (surveillanceCamera.orthographic)
        {
            surveillanceCamera.orthographicSize = enclosureLayout.SharedCameraOrthographicSize;

            stableCameraSize = surveillanceCamera.orthographicSize;
        }

        shiftDirector.BeginCameraObservation(zone.Id);
        lastActionMessage = $"Watching {zone.DisplayName}.";
        UpdateFeedVisibility();
        CameraChanged?.Invoke(zone);
    }
    private IEnumerator SwitchFeedRoutine(int targetIndex)
    {
        switchingCamera = true;
        EnsureSwitchStaticRenderer();

        if (switchStaticRenderer == null || switchStaticFrames == null || switchStaticFrames.Length == 0)
        {
            ApplyFeed(targetIndex);
            switchingCamera = false;
            switchRoutine = null;
            yield break;
        }

        float duration = Mathf.Max(0.02f, switchStaticDuration);
        float elapsed = 0f;
        int displayedFrame = -1;
        bool feedApplied = false;
        switchStaticRenderer.color = new Color(1f, 1f, 1f, switchStaticAlpha);
        switchStaticRenderer.enabled = true;
        PlaySwitchStaticSound();

        while (elapsed < duration)
        {
            float progress = Mathf.Clamp01(elapsed / duration);
            int frameIndex = Mathf.Min(
                switchStaticFrames.Length - 1,
                Mathf.FloorToInt(progress * switchStaticFrames.Length)
            );

            if (frameIndex != displayedFrame)
            {
                displayedFrame = frameIndex;
                switchStaticRenderer.sprite = switchStaticFrames[frameIndex];
                FitSwitchStaticToCamera();
            }

            if (!feedApplied && progress >= 0.5f)
            {
                ApplyFeed(targetIndex);
                feedApplied = true;
                FitSwitchStaticToCamera();
            }

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (!feedApplied)
            ApplyFeed(targetIndex);

        switchStaticRenderer.enabled = false;
        StopSwitchStaticSound();
        switchingCamera = false;
        switchRoutine = null;
    }

    private void EnsureSwitchStaticRenderer()
    {
        if (switchStaticRenderer != null || surveillanceCamera == null)
            return;

        GameObject overlay = new GameObject("Camera Switch Static");
        overlay.transform.SetParent(surveillanceCamera.transform, false);
        overlay.transform.localPosition = new Vector3(0f, 0f, 1f);
        switchStaticRenderer = overlay.AddComponent<SpriteRenderer>();
        switchStaticRenderer.color = new Color(1f, 1f, 1f, switchStaticAlpha);
        switchStaticRenderer.sortingOrder = switchStaticSortingOrder;
        switchStaticRenderer.enabled = false;
    }

    private void PlaySwitchStaticSound()
    {
        if (switchStaticSound == null)
            return;

        if (switchStaticAudioSource == null)
        {
            switchStaticAudioSource = gameObject.AddComponent<AudioSource>();
            switchStaticAudioSource.playOnAwake = false;
            switchStaticAudioSource.loop = false;
            switchStaticAudioSource.spatialBlend = 0f;
        }

        switchStaticAudioSource.Stop();
        switchStaticAudioSource.clip = switchStaticSound;
        switchStaticAudioSource.volume = switchStaticVolume;
        switchStaticAudioSource.Play();
    }

    private void StopSwitchStaticSound()
    {
        if (switchStaticAudioSource != null)
            switchStaticAudioSource.Stop();
    }

    private void FitSwitchStaticToCamera()
    {
        if (switchStaticRenderer == null || switchStaticRenderer.sprite == null || surveillanceCamera == null)
            return;

        Vector2 spriteSize = switchStaticRenderer.sprite.bounds.size;

        if (spriteSize.x <= 0f || spriteSize.y <= 0f)
            return;

        float viewHeight = surveillanceCamera.orthographicSize * 2f;
        float viewWidth = viewHeight * surveillanceCamera.aspect;
        float scale = Mathf.Max(viewWidth / spriteSize.x, viewHeight / spriteSize.y);
        switchStaticRenderer.transform.localScale = Vector3.one * scale;
    }
    public void SelectPreviousFeed()
    {
        if (CanManuallyChangeFeed())
            SelectFeed(selectedFeedIndex - 1, false);
    }

    public void SelectNextFeed()
    {
        if (CanManuallyChangeFeed())
            SelectFeed(selectedFeedIndex + 1, false);
    }

    public void SelectZoneFeed(string zoneId)
    {
        if (!CanManuallyChangeFeed() || enclosureLayout == null || string.IsNullOrWhiteSpace(zoneId))
            return;

        IReadOnlyList<EnclosureZone> zones = enclosureLayout.Zones;
        for (int index = 0; index < zones.Count; index++)
        {
            if (!string.Equals(zones[index].Id, zoneId, StringComparison.OrdinalIgnoreCase))
                continue;

            if (index != selectedFeedIndex)
                SelectFeed(index, false);

            return;
        }
    }

    private bool CanManuallyChangeFeed()
    {
        return shiftDirector != null &&
            (shiftDirector.Phase == ShiftPhase.Active ||
             shiftDirector.Phase == ShiftPhase.IncidentWindow ||
             shiftDirector.Phase == ShiftPhase.Welcome);
    }

    public void FeedSelectedMonkey()
    {
        ValidateSelectedMonkey();

        if (selectedMonkey != null)
        {
            lastActionMessage = shiftDirector.FeedMonkey(selectedMonkey);
            return;
        }

        // Nothing clicked, so fall back to the zone the camera is on and feed whoever
        // is hungriest there. Keeps [F] useful without forcing a selection first.
        lastActionMessage = SelectedZone == null
            ? "Select a monkey to feed."
            : shiftDirector.FeedHungriestMonkey(SelectedZone.Id);
    }

    public void CleanSelectedZone()
    {
        lastActionMessage = shiftDirector == null || shiftDirector.Phase != ShiftPhase.Active
            ? "Cleaning is unavailable right now."
            : SelectedZone == null
            ? "Select an enclosure camera first."
            : shiftDirector.CleanZone(SelectedZone.Id);
    }

    public void TimeOutSelectedMonkey()
    {
        ValidateSelectedMonkey();
        lastActionMessage = selectedMonkey == null
            ? "Select a monkey for time-out."
            : shiftDirector.TimeOutMonkey(selectedMonkey);
    }

    public void ClearSelectedMonkey()
    {
        SetSelectedMonkey(null);
    }

    private void HandleMonkeyMouseInput()
    {
        if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame ||
            IsPointerOverBlockingUi() || surveillanceCamera == null)
            return;

        Vector2 worldPosition = GetMouseWorldPosition();

        DaycareItem mess = itemManager.TryGetCleanableAtWorldPoint(worldPosition);

        if (mess != null)
        {
            DaycareItemKind cleanedKind = mess.Kind;
            itemManager.CleanItem(mess);
            lastActionMessage = cleanedKind == DaycareItemKind.RotBanana
                ? "Cleaned up rotten banana."
                : "Cleaned up poo.";
            return;
        }

        MonkeyActor monkey = TryGetMonkeyAtWorldPoint(worldPosition);

        if (monkey != null)
        {
            SetSelectedMonkey(monkey);

            if (monkey.IsInTimeOut)
                TimeoutMonkeyClicked?.Invoke(monkey);

            lastActionMessage = $"Selected {monkey.DisplayName}.";
        }
        else
        {
            SetSelectedMonkey(null);
        }
    }

    private void UpdateHoveredMonkey()
    {
        MonkeyActor nextHovered = null;
        bool hasHoveredCleanableItem = false;

        bool canHover = shiftDirector.Phase == ShiftPhase.Active &&
            Mouse.current != null && surveillanceCamera != null &&
            !IsPointerOverBlockingUi() &&
            (monkeyRoster == null || !monkeyRoster.IsBlockingInput) &&
            (gameFlowPresenter == null || !gameFlowPresenter.IsMenuBlockingInput);

        if (canHover)
        {
            Vector2 worldPosition = GetMouseWorldPosition();

            nextHovered = TryGetMonkeyAtWorldPoint(worldPosition);

            if (itemManager != null)
                hasHoveredCleanableItem =
                    itemManager.TryGetCleanableAtWorldPoint(worldPosition) != null;
        }

        SetHoveredMonkey(nextHovered);
        HasHoveredCleanableItem = hasHoveredCleanableItem;
    }

    private void SetHoveredMonkey(MonkeyActor monkey)
    {
        if (hoveredMonkey == monkey)
            return;

        if (hoveredMonkey != null)
        {
            MonkeySpriteAnimator previousAnimator =
                hoveredMonkey.GetComponent<MonkeySpriteAnimator>();
            if (previousAnimator != null)
                previousAnimator.SetHovered(false);
        }

        hoveredMonkey = monkey;

        if (hoveredMonkey != null)
        {
            MonkeySpriteAnimator nextAnimator = hoveredMonkey.GetComponent<MonkeySpriteAnimator>();
            if (nextAnimator != null)
                nextAnimator.SetHovered(true);
        }
    }

    private Vector2 GetMouseWorldPosition()
    {
        Vector2 screenPosition = Mouse.current.position.ReadValue();
        Vector3 worldPosition = surveillanceCamera.ScreenToWorldPoint(screenPosition);
        return new Vector2(worldPosition.x, worldPosition.y);
    }

    private void SetSelectedMonkey(MonkeyActor monkey)
    {
        if (selectedMonkey == monkey)
            return;

        selectedMonkey = monkey;
        SetSelectionMarkerVisible(selectedMonkey != null);

        if (selectedMonkey != null)
            UpdateSelectionMarker();

        SelectedMonkeyChanged?.Invoke(selectedMonkey);
    }

    private void ValidateSelectedMonkey()
    {
        if (selectedMonkey != null &&
            !IsMonkeyVisibleInSelectedFeed(selectedMonkey))
            SetSelectedMonkey(null);
    }

    private MonkeyActor TryGetMonkeyAtWorldPoint(Vector2 worldPosition)
    {
        MonkeyActor closest = null;
        float closestDistance = float.MaxValue;

        foreach (MonkeyActor monkey in shiftDirector.Monkeys)
        {
            if (monkey == null || !IsMonkeyVisibleInSelectedFeed(monkey))
                continue;

            SpriteRenderer[] renderers = monkey.GetComponentsInChildren<SpriteRenderer>();
            Bounds bounds = default;
            bool hasBounds = false;

            foreach (SpriteRenderer renderer in renderers)
            {
                if (renderer == null || !renderer.enabled || renderer.sprite == null)
                    continue;

                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            if (!hasBounds)
                continue;

            float padding = 0.12f;

            if (worldPosition.x < bounds.min.x - padding ||
                worldPosition.x > bounds.max.x + padding ||
                worldPosition.y < bounds.min.y - padding ||
                worldPosition.y > bounds.max.y + padding)
                continue;

            float distance = Vector2.SqrMagnitude(worldPosition - (Vector2)monkey.transform.position);

            if (distance >= closestDistance)
                continue;

            closest = monkey;
            closestDistance = distance;
        }

        return closest;
    }

    private bool IsMonkeyVisibleInSelectedFeed(MonkeyActor monkey)
    {
        if (monkey == null || surveillanceCamera == null || SelectedZone == null)
            return false;

        if (!monkey.CanAppearInCameraFeed(SelectedZone))
            return false;

        MonkeySpriteAnimator animator = monkey.GetComponent<MonkeySpriteAnimator>();
        SpriteRenderer renderer = animator != null
            ? animator.BodyRenderer
            : monkey.GetComponent<SpriteRenderer>();

        if (renderer == null)
            return false;

        Bounds bounds = renderer.bounds;
        Vector3 viewportMin = surveillanceCamera.WorldToViewportPoint(bounds.min);
        Vector3 viewportMax = surveillanceCamera.WorldToViewportPoint(bounds.max);
        return viewportMax.z > 0f &&
            viewportMax.x >= horizontalMonkeyViewportPadding &&
            viewportMin.x <= 1f - horizontalMonkeyViewportPadding &&
            viewportMax.y >= bottomMonkeyViewportPadding &&
            viewportMin.y <= 1f - topMonkeyViewportPadding;
    }

    private void UpdateFeedVisibility()
    {
        if (shiftDirector == null)
            return;

        bool cameraPhase = shiftDirector.Phase == ShiftPhase.Active ||
            shiftDirector.Phase == ShiftPhase.IncidentWindow ||
            shiftDirector.Phase == ShiftPhase.Welcome;

        foreach (MonkeyActor monkey in shiftDirector.Monkeys)
        {
            if (monkey == null)
                continue;

            bool visible = cameraPhase && IsMonkeyVisibleInSelectedFeed(monkey);
            MonkeySpriteAnimator animator = monkey.GetComponent<MonkeySpriteAnimator>();

            if (animator != null)
            {
                animator.SetCameraVisible(visible);
                continue;
            }

            foreach (SpriteRenderer renderer in monkey.GetComponentsInChildren<SpriteRenderer>())
                renderer.enabled = visible;
        }
    }

    public void RefreshFeedVisibility()
    {
        UpdateFeedVisibility();
    }

    private void UpdateSelectionMarker()
    {
        if (selectedMonkey == null)
            return;

        EnsureSelectionMarker();
        MonkeySpriteAnimator animator = selectedMonkey.GetComponent<MonkeySpriteAnimator>();
        SpriteRenderer renderer = animator != null
            ? animator.BodyRenderer
            : selectedMonkey.GetComponent<SpriteRenderer>();
        float bob = Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f * selectionIndicatorBobSpeed) *
            selectionIndicatorBobHeight;
        float markerY = renderer != null
            ? renderer.bounds.max.y + selectionIndicatorHeight + bob
            : selectedMonkey.transform.position.y + 0.8f;
        selectionMarker.transform.position = new Vector3(
            renderer != null ? renderer.bounds.center.x : selectedMonkey.transform.position.x,
            markerY,
            -0.2f
        );
    }

    private void SetSelectionMarkerVisible(bool visible)
    {
        if (visible)
            EnsureSelectionMarker();

        if (selectionMarker != null)
            selectionMarker.gameObject.SetActive(visible);
    }

    private void EnsureSelectionMarker()
    {
        if (selectionMarker != null)
            return;

        GameObject markerObject = new GameObject("Selected Monkey Indicator");
        markerObject.transform.SetParent(transform, false);
        markerObject.transform.localScale = Vector3.one * selectionIndicatorScale;
        selectionMarker = markerObject.AddComponent<SpriteRenderer>();
        selectionMarker.sprite = selectionIndicatorSprite;
        selectionMarker.sortingOrder = 60;
    }

    private static bool IsPointerOverBlockingUi()
    {
        if (EventSystem.current == null || Mouse.current == null)
            return false;

        PointerEventData pointer = new PointerEventData(EventSystem.current)
        {
            position = Mouse.current.position.ReadValue()
        };
        UiRaycastResults.Clear();
        EventSystem.current.RaycastAll(pointer, UiRaycastResults);

        foreach (RaycastResult result in UiRaycastResults)
        {
            if (result.gameObject.GetComponentInParent<Selectable>() != null)
                return true;
        }

        return false;
    }

    public void FocusLineup(Vector2 center, float cameraSize)
    {
        if (surveillanceCamera == null)
            return;

        CancelCameraSwitch();
        surveillanceCamera.transform.position = new Vector3(center.x, center.y, cameraDepth);
        stableCameraPosition = surveillanceCamera.transform.position;

        if (surveillanceCamera.orthographic)
        {
            surveillanceCamera.orthographicSize = Mathf.Max(1f, cameraSize);
            stableCameraSize = surveillanceCamera.orthographicSize;
        }
    }

    private void CancelCameraSwitch()
    {
        if (switchRoutine != null)
        {
            StopCoroutine(switchRoutine);
            switchRoutine = null;
        }

        switchingCamera = false;
        if (switchStaticRenderer != null)
            switchStaticRenderer.enabled = false;

        StopSwitchStaticSound();
        SetNormalCctvEffects();

        if (surveillanceCamera == null || selectedFeedIndex < 0)
            return;

        surveillanceCamera.transform.position = stableCameraPosition;

        if (surveillanceCamera.orthographic && stableCameraSize > 0f)
            surveillanceCamera.orthographicSize = stableCameraSize;
    }

    private void RenderStatus()
    {
        if (statusText == null || shiftDirector.Definition == null)
            return;

        display.Clear();

        if (shiftDirector.Phase == ShiftPhase.Active)
        {
            if (SelectedZone == null)
            {
                statusText.text = "CONNECTING TO ENCLOSURE CAMERAS...";
                return;
            }

            display.AppendLine(shiftDirector.Definition.shiftTitle);
            display.AppendLine($"DAYCARE {FormatTime(shiftDirector.ShiftTime)}  |  NEXT INCIDENT {FormatTime(shiftDirector.NextIncidentTime - shiftDirector.ShiftTime)}");
            display.AppendLine($"CULPRITS CAUGHT {shiftDirector.ResolvedIncidentCount}  |  MONKEYS {shiftDirector.MonkeyCount}");
            display.AppendLine(shiftDirector.FeedCooldownRemaining > 0f
                ? $"BANANAS {Mathf.CeilToInt(shiftDirector.FeedCooldownRemaining)}s  |  TIME-OUT {shiftDirector.ActiveTimeOutCount}/{shiftDirector.TimeOutCapacity}"
                : $"BANANAS READY  |  TIME-OUT {shiftDirector.ActiveTimeOutCount}/{shiftDirector.TimeOutCapacity}");
            display.AppendLine($"CAMERA: {SelectedZone.DisplayName.ToUpperInvariant()}");
            display.AppendLine();

            foreach (MonkeyActor monkey in shiftDirector.Monkeys)
            {
                if (monkey.CurrentZone != SelectedZone)
                    continue;

                string selected = monkey == selectedMonkey ? "> " : "  ";
                display.AppendLine($"{selected}{monkey.DisplayName}  H:{monkey.Hunger:0} T:{monkey.Trust:0} N:{monkey.Naughtiness:0}  {monkey.Activity}");
            }

            display.AppendLine();
            display.AppendLine(selectedMonkey != null
                ? $"SELECTED: {selectedMonkey.DisplayName.ToUpperInvariant()}"
                : "CLICK A MONKEY TO SELECT");
            display.AppendLine("[F] FEED SELECTED  [C] CLEAN  [T] TIME-OUT SELECTED");
            display.AppendLine("[M] YOUR MONKEYS");
            display.AppendLine("[LEFT/RIGHT] CHANGE CAMERA");
        }
        else if (shiftDirector.Phase == ShiftPhase.IncidentWindow)
        {
            DaycareIncident incident = shiftDirector.CurrentIncident;
            display.AppendLine($">> {incident.Definition.alertTitle} <<");
            display.AppendLine($"INCIDENT — {Mathf.CeilToInt(shiftDirector.IncidentWindowRemaining)}s");
            display.AppendLine();

            if (SelectedZone != null)
            {
                display.AppendLine($"CAMERA: {SelectedZone.DisplayName.ToUpperInvariant()}");

                foreach (MonkeyActor monkey in shiftDirector.Monkeys)
                {
                    if (monkey.CurrentZone != SelectedZone)
                        continue;

                    display.AppendLine($"{monkey.DisplayName}  {monkey.Activity}");
                }
            }

            display.AppendLine();
            display.AppendLine(incident.WasCaughtOnCamera
                ? "DIRECT EVIDENCE RECORDED"
                : "CRIME MISSED — DO YOU TRUST THE MONKEYS?");
            display.AppendLine("[SPACE] GO TO LINEUP");
        }
        else if (shiftDirector.Phase == ShiftPhase.Lineup)
        {
            DaycareIncident incident = shiftDirector.CurrentIncident;
            display.AppendLine(incident.Definition.alertTitle);
            display.AppendLine(incident.Definition.description);
            display.AppendLine($"It happened in {incident.Zone.DisplayName}.");
            display.AppendLine(incident.WasCaughtOnCamera
                ? $"CAMERA RECORDING: {incident.Culprit.DisplayName} caused the incident."
                : incident.HasFallbackEvidence
                    ? $"PHYSICAL CLUE: {incident.Culprit.DisplayName} caused the incident."
                : "NO DIRECT FOOTAGE. Read the monkeys carefully.");
            display.AppendLine(incident.Definition.effectDescription);
            display.AppendLine();

            foreach (MonkeyWitnessStatement statement in incident.WitnessStatements)
                display.AppendLine(statement.Text);

            display.AppendLine();
            display.AppendLine("LINEUP — ONE GUESS");

            for (int index = 0; index < shiftDirector.LineupCandidates.Count; index++)
            {
                MonkeyActor candidate = shiftDirector.LineupCandidates[index];
                string state = shiftDirector.WasLineupMonkeyAccused(candidate)
                    ? "  (ALREADY QUESTIONED)"
                    : string.Empty;
                display.AppendLine($"[{index + 1}] {candidate.DisplayName}{state}");
            }

            display.AppendLine("[CLICK MONKEY OR 1-4] ACCUSE");
        }
        else
        {
            display.AppendLine(shiftDirector.StatusMessage);
        }

        if (!string.IsNullOrWhiteSpace(lastActionMessage))
        {
            display.AppendLine();
            display.AppendLine(lastActionMessage);
        }

        statusText.text = display.ToString();
    }

    private TMP_Text FindStatusText()
    {
        foreach (TMP_Text text in FindObjectsByType<TMP_Text>(FindObjectsSortMode.None))
        {
            if (text.gameObject.name == "info_text")
                return text;
        }

        return null;
    }

    private static string FormatTime(float time)
    {
        int seconds = Mathf.Max(0, Mathf.CeilToInt(time));
        return $"{seconds / 60:00}:{seconds % 60:00}";
    }

    public void SetCameraGlitching(bool enabled)
    {
        cameraGlitching = enabled;
        cameraGlitchRemaining = enabled ? glitchTransitionDuration : 0f;

        if (!enabled && shakeRemaining <= 0f && surveillanceCamera != null)
        {
            surveillanceCamera.transform.position = stableCameraPosition;
            surveillanceCamera.orthographicSize = stableCameraSize;
        }
    }

    public void ShakeCamera(float duration)
    {
        ShakeCamera(duration, impactShakeAmount);
    }

    private void ShakeCamera(float duration, float amount)
    {
        shakeDuration = Mathf.Max(0.01f, duration);
        shakeRemaining = shakeDuration;
        shakeAmount = Mathf.Max(0f, amount);
    }

    public void PlayTimeoutLanding(MonkeyActor monkey)
    {
        if (monkey == null || shiftDirector == null ||
            shiftDirector.Phase != ShiftPhase.Active ||
            !IsMonkeyVisibleInSelectedFeed(monkey))
            return;

        ShakeCamera(timeoutLandingShakeDuration, timeoutLandingShakeAmount);
    }

    // Glitch and shake compose onto the same cached stable transform so a grenade
    // going off during console interference does not fight the glitch for the camera.
    private void UpdateCameraGlitch()
    {
        if (surveillanceCamera == null)
            return;

        if (shakeRemaining > 0f)
            shakeRemaining = Mathf.Max(0f, shakeRemaining - Time.unscaledDeltaTime);

        if (cameraGlitching)
        {
            cameraGlitchRemaining = Mathf.Max(
                0f,
                cameraGlitchRemaining - Time.unscaledDeltaTime);

            if (cameraGlitchRemaining <= 0f)
                cameraGlitching = false;
        }

        bool shaking = shakeRemaining > 0f;

        if (!cameraGlitching && !shaking)
        {
            if (cameraWasDisturbed)
            {
                surveillanceCamera.transform.position = stableCameraPosition;
                surveillanceCamera.orthographicSize = stableCameraSize;
                cameraWasDisturbed = false;
            }

            return;
        }

        cameraWasDisturbed = true;
        Vector3 offset = Vector3.zero;
        float sizeScale = 1f;

        if (cameraGlitching)
        {
            float time = Time.unscaledTime * 28f;
            float falloff = cameraGlitchRemaining / Mathf.Max(0.01f, glitchTransitionDuration);
            offset += new Vector3(
                Mathf.Sin(time) * glitchShakeAmount * falloff,
                Mathf.Cos(time * 1.7f) * glitchShakeAmount * 0.7f * falloff,
                0f);
            sizeScale *= 1f + Mathf.Abs(Mathf.Sin(time * 0.6f)) * glitchZoomAmount * falloff;
        }

        if (shaking)
        {
            float falloff = shakeRemaining / shakeDuration;
            float shakeTime = Time.unscaledTime * 55f;
            offset += new Vector3(
                Mathf.Sin(shakeTime) * shakeAmount * falloff,
                Mathf.Cos(shakeTime * 1.3f) * shakeAmount * 0.8f * falloff,
                0f
            );
        }

        surveillanceCamera.transform.position = stableCameraPosition + offset;
        surveillanceCamera.orthographicSize = stableCameraSize * sizeScale;
    }

    public void FocusFirstFeed()
    {
        SelectFeed(0, true);
    }

    private void StopCameraDisturbance()
    {
        cameraGlitching = false;
        cameraGlitchRemaining = 0f;
        shakeRemaining = 0f;
        shakeDuration = 0f;
        shakeAmount = 0f;
        cameraWasDisturbed = false;

        if (surveillanceCamera == null)
            return;

        surveillanceCamera.transform.position = stableCameraPosition;
        surveillanceCamera.orthographicSize = stableCameraSize;
    }
    private void SetNormalCctvEffects()
    {
        if (colorAdjustments != null)
        {
            colorAdjustments.contrast.value = normalContrast;
            colorAdjustments.saturation.value = normalSaturation;
        }

        if (filmGrain != null)
            filmGrain.intensity.value = normalGrain;

        if (chromaticAberration != null)
            chromaticAberration.intensity.value = normalChromaticAberration;

        if (lensDistortion != null)
            lensDistortion.intensity.value = normalLensDistortion;
    }

}
