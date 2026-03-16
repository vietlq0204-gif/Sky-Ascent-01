using UnityEngine;


/// <summary>
/// Base cho gameplay spawner: chỉ cần override BuildRequest().
/// </summary>
public abstract class SpawnerBase : MonoBehaviour, IInject<SpawnManager>
{
    [SerializeField] protected SpawnManager spawnManager;
    protected SpawnHandle handle;

    public void Inject(SpawnManager context)
    {
        spawnManager = context;
    }

    /// <summary>
    /// Gameplay gọi để spawn.
    /// </summary>
    /// <returns>SpawnHandle.</returns>
    public SpawnHandle Spawn()
    {
        if (spawnManager == null) spawnManager = FindFirstObjectByType<SpawnManager>();
        if (spawnManager == null) return null;

        var req = BuildRequest();
        handle = spawnManager.Spawn(req);
        return handle;
    }

    /// <summary>
    /// Gameplay gọi để despawn hết.
    /// </summary>
    public void DespawnAll()
    {
        if (handle != null) handle.DespawnAll();
    }

    /// <summary>
    /// Override để tạo request theo logic riêng.
    /// </summary>
    /// <remarks>Không được tạo GC nặng nếu spawn nhiều.</remarks>
    protected abstract SpawnRequest BuildRequest();
}
