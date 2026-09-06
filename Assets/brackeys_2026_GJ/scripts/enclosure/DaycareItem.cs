using UnityEngine;

// One physical thing lying in the enclosure - a banana, a poo, a stash.
[DisallowMultipleComponent]
public class DaycareItem : MonoBehaviour
{
    [Header("Drop Shadow")]
    [SerializeField] private Sprite dropShadowSprite;
    [SerializeField, Range(0f, 1f)] private float dropShadowStartProgress = 0.5f;
    [SerializeField, Range(0.01f, 1f)] private float dropShadowStartScale = 0.25f;
    [SerializeField, Min(0.01f)] private float dropShadowFinalScale = 1f;
    [SerializeField] private float dropShadowVerticalOffset = -0.08f;

    private SpriteRenderer spriteRenderer;
    private SpriteRenderer dropShadowRenderer;
    private Vector3 groundPoint;
    private Vector3 dropOrigin;
    private float dropDuration;
    private float dropElapsed;
    private float rotTimer;

    public DaycareItemDefinition Definition { get; private set; }
    public EnclosureZone Zone { get; private set; }
    public MonkeyActor ClaimedBy { get; private set; }
    public bool IsDropping { get; private set; }
    public bool IsClaimed => ClaimedBy != null;
    public Vector2 Position => transform.position;
    public DaycareItemKind Kind => Definition != null ? Definition.kind : DaycareItemKind.Banana;

    public void Configure(
        DaycareItemDefinition definition,
        EnclosureZone zone,
        Vector3 point,
        bool animateDrop,
        float dropHeight,
        float dropSeconds
    )
    {
        Definition = definition;
        Zone = zone;
        groundPoint = point;
        rotTimer = 0f;

        ApplyDefinitionVisuals();

        if (animateDrop && dropSeconds > 0f && dropHeight > 0f)
        {
            dropOrigin = point + Vector3.up * dropHeight;
            dropDuration = dropSeconds;
            dropElapsed = 0f;
            IsDropping = true;
            transform.position = dropOrigin;
            ShowDropShadow();
        }
        else
        {
            IsDropping = false;
            transform.position = groundPoint;
            HideDropShadow();
        }
    }

    public void Tick(float deltaTime)
    {
        if (IsDropping)
        {
            AdvanceDrop(deltaTime);
            return;
        }

        // A claimed item never spoils under the monkey walking towards it.
        if (Definition == null || !Definition.CanSpoil || IsClaimed)
            return;

        rotTimer += deltaTime;

        if (rotTimer >= Definition.rotAfterSeconds)
            Spoil();
    }

    public bool TryClaim(MonkeyActor monkey)
    {
        if (monkey == null || IsClaimed)
            return false;

        ClaimedBy = monkey;
        return true;
    }

    public void ReleaseClaim()
    {
        ClaimedBy = null;
    }

    private void AdvanceDrop(float deltaTime)
    {
        dropElapsed += deltaTime;

        float t = Mathf.Clamp01(dropElapsed / dropDuration);
        transform.position = Vector3.Lerp(dropOrigin, groundPoint, t * t);
        UpdateDropShadow(t);

        if (t < 1f)
            return;

        transform.position = groundPoint;
        IsDropping = false;
        UpdateDropShadow(1f);
    }

    private void Spoil()
    {
        Definition = Definition.rotsInto;
        rotTimer = 0f;
        ApplyDefinitionVisuals();
    }

    private void ApplyDefinitionVisuals()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        if (spriteRenderer == null || Definition == null)
            return;

        spriteRenderer.sprite = Definition.sprite;
        spriteRenderer.sortingOrder = Definition.sortingOrder;
        name = Definition.displayName;
    }

    private void ShowDropShadow()
    {
        if (Definition == null || dropShadowSprite == null)
            return;

        if (dropShadowRenderer == null)
        {
            GameObject shadow = new GameObject("Drop Shadow");
            shadow.transform.SetParent(transform, false);
            dropShadowRenderer = shadow.AddComponent<SpriteRenderer>();
        }

        dropShadowRenderer.sprite = dropShadowSprite;
        dropShadowRenderer.sortingOrder = Definition.sortingOrder - 1;
        dropShadowRenderer.color = Color.black;
        UpdateDropShadow(0f);
    }

    private void UpdateDropShadow(float progress)
    {
        if (dropShadowRenderer == null)
            return;

        if (progress < dropShadowStartProgress)
        {
            dropShadowRenderer.enabled = false;
            return;
        }

        dropShadowRenderer.enabled = true;
        float growth = Mathf.InverseLerp(dropShadowStartProgress, 1f, progress);
        growth = Mathf.SmoothStep(0f, 1f, growth);
        float size = Mathf.Lerp(dropShadowStartScale, dropShadowFinalScale, growth);
        dropShadowRenderer.transform.position = groundPoint +
            Vector3.up * dropShadowVerticalOffset;
        dropShadowRenderer.transform.localScale = new Vector3(size, size, 1f);
        dropShadowRenderer.color = Color.black;
    }

    private void HideDropShadow()
    {
        if (dropShadowRenderer != null)
            dropShadowRenderer.enabled = false;
    }
}
