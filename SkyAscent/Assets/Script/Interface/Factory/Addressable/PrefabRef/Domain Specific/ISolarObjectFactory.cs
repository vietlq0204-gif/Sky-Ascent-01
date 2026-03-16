using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Factory cụ thể cho SolarObject
/// </summary>
public interface ISolarObjectFactory
{
    /// <summary>
    /// Tạo instance từ SolarObjectSO
    /// </summary>
    /// <param name="so"></param>
    /// <param name="parent"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    Task<GameObject> CreateAsync(
        CosmicObjectSO so,
        Transform parent = null,
        CancellationToken ct = default);

    /// <summary>
    /// Tiền tải Prefab của SolarObjectSO
    /// </summary>
    /// <param name="so"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    Task PreloadAsync(CosmicObjectSO so, CancellationToken ct = default);

    /// <summary>
    ///  tải Prefab của danh sách SolarObjectSO
    /// </summary>
    /// <param name="list"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    Task PreloadAsync(IReadOnlyList<CosmicObjectSO> list, CancellationToken ct = default);

    void ReleaseAllPrefabs();
    void DestroyInstance(GameObject instance);
}
