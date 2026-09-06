using MoreMountains.Feedbacks;
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class MonkeySpriteAnimator : MonoBehaviour
{
    private enum AlertType
    {
        None,
        Hunger,
        Naughty,
        Poo
    }

    private Coroutine alertRoutine;

    private bool hungerAlertActive;
    private bool naughtyAlertActive;
    private bool pooAlertActive;

    private AlertType lastDebugAlertType = AlertType.None;
    private static readonly Color ShineTint = new Color32(0xF3, 0xEB, 0xC4, 0xFF);

    [SerializeField] private SpriteRenderer behindRenderer;
    [SerializeField] private MMF_Player hoverFeedbacks;
    [SerializeField] private SpriteRenderer bodyRenderer;
    [SerializeField] private SpriteRenderer faceRenderer;
    [SerializeField] private SpriteRenderer frontAccessoryRenderer;
    [SerializeField] private SpriteRenderer timeoutBoxRenderer;
    [SerializeField] private SortingGroup sortingGroup;
    [SerializeField] private AudioClip timeoutFallingSound;
    [SerializeField] private AudioClip timeoutLandingSound;
    [SerializeField, Range(0f, 1f)] private float timeoutFallingVolume = 0.7f;
    [SerializeField, Range(0f, 1f)] private float timeoutLandingVolume = 0.9f;
    [SerializeField] private Sprite[] bodyFrames;
    [SerializeField] private Sprite[] faceFrames;
    [SerializeField] private Sprite[] hairFrames;
    [SerializeField] private Sprite[] hair2Frames;
    [SerializeField] private Sprite[] hair3Frames;
    [SerializeField] private Sprite[] hairBowFrames;
    [SerializeField] private Sprite[] bowtieFrames;
    [SerializeField] private Sprite[] hatBehindFrames;
    [SerializeField] private Sprite[] blueHatBehindFrames;
    [SerializeField] private Sprite[] sunglassesFrames;
    [SerializeField] private Sprite[] earringFrames;
    [SerializeField] private Sprite[] shineFrames;
    [SerializeField] private Sprite[] timeoutBoxFrames;
    [SerializeField, Min(0.1f)] private float idleFramesPerSecond = 3.5f;
    [SerializeField, Min(0.1f)] private float walkFramesPerSecond = 7f;
    [SerializeField, Min(0.1f)] private float runFramesPerSecond = 9f;
    [SerializeField, Min(0.1f)] private float snackFramesPerSecond = 4.5f;
    [SerializeField, Min(0)] private int waveFirstFrame = 12;
    [SerializeField, Min(0.1f)] private float timeoutBoxDropHeight = 7f;
    [SerializeField, Min(0.05f)] private float timeoutBoxDropDuration = 0.38f;
    [SerializeField, Min(0.1f)] private float timeoutBoxFramesPerSecond = 7f;
    [SerializeField] private float timeoutBoxLandedHeight = 0.25f;
    [SerializeField] private int baseSortingOrder = 30;
    [SerializeField, Min(0.1f)] private float ySortingScale = 4f;
    [SerializeField] private int minimumSortingOrder = 6;
    [SerializeField] private int maximumSortingOrder = 55;
    [SerializeField] private SpriteRenderer boxRenderer;
    [SerializeField] private SpriteRenderer iconRenderer;
    [SerializeField] private Sprite[] boxFrames;

    [SerializeField] private Sprite[] hungerFrames;
    [SerializeField] private Sprite[] naughtyFrames;
    [SerializeField] private Sprite[] pooFrames;

    [SerializeField, Min(0.1f)] private float alertFramesPerSecond = 8f;
    [SerializeField, Min(0.1f)] private float alertDuration = 2f;
    [SerializeField, Min(0.1f)] private float alertMinInterval = 10f;
    [SerializeField, Min(0.1f)] private float alertMaxInterval = 20f;
    private MonkeyActor monkeyActor;
    private float animationOffset;
    private bool offsetInitialized;
    private Sprite[] activeFrontAccessoryFrames;
    private Sprite[] activeBehindFrames;
    private Color configuredBodyColor = Color.white;
    private bool hovered;
    private bool cameraVisible = true;
    private bool timeoutBoxVisible;
    private Coroutine timeoutBoxDropRoutine;
    private AudioSource timeoutAudioSource;
    private MonkeyActivity displayedActivity;
    private float activityAnimationStartedAt;
    private bool activityAnimationInitialized;

    public float PoopingAnimationDuration => 4f / Mathf.Max(0.1f, snackFramesPerSecond);

    public Sprite PortraitSprite
    {
        get
        {
            EnsureReady();
            return bodyFrames != null && bodyFrames.Length > 0 ? bodyFrames[0] : null;
        }
    }

    public Sprite PortraitBehindSprite
    {
        get
        {
            EnsureReady();
            return GetFirstFrame(activeBehindFrames);
        }
    }

    public Sprite PortraitFaceSprite
    {
        get
        {
            EnsureReady();
            return GetFirstFrame(faceFrames);
        }
    }

    public Sprite PortraitFrontAccessorySprite
    {
        get
        {
            EnsureReady();
            return GetFirstFrame(activeFrontAccessoryFrames);
        }
    }

    public SpriteRenderer BodyRenderer
    {
        get
        {
            EnsureReady();
            return bodyRenderer;
        }
    }

    public SpriteRenderer BehindRenderer
    {
        get
        {
            EnsureReady();
            return behindRenderer;
        }
    }

    public SpriteRenderer FaceRenderer
    {
        get
        {
            EnsureReady();
            return faceRenderer;
        }
    }

    public SpriteRenderer FrontAccessoryRenderer
    {
        get
        {
            EnsureReady();
            return frontAccessoryRenderer;
        }
    }

    public Color PortraitFrontAccessoryColor => IsActiveHairAccessory()
        ? configuredBodyColor
        : IsActiveShineAccessory()
            ? ShineTint
            : Color.white;

    public bool IsCameraVisible => cameraVisible;

    public void Configure(MonkeyProfile profile)
    {
        EnsureReady();

        activeFrontAccessoryFrames = profile.accessory switch
        {
            MonkeyAccessory.Bowtie => bowtieFrames,
            MonkeyAccessory.Hair => hairFrames,
            MonkeyAccessory.Hair2 => hair2Frames,
            MonkeyAccessory.Hair3 => hair3Frames,
            MonkeyAccessory.HairBow => hairBowFrames,
            MonkeyAccessory.Sunglasses => sunglassesFrames,
            MonkeyAccessory.Earring => earringFrames,
            MonkeyAccessory.Shine => shineFrames,
            _ => null
        };
        activeBehindFrames = profile.accessory switch
        {
            MonkeyAccessory.Hat => hatBehindFrames,
            MonkeyAccessory.BlueHat => blueHatBehindFrames,
            _ => null
        };
        configuredBodyColor = profile.displayColor;

        SetBodyColor(profile.displayColor);

        if (behindRenderer != null)
            behindRenderer.color = Color.white;

        if (faceRenderer != null)
            faceRenderer.color = Color.white;

        if (frontAccessoryRenderer != null)
            frontAccessoryRenderer.color = profile.accessory switch
            {
                MonkeyAccessory.Hair => profile.displayColor,
                MonkeyAccessory.Hair2 => profile.displayColor,
                MonkeyAccessory.Hair3 => profile.displayColor,
                MonkeyAccessory.Shine => ShineTint,
                _ => Color.white
            };

        SetFrame(0);
        HideTimeoutBox();
    }

    public void SetBodyColor(Color bodyColor)
    {
        EnsureReady();

        if (bodyRenderer != null)
            bodyRenderer.color = bodyColor;

        if (frontAccessoryRenderer != null && IsActiveHairAccessory())
            frontAccessoryRenderer.color = bodyColor;
    }

    public void SetHovered(bool value)
    {
        EnsureReady();

        if (hovered == value)
            return;

        hovered = value;

        if (hovered && hoverFeedbacks != null)
            hoverFeedbacks.PlayFeedbacks();
    }

    public bool TryGetLayerFrame(
        int frameIndex,
        out Sprite body,
        out Sprite face,
        out Sprite behindAccessory,
        out Sprite frontAccessory
    )
    {
        EnsureReady();
        body = GetFrame(bodyFrames, frameIndex);
        face = GetFrame(faceFrames, frameIndex);
        behindAccessory = GetFrame(activeBehindFrames, frameIndex);
        frontAccessory = GetFrame(activeFrontAccessoryFrames, frameIndex);
        return body != null;
    }

    private static Sprite GetFirstFrame(Sprite[] frames)
    {
        return frames != null && frames.Length > 0 ? frames[0] : null;
    }

    private static Sprite GetFrame(Sprite[] frames, int frameIndex)
    {
        return frames != null && frameIndex >= 0 && frameIndex < frames.Length
            ? frames[frameIndex]
            : null;
    }

    public void SetCameraVisible(bool visible)
    {
        EnsureReady();

        if (cameraVisible == visible)
            return;

        cameraVisible = visible;

        if (!cameraVisible)
            SetRendererVisibility(false);
    }

    public void PlayTimeoutBoxDrop(Action onLanded)
    {
        EnsureReady();

        if (timeoutBoxDropRoutine != null)
            StopCoroutine(timeoutBoxDropRoutine);

        if (timeoutBoxRenderer == null || timeoutBoxFrames == null ||
            timeoutBoxFrames.Length == 0)
        {
            onLanded?.Invoke();
            return;
        }

        timeoutBoxVisible = true;
        timeoutBoxRenderer.sprite = timeoutBoxFrames[0];
        timeoutBoxRenderer.enabled = cameraVisible;
        timeoutBoxRenderer.transform.localPosition = new Vector3(
            0f,
            timeoutBoxDropHeight,
            timeoutBoxRenderer.transform.localPosition.z
        );
        PlayTimeoutFallingSound();
        timeoutBoxDropRoutine = StartCoroutine(DropTimeoutBox(onLanded));
    }

    public void HideTimeoutBox()
    {
        if (timeoutBoxDropRoutine != null)
        {
            StopCoroutine(timeoutBoxDropRoutine);
            timeoutBoxDropRoutine = null;
        }

        timeoutBoxVisible = false;
        StopTimeoutSound();

        if (timeoutBoxRenderer != null)
            timeoutBoxRenderer.enabled = false;
    }
    private IEnumerator DropTimeoutBox(Action onLanded)
    {
        float elapsed = 0f;
        float duration = Mathf.Max(0.05f, timeoutBoxDropDuration);
        Vector3 start = timeoutBoxRenderer.transform.localPosition;
        Vector3 landed = new Vector3(0f, timeoutBoxLandedHeight, start.z);

        while (elapsed < duration)
        {
            float progress = Mathf.Clamp01(elapsed / duration);
            float accelerated = progress * progress;
            timeoutBoxRenderer.transform.localPosition = Vector3.LerpUnclamped(
                start,
                landed,
                accelerated
            );
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        timeoutBoxRenderer.transform.localPosition = landed;
        timeoutBoxDropRoutine = null;
        PlayTimeoutLandingSound();
        onLanded?.Invoke();
    }

    private void PlayTimeoutFallingSound()
    {
        if (timeoutAudioSource == null || timeoutFallingSound == null)
            return;

        timeoutAudioSource.Stop();
        timeoutAudioSource.clip = timeoutFallingSound;
        timeoutAudioSource.loop = true;
        timeoutAudioSource.volume = timeoutFallingVolume;
        timeoutAudioSource.Play();
    }

    private void PlayTimeoutLandingSound()
    {
        if (timeoutAudioSource == null)
            return;

        timeoutAudioSource.Stop();
        timeoutAudioSource.loop = false;
        timeoutAudioSource.clip = null;
        timeoutAudioSource.volume = 1f;

        if (timeoutLandingSound != null)
            timeoutAudioSource.PlayOneShot(timeoutLandingSound, timeoutLandingVolume);
    }

    private void StopTimeoutSound()
    {
        if (timeoutAudioSource == null)
            return;

        timeoutAudioSource.Stop();
        timeoutAudioSource.loop = false;
        timeoutAudioSource.clip = null;
    }

    private void Awake()
    {
        EnsureReady();
        StartAlertRoutine();
    }

    private void LateUpdate()
    {
        EnsureReady();

        if (bodyRenderer == null || monkeyActor == null)
            return;

        UpdateSortingOrder();
        UpdateActivityAnimationClock();

        if (monkeyActor.Activity == MonkeyActivity.Pooping &&
            Time.time - activityAnimationStartedAt >= PoopingAnimationDuration)
            monkeyActor.NotifyPoopingAnimationCompleted();

        if (!cameraVisible)
        {
            SetRendererVisibility(false);
            return;
        }

        UpdateTimeoutBoxFrame();

        SetFlipX(monkeyActor.FacingDirectionX < 0f);

        bool moving = monkeyActor.IsMoving;
        int startFrame;
        float framesPerSecond;

        if (moving)
        {
            startFrame = monkeyActor.IsRunning ? 60 : 4;
            framesPerSecond = monkeyActor.IsRunning
                ? runFramesPerSecond
                : walkFramesPerSecond;
        }
        else
        {
            startFrame = GetActivityStartFrame(monkeyActor.Activity);
            framesPerSecond = startFrame == 0
                ? idleFramesPerSecond
                : snackFramesPerSecond;
        }

        if (bodyFrames == null || bodyFrames.Length < startFrame + 4)
            return;

        float animationTime = !moving && monkeyActor.Activity == MonkeyActivity.Pooping
            ? Time.time - activityAnimationStartedAt
            : Time.time + animationOffset;
        int frameIndex = Mathf.FloorToInt(animationTime * framesPerSecond);
        SetFrame(startFrame + frameIndex % 4);
    }
    public void ShowHunger(bool veryHungry)
    {

        hungerAlertActive = veryHungry;
    }

    public void HideHunger()
    {
        ShowHunger(false);
    }

    public void SetNaughtyAlertActive(bool active)
    {
        naughtyAlertActive = active;
    }

    public void ShowNaughtyAlert()
    {
        SetNaughtyAlertActive(true);
    }

    public void HideNaughtyAlert()
    {
        SetNaughtyAlertActive(false);
    }

    public void SetPooAlertActive(bool active)
    {
        pooAlertActive = active;
    }

    public void ShowPooAlert()
    {
        SetPooAlertActive(true);
    }

    public void HidePooAlert()
    {
        SetPooAlertActive(false);
    }

    private void StartAlertRoutine()
    {
        if (alertRoutine == null)
        {
            alertRoutine = StartCoroutine(AlertLoop());
        }
    }

    private IEnumerator AlertLoop()
    {
        while (true)
        {
            float waitTime = UnityEngine.Random.Range(
                alertMinInterval,
                alertMaxInterval
            );
            yield return new WaitForSeconds(waitTime);

            AlertType alertType = GetActiveAlertType();

            if (alertType == AlertType.None)
            {
                HideAlertRenderers();
                continue;
            }

            Sprite[] frames = GetAlertFrames(alertType);

            if (frames == null || frames.Length == 0)
            {

                HideAlertRenderers();
                continue;
            }

            if (boxFrames == null || boxFrames.Length == 0)
            {
                HideAlertRenderers();
                continue;
            }
            float elapsed = 0f;

            while (elapsed < alertDuration)
            {
                AlertType currentType = GetActiveAlertType();

                if (currentType != alertType)
                {
                    HideAlertRenderers();
                    break;
                }

                if (cameraVisible && iconRenderer != null && boxRenderer != null)
                {
                    int bubbleFrame = Mathf.FloorToInt(
                        elapsed * alertFramesPerSecond
                    ) % boxFrames.Length;

                    int iconFrame = Mathf.FloorToInt(
                        elapsed * alertFramesPerSecond
                    ) % frames.Length;

                    boxRenderer.sprite = boxFrames[bubbleFrame];
                    iconRenderer.sprite = frames[iconFrame];

                    // The frame exists ONLY while the icon exists.
                    iconRenderer.enabled = true;
                    boxRenderer.enabled = iconRenderer.enabled;

                    if (!lastDebugAlertType.Equals(alertType))
                    {
                        lastDebugAlertType = alertType;
                    }
                }
                else
                {
                    HideAlertRenderers();
                }

                elapsed += Time.deltaTime;
                yield return null;
            }

            HideAlertRenderers();
            lastDebugAlertType = AlertType.None;
        }
    }

    private AlertType GetActiveAlertType()
    {
        // Priority:
        // Hunger > Naughty > Poo

        if (hungerAlertActive)
            return AlertType.Hunger;

        if (naughtyAlertActive)
            return AlertType.Naughty;

        if (pooAlertActive)
            return AlertType.Poo;

        return AlertType.None;
    }

    private Sprite[] GetAlertFrames(AlertType type)
    {
        return type switch
        {
            AlertType.Hunger => hungerFrames,
            AlertType.Naughty => naughtyFrames,
            AlertType.Poo => pooFrames,
            _ => null
        };
    }

    private void HideAlertRenderers()
    {
        if (boxRenderer != null)
            boxRenderer.enabled = false;

        if (iconRenderer != null)
            iconRenderer.enabled = false;
    }
    private void UpdateActivityAnimationClock()
    {
        if (activityAnimationInitialized && displayedActivity == monkeyActor.Activity)
            return;

        displayedActivity = monkeyActor.Activity;
        activityAnimationStartedAt = Time.time;
        activityAnimationInitialized = true;
    }

    private void UpdateSortingOrder()
    {
        if (sortingGroup == null)
            return;

        float relativeY = monkeyActor.CurrentZone != null
            ? transform.position.y - monkeyActor.CurrentZone.Center.y
            : transform.position.y;
        int order = baseSortingOrder - Mathf.RoundToInt(relativeY * ySortingScale);
        sortingGroup.sortingOrder = Mathf.Clamp(
            order,
            minimumSortingOrder,
            maximumSortingOrder
        );
    }

    private void UpdateTimeoutBoxFrame()
    {
        if (timeoutBoxRenderer == null)
            return;

        timeoutBoxRenderer.enabled = timeoutBoxVisible && cameraVisible;

        if (!timeoutBoxRenderer.enabled || timeoutBoxFrames == null ||
            timeoutBoxFrames.Length == 0)
            return;

        int frameIndex = Mathf.FloorToInt(
            Time.unscaledTime * Mathf.Max(0.1f, timeoutBoxFramesPerSecond)
        ) % timeoutBoxFrames.Length;
        timeoutBoxRenderer.sprite = timeoutBoxFrames[frameIndex];
    }

    private int GetActivityStartFrame(MonkeyActivity activity)
    {
        return activity switch
        {
            MonkeyActivity.Snacking => 8,
            MonkeyActivity.ShowingOff => 12,
            MonkeyActivity.Socialising => 16,
            MonkeyActivity.Sulking => 20,
            MonkeyActivity.HidingLoot => 20,
            MonkeyActivity.TimeOut => 24,
            MonkeyActivity.Napping => 24,
            MonkeyActivity.Pooping => 28,
            MonkeyActivity.Fighting => 32,
            MonkeyActivity.Angry => 36,
            MonkeyActivity.Running => 0,
            MonkeyActivity.Panicking => 64,
            MonkeyActivity.Waving => waveFirstFrame,
            _ => 0
        };
    }

    private bool IsActiveHairAccessory()
    {
        return activeFrontAccessoryFrames == hairFrames ||
            activeFrontAccessoryFrames == hair2Frames ||
            activeFrontAccessoryFrames == hair3Frames;
    }

    private bool IsActiveShineAccessory()
    {
        return activeFrontAccessoryFrames == shineFrames;
    }

    private static bool IsHairAccessory(MonkeyAccessory accessory)
    {
        return accessory == MonkeyAccessory.Hair ||
            accessory == MonkeyAccessory.Hair2 ||
            accessory == MonkeyAccessory.Hair3;
    }

    private void EnsureReady()
    {
        if (bodyRenderer == null)
            bodyRenderer = GetComponent<SpriteRenderer>();

        if (monkeyActor == null)
            monkeyActor = GetComponent<MonkeyActor>();

        if (sortingGroup == null)
            sortingGroup = GetComponent<SortingGroup>();

        if (timeoutAudioSource == null)
        {
            timeoutAudioSource = GetComponent<AudioSource>();

            if (timeoutAudioSource == null)
                timeoutAudioSource = gameObject.AddComponent<AudioSource>();

            timeoutAudioSource.playOnAwake = false;
            timeoutAudioSource.spatialBlend = 0f;
        }

        if (!offsetInitialized)
        {
            animationOffset = Mathf.Abs(GetInstanceID() % 1000) / 173f;
            offsetInitialized = true;
        }
    }

    private void SetFrame(int frameIndex)
    {
        SetRendererFrame(bodyRenderer, bodyFrames, frameIndex, true);
        SetRendererFrame(faceRenderer, faceFrames, frameIndex, true);
        SetRendererFrame(frontAccessoryRenderer, activeFrontAccessoryFrames, frameIndex, false);
        SetRendererFrame(behindRenderer, activeBehindFrames, frameIndex, false);
    }

    private static void SetRendererFrame(
        SpriteRenderer renderer,
        Sprite[] frames,
        int frameIndex,
        bool requiredLayer
    )
    {
        if (renderer == null)
            return;

        Sprite frame = frames != null && frameIndex >= 0 && frameIndex < frames.Length
            ? frames[frameIndex]
            : null;

        if (frame != null)
            renderer.sprite = frame;

        renderer.enabled = requiredLayer
            ? renderer.sprite != null
            : frame != null;
    }

    private void SetFlipX(bool flipX)
    {
        if (behindRenderer != null)
            behindRenderer.flipX = flipX;
        if (bodyRenderer != null)
            bodyRenderer.flipX = flipX;
        if (faceRenderer != null)
            faceRenderer.flipX = flipX;
        if (frontAccessoryRenderer != null)
            frontAccessoryRenderer.flipX = flipX;
    }

    private void SetRendererVisibility(bool visible)
    {
        if (behindRenderer != null)
            behindRenderer.enabled = visible && behindRenderer.sprite != null;

        if (bodyRenderer != null)
            bodyRenderer.enabled = visible;

        if (faceRenderer != null)
            faceRenderer.enabled = visible;

        if (frontAccessoryRenderer != null)
            frontAccessoryRenderer.enabled =
                visible && frontAccessoryRenderer.sprite != null;

        if (timeoutBoxRenderer != null)
            timeoutBoxRenderer.enabled =
                visible && timeoutBoxVisible && timeoutBoxRenderer.sprite != null;

        if (boxRenderer != null)
            boxRenderer.enabled =
                visible && boxRenderer.sprite != null;
        if (iconRenderer != null)
            iconRenderer.enabled =
                visible && iconRenderer.sprite != null;
    }
}
