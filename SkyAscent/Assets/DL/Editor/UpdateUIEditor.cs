using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(UpdateUI))]
public class UpdateUIEditor : Editor
{
    public override void OnInspectorGUI()
    {
        // Vẽ inspector mặc định
        DrawDefaultInspector();

        UpdateUI updateUI = (UpdateUI)target;

        // Nút Refresh
        if (GUILayout.Button("Refresh Component"))
        {
            updateUI.Refresh();
            EditorUtility.SetDirty(updateUI); // ghi lại thay đổi trong EditMode
        }
    }
}
