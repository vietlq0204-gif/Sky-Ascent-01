using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CosmicObjectSO))]
public class SolarObjectSOEditor : AutoEditor<CosmicObjectSO>
{
    CosmicObjectSO data;

    float lastG, lastM, lastR;

    protected override void OnEnable()
    {
        base.OnEnable();
        data = targetData;
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // Vẽ mặc định
        base.OnInspectorGUI();

        // Áp thay đổi
        serializedObject.ApplyModifiedProperties();

        // preset theo bảng
        ApplyPresetData();

        // Tính lại vận tốc thoát khi tham số thay đổi
        if (data.g != lastG || data.m != lastM || data.r != lastR)
        {
            CalculateEscapeVelocity();
            lastG = data.g;
            lastM = data.m;
            lastR = data.r;
        }

        // đảm bảo _name/description + rename asset
        UbilityHelperUnityEditor.EnsureBasicMeta(data);
    }

    public void ApplyPresetData()
    {
        var _data = SolarDataTable.Get(data._type);
        if (_data == null) return;

        data._name = data._type.ToString();
        data.g = _data.Value.g;
        data.m = _data.Value.m;
        data.r = _data.Value.r;

        EditorUtility.SetDirty(data);
    }

    public void CalculateEscapeVelocity()
    {
        const double g = 6.67430e-11; // m^3/(kg·s^2)
        double M_kg = data.m * 1e3; // tấn -> kg
        double R_m = data.r * 1e3; // km -> m

        double v_m_per_s = System.Math.Sqrt((2 * g * M_kg) / R_m);
        data.v = (float)(v_m_per_s * 3.6); // km/h
        EditorUtility.SetDirty(data);
    }
}
