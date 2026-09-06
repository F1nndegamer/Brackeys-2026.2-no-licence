using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class IncidentRadialTimerGraphic : MaskableGraphic
{
    [SerializeField, Range(0f, 1f)] private float fillAmount = 1f;
    [SerializeField, Min(1f)] private float thickness = 24f;
    [SerializeField, Min(0f)] private float outlineWidth = 5f;
    [SerializeField, Range(12, 160)] private int segmentCount = 96;
    [SerializeField] private float startAngle = 90f;
    [SerializeField] private bool clockwise = true;
    [SerializeField] private Color trackColor = new Color(0.08f, 0.035f, 0.02f, 0.58f);
    [SerializeField] private Color outlineColor = Color.white;

    public float FillAmount
    {
        get => fillAmount;
        set
        {
            float clamped = Mathf.Clamp01(value);
            if (Mathf.Approximately(fillAmount, clamped))
                return;

            fillAmount = clamped;
            SetVerticesDirty();
        }
    }

    protected override void OnPopulateMesh(VertexHelper vertexHelper)
    {
        vertexHelper.Clear();

        Rect rect = GetPixelAdjustedRect();
        float outerRadius = Mathf.Max(0f, Mathf.Min(rect.width, rect.height) * 0.5f);
        if (outerRadius <= 0f)
            return;

        Vector2 center = rect.center;
        float bodyOuterRadius = Mathf.Max(0f, outerRadius - outlineWidth);
        float bodyInnerRadius = Mathf.Max(0f, bodyOuterRadius - thickness);
        float outlineInnerRadius = Mathf.Max(0f, bodyInnerRadius - outlineWidth);

        AddArc(
            vertexHelper,
            center,
            bodyOuterRadius,
            bodyInnerRadius,
            startAngle,
            360f,
            trackColor,
            segmentCount
        );

        if (fillAmount <= 0f)
            return;

        float sweep = 360f * fillAmount * (clockwise ? -1f : 1f);
        int fillSegments = Mathf.Max(1, Mathf.CeilToInt(segmentCount * fillAmount));

        if (outlineWidth > 0f && outlineColor.a > 0f)
        {
            AddArc(
                vertexHelper,
                center,
                outerRadius,
                outlineInnerRadius,
                startAngle,
                sweep,
                outlineColor,
                fillSegments
            );
        }

        AddArc(
            vertexHelper,
            center,
            bodyOuterRadius,
            bodyInnerRadius,
            startAngle,
            sweep,
            color,
            fillSegments
        );
    }

    private static void AddArc(
        VertexHelper vertexHelper,
        Vector2 center,
        float outerRadius,
        float innerRadius,
        float angleStart,
        float angleSweep,
        Color arcColor,
        int segments
    )
    {
        if (outerRadius <= innerRadius || segments <= 0 || arcColor.a <= 0f)
            return;

        int baseVertex = vertexHelper.currentVertCount;

        for (int index = 0; index <= segments; index++)
        {
            float progress = index / (float)segments;
            float radians = (angleStart + angleSweep * progress) * Mathf.Deg2Rad;
            Vector2 direction = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));

            AddVertex(vertexHelper, center + direction * outerRadius, arcColor);
            AddVertex(vertexHelper, center + direction * innerRadius, arcColor);
        }

        for (int index = 0; index < segments; index++)
        {
            int vertex = baseVertex + index * 2;
            vertexHelper.AddTriangle(vertex, vertex + 2, vertex + 1);
            vertexHelper.AddTriangle(vertex + 2, vertex + 3, vertex + 1);
        }
    }

    private static void AddVertex(VertexHelper vertexHelper, Vector2 position, Color vertexColor)
    {
        UIVertex vertex = UIVertex.simpleVert;
        vertex.position = position;
        vertex.color = vertexColor;
        vertexHelper.AddVert(vertex);
    }

#if UNITY_EDITOR
    protected override void OnValidate()
    {
        base.OnValidate();
        fillAmount = Mathf.Clamp01(fillAmount);
        thickness = Mathf.Max(1f, thickness);
        outlineWidth = Mathf.Max(0f, outlineWidth);
        segmentCount = Mathf.Clamp(segmentCount, 12, 160);
        SetVerticesDirty();
    }
#endif
}
