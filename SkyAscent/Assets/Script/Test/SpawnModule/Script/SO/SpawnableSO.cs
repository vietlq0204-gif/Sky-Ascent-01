using System;
using System.Collections.Generic;
using UnityEngine;


/// <summary>
/// Data định nghĩa 1 loại object có thể spawn.
/// </summary>
[CreateAssetMenu(menuName = "Spawn/Spawnable", fileName = "Spawnable_")]
public class SpawnableSO : BaseSO
{
    [Header("Key")]
    public SpawnKey key;

    [Header("Source")]
    public SpawnSourceType sourceType = SpawnSourceType.Prefab;

    [Tooltip("Dùng khi sourceType = Prefab")]
    public List<PrefabVariant> prefabVariants = new List<PrefabVariant>();

    [Tooltip("Dùng khi sourceType = Addressables")]
    public List<AddressableVariant> addressVariants = new List<AddressableVariant>();

    [Header("Pool")]
    public PoolConfig poolConfig = new PoolConfig();

    private void OnValidate()
    {
        key.RecomputeHash();
        poolConfig?.Sanitize();
        SanitizeVariants();
    }

    private void SanitizeVariants()
    {
        if (prefabVariants != null)
        {
            for (int i = 0; i < prefabVariants.Count; i++)
            {
                var v = prefabVariants[i];                 // copy
                v.weight = Mathf.Max(0f, v.weight);        // sửa trên copy
                prefabVariants[i] = v;                     // gán lại
            }
        }

        if (addressVariants != null)
        {
            for (int i = 0; i < addressVariants.Count; i++)
            {
                var v = addressVariants[i];
                v.weight = Mathf.Max(0f, v.weight);
                addressVariants[i] = v;
            }
        }
    }

    [Serializable]
    public struct PrefabVariant
    {
        public GameObject prefab;
        [Min(0f)] public float weight; // trọng số chọn prefab này (so với các variant khác của spawnable). Nếu <=0 thì không bao giờ chọn.
    }

    [Serializable]
    public struct AddressableVariant
    {
        public string addressKey;
        [Min(0f)] public float weight;
    }
}

public static class WeightedPicker
{
    /// <summary>
    /// Chọn index theo weight.
    /// </summary>
    /// <remarks>Deterministic theo seed+salt.</remarks>
    public static int PickIndex(float[] weights, uint seed, uint salt)
    {
        float sum = 0f;
        for (int i = 0; i < weights.Length; i++) sum += weights[i];
        if (sum <= 0f) return 0;

        uint h = Hash(seed ^ salt);
        float r01 = (h >> 8) * (1f / 16777216f); // [0,1)
        float r = r01 * sum;

        float acc = 0f;
        for (int i = 0; i < weights.Length; i++)
        {
            acc += weights[i];
            if (r <= acc) return i;
        }
        return weights.Length - 1;
    }

    /// <summary>
    /// Hash avalanche 32-bit.
    /// </summary>
    private static uint Hash(uint x)
    {
        x ^= x >> 16;
        x *= 0x7feb352du;
        x ^= x >> 15;
        x *= 0x846ca68bu;
        x ^= x >> 16;
        return x == 0 ? 1u : x;
    }
}
