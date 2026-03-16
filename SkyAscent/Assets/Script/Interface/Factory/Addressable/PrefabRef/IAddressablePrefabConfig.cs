using UnityEngine;
using UnityEngine.AddressableAssets;

/// <summary>
/// Interface định nghĩa cấu hình prefab Addressable.
/// Dùng chung cho mọi Config có prefab Addressable.
/// </summary>
/// <remarks>
/// vd: EnemySO, ItemSO...
/// </remarks>
public interface IAddressablePrefabConfig 
{
    AssetReferenceGameObject PrefabRef { get; }

}
