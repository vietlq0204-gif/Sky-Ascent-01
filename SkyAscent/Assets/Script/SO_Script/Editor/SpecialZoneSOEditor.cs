using UnityEditor;

[CustomEditor(typeof(SpecialZoneSO))]
public class SpecialZoneSOEditor : AutoEditor<SpecialZoneSO>
{
    SpecialZoneSO data;

    protected override void OnEnable()
    {
        base.OnEnable();
        data = targetData;
    }

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        UbilityHelperUnityEditor.DrawChildSOList(targetData.strategy, "SpecialZoneStrategySO", "Special Zone Strategy");

        UbilityHelperUnityEditor.EnsureBasicMeta(data);
        ApplyPresetData();

        serializedObject.ApplyModifiedProperties();
    }

    public void ApplyPresetData()
    {
        data._name = data._type.ToString();

        EditorUtility.SetDirty(data);
    }
}
