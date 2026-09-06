using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class EnclosureIncidentEffects : MonoBehaviour
{
    [SerializeField] private ShiftDirector shiftDirector;
    [SerializeField] private EnclosureSurveillanceController surveillanceController;
    [SerializeField] private DaycareAudioController audioController;
    [Range(0f, 1f)] [SerializeField] private float outageBrightness = 0.22f;

    private SpriteRenderer outageOverlay;
    private Texture2D outageTexture;
    private Sprite outageSprite;
    private readonly Dictionary<AudioSource, bool> musicMuteStates = new Dictionary<AudioSource, bool>();
    private bool effectsActive;

    private void Awake()
    {
        if (shiftDirector == null)
            shiftDirector = GetComponent<ShiftDirector>();

        if (surveillanceController == null)
            surveillanceController = GetComponent<EnclosureSurveillanceController>();

        if (audioController == null)
            audioController = GetComponent<DaycareAudioController>();

    }

    private void OnEnable()
    {
        if (shiftDirector != null)
            shiftDirector.IncidentOccurred += ApplyIncident;
    }

    private void OnDisable()
    {
        if (shiftDirector != null)
            shiftDirector.IncidentOccurred -= ApplyIncident;

        RestoreEffects();
    }

    private void Update()
    {
        if (!effectsActive || shiftDirector == null)
            return;

        if (outageOverlay != null && outageOverlay.enabled)
            UpdateOutageOverlayScale();

        if (shiftDirector.Phase != ShiftPhase.IncidentWindow &&
            shiftDirector.Phase != ShiftPhase.Lineup)
            RestoreEffects();
    }

    private void ApplyIncident(DaycareIncident incident)
    {
        RestoreEffects();

        if (incident == null || incident.Definition == null)
            return;

        effectsActive = true;

        switch (incident.Definition.type)
        {
            case DaycareIncidentType.AuxCord:
                if (audioController != null)
                    audioController.SetIncidentMuted(true);
                else
                    MuteLoopingMusic();
                break;
            case DaycareIncidentType.Generator:
                DarkenEnclosure();
                break;
            case DaycareIncidentType.SurveillanceConsole:
                if (surveillanceController != null)
                    surveillanceController.SetCameraGlitching(true);
                break;
            // Grenade screen shake is parked along with the rest of its effects.
            // Crater visual hook: EnclosureZone.HasCrater is set by ShiftDirector
            // (also commented out for now), so a decal prefab can hang off that flag.
            // case DaycareIncidentType.Grenade:
            //     if (surveillanceController != null)
            //         surveillanceController.ShakeCamera(0.9f);
            //     break;
        }
    }

    private void MuteLoopingMusic()
    {
        foreach (AudioSource source in FindObjectsByType<AudioSource>(FindObjectsSortMode.None))
        {
            if (!source.loop || musicMuteStates.ContainsKey(source))
                continue;

            musicMuteStates.Add(source, source.mute);
            source.mute = true;
        }
    }

    private void DarkenEnclosure()
    {
        SetOutageVisible(true);
    }

    private void SetOutageVisible(bool visible)
    {
        if (visible)
            EnsureOutageOverlay();

        if (outageOverlay == null)
            return;

        outageOverlay.color = new Color(0f, 0f, 0f, 1f - outageBrightness);
        outageOverlay.enabled = visible;

        if (visible)
            UpdateOutageOverlayScale();
    }

    private void EnsureOutageOverlay()
    {
        if (outageOverlay != null)
            return;

        Camera camera = surveillanceController != null
            ? surveillanceController.SurveillanceCamera
            : Camera.main;
        if (camera == null)
            return;

        outageTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
        {
            name = "Power Outage Overlay Texture",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };
        outageTexture.SetPixel(0, 0, Color.white);
        outageTexture.Apply();

        outageSprite = Sprite.Create(
            outageTexture,
            new Rect(0f, 0f, 1f, 1f),
            new Vector2(0.5f, 0.5f),
            1f
        );
        outageSprite.name = "Power Outage Overlay Sprite";
        outageSprite.hideFlags = HideFlags.HideAndDontSave;

        GameObject overlayObject = new GameObject("Power Outage Overlay");
        overlayObject.transform.SetParent(camera.transform, false);
        overlayObject.transform.localPosition = new Vector3(0f, 0f, 1f);
        outageOverlay = overlayObject.AddComponent<SpriteRenderer>();
        outageOverlay.sprite = outageSprite;
        outageOverlay.sortingOrder = 170;
        outageOverlay.enabled = false;
    }

    private void UpdateOutageOverlayScale()
    {
        if (outageOverlay == null)
            return;

        Camera camera = outageOverlay.GetComponentInParent<Camera>();
        if (camera == null || !camera.orthographic)
            return;

        float height = camera.orthographicSize * 2f;
        outageOverlay.transform.localScale = new Vector3(
            height * camera.aspect,
            height,
            1f
        );
    }

    private void RestoreEffects()
    {
        SetOutageVisible(false);

        foreach (KeyValuePair<AudioSource, bool> entry in musicMuteStates)
        {
            if (entry.Key != null)
                entry.Key.mute = entry.Value;
        }

        musicMuteStates.Clear();

        if (audioController != null)
            audioController.SetIncidentMuted(false);

        if (surveillanceController != null)
            surveillanceController.SetCameraGlitching(false);

        effectsActive = false;
    }

    private void OnDestroy()
    {
        if (outageOverlay != null)
            Destroy(outageOverlay.gameObject);
        if (outageSprite != null)
            Destroy(outageSprite);
        if (outageTexture != null)
            Destroy(outageTexture);
    }
}
