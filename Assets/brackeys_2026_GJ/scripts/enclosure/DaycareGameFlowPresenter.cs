using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class DaycareGameFlowPresenter : MonoBehaviour
{
    private enum TutorialStep
    {
        Inactive,
        SelectMonkey,
        FeedMonkey,
        CleanMess,
        TimeOutMonkey,
        SwitchCamera,
        FindEvidence,
        EnterLineup,
        SolveLineup,
        Complete
    }

    [System.Serializable]
    private sealed class NewcomerArrivalSlot
    {
        public GameObject root;
        public TMP_Text nameText;
        public Image behind;
        public Image body;
        public Image face;
        public Image frontAccessory;
    }

    [Header("Gameplay")]
    [SerializeField] private ShiftDirector shiftDirector;
    [SerializeField] private EnclosureSurveillanceController surveillanceController;

    [Header("Authored Panels")]
    [SerializeField] private GameObject splashPanel;
    [SerializeField] private GameObject titlePanel;
    [SerializeField] private GameObject resultPanel;
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private GameObject infoPanel;
    [SerializeField] private GameObject creditsPanel;
    [SerializeField] private GameObject settingsPanel;

    [Header("Title Screen")]
    [SerializeField] private TMP_Text splashPrompt;
    [SerializeField, Min(0.1f)] private float splashFlashSpeed = 2f;
    [SerializeField] private GameObject studioIntroPanel;
    [SerializeField] private Image studioIntroImage;
    [SerializeField] private Sprite[] studioLogoSprites;
    [SerializeField, Min(0f)] private float studioLogoFadeDuration = 0.35f;
    [SerializeField, Min(0f)] private float studioLogoHoldDuration = 1.25f;
    [SerializeField, Min(0f)] private float studioLogoGapDuration = 0.15f;
    [SerializeField] private TMP_Text briefingText;
    [SerializeField] private Button startButton;
    [SerializeField] private Button howToPlayButton;
    [SerializeField] private Button titleSettingsButton;
    [SerializeField] private Button creditsButton;
    [SerializeField] private Button titleQuitButton;

    [Header("Pause Menu")]
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button restartButton;
    [SerializeField] private Button pauseSettingsButton;
    [SerializeField] private Button pauseTitleButton;
    [SerializeField] private Button pauseQuitButton;

    [Header("Information Panel")]
    [SerializeField] private TMP_Text infoTitleText;
    [SerializeField] private TMP_Text infoBodyText;
    [SerializeField] private Button infoBackButton;
    [SerializeField, TextArea(5, 14)] private string howToPlayContent =
        "KEEP EVERY MONKEY FED, CALM AND CLEAN.\n\n" +
        "CLICK A MONKEY TO SELECT THEM. USE THE CONSOLE BUTTONS TO FEED, CLEAN OR SEND THEM TO TIME-OUT. CHANGE CAMERAS TO WATCH EVERY ROOM.\n\n" +
        "WHEN THE ALARM SOUNDS, WATCH THE LINE-UP. TRUST IS A PATTERN: USE WHO EACH MONKEY POINTS AT AND HOW RELIABLE THEY ARE TO FIND THE CULPRIT. ONE WRONG ZOOLAG PICK ENDS THE RUN.\n\n" +
        "M  MONKEY ROSTER     ESC / P  PAUSE";

    [Header("Credits Panel")]
    [SerializeField] private TMP_Text creditsBodyText;
    [SerializeField] private Button creditsBackButton;
    [SerializeField, TextArea(5, 14)] private string creditsContent =
        "CREATED FOR BRACKEYS GAME JAM 2026\n\n" +
        "GAME DIRECTION, LEAD DEVELOPMENT\nAUDIO AND INTEGRATION\nISAAC LYNE\n\n" +
        "ART DIRECTION, ANIMATION AND ARTWORK\nANDREW ONORATO\n\n" +
        "ADDITIONAL PROGRAMMING\nPATHFINDING AND EFFECTS\nFINN\n\n" +
        "ADDITIONAL PROGRAMMING\nITEMS, POO AND ALARM SYSTEMS\nMUSTAFA\n\n" +
        "THANK YOU FOR PLAYING";

    [Header("Settings")]
    [SerializeField] private DaycareAudioController audioController;
    [SerializeField] private Slider masterVolumeSlider;
    [SerializeField] private Slider musicVolumeSlider;
    [SerializeField] private Slider sfxVolumeSlider;
    [SerializeField] private Toggle fullscreenToggle;
    [SerializeField] private Toggle extraMonkeyNoisesToggle;
    [SerializeField] private Button settingsBackButton;
    [SerializeField] private Sprite settingsTickSprite;
    [SerializeField] private Sprite settingsTickEmptySprite;

    [Header("Result Screen")]
    [SerializeField] private TMP_Text resultTitleText;
    [SerializeField] private TMP_Text resultSummaryText;
    [SerializeField] private Button replayButton;
    [SerializeField] private Button titleButton;

    [Header("Progress HUD")]
    [SerializeField] private GameObject progressHud;
    [SerializeField] private TMP_Text progressHudText;

    [Header("Guided Tutorial")]
    [SerializeField, Range(0.1f, 1f)] private float tutorialTimeScale = 0.45f;

    [Header("Newcomer Arrival")]
    [SerializeField] private GameObject newcomerArrivalPanel;
    [SerializeField] private TMP_Text newcomerArrivalTitleText;
    [SerializeField] private NewcomerArrivalSlot[] newcomerArrivalSlots;
    [SerializeField] private Button newcomerArrivalConfirmButton;

    [Header("Zoolag Confirmation")]
    [SerializeField] private GameObject zoolagConfirmationPanel;
    [SerializeField] private TMP_Text zoolagConfirmationText;
    [SerializeField] private Image zoolagMonkeyBehindPreview;
    [SerializeField] private Image zoolagMonkeyPreview;
    [SerializeField] private Image zoolagMonkeyFacePreview;
    [SerializeField] private Image zoolagMonkeyFrontAccessoryPreview;
    [SerializeField] private Button zoolagCancelButton;
    [SerializeField] private Button zoolagConfirmButton;

    [Header("Zoolag Sequence")]
    [SerializeField] private GameObject zoolagSequencePanel;
    [SerializeField] private ZoolagSequencePresenter zoolagSequencePresenter;

    private ShiftPhase previousPhase = ShiftPhase.Preparing;
    private int pendingLineupNumber;
    private Coroutine zoolagRoutine;
    private Coroutine studioIntroRoutine;
    private Color pendingZoolagMonkeyColor = Color.white;
    private EnclosureMonkeyRosterPresenter monkeyRoster;
    private DaycareItemManager itemManager;
    private bool paused;
    private bool settingsOpenedFromPause;
    private float timeScaleBeforePause = 1f;
    private bool tutorialActive;
    private bool tutorialWaitingForContinue;
    private TutorialStep tutorialStep = TutorialStep.Inactive;
    private TutorialStep tutorialNextStep = TutorialStep.Inactive;
    private MonkeyActor tutorialMonkey;
    private MonkeyActor tutorialFighterOne;
    private MonkeyActor tutorialFighterTwo;
    private string tutorialStartingZoneId;
    private string tutorialHudMessage;
    private float tutorialIncidentCountdown;
    private float tutorialEvidenceRevealCountdown;
    private Image masterVolumeFillImage;
    private Image musicVolumeFillImage;
    private Image sfxVolumeFillImage;

    private const string MasterVolumeKey = "daycare.masterVolume";
    private const string MusicVolumeKey = "daycare.musicVolume";
    private const string SfxVolumeKey = "daycare.sfxVolume";
    private const string FullscreenKey = "daycare.fullscreen";
    private const string ExtraMonkeyNoisesKey = "daycare.extraMonkeyNoises";

    public bool IsAccusationFlowOpen =>
        zoolagConfirmationPanel != null && zoolagConfirmationPanel.activeSelf ||
        zoolagSequencePanel != null && zoolagSequencePanel.activeSelf;

    public bool IsNewcomerArrivalOpen =>
        newcomerArrivalPanel != null && newcomerArrivalPanel.activeSelf;

    public bool IsPaused => paused;
    public bool IsTutorialActive => tutorialActive;
    public bool IsTutorialInstructionOpen => tutorialActive && tutorialWaitingForContinue;
    public float DesiredGameplayTimeScale => tutorialActive
        ? Mathf.Clamp(tutorialTimeScale, 0.1f, 1f)
        : 1f;

    public bool IsMenuBlockingInput =>
        paused ||
        IsAccusationFlowOpen ||
        IsNewcomerArrivalOpen ||
        studioIntroPanel != null && studioIntroPanel.activeSelf ||
        titlePanel != null && titlePanel.activeSelf ||
        resultPanel != null && resultPanel.activeSelf ||
        infoPanel != null && infoPanel.activeSelf ||
        creditsPanel != null && creditsPanel.activeSelf ||
        settingsPanel != null && settingsPanel.activeSelf;

    private void Awake()
    {
        if (shiftDirector == null)
            shiftDirector = GetComponentInParent<ShiftDirector>();
        if (surveillanceController == null)
            surveillanceController = GetComponentInParent<EnclosureSurveillanceController>();
        if (audioController == null)
            audioController = GetComponentInParent<DaycareAudioController>();
        itemManager = GetComponentInParent<DaycareItemManager>();
        monkeyRoster = GetComponentInParent<EnclosureMonkeyRosterPresenter>();
        ConfigureSettingsArtwork();

        if (startButton != null)
            startButton.onClick.AddListener(StartShift);
        if (howToPlayButton != null)
            howToPlayButton.onClick.AddListener(ShowHowToPlay);
        if (titleSettingsButton != null)
            titleSettingsButton.onClick.AddListener(OpenTitleSettings);
        if (creditsButton != null)
            creditsButton.onClick.AddListener(ShowCredits);
        if (titleQuitButton != null)
            titleQuitButton.onClick.AddListener(QuitGame);
        if (resumeButton != null)
            resumeButton.onClick.AddListener(ResumeGame);
        if (restartButton != null)
            restartButton.onClick.AddListener(RestartShift);
        if (pauseSettingsButton != null)
            pauseSettingsButton.onClick.AddListener(OpenPauseSettings);
        if (pauseTitleButton != null)
            pauseTitleButton.onClick.AddListener(ReturnToTitle);
        if (pauseQuitButton != null)
            pauseQuitButton.onClick.AddListener(QuitGame);
        if (infoBackButton != null)
            infoBackButton.onClick.AddListener(HandleInfoButton);
        if (creditsBackButton != null)
            creditsBackButton.onClick.AddListener(CloseCreditsPanel);
        if (settingsBackButton != null)
            settingsBackButton.onClick.AddListener(CloseSettingsPanel);
        if (masterVolumeSlider != null)
            masterVolumeSlider.onValueChanged.AddListener(SetMasterVolume);
        if (musicVolumeSlider != null)
            musicVolumeSlider.onValueChanged.AddListener(SetMusicVolume);
        if (sfxVolumeSlider != null)
            sfxVolumeSlider.onValueChanged.AddListener(SetSfxVolume);
        if (fullscreenToggle != null)
            fullscreenToggle.onValueChanged.AddListener(SetFullscreen);
        if (extraMonkeyNoisesToggle != null)
            extraMonkeyNoisesToggle.onValueChanged.AddListener(SetExtraMonkeyNoises);
        if (replayButton != null)
            replayButton.onClick.AddListener(StartShift);
        if (titleButton != null)
            titleButton.onClick.AddListener(ReturnToTitle);
        if (zoolagCancelButton != null)
            zoolagCancelButton.onClick.AddListener(CancelAccusation);
        if (zoolagConfirmButton != null)
            zoolagConfirmButton.onClick.AddListener(ConfirmAccusation);
        if (newcomerArrivalConfirmButton != null)
            newcomerArrivalConfirmButton.onClick.AddListener(ConfirmNewcomerArrival);
        if (surveillanceController != null)
        {
            surveillanceController.SelectedMonkeyChanged += HandleTutorialMonkeySelected;
            surveillanceController.CameraChanged += HandleTutorialCameraChanged;
        }
        if (shiftDirector != null)
            shiftDirector.CareActionCompleted += HandleTutorialCareAction;
        if (itemManager != null)
            itemManager.PooCleaned += HandleTutorialPooCleaned;
        LoadSettings();
        ShowTitle();
        SetPanelActive(splashPanel, true);
        SetPanelActive(studioIntroPanel, HasStudioLogos());
    }

    private void Start()
    {
        if (HasStudioLogos())
            studioIntroRoutine = StartCoroutine(PlayStudioIntro());
    }

    private void OnDestroy()
    {
        if (startButton != null)
            startButton.onClick.RemoveListener(StartShift);
        if (howToPlayButton != null)
            howToPlayButton.onClick.RemoveListener(ShowHowToPlay);
        if (titleSettingsButton != null)
            titleSettingsButton.onClick.RemoveListener(OpenTitleSettings);
        if (creditsButton != null)
            creditsButton.onClick.RemoveListener(ShowCredits);
        if (titleQuitButton != null)
            titleQuitButton.onClick.RemoveListener(QuitGame);
        if (resumeButton != null)
            resumeButton.onClick.RemoveListener(ResumeGame);
        if (restartButton != null)
            restartButton.onClick.RemoveListener(RestartShift);
        if (pauseSettingsButton != null)
            pauseSettingsButton.onClick.RemoveListener(OpenPauseSettings);
        if (pauseTitleButton != null)
            pauseTitleButton.onClick.RemoveListener(ReturnToTitle);
        if (pauseQuitButton != null)
            pauseQuitButton.onClick.RemoveListener(QuitGame);
        if (infoBackButton != null)
            infoBackButton.onClick.RemoveListener(HandleInfoButton);
        if (creditsBackButton != null)
            creditsBackButton.onClick.RemoveListener(CloseCreditsPanel);
        if (settingsBackButton != null)
            settingsBackButton.onClick.RemoveListener(CloseSettingsPanel);
        if (masterVolumeSlider != null)
            masterVolumeSlider.onValueChanged.RemoveListener(SetMasterVolume);
        if (musicVolumeSlider != null)
            musicVolumeSlider.onValueChanged.RemoveListener(SetMusicVolume);
        if (sfxVolumeSlider != null)
            sfxVolumeSlider.onValueChanged.RemoveListener(SetSfxVolume);
        if (fullscreenToggle != null)
            fullscreenToggle.onValueChanged.RemoveListener(SetFullscreen);
        if (extraMonkeyNoisesToggle != null)
            extraMonkeyNoisesToggle.onValueChanged.RemoveListener(SetExtraMonkeyNoises);
        if (replayButton != null)
            replayButton.onClick.RemoveListener(StartShift);
        if (titleButton != null)
            titleButton.onClick.RemoveListener(ReturnToTitle);
        if (zoolagCancelButton != null)
            zoolagCancelButton.onClick.RemoveListener(CancelAccusation);
        if (zoolagConfirmButton != null)
            zoolagConfirmButton.onClick.RemoveListener(ConfirmAccusation);
        if (newcomerArrivalConfirmButton != null)
            newcomerArrivalConfirmButton.onClick.RemoveListener(ConfirmNewcomerArrival);
        if (surveillanceController != null)
        {
            surveillanceController.SelectedMonkeyChanged -= HandleTutorialMonkeySelected;
            surveillanceController.CameraChanged -= HandleTutorialCameraChanged;
        }
        if (shiftDirector != null)
            shiftDirector.CareActionCompleted -= HandleTutorialCareAction;
        if (itemManager != null)
            itemManager.PooCleaned -= HandleTutorialPooCleaned;
        if (studioIntroRoutine != null)
            StopCoroutine(studioIntroRoutine);
        StopZoolagRoutine();
        RestoreTimeScale();
    }

    private void Update()
    {
        UpdateSplashPrompt();
        HandleMenuInput();

        if (shiftDirector == null)
            return;

        ShiftPhase phase = shiftDirector.Phase;
        UpdateProgressHud(phase);
        UpdateTutorial();

        if (phase != previousPhase && phase == ShiftPhase.Failed && zoolagRoutine == null)
            ShowResult();

        previousPhase = phase;
    }

    public void StartShift()
    {
        StartShift(false);
    }

    private void StartTutorial()
    {
        if (shiftDirector == null)
        {
            ShowInfo("HOW TO PLAY", howToPlayContent);
            return;
        }

        StartShift(true);
    }

    private void StartShift(bool asTutorial)
    {
        if (shiftDirector == null)
            return;

        RestoreTimeScale();
        ResetTutorialState();
        tutorialActive = asTutorial;
        SetPanelActive(splashPanel, false);
        SetPanelActive(titlePanel, false);
        SetPanelActive(resultPanel, false);
        SetPanelActive(pausePanel, false);
        SetPanelActive(infoPanel, false);
        SetPanelActive(creditsPanel, false);
        SetPanelActive(settingsPanel, false);
        SetPanelActive(zoolagConfirmationPanel, false);
        SetPanelActive(zoolagSequencePanel, false);
        SetPanelActive(newcomerArrivalPanel, false);
        StopZoolagRoutine();
        bool started = asTutorial
            ? shiftDirector.BeginTutorialShift()
            : shiftDirector.BeginShift();

        if (!started)
        {
            ShowTitle();
            return;
        }

        timeScaleBeforePause = asTutorial ? tutorialTimeScale : 1f;
        Time.timeScale = timeScaleBeforePause;
        previousPhase = shiftDirector.Phase;

        if (asTutorial)
        {
            ShowTutorialCard(
                "WELCOME TO MONKEY DAYCARE",
                "THIS IS A QUIET PRACTICE RUN. THE GAME WILL PAUSE BETWEEN EACH LESSON.\n\nFIRST, YOU WILL LEARN HOW TO LOOK AFTER A MONKEY.",
                TutorialStep.SelectMonkey
            );
        }
    }

    public void ReturnToTitle()
    {
        RestoreTimeScale();
        ResetTutorialState();
        Time.timeScale = 1f;
        timeScaleBeforePause = 1f;
        if (shiftDirector != null)
            shiftDirector.PrepareForTitle();

        ShowTitle();
    }

    private void ShowTitle()
    {
        ResetTutorialState();
        SetPanelActive(splashPanel, false);
        SetPanelActive(resultPanel, false);
        SetPanelActive(titlePanel, true);
        SetPanelActive(pausePanel, false);
        SetPanelActive(infoPanel, false);
        SetPanelActive(creditsPanel, false);
        SetPanelActive(settingsPanel, false);
        SetPanelActive(zoolagConfirmationPanel, false);
        SetPanelActive(zoolagSequencePanel, false);
        SetPanelActive(newcomerArrivalPanel, false);
        StopZoolagRoutine();
        SetPanelActive(progressHud, false);

        if (briefingText != null && string.IsNullOrWhiteSpace(briefingText.text) && shiftDirector != null && shiftDirector.Definition != null)
            briefingText.text = shiftDirector.Definition.briefing;

        previousPhase = ShiftPhase.Preparing;
    }

    private void ShowResult()
    {
        RestoreTimeScale();
        SetPanelActive(titlePanel, false);
        SetPanelActive(resultPanel, true);
        SetPanelActive(pausePanel, false);
        SetPanelActive(infoPanel, false);
        SetPanelActive(creditsPanel, false);
        SetPanelActive(settingsPanel, false);
        SetPanelActive(newcomerArrivalPanel, false);
        SetPanelActive(progressHud, false);

        if (resultTitleText != null)
            resultTitleText.text = "GAME OVER";

        if (resultSummaryText == null || shiftDirector == null)
            return;

        resultSummaryText.text =
            $"MONKEYS SENT TO ZOOLAG  <color=#E83726>{shiftDirector.ZoolagSentCount}\n</color>" +
            $"CULPRITS CAUGHT  <color=#E83726>{shiftDirector.ResolvedIncidentCount}\n\n</color>" +
            $"MONKEYS MANAGED  <color=#E83726>{shiftDirector.MonkeyCount}\n</color>" +
            $"DAYCARE TIME  <color=#E83726>{FormatTime(shiftDirector.ShiftTime)}\n</color>" +
            $"FIGHTS  <color=#E83726>{shiftDirector.TotalFightCount}</color>     TIME-OUTS  <color=#E83726>{shiftDirector.TotalTimeoutCount}\n</color>" +
            $"MESSES MADE  <color=#E83726>{shiftDirector.TotalMessCount}</color>";
    }

    private void UpdateProgressHud(ShiftPhase phase)
    {
        bool visible = phase == ShiftPhase.Active || phase == ShiftPhase.IncidentWindow ||
            phase == ShiftPhase.Welcome;
        SetPanelActive(progressHud, visible);

        if (!visible || progressHudText == null)
            return;

        if (tutorialActive && !string.IsNullOrWhiteSpace(tutorialHudMessage))
        {
            progressHudText.text = $"TUTORIAL: {tutorialHudMessage}";
            return;
        }

        float remaining = phase == ShiftPhase.IncidentWindow
            ? shiftDirector.IncidentWindowRemaining
            : phase == ShiftPhase.Welcome
                ? shiftDirector.WelcomeRemaining
                : Mathf.Max(0f, shiftDirector.NextIncidentTime - shiftDirector.ShiftTime);
        bool firstIncidentTutorial = phase == ShiftPhase.IncidentWindow &&
            shiftDirector.ResolvedIncidentCount == 0 && shiftDirector.ZoolagSentCount == 0;

        if (firstIncidentTutorial)
        {
            bool caughtOnCamera = shiftDirector.CurrentIncident != null &&
                shiftDirector.CurrentIncident.WasCaughtOnCamera;
            string evidenceMessage = caughtOnCamera
                ? "DIRECT EVIDENCE RECORDED"
                : "CRIME MISSED - DO YOU TRUST THE MONKEYS?";
            progressHudText.text =
                $"{evidenceMessage}  {FormatTime(remaining)}     MONKEYS  {shiftDirector.MonkeyCount}";
            return;
        }

        string timerLabel = phase == ShiftPhase.IncidentWindow
            ? "INVESTIGATE"
            : phase == ShiftPhase.Welcome ? "WAVING HELLO" : "NEXT INCIDENT";
        progressHudText.text =
            $"{timerLabel}  {FormatTime(remaining)}     MONKEYS  {shiftDirector.MonkeyCount}     CAUGHT  {shiftDirector.ResolvedIncidentCount}";
    }

    public bool RequestAccusation(int lineupNumber, out string message)
    {
        message = string.Empty;

        if (shiftDirector == null || shiftDirector.Phase != ShiftPhase.Lineup ||
            lineupNumber < 1 || lineupNumber > shiftDirector.LineupCandidates.Count ||
            IsAccusationFlowOpen)
            return false;

        MonkeyActor candidate = shiftDirector.LineupCandidates[lineupNumber - 1];
        pendingLineupNumber = lineupNumber;
        message = $"Send {candidate.DisplayName} to the Zoolag?";

        if (zoolagConfirmationText != null)
            zoolagConfirmationText.text =
                $"DO YOU WANT TO SEND\n{candidate.DisplayName.ToUpperInvariant()}\nTO THE ZOOLAG?";

        if (zoolagMonkeyPreview != null)
        {
            MonkeySpriteAnimator animator = candidate.GetComponent<MonkeySpriteAnimator>();
            ApplyPreviewLayer(
                zoolagMonkeyBehindPreview,
                animator != null ? animator.PortraitBehindSprite : null,
                Color.white
            );
            ApplyPreviewLayer(
                zoolagMonkeyPreview,
                animator != null ? animator.PortraitSprite : null,
                candidate.DisplayColor
            );
            ApplyPreviewLayer(
                zoolagMonkeyFacePreview,
                animator != null ? animator.PortraitFaceSprite : null,
                Color.white
            );
            ApplyPreviewLayer(
                zoolagMonkeyFrontAccessoryPreview,
                animator != null ? animator.PortraitFrontAccessorySprite : null,
                animator != null ? animator.PortraitFrontAccessoryColor : Color.white
            );
        }

        SetPanelActive(zoolagConfirmationPanel, true);
        return true;
    }

    private static void ApplyPreviewLayer(Image image, Sprite sprite, Color color)
    {
        if (image == null)
            return;

        image.sprite = sprite;
        image.color = color;
        image.preserveAspect = true;
        image.enabled = sprite != null;
    }

    private void CancelAccusation()
    {
        pendingLineupNumber = 0;
        SetPanelActive(zoolagConfirmationPanel, false);
    }

    private void ConfirmAccusation()
    {
        if (pendingLineupNumber <= 0 || shiftDirector == null)
            return;

        int lineupNumber = pendingLineupNumber;
        MonkeyActor selectedMonkey = lineupNumber <= shiftDirector.LineupCandidates.Count
            ? shiftDirector.LineupCandidates[lineupNumber - 1]
            : null;
        pendingZoolagMonkeyColor = selectedMonkey != null
            ? selectedMonkey.DisplayColor
            : Color.white;
        pendingLineupNumber = 0;
        SetPanelActive(zoolagConfirmationPanel, false);
        shiftDirector.TryAccuse(lineupNumber, out _);
        bool correct = shiftDirector.Phase == ShiftPhase.Zoolag;
        bool resolved = correct || shiftDirector.Phase == ShiftPhase.Failed;

        if (tutorialActive)
        {
            if (correct)
            {
                ShowTutorialCard(
                    "CORRECT!",
                    "YOU FOUND THE CULPRIT AND SENT THEM TO THE ZOOLAG.\n\nIN A REAL RUN, EACH CORRECT ANSWER ADDS MORE MONKEYS AND MAKES THE DAYCARE HARDER.",
                    TutorialStep.Complete
                );
            }
            else
            {
                ShowTutorialCard(
                    "TRY AGAIN",
                    "THE DIRECT-EVIDENCE MONKEY IS THE CULPRIT. LOOK FOR THE BRIGHT HIGHLIGHT, THEN CHOOSE AGAIN.",
                    TutorialStep.SolveLineup
                );
            }

            return;
        }

        if (!resolved)
            return;

        if (zoolagRoutine != null)
            StopCoroutine(zoolagRoutine);

        zoolagRoutine = StartCoroutine(PlayZoolagSequence(correct));
    }

    private IEnumerator PlayZoolagSequence(bool correct)
    {
        if (!correct)
        {
            yield return null;

            while (!FailedLineupContinuePressed())
                yield return null;
        }

        audioController?.SetZoolagSequenceMuted(true);
        SetPanelActive(zoolagSequencePanel, true);
        zoolagSequencePresenter?.Begin(pendingZoolagMonkeyColor);
        float elapsed = 0f;
        float duration = zoolagSequencePresenter != null
            ? zoolagSequencePresenter.Duration
            : 3f;

        while (elapsed < duration)
        {
            zoolagSequencePresenter?.ShowAtTime(elapsed);
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        zoolagSequencePresenter?.Stop();
        SetPanelActive(zoolagSequencePanel, false);
        audioController?.SetZoolagSequenceMuted(false);
        zoolagRoutine = null;

        if (!correct)
        {
            ShowResult();
            yield break;
        }

        ShowNewcomerArrival();
    }

    private static bool FailedLineupContinuePressed()
    {
        bool enterPressed = Keyboard.current != null &&
            (Keyboard.current.enterKey.wasPressedThisFrame ||
                Keyboard.current.numpadEnterKey.wasPressedThisFrame);
        bool clicked = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
        return enterPressed || clicked;
    }

    private void ShowNewcomerArrival()
    {
        if (shiftDirector == null)
            return;

        if (shiftDirector.LatestNewcomers.Count == 0 || newcomerArrivalPanel == null)
        {
            ConfirmNewcomerArrival();
            return;
        }

        if (newcomerArrivalTitleText != null)
            newcomerArrivalTitleText.text = "YOU HAVE NEW MONKEYS!";

        int slotCount = newcomerArrivalSlots != null ? newcomerArrivalSlots.Length : 0;

        for (int index = 0; index < slotCount; index++)
        {
            MonkeyActor newcomer = index < shiftDirector.LatestNewcomers.Count
                ? shiftDirector.LatestNewcomers[index]
                : null;
            PopulateNewcomerSlot(newcomerArrivalSlots[index], newcomer);
        }

        SetPanelActive(newcomerArrivalPanel, true);
    }

    private static void PopulateNewcomerSlot(NewcomerArrivalSlot slot, MonkeyActor newcomer)
    {
        if (slot == null)
            return;

        bool visible = newcomer != null;
        SetPanelActive(slot.root, visible);

        if (!visible)
            return;

        if (slot.nameText != null)
            slot.nameText.text = newcomer.DisplayName.ToUpperInvariant();

        MonkeySpriteAnimator animator = newcomer.GetComponent<MonkeySpriteAnimator>();
        ApplyPreviewLayer(slot.behind, animator != null ? animator.PortraitBehindSprite : null, Color.white);
        ApplyPreviewLayer(slot.body, animator != null ? animator.PortraitSprite : null, newcomer.DisplayColor);
        ApplyPreviewLayer(slot.face, animator != null ? animator.PortraitFaceSprite : null, Color.white);
        ApplyPreviewLayer(
            slot.frontAccessory,
            animator != null ? animator.PortraitFrontAccessorySprite : null,
            animator != null ? animator.PortraitFrontAccessoryColor : Color.white
        );
    }

    private void ConfirmNewcomerArrival()
    {
        SetPanelActive(newcomerArrivalPanel, false);

        if (shiftDirector != null && shiftDirector.BeginPostZoolagWave() &&
            surveillanceController != null)
            surveillanceController.FocusFirstFeed();
    }

    private void StopZoolagRoutine()
    {
        if (zoolagRoutine != null)
            StopCoroutine(zoolagRoutine);

        zoolagSequencePresenter?.Stop();
        audioController?.SetZoolagSequenceMuted(false);
        zoolagRoutine = null;
    }

    private void HandleMenuInput()
    {
        if (studioIntroPanel != null && studioIntroPanel.activeSelf)
            return;

        if (splashPanel != null && splashPanel.activeSelf)
        {
            bool enterPressed = Keyboard.current != null &&
                (Keyboard.current.enterKey.wasPressedThisFrame ||
                    Keyboard.current.numpadEnterKey.wasPressedThisFrame);
            bool clicked = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;

            if (enterPressed || clicked)
                SetPanelActive(splashPanel, false);

            return;
        }

        if (Keyboard.current == null)
            return;

        if (tutorialWaitingForContinue && infoPanel != null && infoPanel.activeSelf &&
            (Keyboard.current.enterKey.wasPressedThisFrame ||
                Keyboard.current.numpadEnterKey.wasPressedThisFrame ||
                Keyboard.current.spaceKey.wasPressedThisFrame))
        {
            ContinueTutorial();
            return;
        }

        if (!Keyboard.current.escapeKey.wasPressedThisFrame && !Keyboard.current.pKey.wasPressedThisFrame)
            return;

        if (settingsPanel != null && settingsPanel.activeSelf)
        {
            CloseSettingsPanel();
            return;
        }

        if (infoPanel != null && infoPanel.activeSelf)
        {
            HandleInfoButton();
            return;
        }

        if (creditsPanel != null && creditsPanel.activeSelf)
        {
            CloseCreditsPanel();
            return;
        }

        if (monkeyRoster != null && monkeyRoster.IsBlockingInput)
        {
            monkeyRoster.Close();
            return;
        }

        if (paused)
        {
            ResumeGame();
            return;
        }

        if (shiftDirector == null || IsAccusationFlowOpen ||
            resultPanel != null && resultPanel.activeSelf ||
            titlePanel != null && titlePanel.activeSelf)
            return;

        ShiftPhase phase = shiftDirector.Phase;
        if (phase == ShiftPhase.Active || phase == ShiftPhase.IncidentWindow || phase == ShiftPhase.Welcome)
            PauseGame();
    }

    private void UpdateSplashPrompt()
    {
        if (splashPrompt == null || splashPanel == null || !splashPanel.activeSelf)
            return;

        float wave = (Mathf.Sin(Time.unscaledTime * splashFlashSpeed * Mathf.PI * 2f) + 1f) * 0.5f;
        Color color = splashPrompt.color;
        color.a = Mathf.Lerp(0.25f, 1f, wave);
        splashPrompt.color = color;
    }

    private bool HasStudioLogos()
    {
        if (studioIntroPanel == null || studioIntroImage == null || studioLogoSprites == null)
            return false;

        foreach (Sprite logo in studioLogoSprites)
        {
            if (logo != null)
                return true;
        }

        return false;
    }

    private IEnumerator PlayStudioIntro()
    {
        SetPanelActive(studioIntroPanel, true);

        foreach (Sprite logo in studioLogoSprites)
        {
            if (logo == null)
                continue;

            studioIntroImage.sprite = logo;
            SetStudioLogoAlpha(0f);
            yield return FadeStudioLogo(0f, 1f, studioLogoFadeDuration);

            if (studioLogoHoldDuration > 0f)
                yield return new WaitForSecondsRealtime(studioLogoHoldDuration);

            yield return FadeStudioLogo(1f, 0f, studioLogoFadeDuration);

            if (studioLogoGapDuration > 0f)
                yield return new WaitForSecondsRealtime(studioLogoGapDuration);
        }

        studioIntroImage.sprite = null;
        SetPanelActive(studioIntroPanel, false);
        studioIntroRoutine = null;
    }

    private IEnumerator FadeStudioLogo(float from, float to, float duration)
    {
        if (duration <= 0f)
        {
            SetStudioLogoAlpha(to);
            yield break;
        }

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            SetStudioLogoAlpha(Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration)));
            yield return null;
        }

        SetStudioLogoAlpha(to);
    }

    private void SetStudioLogoAlpha(float alpha)
    {
        if (studioIntroImage == null)
            return;

        Color color = studioIntroImage.color;
        color.a = alpha;
        studioIntroImage.color = color;
    }

    private void PauseGame()
    {
        if (paused)
            return;

        if (monkeyRoster != null)
            monkeyRoster.Close();

        timeScaleBeforePause = Mathf.Approximately(Time.timeScale, 0f) ? 1f : Time.timeScale;
        paused = true;
        Time.timeScale = 0f;
        SetPanelActive(pausePanel, true);
    }

    public void ResumeGame()
    {
        if (!paused)
            return;

        RestoreTimeScale();
        SetPanelActive(settingsPanel, false);
        SetPanelActive(pausePanel, false);
    }

    private void RestartShift()
    {
        if (tutorialActive)
            StartTutorial();
        else
            StartShift();
    }

    private void ShowHowToPlay()
    {
        StartTutorial();
    }

    private void ShowCredits()
    {
        if (creditsBodyText != null)
            creditsBodyText.text = FormatCreditsContent();

        SetPanelActive(infoPanel, false);
        SetPanelActive(creditsPanel, true);
    }

    private string FormatCreditsContent()
    {
        string normalized = creditsContent.Replace("\r\n", "\n").Trim();
        string[] blocks = normalized.Split(
            new[] { "\n\n" },
            System.StringSplitOptions.RemoveEmptyEntries
        );
        System.Text.StringBuilder formatted = new System.Text.StringBuilder(normalized.Length + 256);

        for (int blockIndex = 0; blockIndex < blocks.Length; blockIndex++)
        {
            string[] lines = blocks[blockIndex].Trim().Split(
                new[] { '\n' },
                System.StringSplitOptions.RemoveEmptyEntries
            );
            if (lines.Length == 0)
                continue;

            if (formatted.Length > 0)
                formatted.Append("\n\n");

            string finalLine = lines[lines.Length - 1].Trim();
            if (lines.Length == 1)
            {
                bool isClosingLine = finalLine.IndexOf(
                    "THANK YOU",
                    System.StringComparison.OrdinalIgnoreCase
                ) >= 0;
                string size = isClosingLine ? "34" : "22";
                string color = isClosingLine ? "#EFA82E" : "#A9B8A7";
                formatted.Append("<size=").Append(size).Append("><color=").Append(color).Append('>')
                    .Append(finalLine)
                    .Append("</color></size>");
                continue;
            }

            formatted.Append("<size=24><color=#EFA82E>");
            for (int lineIndex = 0; lineIndex < lines.Length - 1; lineIndex++)
            {
                if (lineIndex > 0)
                    formatted.Append("  -  ");
                formatted.Append(lines[lineIndex].Trim());
            }

            formatted.Append("</color></size>\n<size=34><color=#F3EBC4>")
                .Append(finalLine)
                .Append("</color></size>");
        }

        formatted.Append("\n\n<size=21><color=#A9B8A7>CLICK ANYWHERE TO GO BACK</color></size>");
        return formatted.ToString();
    }

    private void CloseCreditsPanel()
    {
        SetPanelActive(creditsPanel, false);
    }

    private void ShowInfo(string title, string body)
    {
        if (infoTitleText != null)
            infoTitleText.text = title;
        if (infoBodyText != null)
            infoBodyText.text = body;

        SetPanelActive(infoPanel, true);
    }

    private void CloseInfoPanel()
    {
        SetPanelActive(infoPanel, false);
    }

    private void HandleInfoButton()
    {
        if (tutorialWaitingForContinue)
        {
            ContinueTutorial();
            return;
        }

        CloseInfoPanel();
    }

    private void ShowTutorialCard(string title, string body, TutorialStep nextStep)
    {
        if (!tutorialActive)
            return;

        tutorialWaitingForContinue = true;
        tutorialNextStep = nextStep;
        timeScaleBeforePause = Mathf.Clamp(tutorialTimeScale, 0.1f, 1f);
        Time.timeScale = 0f;
        ShowInfo(
            title,
            $"{body}\n\nPRESS ENTER OR CLICK THE ARROW TO CONTINUE"
        );
    }

    private void ContinueTutorial()
    {
        if (!tutorialActive || !tutorialWaitingForContinue)
            return;

        TutorialStep nextStep = tutorialNextStep;
        tutorialWaitingForContinue = false;
        tutorialNextStep = TutorialStep.Inactive;
        CloseInfoPanel();

        if (nextStep == TutorialStep.Complete)
        {
            ReturnToTitle();
            return;
        }

        Time.timeScale = Mathf.Clamp(tutorialTimeScale, 0.1f, 1f);
        timeScaleBeforePause = Time.timeScale;
        BeginTutorialStep(nextStep);
    }

    private void BeginTutorialStep(TutorialStep step)
    {
        tutorialStep = step;

        switch (step)
        {
            case TutorialStep.SelectMonkey:
                surveillanceController?.ClearSelectedMonkey();
                surveillanceController?.RefreshFeedVisibility();
                tutorialHudMessage = "CLICK ANY MONKEY TO SELECT THEM";
                break;

            case TutorialStep.FeedMonkey:
                tutorialHudMessage = "CLICK THE YELLOW BANANA BUTTON TO FEED THE SELECTED MONKEY";
                break;

            case TutorialStep.CleanMess:
                EnclosureZone cleanZone = surveillanceController != null
                    ? surveillanceController.SelectedZone
                    : null;
                itemManager?.SpawnTutorialPoo(cleanZone);
                tutorialHudMessage = "SOMEONE DID A MESS - CLICK THE GREEN CLEAN BUTTON";
                break;

            case TutorialStep.TimeOutMonkey:
                EnclosureZone fightZone = surveillanceController != null
                    ? surveillanceController.SelectedZone
                    : null;
                bool fightStarted = shiftDirector != null && shiftDirector.StartTutorialFight(
                    fightZone,
                    tutorialMonkey,
                    out tutorialFighterOne,
                    out tutorialFighterTwo
                );
                surveillanceController?.ClearSelectedMonkey();
                tutorialHudMessage = fightStarted
                    ? "SELECT EITHER FIGHTER, THEN CLICK THE RED TIME-OUT BUTTON"
                    : "SELECT A NAUGHTY MONKEY, THEN CLICK THE RED TIME-OUT BUTTON";
                break;

            case TutorialStep.SwitchCamera:
                tutorialStartingZoneId = surveillanceController != null &&
                    surveillanceController.SelectedZone != null
                        ? surveillanceController.SelectedZone.Id
                        : string.Empty;
                tutorialHudMessage = "USE A CAMERA ARROW TO VISIT ANOTHER ROOM";
                break;

            case TutorialStep.FindEvidence:
                tutorialIncidentCountdown = 1.25f;
                tutorialHudMessage = "KEEP WATCHING THIS ROOM - SOMETHING IS ABOUT TO HAPPEN...";
                break;

            case TutorialStep.EnterLineup:
                tutorialHudMessage = string.Empty;
                shiftDirector?.SkipIncidentWindow();
                tutorialStep = TutorialStep.SolveLineup;
                break;

            case TutorialStep.SolveLineup:
                tutorialHudMessage = string.Empty;
                break;
        }
    }

    private void UpdateTutorial()
    {
        if (!tutorialActive || tutorialWaitingForContinue || shiftDirector == null)
            return;

        if (tutorialStep != TutorialStep.FindEvidence)
            return;

        if (shiftDirector.CurrentIncident == null && tutorialIncidentCountdown > 0f)
        {
            tutorialIncidentCountdown = Mathf.Max(
                0f,
                tutorialIncidentCountdown - Time.unscaledDeltaTime
            );

            if (tutorialIncidentCountdown <= 0f)
            {
                if (!shiftDirector.TriggerTutorialIncident())
                {
                    ShowTutorialCard(
                        "TUTORIAL COULD NOT CONTINUE",
                        "RETURN TO THE MENU AND TRY THE PRACTICE RUN AGAIN.",
                        TutorialStep.Complete
                    );
                }
                else
                    tutorialEvidenceRevealCountdown = 1.2f;
            }

            return;
        }

        if (shiftDirector.CurrentIncident != null &&
            shiftDirector.CurrentIncident.WasCaughtOnCamera)
        {
            if (tutorialEvidenceRevealCountdown > 0f)
            {
                tutorialEvidenceRevealCountdown = Mathf.Max(
                    0f,
                    tutorialEvidenceRevealCountdown - Time.unscaledDeltaTime
                );
                return;
            }

            ShowTutorialCard(
                "CAUGHT ON CAMERA",
                "YOU WERE ALREADY WATCHING THE ROOM WHERE THE CRIME OCCURED, SO YOU HAVE DIRECT EVIDENCE. THE CULPRIT WILL BE HIGHLIGHTED IN THE LINE-UP.\n\nSWITCHING CAMERAS AFTER AN ALARM CANNOT RECOVER MISSED FOOTAGE. YOU MUST RELY ON THE MONKEYS POINTING. DO YOU TRUST THEM?",
                TutorialStep.EnterLineup
            );
        }
    }

    private void HandleTutorialMonkeySelected(MonkeyActor monkey)
    {
        if (!tutorialActive || tutorialWaitingForContinue || monkey == null)
            return;

        if (tutorialStep == TutorialStep.TimeOutMonkey)
        {
            tutorialMonkey = monkey;
            return;
        }

        if (tutorialStep != TutorialStep.SelectMonkey)
            return;

        tutorialMonkey = monkey;
        ShowTutorialCard(
            "MONKEY SELECTED",
            $"THIS IS {monkey.DisplayName.ToUpperInvariant()}. THE BOTTOM PANEL SHOWS THEIR HUNGER, MOOD AND HISTORY.\n\nNEXT, GIVE THEM SOME FOOD.",
            TutorialStep.FeedMonkey
        );
    }

    private void HandleTutorialCareAction()
    {
        if (!tutorialActive || tutorialWaitingForContinue || shiftDirector == null)
            return;

        string message = shiftDirector.StatusMessage ?? string.Empty;

        if (tutorialStep == TutorialStep.FeedMonkey &&
            (message.StartsWith("Dropped a banana") || message.StartsWith("Fed ") ||
                message.Contains("refuses the banana")))
        {
            ShowTutorialCard(
                "FEEDING",
                "THE BANANA DROPS INTO THE ROOM AND THE MONKEY WALKS OVER TO EAT IT. A FULL MONKEY WILL REFUSE MORE FOOD.\n\nNOW CLEAN UP A LITTLE MESS THEY DID.",
                TutorialStep.CleanMess
            );
        }
        else if (tutorialStep == TutorialStep.CleanMess && message.StartsWith("Cleaned "))
        {
            CompleteTutorialCleaningLesson();
        }
        else if (tutorialStep == TutorialStep.TimeOutMonkey &&
            ((tutorialFighterOne != null && tutorialFighterOne.IsInTimeOut) ||
                (tutorialFighterTwo != null && tutorialFighterTwo.IsInTimeOut)))
        {
            ShowTutorialCard(
                "TIME-OUT",
                "TIME-OUT CALMS A NAUGHTY MONKEY. USING IT ON AN INNOCENT MONKEY MAKES THEM NAUGHTIER AND BADLY DAMAGES THEIR TRUST, SO USE IT WISELY.\n\nNEXT, LEARN THE CAMERAS.",
                TutorialStep.SwitchCamera
            );
        }
    }

    private void HandleTutorialPooCleaned(DaycareItem _)
    {
        if (tutorialActive && !tutorialWaitingForContinue &&
            tutorialStep == TutorialStep.CleanMess)
            CompleteTutorialCleaningLesson();
    }

    private void CompleteTutorialCleaningLesson()
    {
        ShowTutorialCard(
            "CLEANING",
            "CLEANING REMOVES POO AND ROTTEN FOOD FROM THE CURRENT ROOM.\n\nA FIGHT IS ABOUT TO BREAK OUT IN THIS ROOM! GIVE A TIME-OUT TO EITHER FIGHTER.",
            TutorialStep.TimeOutMonkey
        );
    }

    private void HandleTutorialCameraChanged(EnclosureZone zone)
    {
        if (!tutorialActive || tutorialWaitingForContinue ||
            tutorialStep != TutorialStep.SwitchCamera || zone == null ||
            zone.Id == tutorialStartingZoneId)
            return;

        ShowTutorialCard(
            "CAMERAS",
            "EACH CAMERA WATCHES A DIFFERENT ROOM. IF A CRIME HAPPENS IN THE ROOM YOU ARE CURRENTLY WATCHING, YOU GET DIRECT EVIDENCE.\n\nSTAY ON THIS CAMERA FEED AND WATCH WHAT HAPPENS.",
            TutorialStep.FindEvidence
        );
    }

    private void ResetTutorialState()
    {
        tutorialActive = false;
        tutorialWaitingForContinue = false;
        tutorialStep = TutorialStep.Inactive;
        tutorialNextStep = TutorialStep.Inactive;
        tutorialMonkey = null;
        tutorialFighterOne = null;
        tutorialFighterTwo = null;
        tutorialStartingZoneId = string.Empty;
        tutorialHudMessage = string.Empty;
        tutorialIncidentCountdown = 0f;
        tutorialEvidenceRevealCountdown = 0f;
    }

    private void OpenTitleSettings()
    {
        settingsOpenedFromPause = false;
        SetPanelActive(settingsPanel, true);
    }

    private void OpenPauseSettings()
    {
        settingsOpenedFromPause = true;
        SetPanelActive(settingsPanel, true);
    }

    private void CloseSettingsPanel()
    {
        SetPanelActive(settingsPanel, false);
        PlayerPrefs.Save();

        if (!settingsOpenedFromPause)
            SetPanelActive(titlePanel, true);
    }

    private void LoadSettings()
    {
        float master = PlayerPrefs.GetFloat(MasterVolumeKey, 1f);
        float music = PlayerPrefs.GetFloat(MusicVolumeKey, 1f);
        float sfx = PlayerPrefs.GetFloat(SfxVolumeKey, 1f);
        bool fullscreen = PlayerPrefs.GetInt(FullscreenKey, Screen.fullScreen ? 1 : 0) == 1;
        bool extraMonkeyNoises = PlayerPrefs.GetInt(ExtraMonkeyNoisesKey, 1) == 1;

        if (masterVolumeSlider != null)
            masterVolumeSlider.SetValueWithoutNotify(master);
        if (musicVolumeSlider != null)
            musicVolumeSlider.SetValueWithoutNotify(music);
        if (sfxVolumeSlider != null)
            sfxVolumeSlider.SetValueWithoutNotify(sfx);
        if (fullscreenToggle != null)
            fullscreenToggle.SetIsOnWithoutNotify(fullscreen);
        if (extraMonkeyNoisesToggle != null)
            extraMonkeyNoisesToggle.SetIsOnWithoutNotify(extraMonkeyNoises);

        SetMasterVolume(master);
        SetMusicVolume(music);
        SetSfxVolume(sfx);
        SetExtraMonkeyNoises(extraMonkeyNoises);
        ApplyToggleArtwork(fullscreenToggle, fullscreen);
        Screen.fullScreen = fullscreen;
    }

    private void SetMasterVolume(float value)
    {
        float normalized = Mathf.Clamp01(value);
        AudioListener.volume = normalized;
        UpdateSliderArtwork(masterVolumeSlider, masterVolumeFillImage);
        PlayerPrefs.SetFloat(MasterVolumeKey, normalized);
    }

    private void SetMusicVolume(float value)
    {
        float normalized = Mathf.Clamp01(value);
        if (audioController != null)
            audioController.SetMusicVolume(normalized);
        UpdateSliderArtwork(musicVolumeSlider, musicVolumeFillImage);
        PlayerPrefs.SetFloat(MusicVolumeKey, normalized);
    }

    private void SetSfxVolume(float value)
    {
        float normalized = Mathf.Clamp01(value);
        if (audioController != null)
            audioController.SetSfxVolume(normalized);
        UpdateSliderArtwork(sfxVolumeSlider, sfxVolumeFillImage);
        PlayerPrefs.SetFloat(SfxVolumeKey, normalized);
    }

    private void SetFullscreen(bool fullscreen)
    {
        ApplyToggleArtwork(fullscreenToggle, fullscreen);
        Screen.fullScreen = fullscreen;
        PlayerPrefs.SetInt(FullscreenKey, fullscreen ? 1 : 0);
        PlayerPrefs.Save();
    }

    private void SetExtraMonkeyNoises(bool enabled)
    {
        ApplyToggleArtwork(extraMonkeyNoisesToggle, enabled);
        if (audioController != null)
            audioController.SetExtraMonkeyNoises(enabled);

        PlayerPrefs.SetInt(ExtraMonkeyNoisesKey, enabled ? 1 : 0);
    }

    private void ConfigureSettingsArtwork()
    {
        masterVolumeFillImage = ConfigureSliderArtwork(masterVolumeSlider);
        musicVolumeFillImage = ConfigureSliderArtwork(musicVolumeSlider);
        sfxVolumeFillImage = ConfigureSliderArtwork(sfxVolumeSlider);
        ConfigureToggleArtwork(fullscreenToggle);
        ConfigureToggleArtwork(extraMonkeyNoisesToggle);
    }

    private static Image ConfigureSliderArtwork(Slider slider)
    {
        if (slider == null || slider.fillRect == null)
            return null;

        RectTransform fillRect = slider.fillRect;
        Image fillImage = fillRect.GetComponent<Image>();
        slider.fillRect = null;

        if (fillRect.parent is RectTransform fillArea)
        {
            fillArea.anchorMin = Vector2.zero;
            fillArea.anchorMax = Vector2.one;
            fillArea.anchoredPosition = Vector2.zero;
            fillArea.sizeDelta = Vector2.zero;
        }

        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.anchoredPosition = Vector2.zero;
        fillRect.sizeDelta = Vector2.zero;

        if (fillImage == null)
            return null;

        fillImage.type = Image.Type.Filled;
        fillImage.fillMethod = Image.FillMethod.Horizontal;
        fillImage.fillOrigin = 0;
        fillImage.fillClockwise = true;
        fillImage.preserveAspect = false;
        fillImage.raycastTarget = false;
        return fillImage;
    }

    private static void UpdateSliderArtwork(Slider slider, Image fillImage)
    {
        if (slider == null || fillImage == null)
            return;

        fillImage.fillAmount = slider.normalizedValue;
    }

    private void ConfigureToggleArtwork(Toggle toggle)
    {
        if (toggle == null)
            return;

        if (toggle.targetGraphic is Image background)
        {
            Sprite emptySprite = settingsTickEmptySprite != null
                ? settingsTickEmptySprite
                : background.sprite;
            background.overrideSprite = emptySprite;
            background.color = Color.white;
            background.type = Image.Type.Simple;
            background.preserveAspect = true;
        }

        Image checkmark = toggle.graphic as Image;
        if (checkmark == null)
        {
            Image[] images = toggle.GetComponentsInChildren<Image>(true);
            for (int i = 0; i < images.Length; i++)
            {
                if (images[i] != toggle.targetGraphic)
                {
                    checkmark = images[i];
                    break;
                }
            }
        }

        if (checkmark != null)
        {
            checkmark.gameObject.SetActive(true);
            Sprite tickSprite = settingsTickSprite != null
                ? settingsTickSprite
                : checkmark.sprite;
            checkmark.overrideSprite = tickSprite;
            checkmark.color = Color.white;
            checkmark.type = Image.Type.Simple;
            checkmark.preserveAspect = true;
            checkmark.raycastTarget = false;

            RectTransform rect = checkmark.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
            toggle.graphic = checkmark;
        }

        ApplyToggleArtwork(toggle, toggle.isOn);
    }

    private void ApplyToggleArtwork(Toggle toggle, bool isOn)
    {
        if (toggle == null)
            return;

        if (toggle.targetGraphic is Image background)
        {
            Sprite emptySprite = settingsTickEmptySprite != null
                ? settingsTickEmptySprite
                : background.sprite;
            background.overrideSprite = emptySprite;
        }

        if (toggle.graphic is Image checkmark)
        {
            Sprite tickSprite = settingsTickSprite != null
                ? settingsTickSprite
                : checkmark.sprite;
            checkmark.overrideSprite = tickSprite;
            checkmark.canvasRenderer.SetAlpha(isOn ? 1f : 0f);
        }
    }

    private void QuitGame()
    {
        PlayerPrefs.Save();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void RestoreTimeScale()
    {
        if (paused || Mathf.Approximately(Time.timeScale, 0f))
            Time.timeScale = Mathf.Max(0.01f, timeScaleBeforePause);

        paused = false;
        settingsOpenedFromPause = false;
    }

    private static string FormatTime(float seconds)
    {
        int totalSeconds = Mathf.Max(0, Mathf.FloorToInt(seconds));
        return $"{totalSeconds / 60:00}:{totalSeconds % 60:00}";
    }

    private static void SetPanelActive(GameObject panel, bool active)
    {
        if (panel != null && panel.activeSelf != active)
            panel.SetActive(active);
    }
}
