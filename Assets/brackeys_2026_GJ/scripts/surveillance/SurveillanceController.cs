using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public class SurveillanceController : MonoBehaviour
{
    [Header("References")]
    public ProcRoomGen roomGenerator;
    public CaseDirector caseDirector;
    public Camera surveillanceCamera;

    [Header("Camera")]
    [Min(0f)] public float roomPadding = 0.5f;
    public float cameraDepth = -10f;

    [Header("Controls")]
    public bool enableKeyboardControls = true;
    [Min(0f)] public float feedSwitchCooldown = 1f;

    [Header("UI")]
    public TMP_Text cooldownText;
    public TMP_Text statusText;
    [Header("Switch Effect")]
    [SerializeField] private float switchDuration = 0.1f;
    [SerializeField] private float cameraShakeAmount = 0.08f;
    [SerializeField]
    private AnimationCurve switchShakeCurve =
        AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    private bool isSwitching;
    private readonly List<GeneratedRoom> feeds = new List<GeneratedRoom>();
    private int selectedFeedIndex = -1;
    private float nextFeedSwitchTime;
    private bool isRecordingObservation;

    public int FeedCount => feeds.Count;
    public int SelectedFeedIndex => selectedFeedIndex;
    public bool CanSwitchFeed => Time.unscaledTime >= nextFeedSwitchTime;
    public float FeedSwitchCooldownRemaining => Mathf.Max(0f, nextFeedSwitchTime - Time.unscaledTime);
    public GeneratedRoom SelectedFeed =>
        selectedFeedIndex >= 0 && selectedFeedIndex < feeds.Count
            ? feeds[selectedFeedIndex]
            : null;

    private IEnumerator Start()
    {
        if (roomGenerator == null)
            roomGenerator = GetComponent<ProcRoomGen>();

        if (caseDirector == null)
            caseDirector = GetComponent<CaseDirector>();

        if (surveillanceCamera == null)
            surveillanceCamera = Camera.main;

        while (roomGenerator != null && !roomGenerator.HasValidLayout)
            yield return null;

        RefreshFeeds();
        SelectFeed(0, true);
    }

    private void Update()
    {
        UpdateObservationRecording();
        UpdateCooldownText();
        UpdateStatusText();

        if (!enableKeyboardControls || Keyboard.current == null)
            return;

        if (caseDirector != null && caseDirector.IsBlackoutActive)
            return;

        if (Keyboard.current.leftArrowKey.wasPressedThisFrame)
            SelectPreviousFeed();

        if (Keyboard.current.rightArrowKey.wasPressedThisFrame)
            SelectNextFeed();

        if (Keyboard.current.bKey.wasPressedThisFrame)
            AddBookmark();
    }

    public void RefreshFeeds()
    {
        feeds.Clear();

        if (roomGenerator == null)
            return;

        foreach (GeneratedRoom room in roomGenerator.Rooms)
            feeds.Add(room);

        selectedFeedIndex = -1;
    }

    public void SelectNextFeed()
    {
        if (feeds.Count == 0)
            return;

        SelectFeed((selectedFeedIndex + 1 + feeds.Count) % feeds.Count);
    }

    public void SelectPreviousFeed()
    {
        if (feeds.Count == 0)
            return;

        SelectFeed((selectedFeedIndex - 1 + feeds.Count) % feeds.Count);
    }

    public void SelectFeed(int feedIndex)
    {
        SelectFeed(feedIndex, false);
    }

    private bool SelectFeed(int feedIndex, bool bypassCooldown)
    {
        if (feedIndex < 0 || feedIndex >= feeds.Count || surveillanceCamera == null)
            return false;

        if (!bypassCooldown && caseDirector != null && caseDirector.IsBlackoutActive)
            return false;

        if (feedIndex == selectedFeedIndex)
            return false;

        if (!bypassCooldown && !CanSwitchFeed)
            return false;

        if (isSwitching)
            return false;

        if (bypassCooldown)
        {
            ApplyFeed(feedIndex);
            return true;
        }

        StartCoroutine(SwitchFeedRoutine(feedIndex));

        return true;
    }
    private IEnumerator SwitchFeedRoutine(int feedIndex)
    {
        isSwitching = true;

        EndCurrentObservation();

        Transform cameraTransform = surveillanceCamera.transform;
        Vector3 originalPosition = cameraTransform.position;

        float elapsed = 0f;

        while (elapsed < switchDuration * 0.5f)
        {
            elapsed += Time.unscaledDeltaTime;

            float t = Mathf.Clamp01(elapsed / (switchDuration * 0.5f));
            float shake = switchShakeCurve.Evaluate(t) * cameraShakeAmount;

            cameraTransform.position = originalPosition + new Vector3(
                Random.Range(-shake, shake),
                Random.Range(-shake, shake),
                0f
            );

            yield return null;
        }
        ApplyFeed(feedIndex);

        elapsed = 0f;

        while (elapsed < switchDuration * 0.5f)
        {
            elapsed += Time.unscaledDeltaTime;

            float t = Mathf.Clamp01(elapsed / (switchDuration * 0.5f));
            float shake = switchShakeCurve.Evaluate(1f - t) * cameraShakeAmount;

            Vector3 targetPosition = cameraTransform.position;

            cameraTransform.position = targetPosition + new Vector3(
                Random.Range(-shake, shake),
                Random.Range(-shake, shake),
                0f
            );

            yield return null;
        }

        ApplyFeed(feedIndex);

        nextFeedSwitchTime = Time.unscaledTime + feedSwitchCooldown;

        isSwitching = false;
    }
    private void ApplyFeed(int feedIndex)
    {
        selectedFeedIndex = feedIndex;

        GeneratedRoom room = feeds[selectedFeedIndex];

        surveillanceCamera.transform.position = new Vector3(
            room.Center.x * roomGenerator.cellSize,
            room.Center.y * roomGenerator.cellSize,
            cameraDepth
        );

        if (surveillanceCamera.orthographic)
        {
            float halfRoomWidth =
                room.Bounds.width * roomGenerator.cellSize * 0.5f
                + roomPadding;

            float halfRoomHeight =
                room.Bounds.height * roomGenerator.cellSize * 0.5f
                + roomPadding;

            surveillanceCamera.orthographicSize = Mathf.Max(
                halfRoomHeight,
                halfRoomWidth / surveillanceCamera.aspect
            );
        }

        BeginCurrentObservation(room.Role);
    }
    public void AddBookmark()
    {
        if (SelectedFeed == null)
            return;

        Record(CaseEventType.BookmarkAdded, SelectedFeed.Role);
    }

    private void Record(CaseEventType eventType, string roomRole)
    {
        if (caseDirector == null || caseDirector.Timeline == null)
            return;

        caseDirector.Timeline.Record(
            caseDirector.CaseTime,
            eventType,
            roomRole: roomRole
        );
    }

    private void UpdateObservationRecording()
    {
        if (caseDirector != null && caseDirector.IsRunning && !caseDirector.IsBlackoutActive)
        {
            if (!isRecordingObservation && SelectedFeed != null)
                BeginCurrentObservation(SelectedFeed.Role);

            return;
        }

        EndCurrentObservation();
    }

    private void BeginCurrentObservation(string roomRole)
    {
        if (caseDirector == null || caseDirector.Timeline == null || !caseDirector.IsRunning)
            return;

        caseDirector.Timeline.BeginCameraObservation(caseDirector.CaseTime, roomRole);
        Record(CaseEventType.CameraFeedSelected, roomRole);
        isRecordingObservation = true;
    }

    private void EndCurrentObservation()
    {
        if (!isRecordingObservation)
            return;

        if (caseDirector != null && caseDirector.Timeline != null)
            caseDirector.Timeline.EndCameraObservation(caseDirector.CaseTime);

        isRecordingObservation = false;
    }

    private void UpdateCooldownText()
    {
        if (cooldownText == null)
            return;

        float remaining = FeedSwitchCooldownRemaining;
        cooldownText.text = remaining > 0f
            ? $"SWITCH: {remaining:0.00}"
            : "SWITCH READY";
    }

    private void UpdateStatusText()
    {
        if (statusText == null)
            return;

        string feedLabel = SelectedFeed == null
            ? "CAMERA: --"
            : $"CAMERA: {SelectedFeed.Role.ToUpperInvariant()}";

        if (caseDirector == null || caseDirector.caseDefinition == null)
        {
            statusText.text = $"NO CASE LOADED\n{feedLabel}";
            return;
        }

        if (caseDirector.Timeline == null || caseDirector.Timeline.Events.Count == 0)
        {
            statusText.text = $"PREPARING CASE\n{feedLabel}";
            return;
        }

        if (!caseDirector.IsRunning)
        {
            statusText.text = $"RECORDING COMPLETE\n{feedLabel}";
            return;
        }

        if (caseDirector.IsBlackoutActive)
        {
            statusText.text = "CAMERA BLACKOUT\nSIGNAL LOST";
            return;
        }

        float remainingTime = caseDirector.caseDefinition.durationSeconds - caseDirector.CaseTime;
        statusText.text = $"RECORDING {FormatTime(remainingTime)}\n{feedLabel}\nARROW KEYS: SWITCH CAMERA";
    }

    private static string FormatTime(float time)
    {
        int totalSeconds = Mathf.CeilToInt(Mathf.Max(0f, time));
        return $"{totalSeconds / 60:00}:{totalSeconds % 60:00}";
    }
}
