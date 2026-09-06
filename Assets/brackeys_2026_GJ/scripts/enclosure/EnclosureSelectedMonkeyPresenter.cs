using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class EnclosureSelectedMonkeyPresenter : MonoBehaviour
{
    [Header("Gameplay")]
    [SerializeField] private ShiftDirector shiftDirector;
    [SerializeField] private EnclosureSurveillanceController surveillanceController;
    [SerializeField] private EnclosureMonkeyRosterPresenter monkeyRoster;

    [Header("Authored UI")]
    [SerializeField] private RectTransform panel;
    [SerializeField] private CanvasGroup panelGroup;
    [SerializeField] private Image portraitBehind;
    [SerializeField] private Image portrait;
    [SerializeField] private Image portraitFace;
    [SerializeField] private Image portraitFront;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text identityText;
    [SerializeField] private TMP_Text activityText;
    [SerializeField] private TMP_Text historyText;
    [SerializeField] private TMP_Text hungerText;
    [SerializeField] private RectTransform hungerFill;
    [SerializeField] private TMP_Text angerText;
    [SerializeField] private RectTransform angerFill;
    [SerializeField] private Button feedButton;
    [SerializeField] private TMP_Text feedCooldownText;
    [SerializeField] private Button cleanButton;
    [SerializeField] private Button timeoutButton;
    [SerializeField] private float hiddenY;
    [SerializeField] private float visibleY;
    [SerializeField, Min(1f)] private float transitionSpeed = 8f;

    [Header("Trust Feedback")]
    [SerializeField] private Image trustFeedbackImage;
    [SerializeField] private Sprite[] trustGainFrames;
    [SerializeField] private Sprite[] trustLossFrames;
    [SerializeField, Min(1f)] private float trustFeedbackFramesPerSecond = 25f;
    [SerializeField, Min(1f)] private float trustFeedbackHeight = 160f;
    [SerializeField] private Vector2 trustFeedbackOffset = new Vector2(-28f, 28f);

    private float visibility;
    private MonkeyActor observedMonkey;
    private Sprite[] activeTrustFeedbackFrames;
    private int activeTrustFeedbackFrame;
    private float trustFeedbackFrameTimer;

    private void Awake()
    {
        if (shiftDirector == null)
            shiftDirector = GetComponent<ShiftDirector>();

        if (surveillanceController == null)
            surveillanceController = GetComponent<EnclosureSurveillanceController>();

        if (monkeyRoster == null)
            monkeyRoster = GetComponent<EnclosureMonkeyRosterPresenter>();

        if (panel != null)
        {
            visibility = panel.gameObject.activeSelf ? 1f : 0f;
            ApplyVisibility(visibility);
        }

        EnsureTrustFeedbackImage();
        StopTrustFeedback();
    }

    private void OnEnable()
    {
        if (surveillanceController != null)
        {
            surveillanceController.SelectedMonkeyChanged += HandleSelectionChanged;
            ObserveMonkey(surveillanceController.SelectedMonkey);
        }
    }

    private void OnDisable()
    {
        if (surveillanceController != null)
            surveillanceController.SelectedMonkeyChanged -= HandleSelectionChanged;

        ObserveMonkey(null);
        StopTrustFeedback();

        visibility = 0f;
        ApplyVisibility(0f);
        if (panel != null)
            panel.gameObject.SetActive(false);
    }

    private void Update()
    {
        UpdateTrustFeedback(Time.unscaledDeltaTime);

        MonkeyActor selected = surveillanceController != null
            ? surveillanceController.SelectedMonkey
            : null;

        AnimatePanel(true);

        if (selected != null)
            Refresh(selected);
        else
            RefreshEmptyState();
    }

    public void FeedSelected()
    {
        if (surveillanceController != null)
            surveillanceController.FeedSelectedMonkey();
    }

    public void CleanSelectedArea()
    {
        if (surveillanceController != null)
            surveillanceController.CleanSelectedZone();
    }

    public void TimeOutSelected()
    {
        if (surveillanceController != null)
            surveillanceController.TimeOutSelectedMonkey();
    }

    public void SelectPreviousCamera()
    {
        if (surveillanceController != null)
            surveillanceController.SelectPreviousFeed();
    }

    public void SelectNextCamera()
    {
        if (surveillanceController != null)
            surveillanceController.SelectNextFeed();
    }

    public void ClearSelection()
    {
        if (surveillanceController != null)
            surveillanceController.ClearSelectedMonkey();
    }

    public void ToggleRoster()
    {
        if (monkeyRoster != null)
            monkeyRoster.SetOpen(!monkeyRoster.IsOpen);
    }

    private void HandleSelectionChanged(MonkeyActor selected)
    {
        ObserveMonkey(selected);
        StopTrustFeedback();

        if (selected == null || panel == null)
            return;

        panel.gameObject.SetActive(true);
        Refresh(selected);
    }

    private void ObserveMonkey(MonkeyActor monkey)
    {
        if (observedMonkey == monkey)
            return;

        if (observedMonkey != null)
            observedMonkey.TrustChanged -= HandleTrustChanged;

        observedMonkey = monkey;

        if (observedMonkey != null)
            observedMonkey.TrustChanged += HandleTrustChanged;
    }

    private void HandleTrustChanged(MonkeyActor monkey, float amount)
    {
        if (monkey == null || monkey != observedMonkey)
            return;

        if (amount > 0f)
            PlayTrustFeedback(trustGainFrames);
        else
            PlayTrustFeedback(trustLossFrames);
    }

    private void EnsureTrustFeedbackImage()
    {
        if (trustFeedbackImage != null || identityText == null)
            return;

        GameObject feedbackObject = new GameObject(
            "Trust Change Feedback",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image)
        );
        RectTransform feedbackRect = feedbackObject.GetComponent<RectTransform>();
        feedbackRect.SetParent(identityText.rectTransform, false);
        feedbackRect.anchorMin = new Vector2(1f, 0.5f);
        feedbackRect.anchorMax = new Vector2(1f, 0.5f);
        feedbackRect.pivot = new Vector2(0.5f, 0.5f);
        feedbackRect.anchoredPosition = trustFeedbackOffset;

        trustFeedbackImage = feedbackObject.GetComponent<Image>();
        trustFeedbackImage.raycastTarget = false;
        trustFeedbackImage.preserveAspect = true;
    }

    private void PlayTrustFeedback(Sprite[] frames)
    {
        EnsureTrustFeedbackImage();
        if (trustFeedbackImage == null || frames == null || frames.Length == 0)
            return;

        if (activeTrustFeedbackFrames == frames && trustFeedbackImage.gameObject.activeSelf)
            return;

        Sprite firstFrame = frames[0];
        if (firstFrame == null)
            return;

        activeTrustFeedbackFrames = frames;
        activeTrustFeedbackFrame = 0;
        trustFeedbackFrameTimer = 0f;

        RectTransform feedbackRect = trustFeedbackImage.rectTransform;
        float aspect = firstFrame.rect.width / Mathf.Max(1f, firstFrame.rect.height);
        feedbackRect.sizeDelta = new Vector2(trustFeedbackHeight * aspect, trustFeedbackHeight);
        feedbackRect.anchoredPosition = trustFeedbackOffset;

        trustFeedbackImage.sprite = firstFrame;
        trustFeedbackImage.color = Color.white;
        trustFeedbackImage.gameObject.SetActive(true);
    }

    private void UpdateTrustFeedback(float deltaTime)
    {
        if (trustFeedbackImage == null || !trustFeedbackImage.gameObject.activeSelf ||
            activeTrustFeedbackFrames == null || activeTrustFeedbackFrames.Length == 0)
            return;

        trustFeedbackFrameTimer += deltaTime;
        float frameDuration = 1f / Mathf.Max(1f, trustFeedbackFramesPerSecond);

        while (trustFeedbackFrameTimer >= frameDuration)
        {
            trustFeedbackFrameTimer -= frameDuration;
            activeTrustFeedbackFrame++;

            if (activeTrustFeedbackFrame >= activeTrustFeedbackFrames.Length)
            {
                StopTrustFeedback();
                return;
            }

            ApplyTrustFeedbackFrame();
        }
    }

    private void ApplyTrustFeedbackFrame()
    {
        if (trustFeedbackImage == null || activeTrustFeedbackFrames == null ||
            activeTrustFeedbackFrame < 0 || activeTrustFeedbackFrame >= activeTrustFeedbackFrames.Length)
            return;

        trustFeedbackImage.sprite = activeTrustFeedbackFrames[activeTrustFeedbackFrame];
    }

    private void StopTrustFeedback()
    {
        activeTrustFeedbackFrames = null;
        activeTrustFeedbackFrame = 0;
        trustFeedbackFrameTimer = 0f;

        if (trustFeedbackImage != null)
            trustFeedbackImage.gameObject.SetActive(false);
    }

    private void Refresh(MonkeyActor monkey)
    {
        if (portrait == null)
            return;

        MonkeySpriteAnimator animator = monkey.GetComponent<MonkeySpriteAnimator>();
        SpriteRenderer renderer = animator != null
            ? animator.BodyRenderer
            : monkey.GetComponent<SpriteRenderer>();

        if (animator != null)
        {
            SetPortraitLayer(portraitBehind, animator.BehindRenderer);
            SetPortraitLayer(portrait, animator.BodyRenderer);
            SetPortraitLayer(portraitFace, animator.FaceRenderer);
            SetPortraitLayer(portraitFront, animator.FrontAccessoryRenderer);
        }
        else
        {
            SetPortraitLayer(portraitBehind, null);
            SetPortraitLayer(portrait, renderer, monkey.DisplayColor);
            SetPortraitLayer(portraitFace, null);
            SetPortraitLayer(portraitFront, null);
        }

        nameText.text = monkey.DisplayName.ToUpperInvariant();
        identityText.text = $"{monkey.Disposition.ToString().ToUpperInvariant()}   •   TRUST: {monkey.Trust:0}%";
        activityText.text = $"CURRENTLY: {FormatActivity(monkey.Activity)}";
        historyText.text = $"FIGHTS {monkey.FightCount}   •   TIME-OUTS {monkey.TimeoutCount}\nLAST TROUBLE: {monkey.RecentTrouble.ToUpperInvariant()}";
        hungerText.text = $"HUNGER {monkey.Fullness:0}";
        angerText.text = $"NAUGHTY {monkey.Naughtiness:0}";
        SetBar(hungerFill, monkey.Fullness / 100f);
        SetBar(angerFill, monkey.NaughtinessScore);

        bool actionsAvailable = shiftDirector != null &&
            shiftDirector.Phase == ShiftPhase.Active && !monkey.IsInTimeOut;
        feedButton.interactable = actionsAvailable && shiftDirector.CanFeedMonkey(monkey);
        timeoutButton.interactable = shiftDirector.CanTimeOutMonkey(monkey);
        RefreshCleanButton();
        RefreshFeedCooldown();
    }

    private void RefreshEmptyState()
    {
        SetPortraitLayer(portraitBehind, null);
        SetPortraitLayer(portrait, null);
        SetPortraitLayer(portraitFace, null);
        SetPortraitLayer(portraitFront, null);

        if (nameText != null)
            nameText.text = "SELECT A MONKEY";
        if (identityText != null)
            identityText.text = "CLICK A MONKEY IN THE CURRENT CAMERA";
        if (activityText != null)
            activityText.text = string.Empty;
        if (historyText != null)
            historyText.text = string.Empty;
        if (hungerText != null)
            hungerText.text = "HUNGER --";
        if (angerText != null)
            angerText.text = "NAUGHTY --";

        SetBar(hungerFill, 0f);
        SetBar(angerFill, 0f);

        if (feedButton != null)
            feedButton.interactable = false;
        RefreshCleanButton();
        if (timeoutButton != null)
            timeoutButton.interactable = false;

        RefreshFeedCooldown();
    }

    private void RefreshCleanButton()
    {
        if (cleanButton == null)
            return;

        EnclosureZone selectedZone = surveillanceController != null
            ? surveillanceController.SelectedZone
            : null;
        cleanButton.interactable = shiftDirector != null && selectedZone != null &&
            shiftDirector.CanCleanZone(selectedZone);
    }

    private void RefreshFeedCooldown()
    {
        if (feedCooldownText == null)
            return;

        float remaining = shiftDirector != null
            ? shiftDirector.FeedCooldownRemaining
            : 0f;
        bool visible = shiftDirector != null &&
            shiftDirector.Phase == ShiftPhase.Active && remaining > 0f;

        feedCooldownText.gameObject.SetActive(visible);
        feedCooldownText.text = visible
            ? $"{Mathf.CeilToInt(remaining)}s"
            : string.Empty;
    }

    private void AnimatePanel(bool shouldShow)
    {
        if (panel == null || panelGroup == null)
            return;

        if (shouldShow && !panel.gameObject.activeSelf)
            panel.gameObject.SetActive(true);

        float target = shouldShow ? 1f : 0f;
        visibility = Mathf.MoveTowards(visibility, target, Time.unscaledDeltaTime * transitionSpeed);
        float eased = visibility * visibility * (3f - 2f * visibility);
        ApplyVisibility(eased);

        if (!shouldShow && visibility <= 0f && panel.gameObject.activeSelf)
            panel.gameObject.SetActive(false);
    }

    private void ApplyVisibility(float amount)
    {
        if (panel == null || panelGroup == null)
            return;

        panelGroup.alpha = amount;
        panelGroup.interactable = amount > 0.9f;
        panelGroup.blocksRaycasts = amount > 0.05f;
        panel.anchoredPosition = new Vector2(panel.anchoredPosition.x, Mathf.Lerp(hiddenY, visibleY, amount));
    }

    private static void SetBar(RectTransform fill, float value)
    {
        if (fill == null)
            return;

        Vector3 scale = fill.localScale;
        scale.x = Mathf.Clamp01(value);
        fill.localScale = scale;
    }

    private static void SetPortraitLayer(
        Image image,
        SpriteRenderer source,
        Color? fallbackColor = null
    )
    {
        if (image == null)
            return;

        bool visible = source != null && source.enabled && source.sprite != null;
        image.enabled = visible;
        image.sprite = visible ? source.sprite : null;
        image.material = null;

        if (!visible)
            return;

        image.color = fallbackColor ?? source.color;
        Vector3 scale = image.rectTransform.localScale;
        scale.x = Mathf.Abs(scale.x) * (source.flipX ? -1f : 1f);
        image.rectTransform.localScale = scale;
    }

    private static string FormatActivity(MonkeyActivity activity)
    {
        switch (activity)
        {
            case MonkeyActivity.TimeOut: return "IN TIME-OUT";
            case MonkeyActivity.HidingLoot: return "ACTING SUSPICIOUS";
            default: return activity.ToString().ToUpperInvariant();
        }
    }
}
