using UnityEngine;


/// <summary>
/// Thuật toán tạo pose spawn (position/rotation).
/// </summary>
public interface ISpawnAlgorithm
{
    /// <summary>
    /// Get pose cho index.
    /// </summary>
    /// <remarks>Phải deterministic nếu truyền seed.</remarks>
    void GetPose(int index, uint seed, out Vector3 position, out Quaternion rotation);
}

/// <summary>
/// Thuật toán đơn giản: spawn quanh 1 origin theo vòng tròn.
/// </summary>
public sealed class SimplePointAlgorithm : ISpawnAlgorithm
{
    private readonly Vector3 _origin;
    private readonly float _radius;

    public SimplePointAlgorithm(Vector3 origin, float radius)
    {
        _origin = origin;
        _radius = radius;
    }

    public void GetPose(int index, uint seed, out Vector3 position, out Quaternion rotation)
    {
        // pseudo-random deterministic (không alloc)
        uint x = (uint)(index + 1) * 747796405u + seed * 2891336453u;
        x ^= x >> 16;
        float t = (x & 0xFFFF) / 65535f;

        float angle = t * Mathf.PI * 2f;
        position = _origin + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * _radius;
        rotation = Quaternion.identity;
    }
}

/// <summary>
/// Chế độ auto seed khi user truyền seed = 0.
/// </summary>
public enum SeedMode
{
    RuntimeRandom = 0,
    SessionStable = 1,
    ContextStable = 2
}

/// <summary>
/// Resolve seed: nếu seed == 0 thì tạo seed "ảo" theo mode.
/// </summary>
public static class SeedResolver
{
    private static uint _sessionSeed;
    private static uint _counter; // tăng dần để mỗi request seed=0 khác nhau trong session

    /// <summary>
    /// Khởi tạo seed của session (gọi khi start game/new run).
    /// </summary>
    /// <remarks>Nếu không gọi, sessionSeed sẽ tự được tạo khi lần đầu resolve.</remarks>
    public static void SetSessionSeed(uint sessionSeed)
    {
        _sessionSeed = sessionSeed == 0 ? 1u : sessionSeed;
        _counter = 0;
    }

    /// <summary>
    /// Resolve seed theo mode.
    /// </summary>
    /// <param name="seed">Seed đầu vào; nếu 0 thì auto-seed.</param>
    /// <param name="mode">Chế độ auto-seed.</param>
    /// <param name="keyHash">Key hash để trộn.</param>
    /// <param name="contextHash">Context hash (tuỳ chọn): instanceId/scene/zone.</param>
    /// <returns>Seed cuối cùng (luôn != 0).</returns>
    public static uint Resolve(uint seed, SeedMode mode, int keyHash, int contextHash = 0)
    {
        if (seed != 0) return seed;

        // đảm bảo session seed có giá trị
        if (_sessionSeed == 0)
        {
            // Runtime init (không alloc)
            uint t = (uint)System.Environment.TickCount;
            _sessionSeed = Hash(t ^ 0x9E3779B9u);
            if (_sessionSeed == 0) _sessionSeed = 1u;
            _counter = 0;
        }

        switch (mode)
        {
            case SeedMode.RuntimeRandom:
                {
                    // thay đổi theo thời gian + counter
                    uint t = (uint)System.Environment.TickCount;
                    return NonZero(Hash(_sessionSeed ^ t ^ (++_counter) ^ (uint)keyHash ^ (uint)contextHash));
                }

            case SeedMode.ContextStable:
                {
                    // ổn định theo key+context+session (nếu muốn bỏ session thì bỏ _sessionSeed)
                    return NonZero(Hash(_sessionSeed ^ (uint)keyHash ^ (uint)contextHash));
                }

            case SeedMode.SessionStable:
            default:
                {
                    // khác nhau giữa các request trong session, nhưng debug được (counter)
                    return NonZero(Hash(_sessionSeed ^ (++_counter) ^ (uint)keyHash ^ (uint)contextHash));
                }
        }
    }

    /// <summary>
    /// Hash 32-bit avalanche (nhanh, phân bố tốt).
    /// </summary>
    private static uint Hash(uint x)
    {
        x ^= x >> 16;
        x *= 0x7feb352du;
        x ^= x >> 15;
        x *= 0x846ca68bu;
        x ^= x >> 16;
        return x;
    }

    private static uint NonZero(uint x) => x == 0 ? 1u : x;
}
