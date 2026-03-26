using UnityEditor;

[CustomEditor(typeof(ItemsSO))]
public class ItemsSOEditor : AutoEditor<ItemsSO>
{
    ItemsSO data;

    private SerializedProperty ItemTypeProp;

    protected override void OnEnable()
    {
        base.OnEnable();
        data = (ItemsSO)target;

        ItemTypeProp = serializedObject.FindProperty("itemType");
    }

    public override void OnInspectorGUI()
    {
        EditorGUILayout.PropertyField(ItemTypeProp);

        if (data.itemType == ItemType.none)
        {
            EditorGUILayout.HelpBox("SpecialZoneType is None. No value required.", MessageType.Info);
        }
        else
        {
            UbilityHelperUnityEditor.DrawPropertiesByEnumAttribute<
        ItemType, ItemKeywordAttribute>(serializedObject,
        typeof(ItemsSO),
        data.itemType);
        }

        // áp dụng các thay đổi vào serializedObject
        serializedObject.ApplyModifiedProperties();
        UbilityHelperUnityEditor.EnsureBasicMeta(data);
    }
}
