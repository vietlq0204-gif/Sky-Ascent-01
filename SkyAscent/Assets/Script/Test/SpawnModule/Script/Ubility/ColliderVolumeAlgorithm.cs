using UnityEngine;

/// <summary>
/// Spawn random point trong collider volume (approx) bằng AABB + inside-check.
/// </summary>
public sealed class ColliderVolumeAlgorithm : ISpawnAlgorithm
{
    private readonly Collider _collider;

    private readonly int _maxTryPerPoint;
    private readonly int _candidatesPerPoint;
    private readonly float _minDistance;
    private readonly float _minDistanceSqr;

    // buffer lưu các điểm đã chọn (tối ưu: cấp phát 1 lần)
    private Vector3[] _placed;
    private int _placedCount;

    public ColliderVolumeAlgorithm(
        Collider collider,
        int maxTryPerPoint = 32,
        int candidatesPerPoint = 16,
        float minDistance = 0.5f,
        int maxCount = 256)
    {
        _collider = collider;
        _maxTryPerPoint = Mathf.Max(1, maxTryPerPoint);
        _candidatesPerPoint = Mathf.Max(1, candidatesPerPoint);
        _minDistance = Mathf.Max(0f, minDistance);
        _minDistanceSqr = _minDistance * _minDistance;

        _placed = new Vector3[Mathf.Max(1, maxCount)];
        _placedCount = 0;
    }

    /// <summary>
    /// Reset state trước khi spawn batch mới.
    /// </summary>
    /// <remarks>Spawner nên gọi mỗi lần Spawn().</remarks>
    public void ResetPlaced()
    {
        _placedCount = 0;
    }

    public void GetPose(int index, uint seed, out Vector3 position, out Quaternion rotation)
    {
        rotation = Quaternion.identity;

        position = _collider != null ? _collider.bounds.center : Vector3.zero;
        if (_collider == null) return;

        var b = _collider.bounds;

        // ensure buffer đủ
        if (index == 0 && _placedCount != 0)
        {
            // nếu caller quên ResetPlaced, vẫn cố “chạy” nhưng sẽ bias.
        }

        // Nếu buffer không đủ, fallback
        if (index >= _placed.Length)
        {
            position = b.center;
            return;
        }

        // deterministic state per index
        uint state = Hash((uint)(index + 1) ^ seed);

        Vector3 best = b.center;
        float bestScore = -1f;

        // tìm điểm tốt nhất (candidate có khoảng cách tới nearest placed lớn nhất)
        int totalTries = _maxTryPerPoint;
        while (totalTries-- > 0)
        {
            // tạo 1 batch candidates
            for (int c = 0; c < _candidatesPerPoint; c++)
            {
                Vector3 p = SampleInsideBounds(ref state, b);

                // inside-check
                var cp = _collider.ClosestPoint(p);
                if ((cp - p).sqrMagnitude > 1e-6f) continue;

                // score = khoảng cách tới nearest placed
                float nearestSqr = float.PositiveInfinity;
                for (int i = 0; i < _placedCount; i++)
                {
                    float dSqr = (p - _placed[i]).sqrMagnitude;
                    if (dSqr < nearestSqr) nearestSqr = dSqr;

                    // early break: nếu đã dưới minDistance thì không cần tính tiếp
                    if (_minDistance > 0f && nearestSqr < _minDistanceSqr)
                        break;
                }

                // Nếu chưa có điểm nào trước đó, accept luôn điểm đầu tiên hợp lệ
                if (_placedCount == 0)
                {
                    best = p;
                    bestScore = float.PositiveInfinity;
                    goto ACCEPT;
                }

                // enforce minDistance nếu có
                if (_minDistance > 0f && nearestSqr < _minDistanceSqr)
                    continue;

                // best-candidate: maximize nearest distance
                if (nearestSqr > bestScore)
                {
                    bestScore = nearestSqr;
                    best = p;
                }
            }

            // nếu tìm được candidate hợp lệ
            if (bestScore >= 0f)
                break;
        }

    ACCEPT:
        position = best;
        _placed[_placedCount++] = best;
    }

    /// <summary>
    /// Sample 1 điểm trong bounds với RNG deterministic.
    /// </summary>
    private static Vector3 SampleInsideBounds(ref uint state, Bounds b)
    {
        float rx = To01(Next(ref state));
        float ry = To01(Next(ref state));
        float rz = To01(Next(ref state));

        return new Vector3(
            Mathf.Lerp(b.min.x, b.max.x, rx),
            Mathf.Lerp(b.min.y, b.max.y, ry),
            Mathf.Lerp(b.min.z, b.max.z, rz)
        );
    }

    /// <summary>
    /// RNG step (LCG) nhưng dùng output 32-bit đầy đủ + convert tốt.
    /// </summary>
    private static uint Next(ref uint s)
    {
        s = s * 1664525u + 1013904223u;
        return s;
    }

    private static float To01(uint x)
    {
        // lấy 24-bit cao -> float mantissa ổn, tránh dùng 16-bit thấp
        return (x >> 8) * (1f / 16777216f);
    }

    /// <summary>
    /// Hash avalanche để trộn index/seed.
    /// </summary>
    private static uint Hash(uint x)
    {
        x ^= x >> 16;
        x *= 0x7feb352du;
        x ^= x >> 15;
        x *= 0x846ca68bu;
        x ^= x >> 16;
        return x == 0 ? 1u : x;
    }
}
