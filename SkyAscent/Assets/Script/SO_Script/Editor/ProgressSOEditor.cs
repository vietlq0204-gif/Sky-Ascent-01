using UnityEditor;

[CustomEditor(typeof(ProgressSO))]
public class ProgressSOEditor : AutoEditor<ProgressSO>
{
    ProgressSO data;

    protected override void OnEnable()
    {
        base.OnEnable();
        data = targetData;
    }

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        UbilityHelperUnityEditor.DrawChildSOList(data.Chapter, "ChapterSO", "chi tiết ChapterSO");
        UbilityHelperUnityEditor.EnsureBasicMeta(data);
    }
}
