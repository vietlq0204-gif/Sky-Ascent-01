using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Factory cụ thể cho SolarObject, 
/// tái sử dụng cache generic bằng composition.
/// </summary>
public class AddressablesSolarObjectFactory : ISolarObjectFactory/*, IInject<AddressablesPrefabCache<SolarObjectSO>>*/
{
    private readonly AddressablesPrefabCache<CosmicObjectSO> _cache;

    //public void Inject(AddressablesPrefabCache<SolarObjectSO> context) { _cache = context; }

    // hiện tại không dùng DI composition đựợc nên tự tạo instance cache riêng
    public AddressablesSolarObjectFactory()
    {
        _cache = new AddressablesPrefabCache<CosmicObjectSO>();
    }

    public async Task<GameObject> CreateAsync(
        CosmicObjectSO so,
        Transform parent = null,
        CancellationToken ct = default)
    {
        if (so == null)
        {
            Debug.LogError("SolarObjectFactory.CreateAsync: so == null");
            return null;
        }

        // 1) Instantiate từ cache chung
        var instance = await _cache.InstantiateAsync(so, parent, ct);
        if (instance == null)
            return null;

        // 2) Logic riêng cho SolarObject
        ApplySolarObjectData(so, instance);

        return instance;
    }

    public Task PreloadAsync(CosmicObjectSO so, CancellationToken ct = default)
        => _cache.PreloadAsync(so, ct);

    public Task PreloadAsync(IReadOnlyList<CosmicObjectSO> list, CancellationToken ct = default)
        => _cache.PreloadAsync(list, ct);

    public void ReleaseAllPrefabs()
        => _cache.ReleaseAllPrefabs();

    public void DestroyInstance(GameObject instance)
    {
        if (instance == null) return;
        Object.Destroy(instance);                       // // instance là clone, Destroy là đủ
    }

    #region PRIVATE (logic riêng của SolarObject)

    private void ApplySolarObjectData(CosmicObjectSO so, GameObject instance)
    {
        // // Ví dụ: apply material
        //if (so.material != null)
        //{
        //    var renderer = instance.GetComponentInChildren<Renderer>();
        //    if (renderer != null)
        //    {
        //        renderer.material = so.material;
        //    }
        //}

        // // Có thể set thêm name, scale, component physics theo so.g, so.m,...
        instance.name = $"{so._type}_Instance";
    }
    #endregion
}

//Sau này nếu muốn AddressablesEnemyFactory:
//public class AddressablesEnemyFactory : IEnemyFactory
//{
//    private readonly AddressablesPrefabCache<EnemySO> _cache
//        = new AddressablesPrefabCache<EnemySO>();

//    // // ... logic tương tự, chỉ khác phần ApplyEnemyData
//}
