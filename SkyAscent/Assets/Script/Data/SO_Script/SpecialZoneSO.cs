using UnityEditor;
using UnityEngine;

public enum SpecialZoneType
{
    None,
    Zone_01,
    Zone_02,
    Stone,
}

[CreateAssetMenu(menuName = "Map/special Zone Config SO")]
public class SpecialZoneSO : BaseSO
{
    [Tooltip("")]
    public SpecialZoneType _type;

    //[Tooltip("")]
    //public int amount;

    [Tooltip("Straytegy this zone")]
    public SpecialZoneStrategySO[] strategy;

    [TextArea, Tooltip("đường dẩn đến Prefab")]
    public string prefabPath;

#if UNITY_EDITOR
    [SerializeField]
    private bool AutoResetToDefaul = true;
    private void OnValidate()
    {
        if (!Application.isPlaying)
            if (AutoResetToDefaul)
                ApplyPresetData();
    }

    private void ApplyPresetData()
    {
        _name = _type.ToString();

        EditorUtility.SetDirty(this);
        //AssetDatabase.SaveAssetIfDirty(this);
    }
#endif
}

