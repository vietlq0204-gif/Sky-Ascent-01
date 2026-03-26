using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(PopupElementStrategySO))]
public class PopupElementStrategySOEditor : AutoEditor<PopupElementStrategySO>
{
    PopupElementStrategySO data;

    protected override void OnEnable()
    {
        base.OnEnable();
        data = targetData;
    }

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        UbilityHelperUnityEditor.EnsureBasicMeta(data);
    }
}
