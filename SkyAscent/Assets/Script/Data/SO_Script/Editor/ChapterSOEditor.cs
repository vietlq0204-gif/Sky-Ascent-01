using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(ChapterSO))]
public class ChapterSOEditor : AutoEditor<ChapterSO>
{
    ChapterSO data;

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
