using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class EnclosureCameraHudPresenter : MonoBehaviour, IPointerClickHandler
{
    [Serializable]
    public sealed class ZoneMarker
    {
        public string zoneId;
        public Image frame;
        public TMP_Text label;
        public Sprite selectedArtwork;
    }

    [SerializeField] private EnclosureSurveillanceController surveillanceController;
    [SerializeField] private ShiftDirector shiftDirector;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private TMP_Text zoneNameText;
    [SerializeField] private Image mapArtwork;
    [SerializeField] private Image selectedZoneArtwork;
    [SerializeField] private ZoneMarker[] zoneMarkers;
    [SerializeField] private Color normalLabelColor = new Color(0.95f, 0.9f, 0.7f, 0.85f);
    [SerializeField] private Color activeLabelColor = Color.black;

    private void Awake()
    {
        ResolveReferences();
    }

    private void Update()
    {
        ResolveReferences();

        EnclosureZone selectedZone = surveillanceController != null
            ? surveillanceController.SelectedZone
            : null;
        bool visible = selectedZone != null && shiftDirector != null &&
            (shiftDirector.Phase == ShiftPhase.Active ||
             shiftDirector.Phase == ShiftPhase.IncidentWindow ||
             shiftDirector.Phase == ShiftPhase.Welcome);

        if (canvasGroup != null)
        {
            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = visible;
            canvasGroup.blocksRaycasts = visible;
        }

        if (mapArtwork != null)
            mapArtwork.raycastTarget = visible;

        if (!visible)
            return;

        if (zoneNameText != null)
            zoneNameText.text = selectedZone.DisplayName.ToUpperInvariant();

        if (zoneMarkers == null)
            return;

        Sprite selectedArtwork = null;

        foreach (ZoneMarker marker in zoneMarkers)
        {
            if (marker == null)
                continue;

            bool selected = string.Equals(
                marker.zoneId,
                selectedZone.Id,
                StringComparison.OrdinalIgnoreCase
            );

            if (marker.label != null)
                marker.label.color = selected ? activeLabelColor : normalLabelColor;

            if (selected)
                selectedArtwork = marker.selectedArtwork;
        }

        if (selectedZoneArtwork != null)
        {
            selectedZoneArtwork.sprite = selectedArtwork;
            selectedZoneArtwork.enabled = selectedArtwork != null;
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData == null || surveillanceController == null || zoneMarkers == null)
            return;

        if (canvasGroup != null && (!canvasGroup.interactable || canvasGroup.alpha <= 0f))
            return;

        foreach (ZoneMarker marker in zoneMarkers)
        {
            if (marker == null || marker.frame == null)
                continue;

            if (!RectTransformUtility.RectangleContainsScreenPoint(
                    marker.frame.rectTransform,
                    eventData.position,
                    eventData.pressEventCamera))
                continue;

            surveillanceController.SelectZoneFeed(marker.zoneId);
            return;
        }
    }

    private void ResolveReferences()
    {
        if (surveillanceController == null)
            surveillanceController = FindFirstObjectByType<EnclosureSurveillanceController>();

        if (shiftDirector == null)
            shiftDirector = FindFirstObjectByType<ShiftDirector>();

        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();
    }
}
