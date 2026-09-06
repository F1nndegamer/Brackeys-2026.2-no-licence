using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class EnclosureMonkeyRosterPresenter : MonoBehaviour
{
    [Header("Gameplay")]
    [SerializeField] private ShiftDirector shiftDirector;

    [Header("Authored UI")]
    [SerializeField] private RectTransform panel;
    [SerializeField] private CanvasGroup panelGroup;
    [SerializeField] private EnclosureMonkeyRosterEntryView[] entries;
    [SerializeField] private TMP_Text pageText;
    [SerializeField] private Button previousPageButton;
    [SerializeField] private Button nextPageButton;

    [Header("Input")]
    [SerializeField] private Key toggleKey = Key.M;
    [SerializeField, Min(0.05f)] private float transitionDuration = 0.18f;
    [SerializeField] private float hiddenScale = 0.96f;

    private bool requestedOpen;
    private bool simulationPausedByRoster;
    private float timeScaleBeforeRoster = 1f;
    private float visibility;
    private int currentPage;

    public bool IsOpen => requestedOpen;
    public bool IsBlockingInput => panel != null && panel.gameObject.activeSelf;
    public event Action<bool> VisibilityChanged;

    private void Awake()
    {
        if (shiftDirector == null)
            shiftDirector = GetComponent<ShiftDirector>();

        if ((entries == null || entries.Length == 0) && panel != null)
            entries = panel.GetComponentsInChildren<EnclosureMonkeyRosterEntryView>(true);

        if (panel != null)
        {
            requestedOpen = panel.gameObject.activeSelf;
            visibility = requestedOpen ? 1f : 0f;
            ApplyVisibility(visibility);
        }
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current[toggleKey].wasPressedThisFrame)
        {
            if (requestedOpen)
                SetOpen(false);
            else if (shiftDirector != null && shiftDirector.Phase == ShiftPhase.Active)
                SetOpen(true);
        }

        if (Keyboard.current != null && requestedOpen &&
            Keyboard.current.escapeKey.wasPressedThisFrame)
            SetOpen(false);

        if (Keyboard.current != null && requestedOpen)
        {
            if (Keyboard.current.leftArrowKey.wasPressedThisFrame)
                ShowPreviousPage();
            else if (Keyboard.current.rightArrowKey.wasPressedThisFrame)
                ShowNextPage();
        }

        AnimatePanel();

        if (!requestedOpen)
            return;

        if (shiftDirector == null || shiftDirector.Phase != ShiftPhase.Active)
        {
            SetOpen(false);
            return;
        }

        RefreshEntries();
    }

    private void OnDisable()
    {
        RestoreSimulation();
        requestedOpen = false;
        visibility = 0f;
        ApplyVisibility(0f);

        if (panel != null)
            panel.gameObject.SetActive(false);
    }

    public void SetOpen(bool open)
    {
        if (panel == null || requestedOpen == open)
            return;

        requestedOpen = open;

        if (open)
        {
            PauseSimulation();
            panel.gameObject.SetActive(true);
            RefreshEntries();
        }
        else
        {
            RestoreSimulation();
        }

        VisibilityChanged?.Invoke(open);
    }

    public void Close()
    {
        SetOpen(false);
    }

    public void ShowNextPage()
    {
        SetPage(currentPage + 1);
    }

    public void ShowPreviousPage()
    {
        SetPage(currentPage - 1);
    }

    private void SetPage(int requestedPage)
    {
        if (shiftDirector == null || entries == null || entries.Length == 0)
            return;

        int pageCount = Mathf.Max(1, Mathf.CeilToInt(
            shiftDirector.Monkeys.Count / (float)entries.Length
        ));
        currentPage = (requestedPage % pageCount + pageCount) % pageCount;
        RefreshEntries();
    }

    private void AnimatePanel()
    {
        if (panel == null || panelGroup == null)
            return;

        float target = requestedOpen ? 1f : 0f;
        visibility = Mathf.MoveTowards(
            visibility,
            target,
            Time.unscaledDeltaTime / Mathf.Max(0.05f, transitionDuration)
        );
        float eased = visibility * visibility * (3f - 2f * visibility);
        ApplyVisibility(eased);

        if (!requestedOpen && visibility <= 0f && panel.gameObject.activeSelf)
            panel.gameObject.SetActive(false);
    }

    private void ApplyVisibility(float amount)
    {
        if (panel == null || panelGroup == null)
            return;

        panelGroup.alpha = amount;
        panelGroup.interactable = requestedOpen && amount >= 0.98f;
        panelGroup.blocksRaycasts = requestedOpen && amount > 0.05f;
        float scale = Mathf.Lerp(hiddenScale, 1f, amount);
        panel.localScale = new Vector3(scale, scale, 1f);
    }

    private void RefreshEntries()
    {
        if (shiftDirector == null || entries == null)
            return;

        int monkeyCount = shiftDirector.Monkeys.Count;
        int pageSize = entries.Length;
        int pageCount = Mathf.Max(1, Mathf.CeilToInt(monkeyCount / (float)pageSize));
        currentPage = Mathf.Clamp(currentPage, 0, pageCount - 1);
        int firstMonkeyIndex = currentPage * pageSize;

        if (pageText != null)
            pageText.text = $"{currentPage + 1}/{pageCount}";
        if (previousPageButton != null)
            previousPageButton.interactable = pageCount > 1;
        if (nextPageButton != null)
            nextPageButton.interactable = pageCount > 1;

        for (int index = 0; index < entries.Length; index++)
        {
            EnclosureMonkeyRosterEntryView entry = entries[index];
            if (entry == null)
                continue;

            int monkeyIndex = firstMonkeyIndex + index;
            bool hasMonkey = monkeyIndex < monkeyCount;
            entry.gameObject.SetActive(hasMonkey);

            if (hasMonkey)
                entry.Bind(shiftDirector.Monkeys[monkeyIndex]);
        }
    }

    private void PauseSimulation()
    {
        if (simulationPausedByRoster)
            return;

        timeScaleBeforeRoster = Time.timeScale;
        simulationPausedByRoster = true;
        Time.timeScale = 0f;
    }

    private void RestoreSimulation()
    {
        if (!simulationPausedByRoster)
            return;

        simulationPausedByRoster = false;
        if (Mathf.Approximately(Time.timeScale, 0f))
            Time.timeScale = Mathf.Max(0f, timeScaleBeforeRoster);
    }
}
