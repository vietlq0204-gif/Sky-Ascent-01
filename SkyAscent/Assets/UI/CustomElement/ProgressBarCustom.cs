using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

[UxmlElement]
public partial class ProgressBarCustom : VisualElement
{
    private const float DefaultWidth = 400f;
    private const float DefaultHeight = 25f;

    private float _min = 0f;
    private float _max = 1f;
    private float _value = 1f;
    private float _angle = 0f;
    private Color _fillColor = new(0.22f, 0.72f, 0.33f, 1f);
    private Color _trackColor = new(0.15f, 0.15f, 0.15f, 1f);

    [UxmlAttribute("min")]
    public float min
    {
        get => _min;
        set
        {
            _min = value;
            if (_max < _min)
            {
                _max = _min;
            }

            ClampValue();
            MarkDirtyRepaint();
        }
    }

    [UxmlAttribute("max")]
    public float max
    {
        get => _max;
        set
        {
            _max = Mathf.Max(value, _min);
            ClampValue();
            MarkDirtyRepaint();
        }
    }

    [UxmlAttribute("value")]
    public float value
    {
        get => _value;
        set
        {
            _value = Mathf.Clamp(value, _min, _max);
            MarkDirtyRepaint();
        }
    }

    // 0 = trai sang phai, 90 = tren xuong duoi, 180 = phai sang trai, 270 = duoi len tren.
    [UxmlAttribute("angle")]
    public float angle
    {
        get => _angle;
        set
        {
            _angle = value;
            MarkDirtyRepaint();
        }
    }

    [UxmlAttribute("fill-color")]
    public Color fillColor
    {
        get => _fillColor;
        set
        {
            _fillColor = value;
            MarkDirtyRepaint();
        }
    }

    [UxmlAttribute("track-color")]
    public Color trackColor
    {
        get => _trackColor;
        set
        {
            _trackColor = value;
            MarkDirtyRepaint();
        }
    }

    public float normalizedValue => Mathf.Approximately(_min, _max) ? 0f : Mathf.InverseLerp(_min, _max, _value);

    public ProgressBarCustom()
    {
        style.width = DefaultWidth;
        style.height = DefaultHeight;
        generateVisualContent += OnGenerateVisualContent;
    }

    public void SetValueWithoutNotify(float newValue)
    {
        _value = Mathf.Clamp(newValue, _min, _max);
        MarkDirtyRepaint();
    }

    private void ClampValue()
    {
        _value = Mathf.Clamp(_value, _min, _max);
    }

    private void OnGenerateVisualContent(MeshGenerationContext context)
    {
        Rect rect = contentRect;
        if (rect.width <= 0f || rect.height <= 0f)
        {
            return;
        }

        var painter = context.painter2D;
        float radius = ResolveRadius();

        if (_trackColor.a > 0f)
        {
            DrawRoundedRect(painter, rect, _trackColor, radius);
        }

        if (normalizedValue >= 1f)
        {
            DrawRoundedRect(painter, rect, _fillColor, radius);
            return;
        }

        List<Vector2> fillPolygon = BuildFillPolygon(rect, normalizedValue, _angle);
        if (fillPolygon.Count >= 3)
        {
            DrawPolygon(painter, fillPolygon, _fillColor);
        }
    }

    private float ResolveRadius()
    {
        float topLeft = resolvedStyle.borderTopLeftRadius;
        float topRight = resolvedStyle.borderTopRightRadius;
        float bottomRight = resolvedStyle.borderBottomRightRadius;
        float bottomLeft = resolvedStyle.borderBottomLeftRadius;
        float radius = Mathf.Min(topLeft, topRight, bottomRight, bottomLeft);
        float maxRadius = Mathf.Min(contentRect.width, contentRect.height) * 0.5f;
        return Mathf.Clamp(radius, 0f, maxRadius);
    }

    private static void DrawRoundedRect(Painter2D painter, Rect rect, Color color, float radius)
    {
        if (radius <= 0f)
        {
            DrawPolygon(painter, new List<Vector2>
            {
                new(rect.xMin, rect.yMin),
                new(rect.xMax, rect.yMin),
                new(rect.xMax, rect.yMax),
                new(rect.xMin, rect.yMax),
            }, color);
        }
        else
        {
            DrawPolygon(painter, BuildRoundedRectPolygon(rect, radius), color);
        }
    }

    private static void DrawPolygon(Painter2D painter, List<Vector2> polygon, Color color)
    {
        painter.fillColor = color;
        painter.BeginPath();

        painter.MoveTo(polygon[0]);
        for (int i = 1; i < polygon.Count; i++)
        {
            painter.LineTo(polygon[i]);
        }

        painter.ClosePath();
        painter.Fill();
    }

    private static List<Vector2> BuildRoundedRectPolygon(Rect rect, float radius)
    {
        const int arcSegments = 6;

        var points = new List<Vector2>(arcSegments * 4 + 4);

        AddArcPoints(points, new Vector2(rect.xMax - radius, rect.yMin + radius), radius, -90f, 0f, arcSegments);
        AddArcPoints(points, new Vector2(rect.xMax - radius, rect.yMax - radius), radius, 0f, 90f, arcSegments);
        AddArcPoints(points, new Vector2(rect.xMin + radius, rect.yMax - radius), radius, 90f, 180f, arcSegments);
        AddArcPoints(points, new Vector2(rect.xMin + radius, rect.yMin + radius), radius, 180f, 270f, arcSegments);

        return points;
    }

    private static void AddArcPoints(List<Vector2> points, Vector2 center, float radius, float startAngle, float endAngle, int segments)
    {
        for (int i = 0; i <= segments; i++)
        {
            if (points.Count > 0 && i == 0)
            {
                continue;
            }

            float t = i / (float)segments;
            float angle = Mathf.Lerp(startAngle, endAngle, t) * Mathf.Deg2Rad;
            points.Add(new Vector2(
                center.x + Mathf.Cos(angle) * radius,
                center.y + Mathf.Sin(angle) * radius));
        }
    }

    private static List<Vector2> BuildFillPolygon(Rect rect, float normalized, float angleInDegrees)
    {
        var corners = new List<Vector2>(4)
        {
            new(rect.xMin, rect.yMin),
            new(rect.xMax, rect.yMin),
            new(rect.xMax, rect.yMax),
            new(rect.xMin, rect.yMax),
        };

        if (normalized <= 0f)
        {
            return new List<Vector2>();
        }

        if (normalized >= 1f)
        {
            return corners;
        }

        float radians = angleInDegrees * Mathf.Deg2Rad;
        Vector2 direction = new(Mathf.Cos(radians), Mathf.Sin(radians));

        float minProjection = float.PositiveInfinity;
        float maxProjection = float.NegativeInfinity;
        for (int i = 0; i < corners.Count; i++)
        {
            float projection = Vector2.Dot(corners[i], direction);
            minProjection = Mathf.Min(minProjection, projection);
            maxProjection = Mathf.Max(maxProjection, projection);
        }

        float threshold = Mathf.Lerp(minProjection, maxProjection, normalized);
        return ClipPolygonByHalfPlane(corners, direction, threshold);
    }

    private static List<Vector2> ClipPolygonByHalfPlane(List<Vector2> polygon, Vector2 normal, float distance)
    {
        var result = new List<Vector2>();
        if (polygon.Count == 0)
        {
            return result;
        }

        Vector2 previous = polygon[polygon.Count - 1];
        bool previousInside = Vector2.Dot(previous, normal) <= distance;

        for (int i = 0; i < polygon.Count; i++)
        {
            Vector2 current = polygon[i];
            bool currentInside = Vector2.Dot(current, normal) <= distance;

            if (currentInside != previousInside)
            {
                Vector2 intersection = IntersectSegmentWithPlane(previous, current, normal, distance);
                result.Add(intersection);
            }

            if (currentInside)
            {
                result.Add(current);
            }

            previous = current;
            previousInside = currentInside;
        }

        return result;
    }

    private static Vector2 IntersectSegmentWithPlane(Vector2 start, Vector2 end, Vector2 normal, float distance)
    {
        Vector2 segment = end - start;
        float denominator = Vector2.Dot(segment, normal);
        if (Mathf.Approximately(denominator, 0f))
        {
            return start;
        }

        float t = (distance - Vector2.Dot(start, normal)) / denominator;
        return start + segment * Mathf.Clamp01(t);
    }
}
