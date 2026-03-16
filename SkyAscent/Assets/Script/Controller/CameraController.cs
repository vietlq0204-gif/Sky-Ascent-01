using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using static ListPoint;

public partial class CameraController : CoreEventBase, IInject<Core>, IInject<CameraManager>
{
    #region references // variables

    private Core _core;
    [SerializeField] private CameraManager _cameraManager;

    /// <summary>Cache tham chiếu nhanh tới ListPoint hiện tại</summary>
    [Header("Point Data"), SerializeField]
    private ListPoint _listPoint;

    #endregion

    #region unity lifecycle

    private void Start()
    {
        GetData();
    }

    private void Update()
    {
        GetData();

        bool isUpgradeNow = _core?.SecondaryStateMachine?.CurrentStateType == typeof(UpgradeState);

        if (isUpgradeNow && !_upgradeInitialized)
        {
            _upgradeInitialized = true;
            OnUpgrade();       // chỉ chạy 1 lần
        }
        else if (!isUpgradeNow && _upgradeInitialized)
        {
            _upgradeInitialized = false;
            ExitUpgrade();     // chỉ chạy 1 lần
        }

    }

    #endregion

    #region Inject

    public void Inject(Core context) { _core = context; }
    public void Inject(CameraManager context) { _cameraManager = context; }

    #endregion

    /// <summary>
    /// Lấy dữ liệu Point từ CameraManager → PointBaker
    /// </summary>
    private void GetData()
    {
        if (_cameraManager == null) return;

        var baker = _cameraManager.PointData;
        if (baker == null) return;
        _listPoint = baker.listPoint;
    }

    #region Logic

    /// <summary>
    /// Di chuyển camera dọc theo đường dẫn với tốc độ di chuyển và tốc độ xoay đã cho, sử dụng vị trí của dirObject làm offset.
    /// </summary>
    /// <param name="moveSpeed"></param>
    /// <param name="rotateSpeed"></param>
    /// <param name="dirObject"></param>
    public IEnumerator MoveAlongPath(float moveSpeed, float rotateSpeed, GameObject target)
    {
        CoreEvents.OnMoveAlongToPath.Raise();

        if (target == null)
        {
            Debug.LogWarning("[CameraController] Target is null.");
            yield break;
        }

        Vector3 startPos = transform.position;
        Vector3 endPos = target.transform.position;
        Quaternion startRot = transform.rotation;
        Quaternion endRot = target.transform.rotation; // nếu target có hướng, có thể bỏ nếu không cần xoay

        float distance = Vector3.Distance(startPos, endPos);
        float t = 0f;

        // tránh chia 0
        if (distance < 0.01f)
        {
            transform.position = endPos;
            yield break;
        }

        // Tính thời gian di chuyển theo quãng đường
        float duration = distance / moveSpeed;

        while (t < 1f)
        {
            t += Time.deltaTime / duration;
            transform.position = Vector3.Lerp(startPos, endPos, Mathf.SmoothStep(0f, 1f, t));

            // xoay camera về hướng target
            transform.rotation = Quaternion.Slerp(startRot, endRot, Mathf.SmoothStep(0f, 1f, t));

            yield return null;
        }

        // đảm bảo camera dừng chính xác tại target
        transform.position = endPos;
        transform.rotation = endRot;

        //Debug.LogWarning($"[CameraController] Arrived at {target.name}");

        CoreEvents.OnMoveAlongToPath.Raise(OnMoveAlongPathEvent.End()); // nó đang liên kết với cooldown của Swipe
    }

    public IEnumerator LerpFov(float targetFov, float duration)
    {
        if (_cameraManager.mainCamera == null) yield break;

        float startFov = _cameraManager.mainCamera.fieldOfView;
        float time = 0f;

        while (time < duration)
        {
            time += Time.deltaTime;
            float t = time / duration;
            _cameraManager.mainCamera.fieldOfView = Mathf.Lerp(startFov, targetFov, t);
            yield return null;
        }

        _cameraManager.mainCamera.fieldOfView = targetFov; // đảm bảo chính xác
    }

    private async Task Delay(float seconds)
    {
        //Debug.Log("Start Delay");
        await Task.Delay((int)(seconds * 1000));
        //Debug.Log("End Delay");
    }

    private async void OnDelay(DelayType delayType, float timeDelay)
    {
        if (delayType == DelayType.PrepareNewSession)
        {
            await Delay(timeDelay);
            //OnSession();
        }
    }

    #endregion

}

// API
public partial class CameraController
{
    public void MoveCameraToTarget(GameObject target, float moveSpeed, float rotateSpeed = 0f)
    {
        StartCoroutine(MoveAlongPath(moveSpeed, rotateSpeed, target));
    }
}

// partial class Globular mode
public partial class CameraController
{
    private bool _upgradeInitialized;

    [Header("Orbital / Globular Mode")]
    [SerializeField] private float rotationSpeed = 0.25f;
    [SerializeField] private Vector2 pitchClampDegrees = new Vector2(-85f, 85f);

    private Vector3 _currentOffset;
    private Vector2 _angularVelocity;

    private float _yaw;
    private float _pitch;

    private bool _isUpgrade;

    // NEW: radius runtime để không bị kéo về 10f làm teleport
    private float _runtimeRadius;

    // NEW: chỉ warp 1 lần ở lần drag đầu tiên trong Upgrade
    private bool _isFirstDragInUpgradeDone;

    private Vector3 SphericalCenter => CalculatePivotPosition();

    private float SphericalRadius => 10f; // vẫn giữ default 10f

    /// <summary>
    /// Lấy pivot position cho spherical map dựa trên ListPoint.
    /// </summary>
    /// <remarks>
    /// Ưu tiên MiddlePoint; nếu không có thì dùng centroid của các point hợp lệ.
    /// Không đảm bảo đây là "tâm mặt cầu" theo nghĩa toán học.
    /// </remarks>
    /// <returns>Pivot position.</returns>
    private Vector3 CalculatePivotPosition()
    {
        if (_listPoint == null || _listPoint.pointData == null || _listPoint.pointData.Length == 0)
            return transform.position;

        if (_listPoint.TryGetMiddlePoint(out var midPoint, out _) && midPoint != null)
            return midPoint.transform.position;

        return CalculateCentroidOrFallback(_listPoint.pointData, transform.position);
    }

    /// <summary>
    /// Tính centroid từ mảng pointData.
    /// </summary>
    /// <remarks>
    /// Bỏ qua các point null/destroy.
    /// </remarks>
    /// <returns>Centroid nếu có điểm hợp lệ; nếu không trả fallback.</returns>
    private static Vector3 CalculateCentroidOrFallback(PointData[] pointData, Vector3 fallback)
    {
        Vector3 sum = Vector3.zero;
        int count = 0;

        for (int i = 0; i < pointData.Length; i++)
        {
            var p = pointData[i].point;
            if (p != null)
            {
                sum += p.transform.position;
                count++;
            }
        }

        return count > 0 ? (sum / count) : fallback;
    }

    /// <summary>
    /// Called when entering Upgrade state.
    /// </summary>
    /// <remarks>
    /// Reset cờ first-drag để lần drag đầu có thể warp về point0.
    /// </remarks>
    public void OnUpgrade()
    {
        if (_core?.SecondaryStateMachine?.CurrentStateType != typeof(UpgradeState)) return;

        _isUpgrade = true;

        // reset first drag
        _isFirstDragInUpgradeDone = false;

        // radius mặc định (sẽ bị override khi warp về point0 trong drag đầu)
        _runtimeRadius = SphericalRadius;

        // Khởi tạo offset ban đầu (giữ nguyên logic hiện tại của Ní)
        Vector3 offset = transform.position - SphericalCenter;
        float currentDist = offset.magnitude;

        if (currentDist < 0.1f)
            _currentOffset = Vector3.back * _runtimeRadius;
        else
            _currentOffset = offset.normalized * _runtimeRadius;

        _angularVelocity = Vector2.zero;
    }

    /// <summary>
    /// Called when exiting Upgrade state.
    /// </summary>
    public void ExitUpgrade()
    {
        _isUpgrade = false;
        _angularVelocity = Vector2.zero;

        // reset
        _isFirstDragInUpgradeDone = false;
    }

    /// <summary>
    /// Handle drag input for spherical camera rotation (continuous).
    /// </summary>
    private void OnDrag(DragInputEvent e)
    {
        if (e == null) return;
        if (!_isUpgrade) return;
        if (_listPoint == null) return;

        // Chỉ xử lý khi drag đang di chuyển
        if (!e.isMove) return;

        // First drag: lấy start position = point0
        if (!_isFirstDragInUpgradeDone)
        {
            TryWarpCameraToPoint0AsDragStart();
            _isFirstDragInUpgradeDone = true;
        }

        ApplySphericalDrag(e.deltaScreen);
    }

    /// <summary>
    /// Warp camera về _listPoint[0] khi bắt đầu drag lần đầu trong Upgrade.
    /// </summary>
    /// <remarks>
    /// - Đảm bảo camera bắt đầu đúng tọa độ point0.
    /// - Init yaw/pitch theo offset hiện tại để drag tiếp theo không bị nhảy.
    /// - Dùng runtimeRadius = distance(center, point0) để không bị kéo về 10f.
    /// </remarks>
    private void TryWarpCameraToPoint0AsDragStart()
    {
        GameObject p0 = _listPoint.GetPoint(0);
        if (p0 == null) return;

        transform.position = p0.transform.position;

        InitOrbitFromCurrentPosition();
    }

    /// <summary>
    /// Init yaw/pitch + offset + runtimeRadius từ vị trí hiện tại của camera.
    /// </summary>
    /// <remarks>
    /// Đây là điểm mấu chốt để không teleport khi UpdateCameraTransform chạy.
    /// </remarks>
    private void InitOrbitFromCurrentPosition()
    {
        Vector3 center = SphericalCenter;
        Vector3 offset = transform.position - center;

        if (offset.sqrMagnitude < 0.000001f)
        {
            offset = Vector3.back * SphericalRadius;
            transform.position = center + offset;
        }

        Vector3 dir = offset.normalized;

        // rot sao cho rot * Vector3.back == dir
        Quaternion rot = Quaternion.LookRotation(-dir, Vector3.up);

        float yaw = NormalizeAngle(rot.eulerAngles.y);
        float pitch = NormalizeAngle(rot.eulerAngles.x);

        pitch = Mathf.Clamp(pitch, pitchClampDegrees.x, pitchClampDegrees.y);

        _yaw = yaw;
        _pitch = pitch;

        // radius runtime = đúng khoảng cách hiện tại tới tâm (để giữ đúng point0)
        _runtimeRadius = offset.magnitude;

        // offset chuẩn theo yaw/pitch đã clamp (để đồng nhất với UpdateCameraTransform)
        Quaternion clampedRot = Quaternion.Euler(_pitch, _yaw, 0f);
        _currentOffset = (clampedRot * Vector3.back) * _runtimeRadius;

        // đồng bộ luôn transform (không làm đổi vị trí vì đang khớp)
        transform.position = center + _currentOffset;
        transform.rotation = clampedRot;
    }

    /// <summary>
    /// Normalize góc về [-180..180].
    /// </summary>
    /// <returns>Normalized angle.</returns>
    private static float NormalizeAngle(float angle)
    {
        angle %= 360f;
        if (angle > 180f) angle -= 360f;
        return angle;
    }

    /// <summary>
    /// Rotate camera around spherical center using screen drag + inertia.
    /// </summary>
    private void ApplySphericalDrag(Vector2 deltaScreen)
    {
        if (deltaScreen.sqrMagnitude < 1f) return;

        _yaw += deltaScreen.x * rotationSpeed;
        _pitch -= deltaScreen.y * rotationSpeed;

        _pitch = Mathf.Clamp(_pitch, pitchClampDegrees.x, pitchClampDegrees.y);

        UpdateCameraTransform();
    }

    /// <summary>
    /// Đồng bộ position + rotation của camera
    /// </summary>
    /// <remarks>
    /// Dùng _runtimeRadius để tránh bị teleport về bán kính 10f khi point0 không đúng radius.
    /// </remarks>
    private void UpdateCameraTransform()
    {
        Quaternion rot = Quaternion.Euler(_pitch, _yaw, 0f);

        _currentOffset = rot * Vector3.back * _runtimeRadius;

        transform.position = SphericalCenter + _currentOffset;
        transform.rotation = rot;
    }

    // Optional: gọi trong LateUpdate nếu muốn damping mượt hơn khi không drag
    //private void LateUpdate()
    //{
    //    if (!_isUpgrade) return;
    //    if (_angularVelocity.sqrMagnitude < 0.0001f) return;

    //    ApplyOrbitalRotation(_angularVelocity.x, _angularVelocity.y);
    //    _angularVelocity *= damping;

    //    // Nếu vận tốc gần 0 thì tắt hẳn
    //    if (_angularVelocity.sqrMagnitude < 0.0001f)
    //        _angularVelocity = Vector2.zero;
    //}

    /// <summary>
    /// Move camera to target and complete when done.
    /// </summary>
    /// <remarks>
    /// Wrap coroutine into Task to await sequentially.
    /// </remarks>
    private Task MoveCameraToTargetAsync(GameObject target, float moveSpeed, float rotateSpeed = 0f)
    {
        var tcs = new TaskCompletionSource<bool>(); // là TaskCompletionSource để hoàn thành khi coroutine kết thúc

        StartCoroutine(Co_MoveCameraToTargetAsync(target, moveSpeed, rotateSpeed, tcs));

        return tcs.Task; // trả về Task để caller có thể await
    }

    /// <summary>
    /// Coroutine wrapper that completes a Task when movement ends.
    /// </summary>
    /// <returns>IEnumerator</returns>
    private IEnumerator Co_MoveCameraToTargetAsync(GameObject target, float moveSpeed, float rotateSpeed, TaskCompletionSource<bool> tcs)
    {
        yield return MoveAlongPath(moveSpeed, rotateSpeed, target);
        tcs.TrySetResult(true); // đánh dấu Task hoàn thành sau khi di chuyển xong
    }
}

// Profession
public partial class CameraController
{
    private async void OnMenuPage(bool Open)
    {
        await Task.Delay(100);

        if (_listPoint == null) return;
        if (_core.StateMachine.CurrentStateType != typeof(OnMenuState)) return;

        try
        {
            GameObject target;
            bool oke;

            if (Open)
            {
                //oke = (_listPoint.TryGetLastPoint(out target) && target != null) ? target : _listPoint.GetPoint(1);
                //if (oke) await MoveCameraToTargetAsync(target, 10f);

                oke = (_listPoint.TryGetFirstPoint(out target) && target != null) ? target : _listPoint.GetPoint(0);
                if (oke) await MoveCameraToTargetAsync(target, 25f);
            }
        }
        catch (Exception e)
        {

            throw new Exception($"[CameraController] Error in OnMenuPage: {e.Message}");
        }

    }

    private async void OnUpgradePage(bool Open)
    {
        await Task.Delay(500);
        if (_listPoint == null) return;
        if (_core.SecondaryStateMachine.CurrentStateType != typeof(UpgradeState)) return;

        try
        {
            GameObject target;

            if (Open)
            {
                // var oke = _listPoint.TryGetLastPoint(out target) ? target : _listPoint.GetPoint(2);
                // await MoveCameraToTargetAsync(target, 30f); //  tâm

                _listPoint.TryGetFirstPoint(out target);
                await MoveCameraToTargetAsync(target, 15f); // nghiên
            }

        }
        catch (Exception e)
        {

            throw new Exception($"[CameraController] Error in OnUpgradePage: {e.Message}");
        }
    }

    /// <summary>
    /// Reset camera về First Point khi bắt đầu Session
    /// </summary>
    private async void PrepareNewSession()
    {
        // đợi tí để data được fill đủ
        await Delay(0.5f);

        if (_listPoint == null) return;

        // đưa camera đến điểm view gần
        GameObject target = _listPoint.GetPoint(0);

        StartCoroutine(MoveAlongPath(20f, 0f, target));

        Debug.Log("[CameraConttroller] Reset camera");
    }

    // đưa camera vào rule view sesion
    private async void OnSession(bool started)
    {
        if (started) return;
        if (_listPoint == null) return;

        // đưa camera vào point view sesion
        GameObject target = _listPoint.GetPoint(1);
        if (target == null) return;

        var taskMoveCamera = Core.WaitForCoroutine(this, MoveAlongPath(10f, 3f, target));
        var taskLerpFov = Core.WaitForCoroutine(this, LerpFov(70, 2f));

        await Task.WhenAll(taskMoveCamera, taskLerpFov);

        CoreEvents.OnSession.Raise(new OnSessionEvent(true));
    }

    /// <summary>
    /// đưa camera vào vị trí view ha cánh
    /// </summary>
    private void PrepareEndSession()
    {
        //if (_core.StateMachine.CurrentStateType == typeof(OnMenuEvent)) return;

        if (_listPoint == null) return;

        // đưa camera vào point view gần
        GameObject target = _listPoint.GetPoint(0);

        StartCoroutine(MoveAlongPath(5f, 3f, target));
        StartCoroutine(LerpFov(60, 2f));
    }

}

// Event
public partial class CameraController : CoreEventBase
{

    public override void SubscribeEvents()
    {
        CoreEvents.OnDelay.Subscribe(e => OnDelay(e.DelayType, e.DelayTime), Binder);

        //CoreEvents.OnSwipe.Subscribe(e => SelectRoadPointInMenu(e.Type, e.RoadPoint), Binder);
        CoreEvents.Drag.Subscribe(e => OnDrag(e), Binder);

        CoreEvents.OnMenu.Subscribe(e => OnMenuPage(e.IsOpenMenu), Binder);
        CoreEvents.UpdgadePanel.Subscribe(e => OnUpgradePage(true), Binder);
        //CoreEvents.OnMoveAlongToPath.Subscribe(e => { if (e.IsEnd) OnMenu(e.IsEnd); }, Binder);

        CoreEvents.OnNewSession.Subscribe(_ => PrepareNewSession(), Binder);
        CoreEvents.OnSession.Subscribe(e => OnSession(e.Started), Binder);
        CoreEvents.OnPrepareEnd.Subscribe(_ => PrepareEndSession(), Binder);
        //CoreEvents.OnEndSession.Subscribe(_ => EndSession(), Binder);

    }

}

