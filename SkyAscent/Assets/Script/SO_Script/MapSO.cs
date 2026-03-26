using UnityEngine;

[CreateAssetMenu(menuName = "Map/Map Config SO")]
public class MapSO : BaseSO
{
    [TextArea, Tooltip("đường dẩn đến Prefab, đang test Addresable")]
    public string mapPrefabPath;

    [Tooltip("Danh sách các SolarObjectSO. (KHÔNG ĐỂ NULL NHÉ)")]
    public CosmicObjectSO[] cosmicObjectSO;

    [Tooltip("Chiến lược spawn.")]
    public SpawnStrategySO[] spawnStrategySO;
}


