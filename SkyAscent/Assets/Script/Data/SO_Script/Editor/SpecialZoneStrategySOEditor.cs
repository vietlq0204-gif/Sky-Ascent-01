using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(SpecialZoneStrategySO))]
public class SpecialZoneStrategySOEditor : AutoEditor<SpecialZoneStrategySO>
{
    SpecialZoneStrategySO data;

    private SerializedProperty diffTypeProp;
    private SerializedProperty calculatorTypeProp;
    private SerializedProperty zoneTypeProp;

    protected override void OnEnable()
    {
        base.OnEnable();
        data = (SpecialZoneStrategySO)target;

        diffTypeProp = serializedObject.FindProperty("diffType");
        zoneTypeProp = serializedObject.FindProperty("zoneType");
        calculatorTypeProp = serializedObject.FindProperty("calculatorType");

    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // Draw Key
        EditorGUILayout.LabelField("Key", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(diffTypeProp);
        EditorGUILayout.PropertyField(zoneTypeProp);
        //EditorGUILayout.PropertyField(calculatorTypeProp);

        EditorGUILayout.Space(6);

        // Draw Value (Auto by zoneType tokens)
        EditorGUILayout.LabelField("Value", EditorStyles.boldLabel);

        if (data.zoneType == SpecialZoneType.None)
        {
            EditorGUILayout.HelpBox("SpecialZoneType is None. No value required.", MessageType.Info);
        }
        else
        {
            //UbilityHelperUnityEditor.DrawPropertiesByActiveKeyTokens(
            //    serializedObject,
            //    typeof(SpecialZoneStrategySO), // host type
            //    data.zoneType.ToString() // active key
            //);

            UbilityHelperUnityEditor.DrawPropertiesByEnumAttribute<
                SpecialZoneType, ZoneKeywordAttribute>(serializedObject,
                typeof(SpecialZoneStrategySO),
                data.zoneType
            );
        }

        UbilityHelperUnityEditor.EnsureBasicMeta(data);

        ApplyPresetDataIfChanged();
        serializedObject.ApplyModifiedProperties();
    }

    /// <summary>
    /// Apply preset name only when changed
    /// </summary>
    private void ApplyPresetDataIfChanged()
    {
        string newName = $"{data.zoneType}_{data.diffType}";
        if (data._name == newName) return;

        data._name = newName;
        EditorUtility.SetDirty(data);
    }
}
