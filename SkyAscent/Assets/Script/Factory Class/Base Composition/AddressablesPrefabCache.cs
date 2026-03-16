using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

/// <summary>
/// Cache prefab từ Addressables để tái sử dụng nhiều lần
/// </summary>
/// <typeparam name="TConfig"></typeparam>
public class AddressablesPrefabCache<TConfig>
    where TConfig : class, IAddressablePrefabConfig
{
    /// <summary>
    /// Cache handle prefab đã LoadAssetAsync
    /// </summary>
    private readonly Dictionary<TConfig, AsyncOperationHandle<GameObject>> _prefabHandles
        = new Dictionary<TConfig, AsyncOperationHandle<GameObject>>();

    #region Public Methods

    /// <summary>
    /// Preload 1 config (load prefab vào memory, chưa instantiate)
    /// </summary>
    /// <param name="config"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    public async Task PreloadAsync(TConfig config, CancellationToken ct = default)
    {
        if (config == null || config.PrefabRef == null)
            return;

        await EnsurePrefabLoadedAsync(config, ct);
    }

    /// <summary>
    /// Preload nhiều config (vd: tất cả CosmicObject trong 1 chapter)
    /// </summary>
    /// <param name="list"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    public async Task PreloadAsync(IReadOnlyList<TConfig> list, CancellationToken ct = default)
    {
        if (list == null) return;

        foreach (var cfg in list)
        {
            if (ct.IsCancellationRequested)
                break;

            await PreloadAsync(cfg, ct);
        }
    }

    /// <summary>
    /// Instantiate từ prefab đã cache
    /// </summary>
    /// <param name="config"></param>
    /// <param name="parent"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    public async Task<GameObject> InstantiateAsync(
        TConfig config,
        Transform parent = null,
        CancellationToken ct = default)
    {
        if (config == null)
        {
            Debug.LogError($"{GetType().Name}.InstantiateAsync: config == null");
            return null;
        }

        if (config.PrefabRef == null)
        {
            Debug.LogError($"{GetType().Name}.InstantiateAsync: config {config} chưa gán PrefabRef");
            return null;
        }

        // // đảm bảo đã load prefab
        await EnsurePrefabLoadedAsync(config, ct);

        if (!_prefabHandles.TryGetValue(config, out var handle) ||
            handle.Status != AsyncOperationStatus.Succeeded)
        {
            Debug.LogError($"{GetType().Name}.InstantiateAsync: không load được prefab cho {config}");
            return null;
        }

        var prefab = handle.Result;                       // // GameObject prefab
        var instance = UnityEngine.Object.Instantiate(prefab, parent);

        return instance;
    }

    /// <summary>
    /// Giải phóng toàn bộ prefab cache
    /// </summary>
    public void ReleaseAllPrefabs()
    {
        foreach (var kvp in _prefabHandles)
        {
            var handle = kvp.Value;
            if (handle.IsValid())
                Addressables.Release(handle);
        }

        _prefabHandles.Clear();
    }
    #endregion

    #region Private Methods

    /// <summary>
    /// Đảm bảo prefab đã được load vào memory
    /// </summary>
    /// <param name="config"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    private async Task EnsurePrefabLoadedAsync(TConfig config, CancellationToken ct)
    {
        if (_prefabHandles.TryGetValue(config, out var existingHandle))
        {
            // đã load thành công rồi thì thôi
            if (existingHandle.Status == AsyncOperationStatus.Succeeded)
                return;

            // handle lỗi -> release rồi load lại
            if (existingHandle.IsValid())
                Addressables.Release(existingHandle);

            _prefabHandles.Remove(config);
        }

        // Load asset lần đầu
        AsyncOperationHandle<GameObject> handle =
            config.PrefabRef.LoadAssetAsync<GameObject>();

        _prefabHandles[config] = handle;

        try
        {
            await handle.Task;

            if (ct.IsCancellationRequested)
            {
                if (handle.IsValid()) // release nếu bị hủy
                    Addressables.Release(handle);
                _prefabHandles.Remove(config);
                return;
            }

            if (handle.Status != AsyncOperationStatus.Succeeded) // load thất bại
            {
                Debug.LogError($"{GetType().Name}: LoadAssetAsync thất bại cho {config}");
                if (handle.IsValid())
                    Addressables.Release(handle);
                _prefabHandles.Remove(config);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"{GetType().Name}: Exception khi load {config}: {ex.Message}");
            if (handle.IsValid()) // release nếu có lỗi bất kì
                Addressables.Release(handle);
            _prefabHandles.Remove(config);
        }
    }

    #endregion
}
