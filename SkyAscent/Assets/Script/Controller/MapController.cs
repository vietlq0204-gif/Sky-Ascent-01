using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

#region Struct Progress
[Serializable]
public struct CheckProgressData
{
    [Tooltip("Đích đến")]
    /* [HideInInspector]*/
    public GameObject DestinationPoint;

    [Tooltip("% hành trình")]
    [Range(0, 100)]
    public float Progress;

    [Tooltip("Đã hoàn thành mốc này hay chưa")]
    //[HideInInspector]
    public bool Complete;
}

[Serializable]
public struct CheckProgress
{
    [HideInInspector] public bool AllComplete;
    public CheckProgressData[] ProgressData;
}
#endregion

#region Struct Distance
public enum DistanceType
{
    DisToStart,
    DisToEnd
}
[Serializable]
public struct MapDistance
{
    public float distance;
    DistanceType distanceType;
    public bool hasLoggedNearTarget;
    public bool hasLoggedLeaveStart;
}
#endregion

public class MapController : CoreEventBase, IInject<Core>, IInject<SessionController>
{
    [Header("private reference")]
    private Core _core;
    [SerializeField] private SessionController _sessionController;
    private GameObject player;


    [SerializeField] private PointBaker _pointBaker;
    [Header("Point Data"), SerializeField]
    private ListPoint _listPoint;
    /// <summary>Danh sách các RoadPoint quan trọng trên hành trình</summary>
    [SerializeField, Tooltip("Danh sách các RoadPoint quan trọng trên hành trình")]
    private List<GameObject> roadPoints => _listPoint.pointData
        .Where(p => p.point != null)
        .Select(p => p.point)
        .ToList();

    // khoảng cách để check sự kiện
    private MapDistance mapDistance;
    [Header("Road Progress")]
    [Tooltip("% tiến trình/mỗi đoạn đường/tổng hành trình/1 Session")]
    [SerializeField] private float moveProgress;
    [Tooltip("Helper Event cho % tiến trình/mỗi đoạn đường/tổng hành trình/1 Session")]
    [SerializeField] private CheckProgress checkProgress;
    private GameObject NextRoadPoint;   // roadPoint sắp tới
    private int LastRoadPointIndex = 0; // roadPoint đã tới

    [Header("test")]
    [Tooltip("Vận tốc của Map")]
    [SerializeField] float V_map = 4f;  // vận tốc của máp
    private float _defaultMapVelocity;
    private bool _isMapMovementPaused;
    private int _sessionFlowVersion;

    void Start()
    {
        _defaultMapVelocity = V_map;
        GetData();
    }

    private void OnDisable()
    {
        StopAllCoroutines();
    }

    #region Inject
    public void Inject(Core context) { _core = context; }
    public void Inject(SessionController context) { _sessionController = context; }
    #endregion

    public override void SubscribeEvents()
    {
        //CoreEvents.OnDelay.Subscribe(e => OnDelay(e.DelayType, e.DelayTime), Binder);

        CoreEvents.OnNewSession.Subscribe(_ => PrepareNewSession(), Binder);
        CoreEvents.OnSession.Subscribe(e => OnSession(e.Started), Binder);
        CoreEvents.OnPrepareEnd.Subscribe(_ => PrepareEndSession(), Binder);
        CoreEvents.OnEndSession.Subscribe(e => EndSession(e.IsComplete), Binder);
        CoreEvents.OnPauseSession.Subscribe(_ => PauseSession(), Binder);
        CoreEvents.OnResumeSession.Subscribe(_ => ResumeSession(), Binder);
        CoreEvents.OnQuitSession.Subscribe(_ => QuitSession(), Binder);

        CoreEvents.OnMoveToPoint.Subscribe(e => { if (e.IsEnd) CheckEndSessionEvent(e); }, Binder);
        CoreEvents.OnProgressMoveToPoint.Subscribe(e => AutoControlByMoveProgress(e), Binder);
        CoreEvents.OnAutoCheckDistanceMap.Subscribe(e => AutoControlVelocityByDistance(e), Binder);
        CoreEvents.PlayerFife.Subscribe(e => OnPlayerFife(e), Binder);
    }

    private void GetData()
    {
        player ??= _core.Player;

        GetRoadBaker();
    }

    public void GetRoadBaker()
    {
        _listPoint = _pointBaker?.listPoint;
        if (_listPoint != null) return;

        // fallback
        var foundBakers = gameObject.GetComponentsInChildren<PointBaker>(true);
        if (foundBakers.Length == 0) return;

        _listPoint = foundBakers[1].listPoint;
    }

    #region Check On Progress

    /// <summary>
    /// Hàm này được đăng kí và bắt khi Event OnMoveToPointEvent kết thúc để đưa ra hành động tiép theo
    /// </summary>
    /// <param name="e"></param>
    private void CheckEndSessionEvent(OnMoveToPointEvent e)
    {
        if (e == null) return;
        if (_listPoint == null) return;

        var targetPoint = e.TargetPoint as GameObject; // ép kiểu Object sang GameObject
        string eventName = targetPoint.name;
        List<string> namePoints = _listPoint.pointData
            .Where(p => p.point)
            .Select(p => p.point.name)
            .ToList();


        if (namePoints.Contains(targetPoint.name))
        {
            if (targetPoint.name == namePoints[1])
            {
                //Debug.Log($"[MapController] Ship đạt đủ độ cao. Bắt đầu hành trình");
                CoreEvents.OnSession.Raise(new OnSessionEvent(false));
            }
            if (targetPoint.name == namePoints[roadPoints.Count - 2])
            {
                //Debug.Log($"[MapController] Ship đã vào quỹ đạo của Mục tiêu");
                CoreEvents.OnPrepareEnd.Raise();
            }

            if (targetPoint.name == namePoints[roadPoints.Count - 1])
            {
                //Debug.Log($"[MapController] Đã đến bề măt mục tiêu");

                // chưa kết thúc thực sự
                CoreEvents.OnEndSession.Raise(
                    new OnEndSessionEvent(false, _sessionController.SessionSO ,PopupType.None));
            }
        }
    }

    /// <summary>
    /// Kiểm tra tiến trình (%) quảng đường còn lại (Còn update tiếp)
    /// </summary>
    /// <remarks>
    /// cứ mỗi CheckProgressData được đăng kí vào thí hàm sẽ tự động bắt khi đạt tới
    /// </remarks>
    /// <return>
    /// ĐANG ĐINH GỌI EVENT CHO MỖI MỐC ĐƯỢC ĐĂNG KÍ
    /// </return>
    private void CheckMoveProgress()
    {
        if (checkProgress.AllComplete) return;

        float percent = moveProgress;
        for (int i = 0; i < checkProgress.ProgressData.Length; i++)
        {
            ref var data = ref checkProgress.ProgressData[i];
            //if (data.DestinationPoint == null) return;

            if (!data.Complete && percent >= data.Progress)
            {
                data.DestinationPoint = NextRoadPoint;
                data.Complete = true;

                // ép kiểu sang Object
                var destinationPoint = data.DestinationPoint as object;
                // phát sự kiện ở đây...
                CoreEvents.OnProgressMoveToPoint.Raise
                    (new OnProgressMoveToPointEvent(destinationPoint, data.Progress));

                //Debug.LogWarning($"Đã đạt {data.Progress}% hành trình đến {data.DestinationPoint}");

            }
        }
    }

    private void ResetProgressTracking()
    {
        checkProgress.AllComplete = true;

        for (int i = 0; i < checkProgress.ProgressData.Length; i++)
        {
            checkProgress.ProgressData[i].DestinationPoint = null;
            checkProgress.ProgressData[i].Complete = false;
        }

        //Debug.LogError("reset ProgressTracking ");
    }

    /// <summary>
    /// kiểm tra còn cách đích đến
    /// </summary>
    /// <param name="startPos"></param>
    /// <param name="targetPoint"></param>
    private void CheckDistance(Vector3 startPos, GameObject targetPoint)
    {
        float distToStart = Vector3.Distance(transform.position, startPos);
        float distToTarget = Vector3.Distance(player.transform.position, targetPoint.transform.position);

        // Rời điểm xuất phát x đơn vị
        if (distToStart >= mapDistance.distance && !mapDistance.hasLoggedLeaveStart)
        {
            mapDistance.hasLoggedLeaveStart = true;
            CoreEvents.OnAutoCheckDistanceMap.Raise(new OnAutoCheckDistanceMapEvent(targetPoint.name, DistanceType.DisToStart));
            //Debug.LogError($"[MoveToPoint] Đã rời điểm xuất phát ~{distToStart:F2} đơn vị. target: {targetPoint.name}");
            //isPauseGame = true;
        }

        // Cách đích x đơn vị
        if (distToTarget <= mapDistance.distance && !mapDistance.hasLoggedNearTarget)
        {
            mapDistance.hasLoggedNearTarget = true;
            CoreEvents.OnAutoCheckDistanceMap.Raise(new OnAutoCheckDistanceMapEvent(targetPoint.name, DistanceType.DisToEnd));

            //Debug.LogError($"[MoveToPoint] Map còn cách {distToTarget:F2} đơn vị tới {targetPoint.name}");
            //isPauseGame = true;
        }

    }

    #endregion

    #region Calculator Velocity

    /// <summary>
    /// helper tăng/giảm đến Ips_Current trong một khoản thời gian
    /// </summary>
    /// <param name="target"></param>
    /// <param name="duration"></param>
    /// <remarks>
    /// target: Chỉ số đích.
    /// duration: tốc độ đạt đích
    /// </remarks>
    /// <returns></returns>
    public IEnumerator LerpVelocity(float target, float duration)
    {
        float start = V_map;
        float time = 0f;

        while (time < duration)
        {
            if (_isMapMovementPaused)
            {
                yield return null;
                continue;
            }

            time += Time.deltaTime;
            V_map = Mathf.Lerp(start, target, time / duration);
            yield return null; // đợi frame tiếp theo
        }

        V_map = target; // đảm bảo chính xác ngưỡng cuối
    }

    // gắn với Event Progress Tracking
    private void AutoControlByMoveProgress(OnProgressMoveToPointEvent e)
    {
        if (e.DestinationPoint == null) return;
        var destinationPoint = e.DestinationPoint as GameObject;

        if (destinationPoint.name == roadPoints[1].name) // On Session
        {
            if (e.Progress == 0)
            {
                mapDistance.distance = 15;
            }
        }
        if (destinationPoint.name == roadPoints[_listPoint.GetMiddlePointIndex()].name) // Middle Session
        {
            // tạm thời chưa có gì để làm :)
        }
        if (destinationPoint.name == roadPoints[roadPoints.Count - 2].name) // Prepare End Session
        {
            if (e.Progress == 0)
            {
                mapDistance.distance = 50;
                StartCoroutine(LerpVelocity(60f, 2f));
            }
        }
        if (destinationPoint.name == roadPoints[roadPoints.Count - 1].name) // End Session
        {
            if (e.Progress == 0)
            {
                mapDistance.distance = 15;
                StartCoroutine(LerpVelocity(15f, 1f));
            }
        }

    }

    private async void AutoControlVelocityByDistance(OnAutoCheckDistanceMapEvent e)
    {
        if (e.DistanceType == DistanceType.DisToStart) // cách điểm xuất phát
        {
            if (e.TargetName == roadPoints[1].name)
            {
                StartCoroutine(LerpVelocity(40f, 1f));
            }
            if (e.TargetName == roadPoints[roadPoints.Count - 2].name)
            {
                StartCoroutine(LerpVelocity(35f, 2f));
            }
            if (e.TargetName == roadPoints[roadPoints.Count - 1].name)
            {
                StartCoroutine(LerpVelocity(40f, 1f));
            }
        }
        if (e.DistanceType == DistanceType.DisToEnd) // sắp đến đích
        {
            if (e.TargetName == roadPoints[1].name)
            {
                StartCoroutine(LerpVelocity(5f, 1.5f));
            }
            if (e.TargetName == roadPoints[roadPoints.Count - 2].name)
            {
                StartCoroutine(LerpVelocity(20f, 1f));
            }
            if (e.TargetName == roadPoints[roadPoints.Count - 1].name)
            {
                StartCoroutine(LerpVelocity(4f, 0.5f));
                var task = EffectUtility.RaiseEffect(roadPoints[roadPoints.Count - 1], 0.2f);

                await Task.WhenAll(task);
                await Task.Delay(4000);
                 
                // kết thúc session thực sự
                CoreEvents.OnEndSession.Raise(
                    new OnEndSessionEvent(true, _sessionController.SessionSO, PopupType.Popup_Vistory));
            }
        }
    }

    #endregion

    #region Map move

    /// <summary>
    /// Di Chuyển map đến một tọa độ (PointTarget nào đó).
    /// </summary>
    /// <param name="moveSpeed"></param>
    /// <param name="rotateSpeed"></param>
    /// <param name="targetPoint"></param>
    /// <returns>
    /// Sau khie đến đích thì hàm này gọi OnMoveToPointEvent để thông báo đến các Hệ thống khác.
    /// </returns>
    public IEnumerator MoveToPoint(float moveSpeed, float rotateSpeed, GameObject targetPoint)
    {
        _core.StartTimer();

        checkProgress.AllComplete = false;
        NextRoadPoint = targetPoint;
        CheckMoveProgress();

        if (roadPoints == null || roadPoints.Count == 0) yield break;
        if (targetPoint == null) yield break;


        int targetIndex = roadPoints.IndexOf(targetPoint);
        if (targetIndex == -1 || targetIndex <= LastRoadPointIndex) yield break;

        Vector3 initialMapPos = transform.position;

        // Tính tổng độ dài toàn tuyến
        float totalDistance = 0f;
        for (int i = LastRoadPointIndex; i < targetIndex; i++)
        {
            totalDistance += Vector3.Distance(
                roadPoints[i].transform.position,
                roadPoints[i + 1].transform.position);
        }

        //float elapsed = 0f;
        float normalizedTime = 0f;

        // Duy trì moveSpeed cố định hoặc đọc V_map
        while (normalizedTime < 1f)
        {
            if (_isMapMovementPaused)
            {
                yield return null;
                continue;
            }

            // Ưu tiên V_map runtime, fallback về moveSpeed gốc
            float currentSpeed = V_map > 0 ? V_map : moveSpeed;
            // Tính quãng đường di chuyển trong frame này
            float frameDistance = currentSpeed * Time.deltaTime;
            // Chuyển sang tỷ lệ tiến trình trên toàn tuyến
            float deltaNormalized = frameDistance / totalDistance;
            normalizedTime = Mathf.Clamp01(normalizedTime + deltaNormalized);

            // Cập nhật vị trí ảo dọc đường đi
            NextRoadPoint = targetPoint;
            moveProgress = normalizedTime * 100;
            CheckMoveProgress();

            Vector3 playerVirtualPos = GetPositionAlongPath(LastRoadPointIndex, targetIndex, normalizedTime);
            Vector3 delta = playerVirtualPos - roadPoints[LastRoadPointIndex].transform.position;
            transform.position = initialMapPos - delta;

            CheckDistance(initialMapPos, targetPoint);

            yield return null;
        }

        // Kết thúc hành trình
        LastRoadPointIndex = targetIndex;
        moveProgress = 0f;

        _core.StopTimer();
        _core.ResetTimer();

        // sự kiện này để thông báo đã đến nơi (đang được xử lí bởi CheckEndEvent)
        CoreEvents.OnMoveToPoint.Raise(new OnMoveToPointEvent(targetPoint, true));
        ResetProgressTracking();

        mapDistance.hasLoggedNearTarget = false;
        mapDistance.hasLoggedLeaveStart = false;
    }

    /// <summary>
    /// Trả về vị trí ảo của player dọc tuyến đường (nội suy liên tục qua nhiều segment)
    /// </summary>
    private Vector3 GetPositionAlongPath(int startIndex, int endIndex, float t)
    {
        // Clamp t [0–1]
        t = Mathf.Clamp01(t);

        // Tính tổng độ dài toàn đoạn
        float totalDist = 0f;
        for (int i = startIndex; i < endIndex; i++)
            totalDist += Vector3.Distance(roadPoints[i].transform.position, roadPoints[i + 1].transform.position);

        float targetDist = totalDist * t;
        float accumulated = 0f;

        // Tìm segment mà t đang nằm trong
        for (int i = startIndex; i < endIndex; i++)
        {
            Vector3 a = roadPoints[i].transform.position;
            Vector3 b = roadPoints[i + 1].transform.position;
            float segDist = Vector3.Distance(a, b);

            if (accumulated + segDist >= targetDist)
            {
                float localT = (targetDist - accumulated) / segDist;
                return Vector3.Lerp(a, b, localT);
            }
            accumulated += segDist;
        }

        // Nếu vượt cuối đường
        return roadPoints[endIndex].transform.position;
    }

    #endregion

    #region Session

    private int StartSessionFlow()
    {
        _sessionFlowVersion++;
        return _sessionFlowVersion;
    }

    private bool IsCurrentSessionFlow(int flowVersion)
    {
        return flowVersion == _sessionFlowVersion;
    }

    private void ResetSessionState()
    {
        StopAllCoroutines();

        _isMapMovementPaused = false;
        V_map = _defaultMapVelocity;
        LastRoadPointIndex = 0;
        moveProgress = 0f;
        NextRoadPoint = null;

        mapDistance.hasLoggedNearTarget = false;
        mapDistance.hasLoggedLeaveStart = false;

        _core?.StopTimer();
        _core?.ResetTimer();

        transform.position = Vector3.zero;
        ResetProgressTracking();
    }

    /// <summary>
    /// Chuẩn bị Data, Invironment cho NewSession
    /// - chạy Effect khói bụi và không đợi hết
    /// </summary>
    private async void PrepareNewSession()
    {
        int flowVersion = StartSessionFlow();
        ResetSessionState();

        // effect khói bụi của bề mặc hành tình (Delay 5s, 5s)
        var task = EffectUtility.RaiseEffect(roadPoints[0], 5f, 2f);
        await Task.WhenAll(task); // đợi cho đến khi hết thời gian
        if (!IsCurrentSessionFlow(flowVersion)) return;

        //Debug.Log("[MapController] Ship bắt đầu phóng lên");
        StartCoroutine(MoveToPoint(V_map, 0.5f, roadPoints[1])); // đến Point OnSession
    }

    /// <summary>
    /// nếu stated == true thì bắt đầu hành trình
    /// </summary>
    private async void OnSession(bool stated)
    {
        //G_Current = 0f;
        if (!stated) return;

        int flowVersion = _sessionFlowVersion;

        await Task.Delay(500);
        if (!IsCurrentSessionFlow(flowVersion)) return;

        StartCoroutine(MoveToPoint(V_map, 5f, roadPoints[roadPoints.Count - 2])); // Point áp chót
        //Debug.Log($"[MapController] Ship đang trong hành trình đến đích");
    }

    /// <summary>
    /// Chuẩn bị Data, Invironment cho camera cho EndSession
    /// 
    /// </summary>
    private async void PrepareEndSession()
    {
        //G_Current = mapData.SolarObjectSO[1].G;
        int flowVersion = _sessionFlowVersion;

        await Task.Delay(2000);  // effect mây tảng ra các thứ
        if (!IsCurrentSessionFlow(flowVersion)) return;

        //Debug.Log("[MapController] Ship bắt đầu hạ cánh");
        StartCoroutine(MoveToPoint(V_map, 0.5f, roadPoints[roadPoints.Count - 1]));
    }

    /// <summary>
    /// Xử lí các logic của map khi EndSessionEvent được kích hoạt
    /// - chạy Effect và đợi nó kết thúc
    /// </summary>
    private async void EndSession(bool isComplete)
    {
        if (!isComplete) return;

        int flowVersion = _sessionFlowVersion;

        //Debug.LogWarning($"Name Current SessionSO: {_sessionController.SessionSO._name}");

        // gửi Event kèm Data để cập nhật CurrentDefaultCosmicObject
        // kèm cập nhật viewCosmicObject
        var cosmicObjectSO = _sessionController.SessionSO.mapSO.cosmicObjectSO[1];
        CoreEvents.MapDataEvent.Raise(new OnMapDataEvent(cosmicObjectSO));

        LastRoadPointIndex = 0; // reset để session mới bắt đầu lại từ đầu

        StopAllCoroutines();
        //Debug.Log("[MapController] Ship đã hạ cánh thành công");

        await Task.Delay(1000);
        if (!IsCurrentSessionFlow(flowVersion)) return;
        //var task = EffectUtility.RaiseEffect(roadPointImportant[4], 5f, 2f);
        //await Task.WhenAll(task); // đợi cho đến khi hết thời gian

        gameObject.transform.position = Vector3.zero; // reset vị trí map về gốc
    }

    private void OnPlayerFife(PlayerFifeEvent e)
    {
        if (e == null) return;
        if (e.state == PlayerFifeState.dead)
        {
            _isMapMovementPaused = true;
            return;
        }

        if (e.state == PlayerFifeState.Revive)
        {
            _isMapMovementPaused = false;
        }
    }

    private void PauseSession()
    {
        _isMapMovementPaused = true;
    }

    private void ResumeSession()
    {
        _isMapMovementPaused = false;
    }

    private void QuitSession()
    {
        StartSessionFlow();
        ResetSessionState();
    }

    #endregion

}

#if UNITY_EDITOR

[CustomEditor(typeof(MapController))]
public class MapControllerEditor : Editor
{
    private MapController _target;

    private void Reset()
    {
        _target = (MapController)target;
        _target.GetRoadBaker();
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        if (GUILayout.Button("Get RoadBaker"))
        {
            _target.GetRoadBaker();
        }
    }
}

#endif
