using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Provides spherical navigation data for camera.
/// </summary>
public interface ISphericalMap
{
    /// <summary>World-space center of sphere.</summary>
    Vector3 Center { get; }

    /// <summary>World-space radius.</summary>
    float Radius { get; }
}

/// <summary>
/// Provides spherical navigation data based on baked points.
/// </summary>
/// <remarks>
/// - Uses PointBaker as data source
/// - Implements ISphericalMap for camera abstraction
/// - No camera logic inside
/// </remarks>
[RequireComponent(typeof(PointBaker))]
[RequireComponent(typeof(SphereCollider))]
public sealed class GlobularBaker : MonoBehaviour, ISphericalMap
{
    private PointBaker _pointBaker;
    private SphereCollider _sphereCollider;

    [SerializeField] private Vector3 _center;
    [SerializeField] private float _radius;

    /// <summary>
    /// World-space center of sphere.
    /// </summary>
    public Vector3 Center => _center;

    /// <summary>
    /// World-space radius of sphere.
    /// </summary>
    public float Radius => _radius;

    /// <summary>
    /// Unity Awake.
    /// </summary>
    private void Awake()
    {
        CacheComponents();
        BakeCenter();
        BakeRadius();
    }

    /// <summary>
    /// Cache required components.
    /// </summary>
    private void CacheComponents()
    {
        _pointBaker = GetComponent<PointBaker>();
        _sphereCollider = GetComponent<SphereCollider>();
        _sphereCollider.isTrigger = true;
    }

    /// <summary>
    /// Resolve center I using safe rules.
    /// </summary>
    private void BakeCenter()
    {
        var listPoint = _pointBaker.listPoint;

        if (listPoint == null || listPoint.pointData == null)
        {
            _center = transform.position;
            return;
        }

        int count = listPoint.pointData.Length;

        if (count == 0)
        {
            _center = transform.position;
            return;
        }

        if (count == 1)
        {
            _center = listPoint.pointData[0].point.transform.position;
            return;
        }

        if (count == 2)
        {
            Vector3 a = listPoint.pointData[0].point.transform.position;
            Vector3 b = listPoint.pointData[1].point.transform.position;
            _center = (a + b) * 0.5f;
            return;
        }

        // count >= 3
        var cache = new ListPoint.PathCache();

        if (listPoint.BuildPathCache(cache) &&
            listPoint.TryGetMiddleCoordinate(cache, out var midByLength))
        {
            _center = midByLength;
            return;
        }

        // fallback
        if (listPoint.TryGetMiddlePoint(out var midGo, out _))
        {
            _center = midGo.transform.position;
            return;
        }

        _center = transform.position;
    }


    /// <summary>
    /// Bake scale-aware radius from SphereCollider.
    /// </summary>
    private void BakeRadius()
    {
        _sphereCollider.radius = 10f;

        float maxScale = Mathf.Max(
            transform.lossyScale.x,
            transform.lossyScale.y,
            transform.lossyScale.z
        );

        _radius = _sphereCollider.radius * maxScale;
    }

#if UNITY_EDITOR
    /// <summary>
    /// Editor validation.
    /// </summary>
    private void OnValidate()
    {
        if (_sphereCollider != null)
            _sphereCollider.radius = 10f;
    }
#endif
}
