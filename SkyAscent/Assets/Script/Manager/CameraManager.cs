using UnityEngine;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Lấy RoadPoint 
/// </summary>
public class CameraManager : MonoBehaviour, IInject<Core>
{
    [Header("Camera Base")]
    private const string prefabPath = "Assets/Prefab/Camera/Camera Rule.prefab";
    private const string prefabName = "Camera Rule";

    private Core Core;
    public Camera mainCamera;

    [SerializeField]
    private List<PointBaker> PointBakers;
    /*[HideInInspector]*/
    public PointBaker PointData;

    public void Inject(Core context) { Core = context; }


#if UNITY_EDITOR

    // help đảm bảo chỉ có 1 Instance cameraRule trong Scene
    private void EnsureCameraExists()
    {
        // duyệt hết Scene và đảm bảo chỉ có 1 instance có tên prefabName trong scene
        GameObject[] gameObjects = Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
        if (gameObjects != null)
        {
            int count = 0;
            foreach (GameObject go in gameObjects)
            {
                if (go.name == prefabName)
                {
                    count++;
                }
            }
            if (count > 1)
            {
                Debug.LogWarning($"Phát hiện nhiều '{prefabName}'.");
                // xóa hết chúng đi
                foreach (GameObject go in gameObjects)
                {
                    if (go.name == prefabName)
                    {
                        Undo.DestroyObjectImmediate(go);
                    }
                }
                // load prefab gốc
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefab == null)
                {
                    Debug.LogError($"Không tìm thấy prefab tại đường dẫn: {prefabPath}");
                    return;
                }

                // Tạo instance trong scene
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                instance.name = prefabName;
                Undo.RegisterCreatedObjectUndo(instance, "Create Camera Rule Prefab");
                Debug.Log($"Tự động thêm '{prefabName}' vào scene.");
                GetMainCamera();
                GetRoadBakersInScene();
            }
            else if (count == 1)
            {
                // đã có trong scene
                GetMainCamera();
                GetRoadBakersInScene();
                return;
            }
        }
    }

#endif

    private void Start()
    {
        if (mainCamera == null)
        {
            GetMainCamera();
        }
    }

    private void Update()
    {
        if (Core != null) GetState();
    }

    /// <summary>
    /// dựa vào trạng thái hiện tại của StateMachine để lấy RoadBaker phù hợp
    /// </summary>
    private void GetState()
    {
        if (Core == null) return;

        if (Core.StateMachine.CurrentStateType == typeof(OnMenuState)
            && Core.SecondaryStateMachine.CurrentStateType != typeof(UpgradeState))
        {
            GetRoadBakerByIndex(0);
        }
        if (Core.StateMachine.CurrentStateType == typeof(OnMenuState)
            && Core.SecondaryStateMachine.CurrentStateType == typeof(UpgradeState))
        {
            GetRoadBakerByIndex(1);
        }
        else if (Core.StateMachine.CurrentStateType == typeof(OnNewSessionState))
        {
            GetRoadBakerByIndex(2);
        }

    }

    /// <summary>
    /// lẩy Main Camera trong scene với tag "MainCamera"
    /// </summary>
    /// <returns></returns>
    public Camera GetMainCamera()
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            if (mainCamera == null)
            {
                Debug.LogError("Không tìm thấy Main Camera trong scene với tag 'MainCamera'.");
            }
        }
        return mainCamera;
    }

    /// <summary>
    /// Help lấy RoadBaker theo chỉ số index trong danh sách roadBakers
    /// </summary>
    /// <param name="index"></param>
    private void GetRoadBakerByIndex(int index)
    {
        if (PointBakers == null || PointBakers.Count == 0)
        {
            GetRoadBakersInScene();
        }
        if (index < 0 || index >= PointBakers.Count)
        {
            Debug.LogError($"Index {index} vượt quá phạm vi của danh sách RoadBaker.");
            return;
        }
        PointData = PointBakers[index];
    }

    /// <summary>
    /// Tìm camera rule và tìm và lấy tất cảc component RoadBaker bên trong child của nó và thêm vào list
    /// </summary>
    /// <returns></returns>
    public List<PointBaker> GetRoadBakersInScene()
    {
        if (PointBakers == null || PointBakers.Count == 0)
        {
            GameObject cameraRule = GameObject.Find(prefabName);
            if (cameraRule == null)
            {
                Debug.LogError($"Không tìm thấy '{prefabName}' trong scene.");
                return null;
            }
            PointBakers = new List<PointBaker>(cameraRule.GetComponentsInChildren<PointBaker>());
            if (PointBakers.Count == 0)
            {
                Debug.LogError($"Không tìm thấy RoadBaker trong '{prefabName}'.");
            }
        }
        return PointBakers;
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(CameraManager))]
public class CameraManagerEditor : Editor
{
    private CameraManager _target;
    private void Reset()
    {
        _target = (CameraManager)target;
        if (Application.isPlaying) return;
        _target.GetRoadBakersInScene();
    }

    //public override void OnInspectorGUI()
    //{
    //    DrawDefaultInspector();
    //    if (GUILayout.Button("Get Main Camera"))
    //    {
    //        _target.GetMainCamera();
    //    }
    //    if (GUILayout.Button("Get Road Bakers In Scene"))
    //    {
    //        _target.GetRoadBakersInScene();
    //    }
    //}
}

#endif








