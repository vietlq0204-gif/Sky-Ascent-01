using System;
using System.Collections.Generic;
using UnityEngine;

public readonly struct PoolKey : IEquatable<PoolKey>
{
    public readonly int keyHash;
    public readonly int variant;

    public PoolKey(int keyHash, int variant)
    {
        this.keyHash = keyHash;
        this.variant = variant;
    }

    public bool Equals(PoolKey other) => keyHash == other.keyHash && variant == other.variant;
    public override bool Equals(object obj) => obj is PoolKey other && Equals(other);
    public override int GetHashCode() => unchecked((keyHash * 397) ^ variant);
}

/// <summary>
/// SpawnManager quản lý mọi pool, register theo Catalog, cung cấp API spawn/despawn.
/// gắn vào một GameObject singleton trong scene (ví dụ "Managers").
/// </summary>
public sealed class SpawnManager : MonoBehaviour
{
    [Header("Catalog")]
    [SerializeField] private SpawnCatalogSO catalog; 

    [Header("Pools Root")]
    [SerializeField] private Transform poolsRoot;

    // key.hash -> pool
    private readonly Dictionary<PoolKey, GameObjectPool> _pools = new Dictionary<PoolKey, GameObjectPool>(256);

    // reusable list to reduce GC per Spawn()
    private readonly List<GameObject> _resultBuffer = new List<GameObject>(64);

    private int _nextPoolId = 1;

    private void Awake()
    {
        if (poolsRoot == null)
        {
            var go = new GameObject("PoolsRoot");
            poolsRoot = go.transform;
            poolsRoot.SetParent(transform, false);
        }

        RegisterFromCatalog();
    }

    private void OnDestroy()
    {
        foreach (var kv in _pools)
        {
            kv.Value?.Dispose();
        }
        _pools.Clear();
    }

    /// <summary>
    /// Register tất cả spawnables trong catalog.
    /// </summary>
    /// <remarks>Auto-create pools, có thể prewarm.</remarks>
    public void RegisterFromCatalog()
    {
        if (catalog == null || catalog.items == null) return;

        for (int i = 0; i < catalog.items.Count; i++)
        {
            var so = catalog.items[i];
            if (so == null) continue;

            Register(so);
        }
    }

    /// <summary>
    /// Register 1 spawnable.
    /// </summary>
    /// <returns>true nếu register OK.</returns>
    public bool Register(SpawnableSO spawnable)
    {
        if (spawnable == null) return false;
        var key = spawnable.key;
        if (key.Hash == 0) return false;

        int variantCount = GetVariantCount(spawnable);
        if (variantCount <= 0) return false;

        for (int i = 0; i < variantCount; i++)
        {
            var pk = new PoolKey(key.Hash, i);
            if (_pools.ContainsKey(pk)) continue;

            var factory = CreateFactory(spawnable, i);
            if (factory == null) continue;

#if USE_ADDRESSABLES
        if (factory is AddressablesSpawnFactory af) af.EnsureLoaded();
#endif

            var pool = new GameObjectPool(key, _nextPoolId++, factory, spawnable.poolConfig, poolsRoot);
            _pools.Add(pk, pool);
            pool.Prewarm(spawnable.poolConfig.prewarmCount);
        }

        return true;
    }

    private static int GetVariantCount(SpawnableSO so)
    {
        if (so.sourceType == SpawnSourceType.Prefab)
            return so.prefabVariants != null ? so.prefabVariants.Count : 0;

#if USE_ADDRESSABLES
    if (so.sourceType == SpawnSourceType.Addressables)
        return so.addressVariants != null ? so.addressVariants.Count : 0;
#endif

        return 0;
    }

    private ISpawnFactory CreateFactory(SpawnableSO spawnable, int variantIndex)
    {
        switch (spawnable.sourceType)
        {
            case SpawnSourceType.Prefab:
                {
                    var list = spawnable.prefabVariants;
                    if (list == null || list.Count == 0) return null;

                    variantIndex = Mathf.Clamp(variantIndex, 0, list.Count - 1);
                    var prefab = list[variantIndex].prefab;
                    if (prefab == null) return null;

                    return new PrefabSpawnFactory(prefab);
                }

            case SpawnSourceType.Addressables:
                {
#if USE_ADDRESSABLES
            var list = spawnable.addressVariants;
            if (list == null || list.Count == 0) return null;

            variantIndex = Mathf.Clamp(variantIndex, 0, list.Count - 1);
            var addr = list[variantIndex].addressKey;
            if (string.IsNullOrEmpty(addr)) return null;

            return new AddressablesSpawnFactory(addr);
#else
                    Debug.LogError("Addressables factory requested but USE_ADDRESSABLES is not defined.");
                    return null;
#endif
                }

            default:
                return null;
        }
    }

    /// <summary>
    /// Spawn batch theo request.
    /// </summary>
    /// <returns>SpawnHandle chứa list instances.</returns>
    public SpawnHandle Spawn(in SpawnRequest request)
    {
        _resultBuffer.Clear();

        if (request.count <= 0) return new SpawnHandle(this, new List<GameObject>(0));
        if (request.key.Hash == 0) return new SpawnHandle(this, new List<GameObject>(0));

        // Lấy spawnable từ catalog (để biết variants)
        var so = FindSpawnableInCatalog(request.key);
        if (so == null)
            return new SpawnHandle(this, new List<GameObject>(0));

        // Lazy register pools nếu chưa có
        Register(so);

        var algo = request.algorithm ?? new SimplePointAlgorithm(Vector3.zero, 0f);
        Transform parent = request.parent;

        for (int i = 0; i < request.count; i++)
        {
            int variantIndex = PickVariantIndex(so, request.seed, (uint)(i + 1));

            var pk = new PoolKey(request.key.Hash, variantIndex);
            if (!_pools.TryGetValue(pk, out var pool) || !pool.IsReady)
                continue;

            algo.GetPose(i, request.seed, out var pos, out var rot);

            var go = pool.Rent(parent, pos, rot);
            if (go == null) continue;

            _resultBuffer.Add(go);
        }

        var list = new List<GameObject>(_resultBuffer.Count);
        list.AddRange(_resultBuffer);
        return new SpawnHandle(this, list);
    }

    private static int PickVariantIndex(SpawnableSO so, uint seed, uint salt)
    {
        // Prefab mode
        if (so.sourceType == SpawnSourceType.Prefab)
        {
            var list = so.prefabVariants;
            int n = list != null ? list.Count : 0;
            if (n <= 0) return 0;

            float sum = 0f;
            for (int i = 0; i < n; i++) sum += Mathf.Max(0f, list[i].weight);

            if (sum <= 0f) return 0;

            float r = To01(Hash(seed ^ (salt * 0x9E3779B9u))) * sum;
            float acc = 0f;
            for (int i = 0; i < n; i++)
            {
                acc += Mathf.Max(0f, list[i].weight);
                if (r <= acc) return i;
            }
            return n - 1;
        }

#if USE_ADDRESSABLES
    // Addressables mode
    if (so.sourceType == SpawnSourceType.Addressables)
    {
        var list = so.addressVariants;
        int n = list != null ? list.Count : 0;
        if (n <= 0) return 0;

        float sum = 0f;
        for (int i = 0; i < n; i++) sum += Mathf.Max(0f, list[i].weight);
        if (sum <= 0f) return 0;

        float r = To01(Hash(seed ^ (salt * 0x9E3779B9u))) * sum;
        float acc = 0f;
        for (int i = 0; i < n; i++)
        {
            acc += Mathf.Max(0f, list[i].weight);
            if (r <= acc) return i;
        }
        return n - 1;
    }
#endif

        return 0;
    }

    private static uint Hash(uint x)
    {
        x ^= x >> 16;
        x *= 0x7feb352du;
        x ^= x >> 15;
        x *= 0x846ca68bu;
        x ^= x >> 16;
        return x == 0 ? 1u : x;
    }

    private static float To01(uint x) => (x >> 8) * (1f / 16777216f);

    /// <summary>
    /// Despawn 1 instance về pool.
    /// </summary>
    /// <returns>true nếu trả về pool OK.</returns>
    public bool Despawn(GameObject go)
    {
        if (go == null) return false;
        var pooled = go.GetComponent<PooledObject>();
        if (pooled == null) return false;
        return pooled.ReturnToPool();
    }

    private SpawnableSO FindSpawnableInCatalog(SpawnKey key)
    {
        if (catalog == null || catalog.items == null) return null;
        for (int i = 0; i < catalog.items.Count; i++)
        {
            var so = catalog.items[i];
            if (so == null) continue;
            if (so.key.Hash == key.Hash) return so;
        }
        return null;
    }
}