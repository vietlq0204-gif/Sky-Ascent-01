
using UnityEditor;

[CustomEditor(typeof(MoneySO))]
public class MoneySOEditor : AutoEditor<MoneySO>
{
    MoneySO moneyData;
    //private bool AutoResetToDefaul = true;

    protected override void OnEnable()
    {
        base.OnEnable();
        moneyData = targetData;
    }

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        ApplyPresetData();

        UbilityHelperUnityEditor.EnsureBasicMeta(moneyData);
    }

    public void ApplyPresetData()
    {
        moneyData._name = moneyData.MonenyType.ToString() + "_" + moneyData.quantity;

        EditorUtility.SetDirty(moneyData);
    }
}