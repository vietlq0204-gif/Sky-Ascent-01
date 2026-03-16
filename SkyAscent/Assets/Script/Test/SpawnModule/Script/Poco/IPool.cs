using System.Collections.Generic;
using UnityEngine;


/// <summary>
/// Interface pool cơ bản.
/// </summary>
public interface IPool
{
    SpawnKey Key { get; }
    bool IsReady { get; }

    /// <summary>
    /// Rent 1 instance (có thể null nếu pool full và không grow).
    /// </summary>
    /// <returns>GameObject instance hoặc null.</returns>
    GameObject Rent(Transform parent, Vector3 position, Quaternion rotation);

    /// <summary>
    /// Return instance về pool.
    /// </summary>
    /// <returns>true nếu return OK.</returns>
    bool Return(GameObject go);

    /// <summary>
    /// Prewarm tạo trước N instance.
    /// </summary>
    /// <remarks>Prefab factory: sync; Addressables: pool sẽ ready sau khi factory ready.</remarks>
    void Prewarm(int count);

    /// <summary>
    /// Clear pool (destroy instances).
    /// </summary>
    void Dispose();
}

/// <summary>
/// Pool GameObject dùng LIFO stack để tận dụng cache locality.
/// </summary>
public sealed class GameObjectPool : IPool
{
    private readonly Stack<GameObject> _available;
    private readonly HashSet<int> _activeInstanceIds; // debug/safety
    private readonly ISpawnFactory _factory;
    private readonly PoolConfig _config;

    private readonly Transform _root;
    private readonly int _poolId;

    private int _totalCount;
    private bool _disposed;

    public SpawnKey Key { get; }
    public bool IsReady => _factory.IsReady;

    public GameObjectPool(SpawnKey key, int poolId, ISpawnFactory factory, PoolConfig config, Transform poolsRoot)
    {
        Key = key;
        _poolId = poolId;
        _factory = factory;
        _config = config;
        _config.Sanitize();

        _available = new Stack<GameObject>(Mathf.Max(4, _config.prewarmCount));
        _activeInstanceIds = new HashSet<int>();

        _root = new GameObject($"Pool_{key.Id}").transform;
        _root.SetParent(poolsRoot, false);
        _root.gameObject.SetActive(true);
    }

    public void Prewarm(int count)
    {
        if (_disposed) return;
        if (!IsReady) return;

        int target = Mathf.Min(_config.maxSize, _totalCount + count);
        while (_totalCount < target)
        {
            var go = CreateNewInstance(_root, Vector3.zero, Quaternion.identity);
            if (go == null) break;
            go.SetActive(false);
            _available.Push(go);
        }
    }

    public GameObject Rent(Transform parent, Vector3 position, Quaternion rotation)
    {
        if (_disposed) return null;
        if (!IsReady) return null;

        GameObject go = null;

        // 1) lấy từ available
        while (_available.Count > 0)
        {
            go = _available.Pop();
            if (go != null) break;
        }

        // 2) nếu không có thì tạo mới theo policy
        if (go == null)
        {
            if (_totalCount >= _config.maxSize)
            {
                if (!_config.allowGrow) return null;

                // allowGrow nhưng vẫn phải tôn trọng maxSize: nếu đã max -> fail
                return null;
            }

            int createCount = Mathf.Min(_config.growStep, _config.maxSize - _totalCount);
            // tạo 1 cái dùng ngay, phần còn lại đưa vào available
            go = CreateNewInstance(parent, position, rotation);
            if (go == null) return null;

            for (int i = 1; i < createCount; i++)
            {
                var extra = CreateNewInstance(_root, Vector3.zero, Quaternion.identity);
                if (extra == null) break;
                extra.SetActive(false);
                _available.Push(extra);
            }
        }

        // activate
        var tr = go.transform;
        tr.SetParent(parent, false);
        tr.SetPositionAndRotation(position, rotation);
        go.SetActive(true);

        _activeInstanceIds.Add(go.GetInstanceID());
        return go;
    }

    public bool Return(GameObject go)
    {
        if (_disposed) return false;
        if (go == null) return false;

        var pooled = go.GetComponent<PooledObject>();
        if (pooled == null) return false;

        // đảm bảo đúng pool
        return pooled.ReturnToPool();
    }

    internal bool Return(GameObject go, int poolId)
    {
        if (_disposed) return false;
        if (go == null) return false;
        if (poolId != _poolId) return false;

        int id = go.GetInstanceID();
        if (!_activeInstanceIds.Remove(id))
        {
            // đã return rồi hoặc bị destroy ngoài ý muốn
            // vẫn cố return để tránh leak
        }

        go.SetActive(false);
        var tr = go.transform;
        tr.SetParent(_root, false);
        _available.Push(go);
        return true;
    }

    private GameObject CreateNewInstance(Transform parent, Vector3 pos, Quaternion rot)
    {
        var go = _factory.CreateInstance(parent, pos, rot);
        if (go == null) return null;

        _totalCount++;

        var pooled = go.GetComponent<PooledObject>();
        if (pooled == null) pooled = go.AddComponent<PooledObject>();
        pooled.Bind(this, _poolId);

        return go;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        // destroy all available
        while (_available.Count > 0)
        {
            var go = _available.Pop();
            if (go != null) Object.Destroy(go);
        }

        // active objects: best effort
        _activeInstanceIds.Clear();

        if (_root != null) Object.Destroy(_root.gameObject);

        _factory.Dispose();
    }
}
