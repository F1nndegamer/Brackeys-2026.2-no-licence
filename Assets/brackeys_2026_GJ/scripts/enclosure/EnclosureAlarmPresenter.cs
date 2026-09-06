using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class EnclosureAlarmPresenter : MonoBehaviour
{
    private const float BurstSeconds = 1.2f;
    private const float SlowMotionScale = 0.15f;
    private const float SlowMotionHold = 0.25f;
    private const float SlowMotionRamp = 0.35f;
    private const float WipeSeconds = 0.3f;

    [Header("Gameplay")]
    [SerializeField] private ShiftDirector shiftDirector;
    [SerializeField] private EnclosureSurveillanceController surveillanceController;
    [SerializeField] private DaycareGameFlowPresenter gameFlowPresenter;

    [Header("Bottom Panel Alarms")]
    [SerializeField] private Image[] panelAlarmImages;
    [SerializeField] private Sprite panelAlarmIdleSprite;
    [SerializeField] private Sprite panelAlarmActiveSprite;
    [SerializeField] private Sprite[] panelAlarmActiveFrames;
    [SerializeField, Min(0.1f)] private float panelAlarmFramesPerSecond = 10f;

    [Header("Authored UI")]
    [SerializeField] private RectTransform overlayRoot;
    [SerializeField] private Image flash;
    [SerializeField] private Image[] borders;
    [SerializeField] private TMP_Text title;
    [SerializeField] private TMP_Text countdown;
    [SerializeField] private Image countdownBar;
    [SerializeField] private IncidentRadialTimerGraphic countdownRing;
    [SerializeField] private TMP_Text prompt;
    [SerializeField] private Image wipe;

    private float burstElapsed = BurstSeconds;
    private float slowMotionElapsed;
    private float wipeElapsed = WipeSeconds;
    private float countdownPunch;
    private float countdownBarWidth;
    private float wipeTravel;
    private int lastWholeSecond = -1;
    private bool slowMotionActive;
    private bool wasInWindow;
    private Color alertColor;
    private Color countdownSafeColor;
    private bool panelAlarmState;
    private bool panelAlarmStateInitialised;
    private float panelAlarmAnimationStartedAt;
    private Sprite displayedPanelAlarmSprite;
    private Vector2[] panelAlarmIdleSizes;

    private void Awake()
    {
        if (shiftDirector == null)
            shiftDirector = GetComponent<ShiftDirector>();

        if (surveillanceController == null)
            surveillanceController = GetComponent<EnclosureSurveillanceController>();

        if (gameFlowPresenter == null)
            gameFlowPresenter = GetComponentInChildren<DaycareGameFlowPresenter>(true);

        StyleTextMeshProToMatchImage(title);
        StyleTextMeshProToMatchImage(countdown);

        if (title != null)
            alertColor = title.color;
        if (countdown != null)
            countdownSafeColor = countdown.color;
        if (countdownBar != null)
            countdownBarWidth = countdownBar.rectTransform.sizeDelta.x;
        if (overlayRoot != null)
        {
            RectTransform canvasRect = overlayRoot.parent as RectTransform;
            wipeTravel = canvasRect != null ? canvasRect.rect.width : 1920f;
            overlayRoot.gameObject.SetActive(false);
        }

        CachePanelAlarmIdleSizes();
        SetPanelAlarmState(false);
    }

    private void StyleTextMeshProToMatchImage(TMP_Text tmpText)
    {
        if (tmpText == null) return;

        tmpText.color = new Color32(209, 58, 25, 255);

        Material mat = new Material(tmpText.fontSharedMaterial);
        tmpText.fontMaterial = mat;

        mat.EnableKeyword("OUTLINE_ON");
        mat.EnableKeyword("UNDERLAY_ON");

        mat.SetColor("_OutlineColor", Color.white);
        mat.SetFloat("_OutlineWidth", 0.25f);

        mat.SetColor("_UnderlayColor", Color.black);
        mat.SetFloat("_UnderlayOffsetX", 0.6f);
        mat.SetFloat("_UnderlayOffsetY", -0.6f);
        mat.SetFloat("_UnderlayDilate", 0.15f);
        mat.SetFloat("_UnderlaySoftness", 0f);

        tmpText.UpdateMeshPadding();
    }

    private void OnEnable()
    {
        if (shiftDirector != null)
            shiftDirector.IncidentOccurred += PlayAlarm;
    }

    private void OnDisable()
    {
        if (shiftDirector != null)
            shiftDirector.IncidentOccurred -= PlayAlarm;

        RestoreTimeScale();
        SetPanelAlarmState(false);
    }

    private void Update()
    {
        if (shiftDirector == null)
            return;

        bool inWindow = shiftDirector.Phase == ShiftPhase.IncidentWindow;
        SetPanelAlarmState(inWindow || shiftDirector.Phase == ShiftPhase.Lineup);

        if (overlayRoot == null)
            return;

        if (wasInWindow && !inWindow)
        {
            wipeElapsed = 0f;
            RestoreTimeScale();
        }

        wasInWindow = inWindow;
        bool needsOverlay = inWindow || burstElapsed < BurstSeconds || wipeElapsed < WipeSeconds;

        if (!needsOverlay)
        {
            if (overlayRoot.gameObject.activeSelf)
                overlayRoot.gameObject.SetActive(false);

            return;
        }

        overlayRoot.gameObject.SetActive(true);

        if (gameFlowPresenter != null &&
            (gameFlowPresenter.IsPaused || gameFlowPresenter.IsTutorialInstructionOpen))
            return;

        float deltaTime = Time.unscaledDeltaTime;
        UpdateSlowMotion(deltaTime);
        UpdateBurst(deltaTime);
        UpdateWindow(inWindow, deltaTime);
        UpdateWipe(deltaTime);
    }

    private void PlayAlarm(DaycareIncident incident)
    {
        if (overlayRoot == null || title == null)
            return;

        overlayRoot.gameObject.SetActive(true);
        title.text = incident.Definition.alertTitle;
        burstElapsed = 0f;
        slowMotionElapsed = 0f;
        slowMotionActive = true;
        countdownPunch = 1f;
        lastWholeSecond = -1;

        if (gameFlowPresenter == null || !gameFlowPresenter.IsPaused)
            Time.timeScale = SlowMotionScale;

        if (surveillanceController != null)
            surveillanceController.ShakeCamera(0.2f);
    }

    private void RestoreTimeScale()
    {
        slowMotionActive = false;

        if (gameFlowPresenter == null ||
            (!gameFlowPresenter.IsPaused && !gameFlowPresenter.IsTutorialInstructionOpen))
            Time.timeScale = gameFlowPresenter != null
                ? gameFlowPresenter.DesiredGameplayTimeScale
                : 1f;
    }

    private void UpdateSlowMotion(float deltaTime)
    {
        if (!slowMotionActive)
            return;

        slowMotionElapsed += deltaTime;

        if (slowMotionElapsed <= SlowMotionHold)
            return;

        float ramp = Mathf.InverseLerp(
            SlowMotionHold,
            SlowMotionHold + SlowMotionRamp,
            slowMotionElapsed
        );
        Time.timeScale = Mathf.Lerp(SlowMotionScale, 1f, ramp);

        if (ramp >= 1f)
            RestoreTimeScale();
    }

    private void UpdateBurst(float deltaTime)
    {
        if (burstElapsed >= BurstSeconds)
            return;

        burstElapsed += deltaTime;
        float flashAlpha = burstElapsed >= 0.7f
            ? 0f
            : Mathf.Lerp(0.55f, 0f, Mathf.Repeat(burstElapsed, 0.35f) / 0.35f);
        SetAlpha(flash, flashAlpha);

        float titleScale;

        if (burstElapsed < 0.3f)
            titleScale = Mathf.Lerp(1.6f, 1f, burstElapsed / 0.3f);
        else if (burstElapsed < 0.9f)
            titleScale = 1f;
        else
            titleScale = Mathf.Lerp(1f, 0.55f, Mathf.InverseLerp(0.9f, 1.2f, burstElapsed));

        title.rectTransform.localScale = Vector3.one * titleScale;
    }

    private void UpdateWindow(bool inWindow, float deltaTime)
    {
        countdown.gameObject.SetActive(inWindow);
        if (countdownRing != null)
            countdownRing.gameObject.SetActive(inWindow);
        else if (countdownBar != null)
            countdownBar.gameObject.SetActive(inWindow);
        prompt.gameObject.SetActive(inWindow);

        foreach (Image border in borders)
            border.gameObject.SetActive(inWindow);

        if (!inWindow)
            return;

        float duration = Mathf.Max(0.01f, shiftDirector.IncidentWindowDuration);
        float remaining = shiftDirector.IncidentWindowRemaining;
        float normalised = Mathf.Clamp01(remaining / duration);
        int wholeSecond = Mathf.CeilToInt(remaining);

        if (wholeSecond != lastWholeSecond)
        {
            lastWholeSecond = wholeSecond;
            countdownPunch = 1f;
        }

        countdownPunch = Mathf.Max(0f, countdownPunch - deltaTime * 4f);
        countdown.text = Mathf.Max(0, wholeSecond).ToString();
        countdown.color = Color.Lerp(alertColor, countdownSafeColor, normalised);
        countdown.rectTransform.localScale = Vector3.one * (1f + countdownPunch * 0.45f);
        if (countdownRing != null)
        {
            countdownRing.color = countdown.color;
            countdownRing.FillAmount = normalised;
        }
        else if (countdownBar != null)
        {
            countdownBar.color = countdown.color;
            countdownBar.rectTransform.sizeDelta = new Vector2(
                countdownBarWidth * normalised,
                countdownBar.rectTransform.sizeDelta.y
            );
        }

        DaycareIncident incident = shiftDirector.CurrentIncident;
        prompt.text = incident != null && incident.WasCaughtOnCamera
            ? "CAUGHT ON CAMERA! - DIRECT EVIDENCE RECORDED   [SPACE] LINEUP"
            : "YOU MISSED IT! - DO YOU TRUST THE MONKEYS?   [SPACE] LINEUP";

        float pulse = 0.25f + Mathf.Abs(Mathf.Sin(Time.unscaledTime * 9f)) * 0.18f;

        foreach (Image border in borders)
            SetAlpha(border, pulse);
    }

    private void UpdateWipe(float deltaTime)
    {
        if (wipeElapsed >= WipeSeconds)
        {
            wipe.gameObject.SetActive(false);
            return;
        }

        wipeElapsed += deltaTime;
        wipe.gameObject.SetActive(true);
        wipe.rectTransform.anchoredPosition = new Vector2(
            Mathf.Lerp(-wipeTravel, wipeTravel, wipeElapsed / WipeSeconds),
            wipe.rectTransform.anchoredPosition.y
        );
    }

    private static void SetAlpha(Image image, float alpha)
    {
        if (image == null)
            return;

        Color color = image.color;
        color.a = alpha;
        image.color = color;
    }

    private void SetPanelAlarmState(bool active)
    {
        if (!panelAlarmStateInitialised || panelAlarmState != active)
        {
            panelAlarmState = active;
            panelAlarmStateInitialised = true;
            panelAlarmAnimationStartedAt = Time.unscaledTime;
            displayedPanelAlarmSprite = null;
        }

        Sprite sprite = ResolvePanelAlarmSprite(active);

        if (sprite == displayedPanelAlarmSprite)
            return;

        displayedPanelAlarmSprite = sprite;

        if (panelAlarmImages == null)
            return;

        Vector2 idleSpriteSize = panelAlarmIdleSprite != null
            ? panelAlarmIdleSprite.rect.size
            : Vector2.one;

        for (int index = 0; index < panelAlarmImages.Length; index++)
        {
            Image alarm = panelAlarmImages[index];
            if (alarm == null)
                continue;

            alarm.sprite = sprite;
            alarm.preserveAspect = true;
            alarm.raycastTarget = false;

            if (sprite != null && panelAlarmIdleSizes != null &&
                index < panelAlarmIdleSizes.Length)
            {
                Vector2 frameSize = sprite.rect.size;
                alarm.rectTransform.sizeDelta = new Vector2(
                    panelAlarmIdleSizes[index].x * frameSize.x / Mathf.Max(1f, idleSpriteSize.x),
                    panelAlarmIdleSizes[index].y * frameSize.y / Mathf.Max(1f, idleSpriteSize.y)
                );
            }
        }
    }

    private void CachePanelAlarmIdleSizes()
    {
        if (panelAlarmImages == null)
            return;

        panelAlarmIdleSizes = new Vector2[panelAlarmImages.Length];
        for (int index = 0; index < panelAlarmImages.Length; index++)
        {
            Image alarm = panelAlarmImages[index];
            panelAlarmIdleSizes[index] = alarm != null
                ? alarm.rectTransform.sizeDelta
                : Vector2.zero;
        }
    }

    private Sprite ResolvePanelAlarmSprite(bool active)
    {
        if (!active || panelAlarmActiveFrames == null || panelAlarmActiveFrames.Length == 0)
            return active ? panelAlarmActiveSprite : panelAlarmIdleSprite;

        int frameCount = panelAlarmActiveFrames.Length;

        if (frameCount == 1)
            return panelAlarmActiveFrames[0];

        int step = Mathf.FloorToInt(
            (Time.unscaledTime - panelAlarmAnimationStartedAt) * panelAlarmFramesPerSecond
        );
        int period = (frameCount - 1) * 2;
        int pingPongStep = step % period;
        int frameIndex = pingPongStep < frameCount
            ? pingPongStep
            : period - pingPongStep;
        return panelAlarmActiveFrames[frameIndex] ?? panelAlarmActiveSprite;
    }
}
