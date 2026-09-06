using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(AudioSource))]
public sealed class DaycareAudioController : MonoBehaviour
{
    [Header("Gameplay")]
    [SerializeField] private ShiftDirector shiftDirector;
    [SerializeField] private EnclosureSurveillanceController surveillanceController;

    [Header("Music")]
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private AudioClip menuTrack;
    [SerializeField] private AudioClip gameplayTrack;
    [Range(0f, 1f)] [SerializeField] private float musicVolume = 0.45f;
    [SerializeField] private bool playMusicOnAwake = true;

    [Header("Loops")]
    [SerializeField] private AudioClip ambienceLoop;
    [Range(0f, 1f)] [SerializeField] private float ambienceVolume = 0.22f;

    [Header("Interface")]
    [SerializeField] private AudioClip uiHover;
    [SerializeField] private AudioClip uiClick;
    [SerializeField] private AudioClip careActionPop;
    [Range(0f, 1f)] [SerializeField] private float uiVolume = 0.7f;
    [SerializeField] private Color hoverTint = new Color(1f, 0.92f, 0.78f, 1f);

    [Header("Incidents")]
    [SerializeField] private AudioClip incidentAlarm;
    [SerializeField] private AudioClip lineupCrowd;
    [SerializeField] private AudioClip lineupThunk;
    [Range(0f, 1f)] [SerializeField] private float incidentVolume = 0.75f;
    [Range(0f, 1f)] [SerializeField] private float lineupThunkVolume = 0.95f;

    [Header("Monkeys")]
    [SerializeField] private AudioClip[] monkeyVoices;
    [SerializeField] private AudioClip[] footsteps;
    [Range(0f, 1f)] [SerializeField] private float monkeyVolume = 0.32f;
    [Range(0f, 1f)] [SerializeField] private float footstepVolume = 0.2f;
    [SerializeField, Min(0.1f)] private float footstepInterval = 0.42f;
    [SerializeField] private Vector2 chatterInterval = new Vector2(5f, 11f);

    [Header("Extra Monkey Noises")]
    [SerializeField] private Vector2 extraMonkeyNoiseInterval = new Vector2(0.2f, 0.55f);
    [SerializeField, Range(1, 8)] private int extraMonkeyNoiseSourceCount = 6;
    [SerializeField, Range(1, 4)] private int extraMonkeyNoiseMaxBurst = 3;
    [SerializeField, Range(0f, 1f)] private float extraMonkeyNoiseBurstChance = 0.45f;
    [SerializeField] private Vector2 extraMonkeyNoisePitchRange = new Vector2(0.85f, 1.2f);
    [SerializeField, Range(0f, 1f)] private float extraMonkeyNoiseVolume = 0.24f;
    [SerializeField, Range(0f, 1f)] private float extraMonkeyNoiseStereoSpread = 0.75f;

    private readonly List<Button> registeredButtons = new List<Button>();
    private readonly List<HoverRegistration> hoverRegistrations = new List<HoverRegistration>();
    private readonly List<AudioSource> extraMonkeyVoiceSources = new List<AudioSource>();
    private AudioSource ambienceSource;
    private AudioSource uiSource;
    private AudioSource effectsSource;
    private AudioSource footstepSource;
    private ShiftPhase previousPhase;
    private float nextFootstepTime;
    private float nextChatterTime;
    private float nextExtraMonkeyNoiseTime;
    private int nextFootstepIndex;
    private int nextExtraMonkeyVoiceSource;
    private bool incidentMuted;
    private bool zoolagSequenceMuted;
    private bool managedMusicMuteActive;
    private bool muteStateBeforeManagedMute;
    private float musicVolumeMultiplier = 1f;
    private float sfxVolumeMultiplier = 1f;
    private bool extraMonkeyNoisesEnabled = true;
    private bool chatterPlaybackAllowed;
    private DaycareGameFlowPresenter gameFlowPresenter;

    private void Awake()
    {
        if (shiftDirector == null)
            shiftDirector = GetComponent<ShiftDirector>();

        if (surveillanceController == null)
            surveillanceController = GetComponent<EnclosureSurveillanceController>();
        gameFlowPresenter = GetComponentInChildren<DaycareGameFlowPresenter>(true);

        ConfigureMusicSource();
        ambienceSource = CreateSource(ambienceVolume, true);
        uiSource = CreateSource(uiVolume, false);
        effectsSource = CreateSource(1f, false);
        footstepSource = CreateSource(footstepVolume, false);
        CreateExtraMonkeyVoiceSources();

        if (ambienceLoop != null)
        {
            ambienceSource.clip = ambienceLoop;
            ambienceSource.Play();
        }

        SwitchMusic(menuTrack, playMusicOnAwake);
        previousPhase = shiftDirector != null ? shiftDirector.Phase : ShiftPhase.Preparing;
        ScheduleNextChatter();
        ScheduleNextExtraMonkeyNoise();
    }

    private void OnEnable()
    {
        if (shiftDirector != null)
        {
            shiftDirector.IncidentOccurred += HandleIncident;
            shiftDirector.CareActionCompleted += HandleCareAction;
            shiftDirector.AccusationResolved += HandleAccusation;
        }

        if (surveillanceController != null)
        {
            surveillanceController.SelectedMonkeyChanged += HandleMonkeySelected;
            surveillanceController.TimeoutMonkeyClicked += HandleTimeoutMonkeyClicked;
        }

        RegisterButtons();
    }

    private void OnDisable()
    {
        if (shiftDirector != null)
        {
            shiftDirector.IncidentOccurred -= HandleIncident;
            shiftDirector.CareActionCompleted -= HandleCareAction;
            shiftDirector.AccusationResolved -= HandleAccusation;
        }

        if (surveillanceController != null)
        {
            surveillanceController.SelectedMonkeyChanged -= HandleMonkeySelected;
            surveillanceController.TimeoutMonkeyClicked -= HandleTimeoutMonkeyClicked;
        }

        UnregisterButtons();
        StopExtraMonkeyNoises();
    }

    private void OnValidate()
    {
        ConfigureMusicSource();

        if (musicSource != null && !Application.isPlaying)
            musicSource.clip = menuTrack;
    }

    private void Update()
    {
        UpdateButtonHoverTints();

        if (shiftDirector == null)
            return;

        ShiftPhase phase = shiftDirector.Phase;

        if (phase != previousPhase)
        {
            if (phase == ShiftPhase.Preparing)
                SwitchMusic(menuTrack, true);
            else if (previousPhase == ShiftPhase.Preparing)
                SwitchMusic(gameplayTrack, true);

            if (phase == ShiftPhase.Lineup)
                PlayEffect(lineupCrowd, incidentVolume);

            previousPhase = phase;
        }

        bool canPlayChatter =
            (phase == ShiftPhase.Active || phase == ShiftPhase.IncidentWindow) &&
            (gameFlowPresenter == null || !gameFlowPresenter.IsMenuBlockingInput);

        if (!canPlayChatter)
        {
            if (chatterPlaybackAllowed)
                StopExtraMonkeyNoises();

            chatterPlaybackAllowed = false;
            return;
        }

        if (!chatterPlaybackAllowed)
        {
            chatterPlaybackAllowed = true;
            ScheduleNextChatter();
            ScheduleNextExtraMonkeyNoise();
        }

        UpdateFootsteps();
        UpdateMonkeyChatter();
    }

    public void SetIncidentMuted(bool muted)
    {
        incidentMuted = muted;
        RefreshManagedMusicMute();
    }

    public void SetZoolagSequenceMuted(bool muted)
    {
        zoolagSequenceMuted = muted;
        RefreshManagedMusicMute();
    }

    private void RefreshManagedMusicMute()
    {
        if (musicSource == null)
            return;

        bool shouldMute = incidentMuted || zoolagSequenceMuted;

        if (shouldMute)
        {
            if (!managedMusicMuteActive)
            {
                muteStateBeforeManagedMute = musicSource.mute;
                managedMusicMuteActive = true;
            }

            musicSource.mute = true;
            return;
        }

        if (!managedMusicMuteActive)
            return;

        managedMusicMuteActive = false;
        musicSource.mute = muteStateBeforeManagedMute;
        muteStateBeforeManagedMute = false;
    }

    public void SetMusicVolume(float normalizedVolume)
    {
        musicVolumeMultiplier = Mathf.Clamp01(normalizedVolume);

        if (musicSource != null)
            musicSource.volume = musicVolume * musicVolumeMultiplier;
    }

    public void SetSfxVolume(float normalizedVolume)
    {
        sfxVolumeMultiplier = Mathf.Clamp01(normalizedVolume);

        if (ambienceSource != null)
            ambienceSource.volume = ambienceVolume * sfxVolumeMultiplier;
        if (uiSource != null)
            uiSource.volume = uiVolume * sfxVolumeMultiplier;
        if (footstepSource != null)
            footstepSource.volume = footstepVolume * sfxVolumeMultiplier;

        foreach (AudioSource source in extraMonkeyVoiceSources)
            if (source != null)
                source.volume = extraMonkeyNoiseVolume * sfxVolumeMultiplier;
    }

    public void SetExtraMonkeyNoises(bool enabled)
    {
        extraMonkeyNoisesEnabled = enabled;

        if (enabled)
            nextExtraMonkeyNoiseTime = Time.unscaledTime + 0.05f;
        else
            StopExtraMonkeyNoises();
    }

    private void HandleIncident(DaycareIncident incident)
    {
        PlayEffect(incidentAlarm, incidentVolume);
    }

    private void HandleCareAction()
    {
        if (careActionPop != uiClick)
            PlayEffect(careActionPop, uiVolume);
    }

    private void HandleAccusation(bool correct)
    {
        if (correct)
        {
            PlayEffect(careActionPop, uiVolume);
            return;
        }

        PlayRandomMonkeyVoice();
    }

    private void HandleMonkeySelected(MonkeyActor monkey)
    {
        if (monkey != null)
            PlayUiClick();
    }

    private void HandleTimeoutMonkeyClicked(MonkeyActor monkey)
    {
        PlayRandomMonkeyVoice();
    }

    private void RegisterButtons()
    {
        UnregisterButtons();

        foreach (Button button in FindObjectsByType<Button>(
                     FindObjectsInactive.Include,
                     FindObjectsSortMode.None))
        {
            if (button == null)
                continue;

            button.onClick.AddListener(PlayUiClick);
            registeredButtons.Add(button);
            RegisterHover(button);
        }
    }

    private void UnregisterButtons()
    {
        foreach (Button button in registeredButtons)
        {
            if (button != null)
                button.onClick.RemoveListener(PlayUiClick);
        }

        foreach (HoverRegistration registration in hoverRegistrations)
        {
            if (registration.Trigger != null && registration.Trigger.triggers != null)
            {
                registration.Trigger.triggers.Remove(registration.EnterEntry);
                registration.Trigger.triggers.Remove(registration.ExitEntry);
            }

            RestoreButtonTint(registration);
        }

        registeredButtons.Clear();
        hoverRegistrations.Clear();
    }

    private void RegisterHover(Button button)
    {
        EventTrigger trigger = button.GetComponent<EventTrigger>();

        if (trigger == null)
            trigger = button.gameObject.AddComponent<EventTrigger>();

        if (trigger.triggers == null)
            trigger.triggers = new List<EventTrigger.Entry>();

        EventTrigger.Entry enterEntry = new EventTrigger.Entry
        {
            eventID = EventTriggerType.PointerEnter
        };
        EventTrigger.Entry exitEntry = new EventTrigger.Entry
        {
            eventID = EventTriggerType.PointerExit
        };
        HoverRegistration registration = new HoverRegistration(
            button,
            trigger,
            enterEntry,
            exitEntry,
            button.targetGraphic,
            button.targetGraphic != null ? button.targetGraphic.color : Color.white);

        enterEntry.callback.AddListener(_ => HandleButtonPointerEnter(registration));
        exitEntry.callback.AddListener(_ => HandleButtonPointerExit(registration));
        trigger.triggers.Add(enterEntry);
        trigger.triggers.Add(exitEntry);
        hoverRegistrations.Add(registration);
    }

    private void HandleButtonPointerEnter(HoverRegistration registration)
    {
        registration.Hovered = true;
        ApplyButtonTint(registration);

        if (registration.Button != null &&
            registration.Button.IsActive() &&
            registration.Button.interactable)
        {
            PlayImmediate(uiSource, uiHover, uiVolume * sfxVolumeMultiplier);
        }
    }

    private void HandleButtonPointerExit(HoverRegistration registration)
    {
        registration.Hovered = false;
        RestoreButtonTint(registration);
    }

    private void UpdateButtonHoverTints()
    {
        foreach (HoverRegistration registration in hoverRegistrations)
        {
            if (registration.Hovered)
                ApplyButtonTint(registration);
        }
    }

    private void ApplyButtonTint(HoverRegistration registration)
    {
        if (registration.Button == null || registration.TargetGraphic == null)
            return;

        bool shouldTint = registration.Hovered &&
                          registration.Button.IsActive() &&
                          registration.Button.interactable &&
                          registration.Button.transition == Selectable.Transition.SpriteSwap;

        registration.TargetGraphic.color = shouldTint
            ? registration.NormalColor * hoverTint
            : registration.NormalColor;
    }

    private static void RestoreButtonTint(HoverRegistration registration)
    {
        if (registration.TargetGraphic != null)
            registration.TargetGraphic.color = registration.NormalColor;
    }

    private void PlayUiClick()
    {
        PlayImmediate(uiSource, uiClick, uiVolume * sfxVolumeMultiplier);
    }

    private void UpdateFootsteps()
    {
        if (Time.unscaledTime < nextFootstepTime || footsteps == null || footsteps.Length == 0)
            return;

        bool visibleMonkeyMoving = false;

        foreach (MonkeyActor monkey in shiftDirector.Monkeys)
        {
            if (monkey == null || !monkey.IsMoving)
                continue;

            MonkeySpriteAnimator animator = monkey.GetComponent<MonkeySpriteAnimator>();

            if (animator != null && animator.IsCameraVisible)
            {
                visibleMonkeyMoving = true;
                break;
            }
        }

        if (!visibleMonkeyMoving)
            return;

        AudioClip clip = footsteps[nextFootstepIndex % footsteps.Length];
        nextFootstepIndex++;
        PlayImmediate(footstepSource, clip, footstepVolume * sfxVolumeMultiplier);
        nextFootstepTime = Time.unscaledTime + footstepInterval;
    }

    private void UpdateMonkeyChatter()
    {
        if (Time.unscaledTime >= nextChatterTime)
        {
            PlayRandomMonkeyVoice();
            ScheduleNextChatter();
        }

        if (!extraMonkeyNoisesEnabled || Time.unscaledTime < nextExtraMonkeyNoiseTime)
            return;

        PlayExtraMonkeyNoiseBurst();
        ScheduleNextExtraMonkeyNoise();
    }

    private void PlayRandomMonkeyVoice()
    {
        if (monkeyVoices == null || monkeyVoices.Length == 0)
            return;

        PlayEffect(monkeyVoices[Random.Range(0, monkeyVoices.Length)], monkeyVolume);
    }

    private void ScheduleNextChatter()
    {
        float minimum = Mathf.Max(0.1f, Mathf.Min(chatterInterval.x, chatterInterval.y));
        float maximum = Mathf.Max(minimum, Mathf.Max(chatterInterval.x, chatterInterval.y));
        nextChatterTime = Time.unscaledTime + Random.Range(minimum, maximum);
    }

    public void PlayLineupThunk()
    {
        PlayEffect(lineupThunk, lineupThunkVolume);
    }

    private void ScheduleNextExtraMonkeyNoise()
    {
        float minimum = Mathf.Max(
            0.05f,
            Mathf.Min(extraMonkeyNoiseInterval.x, extraMonkeyNoiseInterval.y));
        float maximum = Mathf.Max(
            minimum,
            Mathf.Max(extraMonkeyNoiseInterval.x, extraMonkeyNoiseInterval.y));
        nextExtraMonkeyNoiseTime = Time.unscaledTime + Random.Range(minimum, maximum);
    }

    private void CreateExtraMonkeyVoiceSources()
    {
        extraMonkeyVoiceSources.Clear();
        int sourceCount = Mathf.Max(1, extraMonkeyNoiseSourceCount);

        for (int index = 0; index < sourceCount; index++)
            extraMonkeyVoiceSources.Add(CreateSource(extraMonkeyNoiseVolume, false));
    }

    private void PlayExtraMonkeyNoiseBurst()
    {
        if (monkeyVoices == null || monkeyVoices.Length == 0 ||
            extraMonkeyVoiceSources.Count == 0)
            return;

        int maximumBurst = Mathf.Clamp(
            extraMonkeyNoiseMaxBurst,
            1,
            extraMonkeyVoiceSources.Count);
        int burstCount = 1;

        while (burstCount < maximumBurst && Random.value < extraMonkeyNoiseBurstChance)
            burstCount++;

        float minimumPitch = Mathf.Min(
            extraMonkeyNoisePitchRange.x,
            extraMonkeyNoisePitchRange.y);
        float maximumPitch = Mathf.Max(
            minimumPitch,
            Mathf.Max(extraMonkeyNoisePitchRange.x, extraMonkeyNoisePitchRange.y));

        for (int index = 0; index < burstCount; index++)
        {
            AudioSource source = extraMonkeyVoiceSources[
                nextExtraMonkeyVoiceSource % extraMonkeyVoiceSources.Count];
            nextExtraMonkeyVoiceSource++;

            if (source == null)
                continue;

            source.Stop();
            source.clip = monkeyVoices[Random.Range(0, monkeyVoices.Length)];
            source.pitch = Random.Range(minimumPitch, maximumPitch);
            source.panStereo = Random.Range(
                -extraMonkeyNoiseStereoSpread,
                extraMonkeyNoiseStereoSpread);
            source.volume = extraMonkeyNoiseVolume * sfxVolumeMultiplier;
            source.PlayDelayed(index * 0.035f);
        }
    }

    private void StopExtraMonkeyNoises()
    {
        foreach (AudioSource source in extraMonkeyVoiceSources)
        {
            if (source == null)
                continue;

            source.Stop();
            source.clip = null;
            source.pitch = 1f;
            source.panStereo = 0f;
        }
    }

    private void PlayEffect(AudioClip clip, float volume)
    {
        if (effectsSource != null && clip != null)
            effectsSource.PlayOneShot(clip, volume * sfxVolumeMultiplier);
    }

    private static void PlayImmediate(AudioSource source, AudioClip clip, float volume)
    {
        if (source == null || clip == null)
            return;

        source.clip = clip;
        source.volume = volume;
        source.Play();
    }

    private void SwitchMusic(AudioClip track, bool play)
    {
        if (musicSource == null || track == null)
            return;

        bool trackChanged = musicSource.clip != track;

        if (trackChanged)
        {
            musicSource.Stop();
            musicSource.clip = track;
        }

        if (play && (trackChanged || !musicSource.isPlaying))
            musicSource.Play();
    }

    private AudioSource CreateSource(float volume, bool loop)
    {
        AudioSource source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = loop;
        source.spatialBlend = 0f;
        source.volume = volume * sfxVolumeMultiplier;
        return source;
    }

    private void ConfigureMusicSource()
    {
        if (musicSource == null)
            musicSource = GetComponent<AudioSource>();

        if (musicSource == null)
            return;

        musicSource.loop = true;
        musicSource.playOnAwake = false;
        musicSource.spatialBlend = 0f;
        musicSource.volume = musicVolume * musicVolumeMultiplier;
    }

    private sealed class HoverRegistration
    {
        public HoverRegistration(
            Button button,
            EventTrigger trigger,
            EventTrigger.Entry enterEntry,
            EventTrigger.Entry exitEntry,
            Graphic targetGraphic,
            Color normalColor)
        {
            Button = button;
            Trigger = trigger;
            EnterEntry = enterEntry;
            ExitEntry = exitEntry;
            TargetGraphic = targetGraphic;
            NormalColor = normalColor;
        }

        public Button Button { get; }
        public EventTrigger Trigger { get; }
        public EventTrigger.Entry EnterEntry { get; }
        public EventTrigger.Entry ExitEntry { get; }
        public Graphic TargetGraphic { get; }
        public Color NormalColor { get; }
        public bool Hovered { get; set; }
    }
}
