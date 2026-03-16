//using System.Collections.Generic;
//using System.Reflection;
//using UnityEngine;

//public class ViewObje : CoreEventBase
//{
//    public enum ObjectLayoutMode
//    {
//        OneObjectPerPoint,  // mỗi object gắn với 1 point
//        SpacingOnPath,      // mỗi object lệch nhau theo độ dài path
//    }

//    public enum SnapGroupMode
//    {
//        SnapGroupToFirst,   // _object[0] vào Middle, _object[i] tăng dần về hướng _point[0]
//        SnapGroupToLast,    // _object[0] vào Middle, _object[i] tăng dần về hướng _point[last]
//        SnapGroupToMiddle,  // group nằm giữa
//    }

//    public enum DragBehaviorMode
//    {
//        Normal,
//        Inertia,    // có quán tính
//        FixPoint    // mỗi swipe = nhảy 1 point
//    }

//    public enum FixPointCooldownMode
//    {
//        UntilReachedTarget,
//        Time,
//    }

//    [Header("----------------------------------------------------------------------------")]
//    //[SerializeField] bool ThisReady = false;

//    [SerializeField] private ObjectLayoutMode _layoutMode;
//    [SerializeField] private SnapGroupMode _snapMode;
//    [SerializeField] private DragBehaviorMode _dragMode;

//    [Header("----------------------------------------------------------------------------")]
//    //[SerializeField]
//    private List<Transform> ChildObjects = new List<Transform>();

//    /*[SerializeField]*/
//    private Transform _objectRoot; // nơi chứa các object cần kéo
//    /*[SerializeField]*/
//    private Transform _queueRoot; // nơi chứa object dư (ẩn / gom lại)
//    /*[SerializeField]*/
//    private PointBaker _pointBaker;

//    [SerializeField] private List<Transform> _points = new List<Transform>(); // các điểm tạo đường
//    [SerializeField] private List<Transform> _objects = new List<Transform>(); // các object cần kéo

//    private ListPoint.PathCache _pathCache;
//    private Transform _middlePoint; // nếu không set, lấy _points[midIndex]

//    [Header("----------------------------------------------------------------------------")]
//    [SerializeField] private bool _useOnlyHorizontal = true; // true: chỉ dùng vuốt X
//    [SerializeField] private bool _invertSwipe = false;      // đảo ngược hướng vuốt

//    private int _midObjIndexCache;
//    private float _shiftFloat;
//    private float _midS;                             // vị trí dọc đường của Middle
//    private int _midIndex;                           // index của Middle trong list points (gần đúng)

//    [Header("----------------------------------------------------------------------------")]
//    [SerializeField] private float _spacingOnPath = 40; // khoản cách giữa các _object (world)
//    [SerializeField] private float _speed = 1.0f; // tốc độ đi theo "độ vuốt"
//    [SerializeField] private float _pixelsForFullPath = 800f; // kéo ~800px đi hết path (tùy cảm giác)

//    private const float EDGE_EPS = 0.0005f; // chống rung biên

//    private readonly List<float> _offsetS = new List<float>(64); // offset theo độ dài path
//    private float _baseS; // progress chung (0..totalLen)
//    private readonly Dictionary<Transform, Transform> _originalParent = new Dictionary<Transform, Transform>(64); // parent gốc
//    private float _minOffsetS;
//    private float _maxOffsetS;

//    [SerializeField] private Vector3 _positionOffset = Vector3.zero;

//    [Header("------inertia---------------------------------------------------------------------")]
//    private bool _dragActive;
//    private float _totalDragAxis; // tổng swipe (pixel) dùng cho FixPoint
//    [SerializeField] private float _inertiaDamping = 5f; // càng lớn dừng càng nhanh
//    private float _velocityS;        // cho SpacingOnPath
//    private float _velocityIndex;    // cho OneObjectPerPoint

//    [SerializeField] private float _minInertiaSpeed = 0.01f; // ngưỡng dừng

//    [SerializeField] private float _fixPointSwipeThreshold = 80f; // px: đủ dài mới nhảy 1 step


//    [Header("------FixPoint---------------------------------------------------------------------")]

//    [SerializeField] private FixPointCooldownMode _fixPointCooldownMode;
//    [SerializeField] private bool _fixPointCooldown = true;     // bật/tắt cooldown
//    [SerializeField] private float _fixPointCooldownTime = 0.08f; // giây (chỉ dùng khi mode = Time)
//    [SerializeField] private float _fixPointSmoothTime = 0.08f; // thời gian mượt (giây)
//    [SerializeField] private float _fixPointStopEps = 0.0005f;   // ngưỡng coi như tới nơi

//    private bool _isFixPointAnimating;
//    private float _targetBaseS;
//    private float _targetShiftFloat;

//    private float _smoothVelS;       // velocity cho SmoothDamp baseS
//    private float _smoothVelShift;   // velocity cho SmoothDamp shiftFloat

//    private bool _isFixPointCooldown; // true = đang cooldown, không cho swipe
//    private float _fixPointCooldownTimer;

//    [Header("------Middle Detect & AutoFix--------------------------------------------")]

//    [SerializeField] private Transform _currentMiddleObject;
//    [SerializeField] private bool _wasReachedMiddle = false;
//    [SerializeField] private bool _logWhenReachMiddle = true;


//    /// <summary> Ngưỡng (world units) để coi như object "đạt Middle".  </summary>
//    [SerializeField] private float _reachMiddlePosEps = 0.0025f;

//    /// <summary>Bật auto-fix khi object ở Middle bị lệch vị trí (do swipe quá nhanh khi tắt cooldown).  </summary>

//    [SerializeField] private bool _autoFixMiddleDrift = true;

//    /// <summary>Ngưỡng bắt đầu fix (lớn) và ngưỡng kết thúc fix (nhỏ) để tránh rung.</summary>
//    [SerializeField] private float _autoFixStartEps = 0.02f;

//    /// <summary> Log + notify khi object chạm Middle (hoặc đổi object ở Middle).</summary>
//    //public event System.Action<Transform> OnReachMiddle;

//#if UNITY_EDITOR
//    private void Reset()
//    {
//        if (!Application.isPlaying)
//            BuildPathCache();
//    }
//#endif

//    #region Unity lifecycle

//    protected override void Awake()
//    {
//        base.Awake();

//        FindAndGetChildGameObject(transform, ChildObjects);

//        InitRef();
//    }

//    private void Update()
//    {

//        if (_pathCache == null || _pathCache.positions == null || _pathCache.positions.Count < 2) return;

//        // FixPoint cooldown tick (mode = Time)
//        if (_isFixPointCooldown && _fixPointCooldown && _fixPointCooldownMode == FixPointCooldownMode.Time)
//        {
//            _fixPointCooldownTimer -= Time.deltaTime;
//            if (_fixPointCooldownTimer <= 0f)
//            {
//                _isFixPointCooldown = false;
//                _fixPointCooldownTimer = 0f;
//            }
//        }

//        if (!_dragActive && _dragMode == DragBehaviorMode.Inertia)
//        {
//            float dt = Time.deltaTime;
//            float lerpT = Mathf.Clamp01(_inertiaDamping * dt);

//            if (_layoutMode == ObjectLayoutMode.SpacingOnPath)
//            {
//                if (Mathf.Abs(_velocityS) > _minInertiaSpeed)
//                {
//                    _baseS += _velocityS * dt;
//                    _velocityS = Mathf.Lerp(_velocityS, 0f, lerpT);

//                    ClampBaseS_MiddleLock_Spacing();
//                    ApplyObjectsWithQueue_Spacing();
//                }
//            }
//            else
//            {
//                if (Mathf.Abs(_velocityIndex) > _minInertiaSpeed)
//                {
//                    _shiftFloat += _velocityIndex * dt;
//                    _velocityIndex = Mathf.Lerp(_velocityIndex, 0f, lerpT);

//                    ClampShiftFloat_OnePerPoint();
//                    ApplyObjectsWithQueue_OnePerPoint();
//                }
//            }
//        }

//        if (!_dragActive && _dragMode == DragBehaviorMode.FixPoint)
//        {
//            AutoFixMiddleDrift_FixPoint();
//        }

//        // FixPoint smooth animation tick (when NOT dragging)
//        if (!_dragActive && _dragMode == DragBehaviorMode.FixPoint && _isFixPointAnimating)
//        {
//            if (_layoutMode == ObjectLayoutMode.OneObjectPerPoint)
//            {
//                _shiftFloat = Mathf.SmoothDamp(_shiftFloat, _targetShiftFloat, ref _smoothVelShift, _fixPointSmoothTime);

//                ClampShiftFloat_OnePerPoint();
//                ApplyObjectsWithQueue_OnePerPoint();

//                if (Mathf.Abs(_shiftFloat - _targetShiftFloat) <= _fixPointStopEps)
//                {
//                    _shiftFloat = _targetShiftFloat;
//                    _smoothVelShift = 0f;
//                    _isFixPointAnimating = false;

//                    ApplyObjectsWithQueue_OnePerPoint();

//                    // Cooldown mode 2: release when reached target
//                    if (_fixPointCooldown && _fixPointCooldownMode == FixPointCooldownMode.UntilReachedTarget)
//                        _isFixPointCooldown = false;

//                    //AutoFixMiddleDrift();
//                }

//            }
//            else
//            {
//                _baseS = Mathf.SmoothDamp(_baseS, _targetBaseS, ref _smoothVelS, _fixPointSmoothTime);

//                ClampBaseS_MiddleLock_Spacing();
//                ApplyObjectsWithQueue_Spacing();

//                if (Mathf.Abs(_baseS - _targetBaseS) <= _fixPointStopEps)
//                {
//                    _baseS = _targetBaseS;
//                    _smoothVelS = 0f;
//                    _isFixPointAnimating = false;

//                    ApplyObjectsWithQueue_Spacing();

//                    // Cooldown mode 2: release when reached target
//                    if (_fixPointCooldown && _fixPointCooldownMode == FixPointCooldownMode.UntilReachedTarget)
//                        _isFixPointCooldown = false;
//                }
//            }


//        }

//        // Detect + log middle
//        if (!_dragActive)
//        {
//            TickMiddleDetectAndLog();
//        }

//    }

//    #endregion

//    private void InitRef()
//    {
//        _objectRoot = ChildObjects[0];
//        _queueRoot = ChildObjects[1];
//        _pointBaker = ChildObjects[2].GetComponent<PointBaker>();
//    }

//    private void InitData(bool ready)
//    {
//        if (!ready) return;

//        FindAndGetChildGameObject(_objectRoot, _objects);

//        BuildPathCache();
//        CacheMiddleSAndIndex();

//        CacheOriginalParents();
//        EnsureQueueRoot();

//        SnapGroup();
//    }

//    #region Event

//    public override void SubscribeEvents()
//    {
//        // sau khi ChapterView chuẩn bị xong data
//        CoreEvents.LoadChapter.Subscribe(e =>
//        {
//            if (e.typeData == LoadDataEvent.TypeData.Chapter)
//            {
//                InitData(e.Completed);
//            }
//        }, Binder);

//        // nhận thông báo Input drag
//        CoreEvents.Drag.Subscribe(OnDragEvent, Binder);

//        // nhận thông báo yêu cầu TargetToMiddle từ Mapcontroller (cần fix sang dùng TargetObjectEvent)
//        CoreEvents.MapDataEvent.Subscribe(e =>
//        TargetGroupOnObjectToMiddle(e.CosmicObjectSO.name), Binder);

//        // nhận thông báo yêu cầu TargetToMiddle từ bên ngoài
//        CoreEvents.TargetObject.Subscribe(e =>
//        {
//            if (e.TypeOfTarget == TargetObjectEvent.TypeTarget.Data_To_UI)
//            {
//                var taget = e.CosmicObjectSO;
//                string name = taget._name;
//                TargetGroupOnObjectToMiddle(name);
//            }
//        }, Binder);

//    }

//    #region Drag event

//    /// <summary>
//    /// Receive drag events from CoreEvents.Drag (DragInputEvent).
//    /// </summary>
//    /// <remarks>
//    /// - Start: reset inertia + init accum
//    /// - Move : apply drag (Normal/Inertia), FixPoint only accumulate
//    /// - End  : FixPoint decide step, Normal stop, Inertia keep velocity
//    /// </remarks>
//    private void OnDragEvent(DragInputEvent e)
//    {
//        if (e == null) return;
//        if (_pathCache == null || _pathCache.positions == null || _pathCache.positions.Count < 2) return;

//        switch (e.phase)
//        {
//            case DragPhase.Start:
//                HandleEvent_Start(e);
//                break;

//            case DragPhase.Move:
//                HandleEvent_Move(e);
//                break;

//            case DragPhase.End:
//                HandleEvent_End(e);
//                break;
//        }
//    }

//    /// <summary>
//    /// Start phase.
//    /// </summary>
//    private void HandleEvent_Start(DragInputEvent e)
//    {
//        // FixPoint cooldown gating
//        if (_dragMode == DragBehaviorMode.FixPoint && _fixPointCooldown && _isFixPointCooldown)
//        {
//            _dragActive = false;
//            return;
//        }

//        _dragActive = true;

//        // reset accum
//        _totalDragAxis = 0f;

//        // reset inertia
//        _velocityS = 0f;
//        _velocityIndex = 0f;
//    }

//    /// <summary>
//    /// Move phase.
//    /// </summary>
//    private void HandleEvent_Move(DragInputEvent e)
//    {
//        if (!_dragActive) return;

//        // dùng deltaScreen do DragInput publish
//        float drag = GetDragAxis(e.deltaScreen);
//        _totalDragAxis += drag;

//        // FixPoint: không apply khi kéo
//        if (_dragMode == DragBehaviorMode.FixPoint)
//            return;

//        ApplyDragAxis(drag);
//    }

//    /// <summary>
//    /// End phase.
//    /// </summary>
//    private void HandleEvent_End(DragInputEvent e)
//    {
//        if (!_dragActive) return;
//        _dragActive = false;

//        // FixPoint: quyết định step theo tổng swipe
//        if (_dragMode == DragBehaviorMode.FixPoint)
//        {
//            if (_fixPointCooldown && _isFixPointCooldown)
//                return;

//            if (Mathf.Abs(_totalDragAxis) >= _fixPointSwipeThreshold)
//            {
//                int step = _totalDragAxis > 0 ? 1 : -1;
//                if (_invertSwipe) step *= -1;

//                ApplyFixPointStep(step);

//                if (_fixPointCooldown)
//                {
//                    _isFixPointCooldown = true;

//                    // Mode 1: cooldown by time
//                    if (_fixPointCooldownMode == FixPointCooldownMode.Time)
//                        _fixPointCooldownTimer = Mathf.Max(0.0001f, _fixPointCooldownTime);
//                    // Mode 2: UntilReachedTarget -> timer không cần, sẽ release khi anim xong
//                }
//            }

//            _velocityS = 0f;
//            _velocityIndex = 0f;
//            return;
//        }

//        // Normal: stop immediately
//        if (_dragMode == DragBehaviorMode.Normal)
//        {
//            _velocityS = 0f;
//            _velocityIndex = 0f;
//        }
//    }

//    private float GetDragAxis(Vector2 screenDelta)
//    {
//        float dx = screenDelta.x;
//        float dy = screenDelta.y;

//        if (_useOnlyHorizontal) return dx;

//        // chọn trục dominant 
//        return Mathf.Abs(dx) >= Mathf.Abs(dy) ? dx : dy;
//    }

//    private void ApplyDragAxis(float dragPixel)
//    {
//        float dir = _invertSwipe ? -1f : 1f;
//        float denom = Mathf.Max(1f, _pixelsForFullPath);

//        if (_layoutMode == ObjectLayoutMode.SpacingOnPath)
//        {
//            float ds = (dragPixel / denom) * _pathCache.totalLength * _speed * dir;
//            _baseS += ds;

//            // cache vận tốc để dùng cho inertia khi thả chuột
//            _velocityS = ds / Mathf.Max(0.0001f, Time.deltaTime);

//            ClampBaseS_MiddleLock_Spacing();
//            ApplyObjectsWithQueue_Spacing();
//        }
//        else // OneObjectPerPoint
//        {
//            if (_points == null) return;
//            int pointCount = _points.Count;
//            if (pointCount < 2) return;

//            float deltaIdx = (dragPixel / denom) * (pointCount - 1) * _speed * dir;
//            _shiftFloat += deltaIdx;

//            // cache vận tốc inertia
//            _velocityIndex = deltaIdx / Mathf.Max(0.0001f, Time.deltaTime);

//            ClampShiftFloat_OnePerPoint();
//            ApplyObjectsWithQueue_OnePerPoint();
//        }
//    }
//    #endregion

//    #endregion

//    #region profession

//    #endregion

//    #region API

//    /// <summary>
//    /// Thử lấy object đang ở Middle.
//    /// </summary>
//    /// <param name="obj">Object ở Middle.</param>
//    /// <returns>True nếu tìm được.</returns>
//    public bool TryGetCurrentMiddleObject(out Transform obj)
//    {
//        obj = _currentMiddleObject;
//        return obj != null;
//    }

//    /// <summary>
//    /// Lấy object hiện đang được coi là ở Middle (có thể null).
//    /// </summary>
//    /// <returns>Transform của object đang ở Middle.</returns>
//    public Transform GetCurrentMiddleObject()
//    {
//        return _currentMiddleObject;
//    }

//    /// <summary>
//    /// Đưa object có tên <paramref name="objectName"/> vào Middle point.
//    /// </summary>
//    /// <param name="objectName"> name của object cần đưa vào Middle </param>
//    /// <param name="smoothIfFixPoint"> nếu đang FixPoint thì animate mượt về target </param>
//    /// <returns></returns>
//    public bool TargetGroupOnObjectToMiddle(string objectName, bool smoothIfFixPoint = true)
//    {
//        string name = objectName + "_Instance";
//        //Debug.LogWarning($"Name CosmicObject: {name}");
//        return CenterGroupOnObjectName(name, smoothIfFixPoint);
//    }

//    #endregion

//    #region  Helper API

//    /// <summary>
//    /// Update trạng thái object ở Middle: chỉ fire 1 lần khi "vừa chạm Middle"
//    /// và reset khi rời Middle.
//    /// </summary>
//    private void TickMiddleDetectAndLog()
//    {
//        if (!_logWhenReachMiddle /*&& OnReachMiddle == null*/) return;

//        if (!TryFindNearestObjectToMiddle(out Transform candidate, out int _))
//            return;

//        Vector3 midPos = GetMiddleWorldPosition();
//        float dist = Vector3.Distance(candidate.position, midPos);

//        bool reached = dist <= _reachMiddlePosEps;

//        // Update current middle object luôn (để API GetCurrentMiddleObject đúng)
//        if (candidate != _currentMiddleObject)
//            _currentMiddleObject = candidate;

//        // Edge trigger: chỉ fire lúc transitioned false -> true
//        if (reached && !_wasReachedMiddle)
//        {
//            _wasReachedMiddle = true;

//            if (_logWhenReachMiddle)
//                Debug.Log($"[ViewObjectOnPath] Reach Middle: {_currentMiddleObject.name}");

//            // thông báo Data cho SessionMamager
//            string name = GetCleanObjectName(_currentMiddleObject.name);
//            CoreEvents.TargetObject.Raise(new TargetObjectEvent(
//                TargetObjectEvent.TypeTarget.UI_To_Data,
//                name));
//        }
//        else if (!reached && _wasReachedMiddle)
//        {
//            // Rời Middle => cho phép lần sau fire lại
//            _wasReachedMiddle = false;
//        }
//    }

//    /// <summary>
//    /// Trả về tên object đã loại bỏ hậu tố "_Instance" nếu có.
//    /// </summary>
//    private string GetCleanObjectName(string rawName)
//    {
//        const string suffix = "_Instance";

//        if (rawName.EndsWith(suffix))
//            return rawName.Substring(0, rawName.Length - suffix.Length);

//        return rawName;
//    }

//    /// <summary>
//    /// Tính vị trí world của Middle trên path.
//    /// </summary>
//    /// <returns>World position của Middle.</returns>
//    private Vector3 GetMiddleWorldPosition()
//    {
//        // Ưu tiên path cache (Spacing), nhưng dùng chung vẫn OK
//        // Nếu muốn đúng tuyệt đối theo baker: EvaluatePosition(_midS)
//        return EvaluatePosition(_midS);
//    }

//    /// <summary>
//    /// Tìm object gần Middle nhất (trong các object đang active / không bị queue).
//    /// </summary>
//    /// <param name="obj">Object gần Middle nhất.</param>
//    /// <param name="objIndex">Index của object trong _objects.</param>
//    /// <returns>True nếu tìm được.</returns>
//    private bool TryFindNearestObjectToMiddle(out Transform obj, out int objIndex)
//    {
//        obj = null;
//        objIndex = -1;

//        if (_objects == null || _objects.Count == 0) return false;
//        if (_queueRoot == null) return false;

//        Vector3 midPos = GetMiddleWorldPosition();
//        float best = float.MaxValue;

//        for (int i = 0; i < _objects.Count; i++)
//        {
//            Transform t = _objects[i];
//            if (t == null) continue;

//            // Đang ở queue / inactive thì bỏ qua
//            if (t.parent == _queueRoot) continue;
//            if (!t.gameObject.activeInHierarchy) continue;

//            float d = (t.position - midPos).sqrMagnitude;
//            if (d < best)
//            {
//                best = d;
//                obj = t;
//                objIndex = i;
//            }
//        }

//        return obj != null;
//    }

//    /// <summary>
//    /// Kéo cả group sao cho object có tên <paramref name="objectName"/> nằm tại Middle point.
//    /// </summary>
//    /// <remarks>
//    /// - Hỗ trợ cả 2 layout: OneObjectPerPoint và SpacingOnPath.
//    /// - Nếu đang ở DragBehaviorMode.FixPoint và <paramref name="smoothIfFixPoint"/> = true:
//    ///   sẽ dùng cơ chế target + smooth animation sẵn có (_targetBaseS/_targetShiftFloat).
//    /// - Nếu object đang bị queue (ẩn), API vẫn có thể đưa nó về Middle (tùy clamp/range).
//    /// </remarks>
//    /// <param name="objectName">Tên Transform.name của object cần đưa vào Middle.</param>
//    /// <param name="smoothIfFixPoint">Nếu đang FixPoint thì animate mượt về target.</param>
//    /// <returns>True nếu tìm thấy object và set được vị trí, ngược lại false.</returns>
//    private bool CenterGroupOnObjectName(string objectName, bool smoothIfFixPoint = true)
//    {
//        if (string.IsNullOrEmpty(objectName)) return false;
//        if (_objects == null || _objects.Count == 0) return false;
//        if (_pathCache == null || _pathCache.positions == null || _pathCache.positions.Count < 2) return false;

//        // 1) Find object index by name
//        int objIndex = -1;
//        for (int i = 0; i < _objects.Count; i++)
//        {
//            var t = _objects[i];
//            if (t == null) continue;

//            // match by exact name (có thể đổi sang Equals(..., OrdinalIgnoreCase) nếu cần)
//            if (t.name == objectName)
//            {
//                objIndex = i;
//                break;
//            }
//        }
//        if (objIndex < 0) return false;

//        // 2) Tính target theo layout
//        if (_layoutMode == ObjectLayoutMode.SpacingOnPath)
//        {
//            // đảm bảo offsets đã build
//            if (_offsetS == null || _offsetS.Count != _objects.Count)
//                BuildOffsets_Spacing();

//            if (_offsetS == null || _offsetS.Count != _objects.Count)
//                return false;

//            float targetBaseS = _midS - _offsetS[objIndex];

//            // clamp theo lock Middle + range
//            float backup = _baseS;
//            _baseS = targetBaseS;
//            ClampBaseS_MiddleLock_Spacing();
//            targetBaseS = _baseS;
//            _baseS = backup;

//            if (_dragMode == DragBehaviorMode.FixPoint && smoothIfFixPoint)
//            {
//                _targetBaseS = targetBaseS;
//                _smoothVelS = 0f;
//                _isFixPointAnimating = true;
//            }
//            else
//            {
//                _baseS = targetBaseS;
//                ClampBaseS_MiddleLock_Spacing();
//                ApplyObjectsWithQueue_Spacing();
//            }

//            return true;
//        }
//        else // OneObjectPerPoint
//        {
//            int objCount = _objects.Count;
//            int last = objCount - 1;

//            float targetShift;

//            // công thức map từ objIndex -> shiftFloat để objIndex nằm tại middle index
//            if (_snapMode == SnapGroupMode.SnapGroupToFirst)
//            {
//                // idx(objIndex) = shift - objIndex == midIndex  => shift = midIndex + objIndex
//                targetShift = _midIndex + objIndex;
//            }
//            else if (_snapMode == SnapGroupMode.SnapGroupToLast)
//            {
//                // idx(objIndex) = shift + (last - objIndex) == midIndex => shift = midIndex - (last - objIndex)
//                targetShift = _midIndex - (last - objIndex);
//            }
//            else // SnapGroupToMiddle
//            {
//                // idx(objIndex) = shift + (objIndex - midObj) == midIndex => shift = midIndex - (objIndex - midObj)
//                targetShift = _midIndex - (objIndex - _midObjIndexCache);
//            }

//            // clamp theo lock Middle + range points
//            float backup = _shiftFloat;
//            _shiftFloat = targetShift;
//            ClampShiftFloat_OnePerPoint();
//            targetShift = _shiftFloat;
//            _shiftFloat = backup;

//            if (_dragMode == DragBehaviorMode.FixPoint && smoothIfFixPoint)
//            {
//                _targetShiftFloat = targetShift;
//                _smoothVelShift = 0f;
//                _isFixPointAnimating = true;
//            }
//            else
//            {
//                _shiftFloat = targetShift;
//                ClampShiftFloat_OnePerPoint();
//                ApplyObjectsWithQueue_OnePerPoint();
//            }

//            return true;
//        }
//    }


//    #endregion

//    #region Start
//    private void FindAndGetChildGameObject(Transform root, List<Transform> List)
//    {
//        for (int i = 0; i < root.childCount; i++)
//        {
//            Transform child = root.GetChild(i);
//            List.Add(child);
//        }
//    }

//    /// <summary>
//    /// Lưu tranform.parent ban đầu của các Object trong _object. 
//    /// dùng để reset về trạng thái ban đầu,
//    /// so sánh trước và sau khi thay đổi tranform,
//    /// tránh mất reference parent gốc khi re_Parent nhiều lần
//    /// </summary>
//    /// <remarks>
//    /// CẦN CẢI TIẾN VÀ ĐƯA HÀM NÀY VÀO UBILITY
//    /// </remarks>
//    private void CacheOriginalParents()
//    {
//        _originalParent.Clear(); // đảm bảo cache luôn đồng bộ với _object

//        if (_objects == null) return;

//        for (int i = 0; i < _objects.Count; i++)
//        {
//            Transform t = _objects[i]; // cache local reference
//            if (t == null) continue;

//            if (!_originalParent.ContainsKey(t)) // tránh add trùng key
//                _originalParent.Add(t, t.parent);
//        }
//    }

//    /// <summary>
//    /// Build cache từ PointBaker và đồng bộ danh sách _points để dễ theo dõi trong Inspector.
//    /// </summary>
//    /// <remarks>
//    /// _points chỉ là debug view, không phải nguồn dữ liệu chính.
//    /// </remarks>
//    private void BuildPathCache()
//    {
//        if (_pointBaker == null || _pointBaker.listPoint == null)
//        {
//            Debug.LogError("[DragObjcetOnRoad] Missing PointBaker or listPoint.");
//            return;
//        }

//        if (_pathCache == null)
//            _pathCache = new ListPoint.PathCache();

//        bool ok = _pointBaker.listPoint.BuildPathCache(_pathCache, includeInactive: true);
//        if (!ok)
//        {
//            Debug.LogError("[DragObjcetOnRoad] BuildPathCache failed. Check PointBaker Bake data.");
//            return;
//        }

//        _points.Clear();
//        var pd = _pointBaker.listPoint.pointData;
//        if (pd != null)
//        {
//            for (int i = 0; i < pd.Length; i++)
//            {
//                if (pd[i].point != null)
//                    _points.Add(pd[i].point.transform);
//            }
//        }
//    }
//    private Vector3 EvaluatePosition(float s)
//    {
//        if (_pointBaker == null || _pointBaker.listPoint == null || _pathCache == null)
//            return Vector3.zero;

//        _pointBaker.listPoint.EvaluateByDistance(_pathCache, s, out Vector3 pos);
//        return pos;
//    }

//    /// <summary>
//    /// Cache middle index + middle distance (S) trên path.
//    /// </summary>
//    /// <remarks>
//    /// Ưu tiên:
//    /// 1) _middlePoint (nếu set)
//    /// 2) PointBaker middle index
//    /// 3) fallback _points.Count/2 (debug view)
//    /// </remarks>
//    private void CacheMiddleSAndIndex()
//    {
//        // 1) Xác định mid Transform (nếu có)
//        Transform mid = _middlePoint;

//        // 2) Nếu không có mid transform => chọn theo index (baker -> points)
//        if (mid == null)
//        {
//            int candidateIndex =
//                (_pointBaker != null) ? _pointBaker.listPoint.GetMiddlePointIndex()
//                                      : ((_points != null && _points.Count > 0) ? (_points.Count / 2) : 0);

//            // Nếu _points là debug view đã sync từ baker thì dùng được.
//            if (_points != null && _points.Count > 0)
//            {
//                int clamped = Mathf.Clamp(candidateIndex, 0, _points.Count - 1);
//                mid = _points[clamped];
//            }
//        }

//        // 3) Tính _midIndex (ưu tiên reference -> nearest)
//        if (mid != null)
//            _midIndex = FindNearestPointIndex(mid);
//        else
//            _midIndex = (_points != null && _points.Count > 0) ? Mathf.Clamp(_points.Count / 2, 0, _points.Count - 1) : 0;

//        // 4) Tính _midS theo cache (nếu có)
//        if (_pathCache != null && _pathCache.cumulativeLengths != null && _pathCache.cumulativeLengths.Count > 0)
//            _midS = _pathCache.cumulativeLengths[Mathf.Clamp(_midIndex, 0, _pathCache.cumulativeLengths.Count - 1)];
//        else
//            _midS = 0f;
//    }

//    /// <summary>
//    /// helper tìm index of target in list point
//    /// </summary>
//    /// <param name="target"></param>
//    /// <remarks>
//    /// NEED UBITITY
//    /// </remarks>
//    /// <returns>
//    /// index (int)
//    /// </returns>
//    private int FindNearestPointIndex(Transform target)
//    {
//        #region guard clause
//        if (_points == null || _points.Count == 0 || target == null) return 0;
//        #endregion

//        // if target is element in list _point -> return index (match by reference)
//        for (int i = 0; i < _points.Count; i++)
//        {
//            if (_points[i] == target) return i;
//        }

//        // if not element in list _point -> find nearest point by location
//        int best = -1;                  // haven't found anything
//        float bestD = float.MaxValue;
//        Vector3 p = target.position;

//        for (int i = 0; i < _points.Count; i++)
//        {
//            Transform pi = _points[i];
//            if (pi == null) continue;

//            float d = (pi.position - p).sqrMagnitude; // use sqrMagnitude because faster Distance
//            if (d < bestD)
//            {
//                bestD = d;
//                best = i;
//            }
//        }

//        // if list is all null -> fallback 0
//        return best >= 0 ? best : 0;
//    }

//    /// <summary>
//    /// Snap group _object to path by 2 layout
//    /// </summary>
//    private void SnapGroup()
//    {
//        #region guard clause
//        if (_objects == null || _objects.Count == 0) return;
//        if (_pathCache == null || _pathCache.positions == null || _pathCache.positions.Count < 2) return;

//        #endregion

//        // SpacingOnPath
//        if (_layoutMode == ObjectLayoutMode.SpacingOnPath)
//        {
//            BuildOffsets_Spacing(); // build _offsetS + min/max

//            // 1) xác định anchor baseS theo snap mode
//            switch (_snapMode)
//            {
//                case SnapGroupMode.SnapGroupToMiddle:
//                    {
//                        // // baseS là S của anchor object[midObj]
//                        _baseS = _midS - _offsetS[_midObjIndexCache]; // offset[midObj] = 0
//                        break;
//                    }

//                case SnapGroupMode.SnapGroupToFirst:
//                    // object[0] nằm tại middle
//                    _baseS = _midS;
//                    break;

//                case SnapGroupMode.SnapGroupToLast:
//                    _baseS = _midS; // object[last] ở Middle vì offsetS[last] = 0
//                    break;
//            }

//            ClampBaseS_MiddleLock_Spacing();
//            ApplyObjectsWithQueue_Spacing();
//            return;
//        }

//        // OneObjectPerPoint
//        if (_layoutMode == ObjectLayoutMode.OneObjectPerPoint)
//        {
//            switch (_snapMode)
//            {
//                case SnapGroupMode.SnapGroupToMiddle:
//                    {
//                        int midObj = (_objects.Count - 1) / 2;
//                        _shiftFloat = _midIndex; // // neo: object[midObj] đứng tại point middle
//                        _midObjIndexCache = midObj; // // (tạo thêm biến int để Apply dùng)
//                        break;
//                    }

//                case SnapGroupMode.SnapGroupToFirst:
//                    // object[0] tại middle index
//                    _shiftFloat = _midIndex;
//                    break;

//                case SnapGroupMode.SnapGroupToLast:
//                    _shiftFloat = _midIndex; // // anchor = object[last] ở Middle
//                    break;

//            }

//            ClampShiftFloat_OnePerPoint();
//            ApplyObjectsWithQueue_OnePerPoint();
//        }
//    }

//    /// <summary>
//    /// helper calculator list offset for each object vs anchor
//    /// </summary>
//    private void BuildOffsets_Spacing()
//    {
//        _offsetS.Clear();

//        int n = _objects.Count;
//        if (n <= 0) return;

//        _minOffsetS = float.MaxValue;
//        _maxOffsetS = float.MinValue;

//        switch (_snapMode)
//        {
//            // Center alignment
//            case SnapGroupMode.SnapGroupToMiddle:
//                {
//                    int midObj = (n - 1) / 2;
//                    _midObjIndexCache = midObj; // // dùng chung cho OnePerPoint nếu muốn

//                    for (int i = 0; i < n; i++)
//                    {
//                        float off = (i - midObj) * _spacingOnPath; // // tỏa 2 bên
//                        _offsetS.Add(off);

//                        if (off < _minOffsetS) _minOffsetS = off;
//                        if (off > _maxOffsetS) _maxOffsetS = off;
//                    }
//                    break;
//                }


//            // object[0] là anchor
//            case SnapGroupMode.SnapGroupToFirst:
//                {
//                    for (int i = 0; i < n; i++)
//                    {
//                        float off = -i * _spacingOnPath;
//                        _offsetS.Add(off);

//                        if (off < _minOffsetS) _minOffsetS = off;
//                        if (off > _maxOffsetS) _maxOffsetS = off;
//                    }
//                    break;
//                }

//            // object[object.Count - 1] là anchor
//            case SnapGroupMode.SnapGroupToLast:
//                {
//                    int last = n - 1;

//                    for (int i = 0; i < n; i++)
//                    {
//                        float off = (last - i) * _spacingOnPath; // // anchor = object[last] (offset=0), các object khác tiến về end
//                        _offsetS.Add(off);

//                        if (off < _minOffsetS) _minOffsetS = off;
//                        if (off > _maxOffsetS) _maxOffsetS = off;
//                    }
//                    break;
//                }


//        }
//    }

//    #endregion

//    #region Clamp & Apply

//    /// <summary>
//    /// Fix drift chỉ dành cho DragBehaviorMode.FixPoint:
//    /// Nếu object gần Middle bị lệch quá ngưỡng, đặt target (_targetBaseS/_targetShiftFloat)
//    /// và dùng chính FixPoint smooth animation để kéo về.
//    /// </summary>
//    private void AutoFixMiddleDrift_FixPoint()
//    {
//        if (!_autoFixMiddleDrift) return;
//        if (_dragMode != DragBehaviorMode.FixPoint) return;
//        if (_dragActive) return;

//        // Nếu đang anim FixPoint rồi thì để anim chạy tới nơi (tránh giật target liên tục)
//        if (_isFixPointAnimating) return;

//        if (_pathCache == null || _pathCache.positions == null || _pathCache.positions.Count < 2) return;
//        if (!TryFindNearestObjectToMiddle(out Transform obj, out int objIndex)) return;

//        float drift = Vector3.Distance(obj.position, GetMiddleWorldPosition());

//        // chỉ trigger khi drift đủ lớn
//        if (drift < _autoFixStartEps) return;

//        if (_layoutMode == ObjectLayoutMode.OneObjectPerPoint)
//        {
//            int objCount = _objects.Count;
//            int last = objCount - 1;

//            float targetShift;
//            if (_snapMode == SnapGroupMode.SnapGroupToFirst)
//                targetShift = _midIndex + objIndex;
//            else if (_snapMode == SnapGroupMode.SnapGroupToLast)
//                targetShift = _midIndex - (last - objIndex);
//            else
//                targetShift = _midIndex - (objIndex - _midObjIndexCache);

//            // clamp target bằng clamp hiện có
//            float backup = _shiftFloat;
//            _shiftFloat = targetShift;
//            ClampShiftFloat_OnePerPoint();
//            targetShift = _shiftFloat;
//            _shiftFloat = backup;

//            _targetShiftFloat = targetShift;

//            // reset velocity của FixPoint animation
//            _smoothVelShift = 0f;
//            _isFixPointAnimating = true;

//            // nếu cooldown mode = UntilReachedTarget và đang cooldown (do trước đó), cho phép anim “sửa drift”
//            // (tuỳ ý) _isFixPointCooldown = true/false giữ nguyên theo design của Ní
//        }
//        else // SpacingOnPath
//        {
//            if (_offsetS == null || _offsetS.Count != _objects.Count) return;

//            float targetBaseS = _midS - _offsetS[objIndex];

//            // clamp target bằng clamp hiện có
//            float backup = _baseS;
//            _baseS = targetBaseS;
//            ClampBaseS_MiddleLock_Spacing();
//            targetBaseS = _baseS;
//            _baseS = backup;

//            _targetBaseS = targetBaseS;

//            _smoothVelS = 0f;
//            _isFixPointAnimating = true;
//        }
//    }

//    private void ClampBaseS_MiddleLock_Spacing()
//    {
//        #region guard clause
//        if (_objects == null || _objects.Count == 0) return;
//        if (_offsetS == null || _offsetS.Count != _objects.Count) return;
//        #endregion

//        // SnapGroupToFirst: offsets = [0, -sp, -2sp ...]
//        // -> object last have offset MIN (largest nagitive)
//        if (_snapMode == SnapGroupMode.SnapGroupToFirst)
//        {
//            float min = _midS;                // // khóa: object[0] không được đi về start qua Middle
//            float max = _midS - _minOffsetS;  // // cho phép kéo ngược tới khi object cuối chạm Middle
//                                              // // vì: S_last = baseS + minOffset = midS => baseS = midS - minOffset
//            _baseS = Mathf.Clamp(_baseS, min, max);
//            return;
//        }

//        // SnapGroupToLast: offsets = [0, +sp, +2sp ...]
//        // -> object cuối có offset MAX (largest positive)
//        if (_snapMode == SnapGroupMode.SnapGroupToLast)
//        {
//            float min = _midS - _maxOffsetS;  // // cho phép kéo tới khi object cuối chạm Middle (đối xứng)
//            float max = _midS;                // // khóa: object[0] không được vượt Middle về end
//            _baseS = Mathf.Clamp(_baseS, min, max);
//            return;
//        }

//        // SnapGroupToMiddle: “end stop at middle” (2 direction)
//        if (_snapMode == SnapGroupMode.SnapGroupToMiddle)
//        {
//            int last = _objects.Count - 1;

//            // offset[0] thường âm, offset[last] thường dương
//            float off0 = _offsetS[0];
//            float offLast = _offsetS[last];

//            // // Khi kéo về end: object[0] sẽ tiến về Middle -> khóa tại Middle
//            float max = _midS - off0;

//            // // Khi kéo về start: object[last] sẽ tiến về Middle -> khóa tại Middle
//            float min = _midS - offLast;

//            // // đảm bảo min <= max (phòng spacing âm / list lạ)
//            if (min > max)
//            {
//                float tmp = min; min = max; max = tmp;
//            }

//            _baseS = Mathf.Clamp(_baseS, min, max);
//            return;
//        }
//    }

//    //OneObjectPerPoint: khóa theo Middle tùy SnapMode
//    // - ToFirst: khi object[0] ở Middle thì KHÔNG cho kéo về hướng point[0]
//    // - ToLast : đối xứng
//    private void ClampShiftFloat_OnePerPoint()
//    {
//        if (_points == null || _points.Count == 0)
//        {
//            _shiftFloat = 0f;
//            return;
//        }

//        int pointCount = _points.Count;
//        int objCount = (_objects != null) ? _objects.Count : 0;

//        if (objCount <= 0)
//        {
//            _shiftFloat = Mathf.Clamp(_shiftFloat, 0f, pointCount - 1);
//            return;
//        }

//        // SnapGroupToMiddle: “end stop at middle” (2 direction)
//        if (_snapMode == SnapGroupMode.SnapGroupToMiddle)
//        {
//            int lastObj = objCount - 1;
//            int midObj = _midObjIndexCache;

//            float min = _midIndex - (lastObj - midObj); // lock when object[last] reached Middle (drag to start)
//            float max = _midIndex + midObj;             // lock when object[0] touch reached (drag to end)

//            // simutaneously not allow out of range point
//            float hardMin = 0f;
//            float hardMax = pointCount - 1;

//            _shiftFloat = Mathf.Clamp(_shiftFloat, Mathf.Max(min, hardMin), Mathf.Min(max, hardMax));
//            return;
//        }

//        // SnapGroupToFirst: _shiftFloat is index of object[0]
//        if (_snapMode == SnapGroupMode.SnapGroupToFirst)
//        {
//            float min = _midIndex;                                // object[0] không được đi "qua trước" Middle
//            float max = _midIndex + Mathf.Max(0, objCount - 1);    // kéo tối đa để object cuối cùng chạm Middle
//            _shiftFloat = Mathf.Clamp(_shiftFloat, min, max);
//            return;
//        }

//        // SnapGroupToLast: 
//        if (_snapMode == SnapGroupMode.SnapGroupToLast)
//        {
//            float min = _midIndex - Mathf.Max(0, objCount - 1); // drag until object[0] reached Middle
//            float max = _midIndex;                              // object[last] not exceed Middle to end
//            _shiftFloat = Mathf.Clamp(_shiftFloat, min, max);
//            return;
//        }

//        // SnapGroupToMiddle: anchor trong range points
//        _shiftFloat = Mathf.Clamp(_shiftFloat, 0f, pointCount - 1);
//    }

//    private void ApplyObjectsWithQueue_Spacing()
//    {
//        if (_objects == null || _objects.Count == 0) return;
//        if (_offsetS == null || _offsetS.Count != _objects.Count) return;

//        for (int i = 0; i < _objects.Count; i++)
//        {
//            Transform t = _objects[i];
//            if (t == null) continue;

//            float s = _baseS + _offsetS[i];

//            // 1) Nếu vượt biên nhiều -> queue
//            float totalLen = _pathCache.totalLength;

//            if (s < -EDGE_EPS || s > totalLen + EDGE_EPS)
//            {
//                SendToQueue(t); // object thật sự out
//                continue;
//            }

//            // 2) Nếu chỉ vượt nhẹ do float -> clamp vào biên, KHÔNG queue
//            s = Mathf.Clamp(s, 0f, _pathCache.totalLength);


//            RestoreFromQueue(t);

//            Vector3 p = EvaluatePosition(s);
//            t.position = p + _positionOffset;

//            //t.position = p;
//        }
//    }

//    /// <summary>
//    /// control all object with queue. (one per point mode) 
//    /// </summary>
//    private void ApplyObjectsWithQueue_OnePerPoint()
//    {
//        #region guard clause
//        if (_points == null || _points.Count < 2) return;
//        if (_objects == null || _objects.Count == 0) return;
//        #endregion

//        #region variable
//        int pointCount = _points.Count;
//        int objCount = _objects.Count;
//        float maxIdx = pointCount - 1;
//        #endregion

//        for (int i = 0; i < objCount; i++)
//        {
//            Transform obj = _objects[i];
//            if (obj == null) continue;

//            float idx;

//            if (_snapMode == SnapGroupMode.SnapGroupToFirst)
//            {
//                // object[0] where Middle, i increase to start
//                idx = _shiftFloat - i;
//            }
//            else if (_snapMode == SnapGroupMode.SnapGroupToLast)
//            {
//                // anchor = object[last] where Middle
//                int last = objCount - 1;
//                idx = _shiftFloat + (last - i); // i=last => idx=shift, i=0 => idx=shift+last ( move towards the end)
//            }
//            else // SnapGroupToMiddle
//            {
//                // object[midObj] where middle point, radiate 2 sides
//                idx = _shiftFloat + (i - _midObjIndexCache);
//            }

//            // out-of-range -> queue
//            if (idx < -EDGE_EPS || idx > maxIdx + EDGE_EPS)
//            {
//                SendToQueue(obj);
//                continue;
//            }

//            // rung biên nhẹ -> clamp, không queue
//            idx = Mathf.Clamp(idx, 0f, maxIdx);

//            RestoreFromQueue(obj);

//            int lo = Mathf.FloorToInt(idx);
//            int hi = Mathf.Min(lo + 1, pointCount - 1);
//            float t = idx - lo;

//            Transform p0 = _points[lo];
//            Transform p1 = _points[hi];

//            // if point null -> put in object to queue
//            if (p0 == null || p1 == null)
//            {
//                SendToQueue(obj);
//                continue;
//            }

//            Vector3 p = Vector3.Lerp(p0.position, p1.position, t);

//            obj.position = p + _positionOffset;

//            obj.position = p;
//        }
//    }

//    private void ApplyFixPointStep(int step)
//    {
//        // step = +1 hoặc -1 (1 point liền kề theo hướng vuốt)

//        if (_layoutMode == ObjectLayoutMode.OneObjectPerPoint)
//        {
//            // target là integer step (point kế tiếp)
//            _targetShiftFloat = _shiftFloat + step;

//            // clamp theo lock Middle + range
//            float backup = _shiftFloat;
//            _shiftFloat = _targetShiftFloat;
//            ClampShiftFloat_OnePerPoint();
//            _targetShiftFloat = _shiftFloat;
//            _shiftFloat = backup;

//            _isFixPointAnimating = true;
//            return;
//        }

//        // SpacingOnPath
//        _targetBaseS = _baseS + step * Mathf.Max(0.0001f, _spacingOnPath);

//        // clamp theo lock Middle + range
//        float backupS = _baseS;
//        _baseS = _targetBaseS;
//        ClampBaseS_MiddleLock_Spacing();
//        _targetBaseS = _baseS;
//        _baseS = backupS;

//        _isFixPointAnimating = true;
//    }

//    #endregion

//    #region Queue

//    /// <summary>
//    /// helper Đảm bảo Queue tồn tại
//    /// </summary>
//    private void EnsureQueueRoot()
//    {
//        if (_queueRoot != null) return;

//        // auto tạo queue nếu chưa set (đỡ quên kéo assign)
//        GameObject obj = new GameObject("queue");
//        obj.transform.SetParent(transform, false);
//        _queueRoot = obj.transform;
//    }

//    /// <summary>
//    /// send object to queue
//    /// </summary>
//    /// <param name="obj"></param>
//    private void SendToQueue(Transform obj)
//    {
//        if (obj == null || _queueRoot == null) return;

//        // nếu đã ở queue + đã inactive thì thôi (tránh gọi lặp)
//        if (obj.parent == _queueRoot && !obj.gameObject.activeSelf) return;

//        if (obj.parent != _queueRoot)
//            obj.SetParent(_queueRoot, false); // worldPositionStays không quan trọng vì reset local

//        // reset local để queue gọn
//        obj.localPosition = Vector3.zero;
//        obj.localRotation = Quaternion.identity;
//        // obj.localScale = Vector3.one; // bật nếu muốn chuẩn hóa scale

//        if (obj.gameObject.activeSelf)
//            obj.gameObject.SetActive(false);
//    }

//    /// <summary>
//    /// restone object to _originalParent
//    /// </summary>
//    /// <param name="obj"></param>
//    private void RestoreFromQueue(Transform obj)
//    {
//        if (obj == null) return;

//        // chỉ restore parent nếu đang ở queue (giảm SetParent dư)
//        if (_queueRoot != null && obj.parent == _queueRoot)
//        {
//            if (_originalParent.TryGetValue(obj, out Transform parent) && parent != null)
//            {
//                obj.SetParent(parent, true); // giữ world, nhưng Apply sẽ set position lại
//            }
//        }

//        if (!obj.gameObject.activeSelf)
//            obj.gameObject.SetActive(true);
//    }

//    #endregion

//}