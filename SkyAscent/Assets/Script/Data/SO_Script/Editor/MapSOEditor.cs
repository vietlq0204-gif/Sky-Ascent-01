using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MapSO))]
public class MapSOEditor : AutoEditor<MapSO>
{
    MapSO data;

    protected override void OnEnable()
    {
        base.OnEnable();
        data = targetData;
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        base.OnInspectorGUI();

        EditorGUILayout.Space(10);

        //mapData = (MapSO)target;
        if (data.cosmicObjectSO != null)
        {
            UbilityHelperUnityEditor.DrawChildSOList(data.cosmicObjectSO, "CosmicObjet", "Chi tiết Cosmic Objects");
        }
        if (data.spawnStrategySO != null)
        {
            UbilityHelperUnityEditor.DrawChildSOList(data.spawnStrategySO, "zone", "Chi tiết Special Zones");
        }

        serializedObject.ApplyModifiedProperties();

        UbilityHelperUnityEditor.EnsureBasicMeta(data);
    }
}
