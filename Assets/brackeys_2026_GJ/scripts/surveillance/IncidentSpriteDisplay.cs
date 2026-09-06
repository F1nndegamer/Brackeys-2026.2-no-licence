using UnityEngine;

[DisallowMultipleComponent]
public sealed class IncidentSpriteDisplay : MonoBehaviour
{
    [SerializeField] private ShiftDirector shiftDirector;
    [SerializeField] private EnclosureLayout enclosureLayout;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private int sortingOrder = -10;

    private void Awake()
    {
        if (shiftDirector == null)
            shiftDirector = GetComponent<ShiftDirector>();

        if (spriteRenderer == null)
        {
            GameObject display = new GameObject("Incident Background");
            display.transform.SetParent(transform, false);
            spriteRenderer = display.AddComponent<SpriteRenderer>();
        }

        if (enclosureLayout == null)
            enclosureLayout = GetComponent<EnclosureLayout>();

        spriteRenderer.gameObject.name = "Incident Background";
        spriteRenderer.gameObject.SetActive(true);
        spriteRenderer.sortingOrder = sortingOrder;
        spriteRenderer.enabled = false;
    }

    private void OnEnable()
    {
        if (shiftDirector != null)
        {
            shiftDirector.IncidentOccurred += ShowIncident;
            shiftDirector.AccusationResolved += HideIncident;
        }
    }

    private void OnDisable()
    {
        if (shiftDirector != null)
        {
            shiftDirector.IncidentOccurred -= ShowIncident;
            shiftDirector.AccusationResolved -= HideIncident;
        }
    }

    private void Update()
    {
        if (spriteRenderer != null && spriteRenderer.enabled &&
            (shiftDirector == null || shiftDirector.CurrentIncident == null))
            HideIncident(false);
    }

    private void ShowIncident(DaycareIncident incident)
    {
        HideIncident(false);

        if (incident == null || incident.Definition == null)
            return;

        if (spriteRenderer == null)
            return;

        if (incident.Zone == null)
            return;

        Sprite incidentSprite = incident.Definition.IncidentSprite;

        if (incidentSprite == null)
        {
            spriteRenderer.enabled = false;
            return;
        }

        if (enclosureLayout == null)
        {
            Debug.LogWarning(
                "IncidentSpriteDisplay is missing its EnclosureLayout reference.",
                this
            );
            return;
        }

        GameObject room = enclosureLayout.GetZoneEnvironment(incident.Zone.Id);

        if (room == null)
        {
            Debug.LogWarning(
                $"Could not find environment for zone '{incident.Zone.DisplayName}' ({incident.Zone.Id}).",
                this
            );
            return;
        }

        Transform spriteTransform = spriteRenderer.transform;

        spriteRenderer.gameObject.SetActive(true);
        spriteRenderer.sprite = incidentSprite;
        spriteRenderer.color = Color.white;
        spriteRenderer.flipX = false;
        spriteRenderer.flipY = false;
        spriteRenderer.drawMode = SpriteDrawMode.Simple;
        spriteRenderer.sortingOrder = sortingOrder;
        spriteTransform.SetParent(room.transform, false);
        spriteTransform.localPosition = Vector3.zero;
        spriteTransform.localRotation = Quaternion.identity;

        Vector2 spriteSize = incidentSprite.bounds.size;
        if (enclosureLayout.TryGetZoneFrameSize(incident.Zone.Id, out Vector2 frameSize))
        {
            spriteTransform.localScale = new Vector3(
                spriteSize.x > 0f ? frameSize.x / spriteSize.x : 1f,
                spriteSize.y > 0f ? frameSize.y / spriteSize.y : 1f,
                1f
            );
            spriteTransform.localPosition = PickLocalOffset(incident, frameSize);
        }
        else
        {
            spriteTransform.localScale = Vector3.one;
        }

        spriteRenderer.enabled = true;
    }

    private void HideIncident(bool _)
    {
        if (spriteRenderer == null)
            return;

        spriteRenderer.enabled = false;
    }

    private Vector3 PickLocalOffset(DaycareIncident incident, Vector2 frameSize)
    {
        Vector2 minimum = Vector2.Min(
            incident.Definition.IncidentOffsetMinimum,
            incident.Definition.IncidentOffsetMaximum
        );
        Vector2 maximum = Vector2.Max(
            incident.Definition.IncidentOffsetMinimum,
            incident.Definition.IncidentOffsetMaximum
        );

        int seed = shiftDirector != null ? shiftDirector.CurrentRunSeed : 0;
        seed = unchecked(seed * 397 ^ (int)incident.Definition.type * 7919);
        seed = unchecked(seed * 397 ^ Mathf.RoundToInt(incident.Time * 1000f));
        seed = unchecked(seed * 397 ^ Animator.StringToHash(incident.Zone.Id));
        System.Random random = new System.Random(seed);

        float normalisedX = Mathf.Lerp(minimum.x, maximum.x, (float)random.NextDouble());
        float normalisedY = Mathf.Lerp(minimum.y, maximum.y, (float)random.NextDouble());
        return new Vector3(normalisedX * frameSize.x, normalisedY * frameSize.y, 0f);
    }
}
