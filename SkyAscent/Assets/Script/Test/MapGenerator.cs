using System;
using UnityEngine;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

#if UNITY_EDITOR
using UnityEditor;
#endif

public enum SpawnMode
{
    FixPoint,
    Path
}

[Serializable]
public class MapStructure
{
    //public MapStructure(GameObject rm,
    //    GameObject sb, 
    //    GameObject sz,
    //    GameObject p, 
    //    GameObject sr)
    //{
    //    this.rootMap = rm;
    //    this.spaceBox = sb;
    //    this.specialZone = sz;
    //    this.plane = p;
    //    this.shipRoad = sr;
    //}

    public GameObject rootMap;

    public GameObject spaceBox;
    public GameObject specialZone;

    public GameObject cosmicObject;
    public GameObject cosmicObject_A;
    public GameObject cosmicObject_B;

    public GameObject shipRoad;

    public void BuildRootTranform(Transform root)
    {
        if (root == null) { Debug.LogWarning("rootMap chưa được gán!"); return; }
        rootMap = root.gameObject;

        spaceBox ??= FindChildByName(root, "SpaceBox");
        specialZone ??= FindChildByName(root, "SpecialZone");

        cosmicObject ??= FindChildByName(root, "CosmicObject");
        cosmicObject_A ??= FindChildByName(root, "CosmicObject_A");
        cosmicObject_B ??= FindChildByName(root, "CosmicObject_B");

        shipRoad ??= FindChildByName(root, "ShipRoad");
    }

    private GameObject FindChildByName(Transform parent, string name)
    {
        Transform[] all = parent.GetComponentsInChildren<Transform>(true);
        foreach (var t in all)
        {
            if (t.name == name)
                return t.gameObject;
        }
        return null;
    }
}

public partial class MapGenerator : CoreEventBase, IInject<Core>, IInject<SessionController>, IInject<ISolarObjectFactory>
{
    #region Reference // variables
    [SerializeField] Core _core;
    [SerializeField] SessionController _sessionController;
    ISolarObjectFactory solarObjectFactory;

    [SerializeField] MapStructure mapStructure;

    [SerializeField] PointBaker _pointBaker;
    [SerializeField] ListPoint _listPointSpecialZone;
    List<GameObject> roadPoints => _listPointSpecialZone.pointData
        .Where(p => p.point != null)
        .Select(p => p.point)
        .ToList();

    [SerializeField] List<Transform> BodyCosmicObject = new List<Transform>();
    private CancellationTokenSource _spawnCts;

    #endregion

    #region  Unity lifecycle

    protected override void Awake()
    {
        base.Awake();
        mapStructure = new MapStructure();
    }

    private void Start()
    {
        mapStructure.BuildRootTranform(transform);
        GetPointbaker();

        CacheBodyCosmicObjects();
    }

    private void OnDisable()
    {
        CancelSpawnScope();
        CleanCosmicObjects();
        ClearOldSpawn(mapStructure.specialZone);
        mapStructure = null;
    }

    #endregion

    #region Inject
    public void Inject(Core context) => _core = context;
    public void Inject(SessionController context) => _sessionController = context;
    public void Inject(ISolarObjectFactory context) { solarObjectFactory = context; }

    #endregion

    #region Logic

    private CancellationToken BeginSpawnScope()
    {
        CancelSpawnScope();
        _spawnCts = new CancellationTokenSource();
        return _spawnCts.Token;
    }

    private void CancelSpawnScope()
    {
        if (_spawnCts == null) return;

        if (!_spawnCts.IsCancellationRequested)
            _spawnCts.Cancel();

        _spawnCts.Dispose();
        _spawnCts = null;
    }

    private void ResetGeneratedContent()
    {
        CancelSpawnScope();
        CleanCosmicObjects();
        ClearOldSpawn(mapStructure.specialZone);
    }

    #region Zone Spawn

    /// <summary>
    /// Entry point spawn theo config trong MapSO
    /// </summary>
    /// <remarks>
    /// - Chỉ xử lý SpawnType.Zone
    /// - Mỗi SpawnStrategySO quyết định mode + amount + pool zone
    /// </remarks>
    private void SpawnSpecialPoint()
    {

        if (_sessionController == null)
        {
            Debug.LogWarning("MapGenerator: _sessionController null");
            return;
        }
        if (_sessionController.SessionSO == null)
        {
            Debug.LogWarning("MapGenerator: SessionSO");
            return;
        }

        var mapSO = _sessionController.SessionSO.mapSO;
        if (mapSO == null || mapSO.spawnStrategySO == null || mapSO.spawnStrategySO.Length == 0)
        {
            Debug.LogWarning("MapGenerator: mapSO hoặc spawnStrategySO null/empty");
            return;
        }

        try
        {
            // Clear spawn cũ trước (tuỳ rule dự án)
            ClearOldSpawn(mapStructure.specialZone);

            for (int i = 0; i < mapSO.spawnStrategySO.Length; i++)
            {
                var st = mapSO.spawnStrategySO[i];
                if (st == null) continue;
                if (st.spawnType != SpawnType.Zone) continue;

                var pool = BuildZonePool(st);
                if (pool.Count == 0) continue;

                switch (st.spawnMode)
                {
                    case SpawnMode.FixPoint:
                        SpawnZones_FixPoint(pool);
                        break;

                    case SpawnMode.Path:
                        SpawnZones_AlongPath(pool);
                        break;
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning(e);
        }

    }

    #region Special Zone helpers

    /// <summary>
    /// Build pool SpecialZoneSO theo 1 SpawnStrategySO
    /// </summary>
    /// <returns>Danh sách zone cần spawn (size = Amount, có thể lặp zone)</returns>
    private List<SpecialZoneSO> BuildZonePool(SpawnStrategySO st)
    {
        var result = new List<SpecialZoneSO>(Mathf.Max(0, st.TotalAmount));
        if (st.TotalAmount <= 0) return result;

        var candidates = st.specialZoneSO;
        if (candidates == null || candidates.Length == 0) return result;

        // Round-robin để đảm bảo không thiên vị (có thể đổi sang random/weighted)
        for (int i = 0; i < st.TotalAmount; i++)
        {
            var zone = candidates[i % candidates.Length];
            if (zone != null)
                result.Add(zone);
        }

        // Nếu muốn random:
        // ShuffleInPlace(result);

        return result;
    }

    /// <summary>
    /// Spawn theo các roadPoints: chọn index trung tâm -> trái -> phải
    /// </summary>
    private void SpawnZones_FixPoint(List<SpecialZoneSO> pool)
    {
        if (roadPoints == null || roadPoints.Count < 3) return;

        // usable: bỏ đầu và cuối
        var usable = new List<Transform>(roadPoints.Count - 2);
        for (int i = 1; i < roadPoints.Count - 1; i++)
            usable.Add(roadPoints[i].transform);

        if (usable.Count == 0) return;

        int spawnCount = Mathf.Min(pool.Count, usable.Count);

        // index: center, left, right...
        var idxList = BuildCenterOutIndices(usable.Count, spawnCount);

        for (int i = 0; i < spawnCount; i++)
        {
            var zoneSO = pool[i];
            var pos = usable[idxList[i]].position;
            SpawnZoneInstance(zoneSO, pos, $"Zone_{zoneSO._name}_{i}");
        }
    }

    /// <summary>
    /// Spawn đều theo chiều dài path từ A=point[1] đến B=point[count-2] (bỏ start/end)
    /// </summary>
    /// <remarks>
    /// - Dùng đoạn path con: roadPoints[1..count-2]
    /// - Spawn đúng Amount (pool.Count) theo arc-length
    /// - Nếu đoạn path con quá ngắn hoặc segLen=0 => skip hoặc dồn về 1 điểm (tuỳ rule)
    /// </remarks>
    private void SpawnZones_AlongPath(List<SpecialZoneSO> pool)
    {
        if (pool == null || pool.Count == 0) return;

        // cần tối thiểu 4 điểm: [0][1]...[count-2][count-1]
        if (roadPoints == null || roadPoints.Count < 4)
        {
            Debug.LogWarning("LineAB requires at least 4 road points.");
            return;
        }

        // Build pts chỉ từ A đến B: index 1 .. (count-2)
        int startIndex = 1;
        int endIndex = roadPoints.Count - 2;

        // số điểm trong đoạn con
        int subCount = endIndex - startIndex + 1;
        if (subCount < 2)
        {
            Debug.LogWarning("LineAB sub path not enough points.");
            return;
        }

        var pts = new List<Vector3>(subCount);
        for (int i = startIndex; i <= endIndex; i++)
            pts.Add(roadPoints[i].transform.position);

        // seg lengths
        float totalLen = 0f;
        var segLen = new float[pts.Count - 1];
        for (int i = 0; i < segLen.Length; i++)
        {
            segLen[i] = Vector3.Distance(pts[i], pts[i + 1]);
            totalLen += segLen[i];
        }

        // Nếu totalLen ~ 0 => các điểm trùng nhau, spawn không có ý nghĩa theo chiều dài
        if (totalLen <= 0.0001f)
        {
            Debug.LogWarning("LineAB totalLen is ~0. Spawn fallback at A.");
            // fallback: spawn chồng tại pts[0] hoặc rải nhỏ offset
            for (int i = 0; i < pool.Count; i++)
            {
                var zoneSO = pool[i];
                var pos = pts[0];
                SpawnZoneInstance(zoneSO, pos, $"Zone_{zoneSO._name}_AB_Fallback_{i}");
            }
            return;
        }

        int spawnCount = pool.Count; // LineAB: spawn hết amount
        float step = totalLen / (spawnCount + 1);

        for (int n = 1; n <= spawnCount; n++)
        {
            float target = step * n;

            float acc = 0f;
            for (int i = 0; i < segLen.Length; i++)
            {
                float len = segLen[i];
                if (acc + len >= target)
                {
                    float remain = target - acc;
                    float t = (len <= 0.0001f) ? 0f : (remain / len);

                    Vector3 pos = Vector3.Lerp(pts[i], pts[i + 1], t);
                    var zoneSO = pool[n - 1];

                    SpawnZoneInstance(zoneSO, pos, $"Zone_{zoneSO._name}_AB_{n - 1}");
                    break;
                }
                acc += len;
            }
        }
    }

    /// <summary>
    /// Tạo danh sách index center-out
    /// </summary>
    /// <returns>List index có size = count</returns>
    private List<int> BuildCenterOutIndices(int total, int count)
    {
        var idx = new List<int>(count);

        int center = total / 2;
        idx.Add(center);

        int left = center - 1;
        int right = center + 1;

        while (idx.Count < count && (left >= 0 || right < total))
        {
            if (left >= 0 && idx.Count < count) idx.Add(left--);
            if (right < total && idx.Count < count) idx.Add(right++);
        }

        return idx;
    }

    /// <summary>
    /// Spawn 1 instance zone tại position
    /// </summary>
    /// <remarks>
    /// Hiện dùng Resources.Load theo prefabPath (nên chuyển Addressables sau)
    /// </remarks>
    private void SpawnZoneInstance(SpecialZoneSO zoneSO, Vector3 pos, string instanceName)
    {
        if (zoneSO == null) return;

        GameObject prefab = Resources.Load<GameObject>(zoneSO.prefabPath);
        if (!prefab)
        {
            Debug.LogError("Missing prefab: " + zoneSO.prefabPath);
            return;
        }

        var clone = Instantiate(prefab, pos, Quaternion.identity, mapStructure.specialZone.transform);
        clone.name = instanceName;

        // Inject context (đang dùng Injector của project)
        Injector.Injects(clone, _sessionController.SessionSO, zoneSO);
    }

    #endregion

    #endregion

    #region Cosmic Object Spawn

    /// <summary>
    /// Spawn CosmicObject vào slot đã cache (A/B)
    /// </summary>
    /// <remarks>
    /// - Slot[0] = A
    /// - Slot[1] = B (nếu có)
    /// - Data lấy từ SessionManager.SessionSO
    /// </remarks>
    private async void SpawnCosmicObjects(CancellationToken ct)
    {
        if (_sessionController == null || _sessionController.SessionSO == null)
            return;

        var mapSO = _sessionController.SessionSO.mapSO;
        if (mapSO == null || mapSO.cosmicObjectSO == null)
            return;

        var cosmicArr = mapSO.cosmicObjectSO;
        if (cosmicArr.Length == 0) return;

        try
        {
            for (int i = 0; i < BodyCosmicObject.Count && i < cosmicArr.Length; i++)
            {
                ct.ThrowIfCancellationRequested();

                var slot = BodyCosmicObject[i];
                var so = cosmicArr[i];

                if (slot == null || so == null) continue;

                await SpawnCosmicObject_ToSlot(slot, so, i, ct);
            }
        }
        catch (OperationCanceledException)
        {
            // QuitSession / session mới sẽ hủy các spawn cũ.
        }
    }

    /// <summary>
    /// Clean toàn bộ CosmicObject đã spawn trong các slot cache (A/B)
    /// </summary>
    /// <remarks>
    /// Chỉ xóa con của slot, không xóa slot/root.
    /// </remarks>
    private void CleanCosmicObjects()
    {
        for (int i = 0; i < BodyCosmicObject.Count; i++)
        {
            var slot = BodyCosmicObject[i];
            if (slot == null) continue;
            ClearOldSpawn(slot.gameObject);
        }
    }

    #region Cosmic Object helpers

    /// <summary>
    /// Cache transform slot (child đầu tiên) của CosmicObject_A và CosmicObject_B
    /// </summary>
    /// <remarks>
    /// - Gọi sau khi BuildRootTransform
    /// - Có thể gọi lại khi đổi Map / NewSession
    /// </remarks>
    private void CacheBodyCosmicObjects()
    {
        BodyCosmicObject.Clear();

        if (mapStructure.cosmicObject_A != null)
        {
            var slotA = GetFirstChildOfCosmicObject(mapStructure.cosmicObject_A.transform);
            if (slotA != null)
                BodyCosmicObject.Add(slotA);
            else
                Debug.LogWarning("CosmicObject_A không có child slot");
        }

        if (mapStructure.cosmicObject_B != null)
        {
            var slotB = GetFirstChildOfCosmicObject(mapStructure.cosmicObject_B.transform);
            if (slotB != null)
                BodyCosmicObject.Add(slotB);
            else
                Debug.LogWarning("CosmicObject_B không có child slot");
        }
    }

    /// <summary>
    /// Spawn 1 CosmicObject vào slot đã cache
    /// </summary>
    /// <remarks>
    /// - Chỉ clear con của slot
    /// - Không đụng root CosmicObject_A/B
    /// </remarks>
    private async Task SpawnCosmicObject_ToSlot(Transform slot, CosmicObjectSO so, int index, CancellationToken ct)
    {
        if (slot == null || so == null) return;

        ClearOldSpawn(slot.gameObject);

        var instance = await solarObjectFactory.CreateAsync(so, slot, ct);
        if (ct.IsCancellationRequested)
        {
            solarObjectFactory?.DestroyInstance(instance);
            return;
        }

        if (instance != null)
        {
            instance.name = $"{so._name}_Slot_{index}";
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
        }
    }
    #endregion

    #endregion

    #region Map Generator helpers

    /// <summary>
    /// lấy child object đầu tiên
    /// </summary>
    /// <param name="parent"></param>
    /// <returns></returns>
    private Transform GetFirstChildOfCosmicObject(Transform parent)
    {
        if (parent == null) return null;
        if (parent.transform.childCount == 0) return null;
        return parent.GetChild(0).transform;
    }

    /// <summary>
    /// Xóa toàn bộ GameObject con của các Transform trong list
    /// </summary>
    /// <param name="transforms">Danh sách Transform cần clear child</param>
    /// <remarks>
    /// - Chỉ xóa child, KHÔNG xóa chính transform
    /// - Dùng Destroy (an toàn runtime)
    /// - Không async thật sự, giữ Task để đồng bộ pipeline async
    /// </remarks>
    /// <returns>Task hoàn thành</returns>
    private Task ClearAllChildObjectOnTransform(List<Transform> transforms)
    {
        if (transforms == null || transforms.Count == 0)
            return Task.CompletedTask;

        for (int i = 0; i < transforms.Count; i++)
        {
            var parent = transforms[i];
            if (parent == null) continue;

            // copy list để tránh modify collection khi destroy
            var children = new List<GameObject>();
            foreach (Transform child in parent)
                children.Add(child.gameObject);

            for (int j = 0; j < children.Count; j++)
                Destroy(children[j]);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Clear toàn bộ con của parent (chỉ xóa object đã spawn)
    /// </summary>
    /// <remarks>
    /// - Runtime: Destroy
    /// - Editor (not playing): DestroyImmediate
    /// </remarks>
    private void ClearOldSpawn(GameObject parent)
    {
        if (parent == null) return;

        var list = new List<GameObject>();
        foreach (Transform t in parent.transform)
            list.Add(t.gameObject);

        for (int i = 0; i < list.Count; i++)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
                DestroyImmediate(list[i]);
            else
                Destroy(list[i]);
#else
        Destroy(list[i]);
#endif
        }
    }

    private Vector3 RandomPointInside(Collider col)
    {
        Bounds b = col.bounds;

        //float x = UnityEngine.Random.Range(b.min.x, b.max.x);
        float y = UnityEngine.Random.Range(b.min.y, b.max.y);
        //float z = UnityEngine.Random.Range(b.min.z, b.max.z);

        Vector3 p = new Vector3(0, y, -100);

        // Nếu là MeshCollider/không convex → đảm bảo nằm trong
        if (!col.bounds.Contains(p))
            return RandomPointInside(col);

        return p;
    }

    #endregion

    #endregion

    #region Public Methods

    /// <summary>
    /// Lấy ListPoint từ PointBaker con của MapGenerator
    /// </summary>
    /// <remarks>
    /// Lấy Phần tử [0]
    /// </remarks>
    public void GetPointbaker()
    {
        _listPointSpecialZone = _pointBaker?.listPoint;
        if (_listPointSpecialZone != null) return;

        // fallback
        var foundBakers = gameObject.GetComponentsInChildren<PointBaker>(true);
        if (foundBakers.Length == 0) return;
        _listPointSpecialZone = foundBakers[0].listPoint;
    }

    #endregion
}

// Prefession
public partial class MapGenerator
{
    private void NewSession()
    {
        var ct = BeginSpawnScope();
        SpawnCosmicObjects(ct);
        SpawnSpecialPoint();
    }

    private void EndSession(bool isComplete)
    {
        if (!isComplete) return;

        ResetGeneratedContent();
    }

    private void QuitSession()
    {
        ResetGeneratedContent();
    }

}

// Event
public partial class MapGenerator : CoreEventBase
{
    public override void SubscribeEvents()
    {
        CoreEvents.OnNewSession.Subscribe(e => NewSession(), Binder);

        CoreEvents.OnEndSession.Subscribe(e => EndSession(e.IsComplete), Binder);
        CoreEvents.OnQuitSession.Subscribe(_ => QuitSession(), Binder);
    }

}



#if UNITY_EDITOR
[CustomEditor(typeof(MapGenerator))]
public class MapGeneratorEditor : Editor
{
    MapGenerator _target;

    private void Reset()
    {
        _target = (MapGenerator)target;

        _target.GetPointbaker();
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        //if (GUILayout.Button("ReSpawn Zone"))
        //{
        //    _target.TestSpawnZone();
        //}
    }
}

#endif


