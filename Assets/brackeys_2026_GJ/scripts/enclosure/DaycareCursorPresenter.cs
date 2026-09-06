using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class DaycareCursorPresenter : MonoBehaviour
{
    [SerializeField] private Texture2D normalCursor;
    [SerializeField] private Texture2D hoverCursor;
    [SerializeField] private Texture2D pressedCursor;
    [SerializeField] private Texture2D pressedHoverCursor;
    [SerializeField] private Vector2 hotSpot = new Vector2(58f, 42f);
    [SerializeField] private CursorMode cursorMode = CursorMode.ForceSoftware;
    [SerializeField] private Vector2 referenceResolution = new Vector2(1920f, 1080f);
    [SerializeField, Min(1)] private int minimumCursorSize = 12;
    [SerializeField, Min(1)] private int maximumCursorSize = 64;

    private static readonly List<RaycastResult> RaycastResults = new List<RaycastResult>();
    private readonly Dictionary<Texture2D, Texture2D> scaledCursors = new Dictionary<Texture2D, Texture2D>();
    private EnclosureSurveillanceController surveillanceController;
    private EnclosureLineupPresenter lineupPresenter;
    private Texture2D activeSourceCursor;
    private Texture2D activeScaledCursor;
    private int cachedScreenWidth;
    private int cachedScreenHeight;

    private void Awake()
    {
        surveillanceController = FindFirstObjectByType<EnclosureSurveillanceController>();
        lineupPresenter = FindFirstObjectByType<EnclosureLineupPresenter>();
        RefreshScaledCursors(true);
        ApplyCursor(normalCursor);
    }

    private void Update()
    {
        if (Mouse.current == null)
            return;

        RefreshScaledCursors(false);

        bool hovering = IsPointerOverInteractiveUi() ||
            (surveillanceController != null &&
                (surveillanceController.HasHoveredMonkey ||
                 surveillanceController.HasHoveredCleanableItem)) ||
            (lineupPresenter != null && lineupPresenter.HasHoveredSuspect);
        bool pressed = Mouse.current.leftButton.isPressed;
        Texture2D cursor = pressed
            ? hovering ? pressedHoverCursor : pressedCursor
            : hovering ? hoverCursor : normalCursor;
        ApplyCursor(cursor != null ? cursor : normalCursor);
    }

    private void OnDisable()
    {
        activeSourceCursor = null;
        activeScaledCursor = null;
        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
    }

    private void OnDestroy()
    {
        DestroyScaledCursors();
    }

    private void ApplyCursor(Texture2D cursor)
    {
        Texture2D scaledCursor = GetScaledCursor(cursor);
        if (activeSourceCursor == cursor && activeScaledCursor == scaledCursor)
            return;

        activeSourceCursor = cursor;
        activeScaledCursor = scaledCursor;

        if (cursor == null || scaledCursor == null)
        {
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
            return;
        }

        Vector2 scaledHotSpot = new Vector2(
            hotSpot.x * scaledCursor.width / cursor.width,
            hotSpot.y * scaledCursor.height / cursor.height);
        Cursor.SetCursor(scaledCursor, scaledHotSpot, cursorMode);
    }

    private void RefreshScaledCursors(bool force)
    {
        if (!force && cachedScreenWidth == Screen.width && cachedScreenHeight == Screen.height)
            return;

        cachedScreenWidth = Screen.width;
        cachedScreenHeight = Screen.height;
        Texture2D cursorToRestore = activeSourceCursor != null ? activeSourceCursor : normalCursor;

        DestroyScaledCursors();
        AddScaledCursor(normalCursor);
        AddScaledCursor(hoverCursor);
        AddScaledCursor(pressedCursor);
        AddScaledCursor(pressedHoverCursor);

        activeSourceCursor = null;
        activeScaledCursor = null;
        ApplyCursor(cursorToRestore);
    }

    private void AddScaledCursor(Texture2D source)
    {
        if (source == null || scaledCursors.ContainsKey(source))
            return;

        float referenceWidth = Mathf.Max(1f, referenceResolution.x);
        float referenceHeight = Mathf.Max(1f, referenceResolution.y);
        float viewportScale = Mathf.Min(Screen.width / referenceWidth, Screen.height / referenceHeight);
        int minSize = Mathf.Max(1, minimumCursorSize);
        int maxSize = Mathf.Max(minSize, maximumCursorSize);
        int targetWidth = Mathf.Clamp(Mathf.RoundToInt(source.width * viewportScale), minSize, maxSize);
        int targetHeight = Mathf.Clamp(Mathf.RoundToInt(source.height * viewportScale), minSize, maxSize);

        scaledCursors.Add(source, CreateScaledCursor(source, targetWidth, targetHeight));
    }

    private Texture2D GetScaledCursor(Texture2D source)
    {
        if (source == null)
            return null;

        if (!scaledCursors.TryGetValue(source, out Texture2D scaledCursor))
        {
            AddScaledCursor(source);
            scaledCursors.TryGetValue(source, out scaledCursor);
        }

        return scaledCursor;
    }

    private static Texture2D CreateScaledCursor(Texture2D source, int width, int height)
    {
        Texture2D scaled = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            name = source.name + "_RuntimeCursor_" + width + "x" + height,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };

        Color[] pixels = new Color[width * height];
        for (int y = 0; y < height; y++)
        {
            float v = (y + 0.5f) / height;
            for (int x = 0; x < width; x++)
            {
                float u = (x + 0.5f) / width;
                pixels[y * width + x] = source.GetPixelBilinear(u, v);
            }
        }

        scaled.SetPixels(pixels);
        scaled.Apply(false, false);
        return scaled;
    }

    private void DestroyScaledCursors()
    {
        foreach (KeyValuePair<Texture2D, Texture2D> pair in scaledCursors)
        {
            if (pair.Value != null && pair.Value != pair.Key)
                Destroy(pair.Value);
        }

        scaledCursors.Clear();
    }

    private static bool IsPointerOverInteractiveUi()
    {
        if (EventSystem.current == null || Mouse.current == null)
            return false;

        PointerEventData pointer = new PointerEventData(EventSystem.current)
        {
            position = Mouse.current.position.ReadValue()
        };
        RaycastResults.Clear();
        EventSystem.current.RaycastAll(pointer, RaycastResults);

        foreach (RaycastResult result in RaycastResults)
        {
            Selectable selectable = result.gameObject.GetComponentInParent<Selectable>();
            if (selectable != null && selectable.IsInteractable())
                return true;
        }

        return false;
    }
}
