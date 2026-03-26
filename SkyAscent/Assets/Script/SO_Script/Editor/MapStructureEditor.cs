//using UnityEditor;
//using UnityEngine;

//[CustomEditor(typeof(MapStructureSO))]
//public class MapStructureEditor : AutoEditor<MapStructureSO>
//{
//    //MapStructureSO data;

//    //protected override void OnEnable()
//    //{
//    //    base.OnEnable();
//    //    data = targetData;
//    //}

//    //public override void OnInspectorGUI()
//    //{

//    //    EditorGUI.BeginChangeCheck();
//    //    DrawDefaultInspector();

//    //    // Khi rootMapStructure thay đổi → auto fill
//    //    if (EditorGUI.EndChangeCheck())
//    //    {
//    //        if (data.rootMapStructure != null)
//    //        {
//    //            AutoFill(data);
//    //            EditorUtility.SetDirty(data);
//    //        }
//    //    }

//    //    // Nút debug cho Ní
//    //    if (GUILayout.Button("Refresh Map Structure"))
//    //    {
//    //        if (data.rootMapStructure != null)
//    //        {
//    //            AutoFill(data);
//    //            EditorUtility.SetDirty(data);
//    //        }
//    //    }
//    //}

//    //// ---------------------------
//    //// Auto scan hierarchy
//    //// ---------------------------
//    //private void AutoFill(MapStructureSO data)
//    //{
//    //    Transform root = data.rootMapStructure.transform;

//    //    data.spaceBox = FindChild(root, "Space Box");
//    //    data.specialZone = FindChild(root, "SpecialZone");
//    //    data.plane = FindChild(root, "Plane");
//    //    data.shipRoad = FindChild(root, "Ship Road");
//    //}

//    //private GameObject FindChild(Transform parent, string name)
//    //{
//    //    foreach (Transform t in parent.GetComponentsInChildren<Transform>(true))
//    //    {
//    //        if (t.name == name)
//    //            return t.gameObject;
//    //    }

//    //    return null;
//    //}
//}
