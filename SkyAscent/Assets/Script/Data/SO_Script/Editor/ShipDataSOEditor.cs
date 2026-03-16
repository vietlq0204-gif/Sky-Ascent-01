using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(ShipDataSO))]
public class ShipDataSOEditor : AutoEditor<ShipDataSO>
{
    ShipDataSO shipData;

    protected override void OnEnable()
    {
        base.OnEnable();
        shipData = (ShipDataSO)target;
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.LabelField("SHIP DATA SO", EditorStyles.largeLabel);
        EditorGUILayout.Space(5);

        DrawReferences();
        DrawTotalWeight();
        EditorGUILayout.Space(10);

        UbilityHelperUnityEditor.DrawChildSOList(
            new ScriptableObject[] { shipData.MainCompartmentDataSO }, "MainCompartment", "MAIN COMPARTMENT DATA");

        UbilityHelperUnityEditor.DrawChildSOList(
            new ScriptableObject[] { shipData.EngineDataSO }, "Engine", "ENGINE DATA");

        UbilityHelperUnityEditor.DrawChildSOList(
            new ScriptableObject[] { shipData.FuelTankDataSO }, "FuelTank", "FUEL TANK DATA");

        EditorGUILayout.Space(10);

        if (GUILayout.Button("Save Changes"))
        {
            OnSaveChange();
        }

        // Áp serialized trước khi xử lý meta
        serializedObject.ApplyModifiedProperties();

        // đảm bảo _name/description + rename asset
        UbilityHelperUnityEditor.EnsureBasicMeta(shipData);
    }

    private void DrawReferences()
    {
        EditorGUILayout.BeginVertical("box");

        //shipData._name = EditorGUILayout.TextField(shipData._name);

        EditorGUILayout.PropertyField(Prop("_name"));

        shipData.MainCompartmentDataSO = (MainCompartmentDataSO)EditorGUILayout.ObjectField(
            "Main Compartment Data", shipData.MainCompartmentDataSO, typeof(MainCompartmentDataSO), false);

        shipData.EngineDataSO = (EngineDataSO)EditorGUILayout.ObjectField(
            "Engine Data", shipData.EngineDataSO, typeof(EngineDataSO), false);

        shipData.FuelTankDataSO = (FuelTankDataSO)EditorGUILayout.ObjectField(
            "Fuel Tank Data", shipData.FuelTankDataSO, typeof(FuelTankDataSO), false);

        EditorGUILayout.EndVertical();
    }

    private void DrawTotalWeight()
    {
        shipData.TotalWeight = CalculatorTotalWeight();

        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.Space(2);
        EditorGUILayout.LabelField($"Total Weight: {shipData.TotalWeight:F2} tấn", EditorStyles.boldLabel);
        EditorGUILayout.EndVertical();
    }

    private float CalculatorTotalWeight()
    {
        float totalWeight = 0f;

        if (shipData.EngineDataSO != null)
            totalWeight += shipData.EngineDataSO.Weight;

        if (shipData.FuelTankDataSO != null)
            totalWeight += shipData.FuelTankDataSO.Weigh;

        if (shipData.MainCompartmentDataSO != null)
        {
            totalWeight += shipData.MainCompartmentDataSO.Weigh;

            if (shipData.MainCompartmentDataSO.FuelTankDataSO != null)
                totalWeight += shipData.MainCompartmentDataSO.FuelTankDataSO.Weigh;
        }

        return totalWeight;
    }

    private void OnSaveChange()
    {
        EditorUtility.SetDirty(shipData);
        if (shipData.EngineDataSO != null) EditorUtility.SetDirty(shipData.EngineDataSO);
        if (shipData.FuelTankDataSO != null) EditorUtility.SetDirty(shipData.FuelTankDataSO);
        if (shipData.MainCompartmentDataSO != null) EditorUtility.SetDirty(shipData.MainCompartmentDataSO);

        AssetDatabase.SaveAssets();
        Debug.Log($"[ShipDataSOEditor] Dữ liệu đã được lưu lại! Tổng trọng lượng = {shipData.TotalWeight:F2} tấn");
    }
}
