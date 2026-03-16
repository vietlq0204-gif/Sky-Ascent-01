using UnityEditor;

[CustomEditor(typeof(SpawnStrategySO))]
public class SpawnStrategySOEditor : AutoEditor<SpawnStrategySO>
{
    SpawnStrategySO data;

    private SerializedProperty spawnTypeProp;
    private SerializedProperty spawnModeProp;
    private SerializedProperty amount;

    protected override void OnEnable()
    {
        base.OnEnable();
        data = (SpawnStrategySO)target;

        spawnTypeProp = serializedObject.FindProperty("spawnType");
        spawnModeProp = serializedObject.FindProperty("spawnMode");
        amount = serializedObject.FindProperty("TotalAmount");
    }

    public override void OnInspectorGUI()
    {
        //base.OnInspectorGUI();

        EditorGUILayout.LabelField("Key", EditorStyles.boldLabel);

        EditorGUILayout.PropertyField(spawnTypeProp);
        EditorGUILayout.PropertyField(spawnModeProp);
        EditorGUILayout.PropertyField(amount);

        EditorGUILayout.Space(10);

        UbilityHelperUnityEditor.DrawPropertiesByEnumAttribute<
            SpawnType, SpawnKeywordAttribute>(
            serializedObject,
            typeof(SpawnStrategySO),
            data.spawnType
        );

        serializedObject.ApplyModifiedProperties();
        UbilityHelperUnityEditor.EnsureBasicMeta(data);
        ApplyPresetDataIfChanged();

    }

    private void ApplyPresetDataIfChanged()
    {
        string newName = $"Spawn_{data.spawnType.ToString()}_{data.spawnMode.ToString()}";
        if (data._name == newName) return;

        data._name = newName;
        EditorUtility.SetDirty(data);
    }
}