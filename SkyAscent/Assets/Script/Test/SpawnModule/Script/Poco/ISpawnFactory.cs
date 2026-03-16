using UnityEngine;


/// <summary>
/// Factory tạo instance cho pool (Prefab/Addressables).
/// </summary>
public interface ISpawnFactory
{
    bool IsReady { get; }

    /// <summary>
    /// Tạo 1 instance.
    /// </summary>
    /// <returns>GameObject hoặc null nếu chưa ready.</returns>
    GameObject CreateInstance(Transform parent, Vector3 position, Quaternion rotation);

    /// <summary>
    /// Dispose resource của factory.
    /// </summary>
    void Dispose();
}

/// <summary>
/// Factory dùng prefab thường (sync).
/// </summary>
public sealed class PrefabSpawnFactory : ISpawnFactory
{
    private readonly GameObject _prefab;

    public bool IsReady => _prefab != null;

    public PrefabSpawnFactory(GameObject prefab)
    {
        _prefab = prefab;
    }

    public GameObject CreateInstance(Transform parent, Vector3 position, Quaternion rotation)
    {
        if (_prefab == null) return null;
        var go = Object.Instantiate(_prefab, position, rotation, parent);
        return go;
    }

    public void Dispose()
    {
        // nothing
    }
}
