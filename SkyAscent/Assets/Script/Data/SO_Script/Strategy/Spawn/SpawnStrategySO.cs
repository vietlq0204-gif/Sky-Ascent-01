using UnityEngine;

public enum SpawnType
{
    Zone,
    SomethingOther,
}

[CreateAssetMenu(fileName = "SpecialZoneSpawnStrategySO",
    menuName = "Strategy/Zone/Special Zone Spawn Strategy SO", order = 0)]
public class SpawnStrategySO : BaseSO
{
    [Tooltip("Bạn muốn Spawn cái gì")]
    public SpawnType spawnType;

    [Tooltip("Bạn muốn Spawn kiểu gì")]
    public SpawnMode spawnMode;

    //[SpawnKeyword(SpawnType.Zone)]
    public int TotalAmount;

    [Tooltip("Danh sách các SpecialZone. (CÓ THỂ NULL NHÉ)")]
    [SpawnKeyword(SpawnType.Zone)] 
    public SpecialZoneSO[] specialZoneSO;


}

