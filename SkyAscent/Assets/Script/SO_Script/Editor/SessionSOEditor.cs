using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(SessionSO))]
public class SessionSOEditor : AutoEditor<SessionSO>
{
    SessionSO data;

    protected override void OnEnable()
    {
        base.OnEnable();
        data = targetData;
    }

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        EditorGUILayout.Space(10);
        if (data.mapSO != null)
        {
            UbilityHelperUnityEditor.DrawChildSOList(data.mapSO.cosmicObjectSO, "CosmicObjectSO_Session", "Chi tiết CosmicObjectSO_Session");
            //UbilityHelperUnityEditor.DrawChildSOList(data.mapSO.specialZoneSO, "SpectialZoneSO_Session", "Chi tiết SpectialZoneSO_Session");
        }
        EditorGUILayout.Space(10);
        if (data.itemsSO != null)
        {
            UbilityHelperUnityEditor.DrawChildSOList(data.itemsSO, "ItemsSO_Session", "Chi tiết ItemsSO_Session");

            // duyệt itemSO vì DrawChildSOList chỉ hổ trợ đối tượng đơn (nâng cấp sau nhé)
            foreach (var item in data.itemsSO)
            {
                if (item == null) continue;
                UbilityHelperUnityEditor.DrawChildSOList(item.moneySO, "MoneySO_SessionSO", "Chi tiết MoneySO_SessionSO");
            }
        }

        UbilityHelperUnityEditor.EnsureBasicMeta(data);
    }

}
