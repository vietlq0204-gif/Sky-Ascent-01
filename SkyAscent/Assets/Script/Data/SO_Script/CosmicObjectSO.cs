using UnityEditor;
using UnityEngine;
using UnityEngine.AddressableAssets;

public enum SolarObjects
{
    // Star
    Sun,
    // Planets
    Mercury, Venus, Earth, Mars, Jupiter, Saturn, Uranus, Neptune,
    // Dwarf planets
    Ceres, Orcus, Pluto, Haumea, Quaoar, Makemake, Gonggong, Eris, Sedna
}

[CreateAssetMenu(menuName = "Map/Solar/Solar Object Config SO")]
public class CosmicObjectSO : BaseSO, IAddressablePrefabConfig
{
    [Tooltip("loại vật thể trong hệ mặt trời")]
    public SolarObjects _type;

    [Tooltip("Material của vật thể trong hệ mặt trời")]
    public Material material;

    [Tooltip("Gia tốc trọng trường m/s^2")]
    public float g;

    [Header("Prefab (Addressables)")]
    [Tooltip("Prefab Addressables của vật thể")]
    public AssetReferenceGameObject prefabRef;
    public AssetReferenceGameObject PrefabRef => prefabRef;


    [HideInInspector, Tooltip("Khối lượng (ton)")]
    public float m;

    [HideInInspector, Tooltip("Bán kính (km)")]
    public float r;

    [HideInInspector, Tooltip("Vận tốc thoát ly (km/h)")]
    public float v;


#if UNITY_EDITOR
    [SerializeField] private bool AutoResetToDefaul = true;

    private void Reset()
    {
        if (!Application.isPlaying)
            if (AutoResetToDefaul)
                ApplyPresetData();
    }

    private void ApplyPresetData()
    {
        var data = SolarDataTable.Get(_type);
        if (data == null) return;

        _name = _type.ToString();
        g = data.Value.g;
        m = data.Value.m;
        r = data.Value.r;

        EditorUtility.SetDirty(this);
        //AssetDatabase.SaveAssetIfDirty(this);
    }
#endif
}
