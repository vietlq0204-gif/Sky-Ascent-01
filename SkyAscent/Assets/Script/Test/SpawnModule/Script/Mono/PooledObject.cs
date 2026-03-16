using UnityEngine;


/// <summary>
/// Component gắn lên mọi object trong pool để return O(1).
/// </summary>
public sealed class PooledObject : MonoBehaviour
{
    private GameObjectPool _ownerPool;
    private int _poolId;

    /// <summary>
    /// Bind object với pool owner.
    /// </summary>
    /// <remarks>Chỉ pool gọi.</remarks>
    public void Bind(GameObjectPool owner, int poolId)
    {
        _ownerPool = owner;
        _poolId = poolId;
    }

    /// <summary>
    /// Trả object về pool.
    /// </summary>
    /// <returns>true nếu return thành công.</returns>
    public bool ReturnToPool()
    {
        if (_ownerPool == null) return false;
        return _ownerPool.Return(gameObject, _poolId);
    }
}