using System.Collections.Generic;
using UnityEngine;


/// <summary>
/// Catalog chứa tất cả SpawnableSO để SpawnManager auto-register.
/// </summary>
[CreateAssetMenu(menuName = "Spawn/Spawn Catalog", fileName = "SpawnCatalog")]
public class SpawnCatalogSO : ScriptableObject
{
    public List<SpawnableSO> items = new List<SpawnableSO>();

    private void OnValidate()
    {
        if (items == null) return;
        for (int i = 0; i < items.Count; i++)
        {
            if (items[i] != null)
            {
                items[i].key.RecomputeHash();
                items[i].poolConfig?.Sanitize();
            }
        }
    }
}
