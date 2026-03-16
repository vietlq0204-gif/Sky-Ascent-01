using System;
using System.Collections.Generic;
using UnityEngine;

public interface IPathView
{
    void Initialize();
    void Apply(PathMoveData data);
    void Tick(float deltaTime);
    void ResetMotion();

    float GetPathLength();
    float GetBaseS();
    float GetOffsetAt(int index);
    float GetCurrentPosition();

    int FindIndexByName(string name);
    float GetStepSize();

    bool IsDriftFixing();

    bool CenterOnObjectToMiddle(string name);

    /// <summary>
    /// Chỉ yêu cầu kiểm tra & sửa drift sau khi controller đã Snap xong FixPoint.
    /// </summary>
    void RequestDriftCheckAfterSnap();

    /// <summary>
    /// Snap group sao cho object gần middle nhất về đúng middle.
    /// </summary>
    /// <remarks>
    /// - Dùng cho Normal/Inertia khi dừng.
    /// </remarks>
    void SnapNearestToMiddle(float smoothTime, float stopEps);
}

public struct PathMoveData
{
    public float Delta;
    public bool NotifyMiddle;
}

public enum ObjectLayoutMode { OneObjectPerPoint, SpacingOnPath }
public enum SnapGroupMode { SnapGroupToFirst, SnapGroupToLast, SnapGroupToMiddle }
public enum FixPointCooldownMode { UntilReachedTarget, Time }

/// <summary>
/// View/Model for objects on a path:
/// - Cache path
/// - Build offsets
/// - Apply delta (baseS/shiftFloat)
/// - Detect middle + drift fix
/// - Center group by object name
/// </summary>
public sealed class ViewObjectOnPath : MonoBehaviour, IPathView
{
    #region Inspector
    [SerializeField] private ObjectLayoutMode _layoutMode = ObjectLayoutMode.SpacingOnPath;
    [SerializeField] private SnapGroupMode _snapMode = SnapGroupMode.SnapGroupToMiddle;
    [SerializeField] private PointBaker _pointBaker;
    [SerializeField] private Transform _objectRoot;
    [SerializeField] private QueueManager _queueManager;

    [SerializeField] private float _spacingOnPath = 40f;
    [SerializeField] private Vector3 _positionOffset = Vector3.zero;
    [SerializeField] private float _reachMiddlePosEps = 0.01f;

    [Header("Fix Middle Drift (OneObjectPerPoint)")]
    [SerializeField] private float _middleFixStartEps = 0.02f;
    [SerializeField] private float _middleFixStopEps = 0.002f;
    [SerializeField] private float _middleFixSmoothTime = 0.08f;
    #endregion

    #region Runtime
    private readonly List<Transform> _objects = new();
    private float[] _offsets;
    private readonly ListPoint.PathCache _pathCache = new();

    // SpacingOnPath
    private float _baseS;
    private float _minOffset;
    private float _maxOffset;

    // OneObjectPerPoint
    private float _shiftFloat;
    private int _midObjIndexCache;
    private int _midIndex;
    private float _midS;

    [SerializeField] private Transform _currentMiddle;

    // Drift fixing state
    private bool _isMiddleFixing;
    private float _middleFixTargetShift;
    private float _middleFixVelocity;

    // Only run drift-check after controller says "snap done"
    private bool _pendingDriftCheck;
    #endregion

    public event Action<Transform> OnMiddleObjectChanged;

    // Auto snap state
    private bool _isAutoSnapping;
    private float _autoSnapTargetPos;
    private float _autoSnapVel;
    private float _autoSnapSmoothTime;
    private float _autoSnapStopEps;

    public bool IsDriftFixing() => _isMiddleFixing;

    public void Initialize()
    {
        CollectObjects();
        BuildPathCache();
        CacheMiddle();
        BuildOffsets();
        SnapInitial();
        UpdateVisual();

        DetectMiddle();
        _pendingDriftCheck = false;
        _isMiddleFixing = false;
    }

    /// <summary>
    /// Controller gọi sau khi FixPoint đã snap xong.
    /// </summary>
    public void RequestDriftCheckAfterSnap()
    {
        _pendingDriftCheck = true;
    }

    /// <summary>
    /// Snap group sao cho object gần middle nhất về đúng middle.
    /// </summary>
    /// <remarks>
    /// - SpacingOnPath: chọn nearest-to-middle theo world distance, rồi CenterOnObjectToMiddle(cleanName).
    /// - OneObjectPerPoint: chọn nearest-to-middle (world) rồi center theo index.
    /// - Thực hiện bằng SmoothDamp trong Tick (không giật).
    /// </remarks>
    public void SnapNearestToMiddle(float smoothTime, float stopEps)
    {
        if (!TryFindNearestObjectToMiddle(out Transform nearest, out int nearestIndex))
            return;

        _autoSnapSmoothTime = Mathf.Max(0.0001f, smoothTime);
        _autoSnapStopEps = Mathf.Max(0.00001f, stopEps);

        // Tính target position theo layout mode
        if (_layoutMode == ObjectLayoutMode.SpacingOnPath)
        {
            float offset = GetOffsetAt(nearestIndex);
            float targetBaseS = _midS - offset;

            // Clamp vào range hợp lệ giống Apply()
            float backup = _baseS;
            _baseS = targetBaseS;
            ClampBaseS();
            _autoSnapTargetPos = _baseS;
            _baseS = backup;
        }
        else
        {
            int last = _objects.Count - 1;
            float targetShift;

            switch (_snapMode)
            {
                case SnapGroupMode.SnapGroupToFirst:
                    targetShift = _midIndex + nearestIndex;
                    break;

                case SnapGroupMode.SnapGroupToLast:
                    targetShift = _midIndex - (last - nearestIndex);
                    break;

                default:
                    targetShift = _midIndex - (nearestIndex - _midObjIndexCache);
                    break;
            }

            float backup = _shiftFloat;
            _shiftFloat = targetShift;
            ClampShiftFloat();
            _autoSnapTargetPos = _shiftFloat;
            _shiftFloat = backup;
        }

        _autoSnapVel = 0f;
        _isAutoSnapping = true;

        // để ReachMiddle có thể fire lại đúng object sau snap
        _wasReachedMiddle = false;
    }

    private void CollectObjects()
    {
        _objects.Clear();
        if (_objectRoot == null) return;

        for (int i = 0; i < _objectRoot.childCount; i++)
            _objects.Add(_objectRoot.GetChild(i));

        _queueManager?.Initialize(_objects);
    }

    private void BuildPathCache()
    {
        if (_pointBaker == null || _pointBaker.listPoint == null) return;
        if (_pointBaker.listPoint == null) return;

        _pointBaker.listPoint.BuildPathCache(_pathCache, includeInactive: true);
    }

    private void CacheMiddle()
    {
        if (_pathCache.totalLength <= 0) return;

        _midIndex = _pointBaker?.listPoint.GetMiddlePointIndex() ?? (_pathCache.positions.Count / 2);
        _midS = 0f;

        if (_pathCache.cumulativeLengths != null && _midIndex < _pathCache.cumulativeLengths.Count)
            _midS = _pathCache.cumulativeLengths[_midIndex];
        else if (_pathCache.totalLength > 0)
            _midS = _pathCache.totalLength * 0.5f;
    }

    private void BuildOffsets()
    {
        int n = _objects.Count;
        if (n == 0) return;

        _offsets = new float[n];
        _minOffset = float.MaxValue;
        _maxOffset = float.MinValue;

        switch (_snapMode)
        {
            case SnapGroupMode.SnapGroupToMiddle:
                {
                    int mid = (n - 1) / 2;
                    _midObjIndexCache = mid;

                    for (int i = 0; i < n; i++)
                    {
                        _offsets[i] = (i - mid) * _spacingOnPath;
                        UpdateMinMax(_offsets[i]);
                    }
                    break;
                }

            case SnapGroupMode.SnapGroupToFirst:
                {
                    for (int i = 0; i < n; i++)
                    {
                        _offsets[i] = -i * _spacingOnPath;
                        UpdateMinMax(_offsets[i]);
                    }
                    break;
                }

            case SnapGroupMode.SnapGroupToLast:
                {
                    int last = n - 1;
                    for (int i = 0; i < n; i++)
                    {
                        _offsets[i] = (last - i) * _spacingOnPath;
                        UpdateMinMax(_offsets[i]);
                    }
                    break;
                }
        }
    }

    private void UpdateMinMax(float off)
    {
        if (off < _minOffset) _minOffset = off;
        if (off > _maxOffset) _maxOffset = off;
    }

    private void SnapInitial()
    {
        if (_layoutMode == ObjectLayoutMode.SpacingOnPath)
        {
            if (_pathCache.totalLength > Mathf.Epsilon)
            {
                // Base center
                _baseS = _midS;

                // Clamp theo mode
                ClampBaseS();
            }
        }
        else
        {
            int pointCount = _pathCache.positions.Count;
            if (pointCount < 2) return;

            _shiftFloat = _midIndex;
            ClampShiftFloat();
        }
    }

    public void Apply(PathMoveData data)
    {
        if (_layoutMode == ObjectLayoutMode.SpacingOnPath)
        {
            _baseS += data.Delta;
            ClampBaseS();
        }
        else
        {
            _shiftFloat += data.Delta;
            ClampShiftFloat();
        }

        UpdateVisual();

        if (data.NotifyMiddle)
            DetectMiddle();
    }

    private void ClampBaseS()
    {
        if (_pathCache.totalLength <= 0) return;

        float min, max;

        switch (_snapMode)
        {
            case SnapGroupMode.SnapGroupToFirst:
                min = _midS;
                max = _midS - _minOffset;
                break;

            case SnapGroupMode.SnapGroupToLast:
                min = _midS - _maxOffset;
                max = _midS;
                break;

            default:
                min = _midS - _maxOffset;
                max = _midS - _minOffset;
                break;
        }

        _baseS = Mathf.Clamp(_baseS, min, max);
    }

    private void ClampShiftFloat()
    {
        int pointCount = _pathCache.positions.Count;
        if (pointCount < 2) return;

        int objCount = _objects.Count;
        float min, max;

        switch (_snapMode)
        {
            case SnapGroupMode.SnapGroupToMiddle:
                {
                    int last = objCount - 1;
                    min = _midIndex - (last - _midObjIndexCache);
                    max = _midIndex + _midObjIndexCache;
                    break;
                }

            case SnapGroupMode.SnapGroupToFirst:
                min = _midIndex;
                max = _midIndex + Mathf.Max(0, objCount - 1);
                break;

            case SnapGroupMode.SnapGroupToLast:
                min = _midIndex - Mathf.Max(0, objCount - 1);
                max = _midIndex;
                break;

            default:
                min = 0f;
                max = pointCount - 1;
                break;
        }

        _shiftFloat = Mathf.Clamp(_shiftFloat, min, max);
    }

    private void UpdateVisual()
    {
        if (_objects.Count == 0) return;

        if (_layoutMode == ObjectLayoutMode.SpacingOnPath)
        {
            if (_pathCache.totalLength <= Mathf.Epsilon) return;
            if (_pointBaker == null || _pointBaker.listPoint == null) return;

            for (int i = 0; i < _objects.Count; i++)
            {
                Transform t = _objects[i];
                if (t == null) continue;

                float s = _baseS + GetOffsetAt(i);

                if (s < -0.001f || s > _pathCache.totalLength + 0.001f)
                {
                    _queueManager?.SendToQueue(t);
                    continue;
                }

                _queueManager?.RestoreFromQueue(t);

                if (_pointBaker.listPoint.EvaluateByDistance(_pathCache, s, out Vector3 pos))
                    t.position = pos + _positionOffset;
                else
                    _queueManager?.SendToQueue(t);
            }
        }
        else
        {
            int pointCount = _pathCache.positions.Count;
            if (pointCount < 2) return;

            for (int i = 0; i < _objects.Count; i++)
            {
                Transform t = _objects[i];
                if (t == null) continue;

                float idx;
                switch (_snapMode)
                {
                    case SnapGroupMode.SnapGroupToFirst:
                        idx = _shiftFloat - i;
                        break;

                    case SnapGroupMode.SnapGroupToLast:
                        idx = _shiftFloat + (_objects.Count - 1 - i);
                        break;

                    default:
                        idx = _shiftFloat + (i - _midObjIndexCache);
                        break;
                }

                if (idx < -0.001f || idx > pointCount - 1 + 0.001f)
                {
                    _queueManager?.SendToQueue(t);
                    continue;
                }

                _queueManager?.RestoreFromQueue(t);

                int lo = Mathf.FloorToInt(idx);
                int hi = Mathf.Min(lo + 1, pointCount - 1);
                float frac = idx - lo;

                Vector3 pos = Vector3.Lerp(_pathCache.positions[lo], _pathCache.positions[hi], frac);
                t.position = pos + _positionOffset;
            }
        }
    }

    /// <summary>
    /// Tick per-frame.
    /// </summary>
    /// <remarks>
    /// - DetectMiddle luôn chạy.
    /// - Drift-fix chỉ chạy khi controller gọi RequestDriftCheckAfterSnap() hoặc đang fix dở.
    /// - ReachMiddle edge-trigger chạy theo nearest-to-middle.
    /// </remarks>
    public void Tick(float deltaTime)
    {
        DetectMiddle();

        // Auto snap (Normal/Inertia)
        if (_isAutoSnapping)
        {
            TickAutoSnap(deltaTime);
        }

        if (_pendingDriftCheck || _isMiddleFixing)
        {
            FixMiddleDrift(deltaTime);

            if (!_isMiddleFixing)
                _pendingDriftCheck = false;
        }

        TickMiddleDetectAndNotify();
    }

    /// <summary>
    /// Reset motion state về initial snap.
    /// </summary>
    public void ResetMotion()
    {
        SnapInitial();
        UpdateVisual();

        DetectMiddle();
        _pendingDriftCheck = false;
        _isMiddleFixing = false;
    }

    public float GetPathLength() => _pathCache.totalLength;

    public float GetBaseS() => _baseS;

    public float GetOffsetAt(int index)
    {
        if (_offsets == null || index < 0 || index >= _offsets.Length) return 0f;
        return _offsets[index];
    }

    public float GetCurrentPosition()
    {
        return _layoutMode == ObjectLayoutMode.OneObjectPerPoint ? _shiftFloat : _baseS;
    }

    /// <summary>
    /// Find index by name. Supports both raw ("Mars_Instance") and clean ("Mars").
    /// </summary>
    /// <remarks>
    /// - Trim input để tránh ký tự ẩn.
    /// - Ưu tiên match raw exact.
    /// - Nếu input clean thì thử thêm "_Instance".
    /// - Sau cùng match theo clean (strip suffix).
    /// </remarks>
    /// <returns>Index hoặc -1 nếu không tìm thấy.</returns>
    public int FindIndexByName(string name)
    {
        if (string.IsNullOrEmpty(name)) return -1;

        const string suffix = "_Instance";
        string input = name.Trim();

        // 1) exact raw
        for (int i = 0; i < _objects.Count; i++)
        {
            var t = _objects[i];
            if (t == null) continue;
            if (t.name == input) return i;
        }

        // 2) clean -> rawTry
        if (!input.EndsWith(suffix))
        {
            string rawTry = input + suffix;
            for (int i = 0; i < _objects.Count; i++)
            {
                var t = _objects[i];
                if (t == null) continue;
                if (t.name == rawTry) return i;
            }
        }

        // 3) clean compare
        string cleanInput = input.EndsWith(suffix) ? input.Substring(0, input.Length - suffix.Length) : input;

        for (int i = 0; i < _objects.Count; i++)
        {
            var t = _objects[i];
            if (t == null) continue;

            string raw = t.name;
            string clean = (!string.IsNullOrEmpty(raw) && raw.EndsWith(suffix))
                ? raw.Substring(0, raw.Length - suffix.Length)
                : raw;

            if (clean == cleanInput) return i;
        }

        return -1;
    }

    public float GetStepSize() => _layoutMode == ObjectLayoutMode.OneObjectPerPoint ? 1f : _spacingOnPath;

    /// <summary>
    /// Center group sao cho objectName nằm đúng middle.
    /// </summary>
    /// <remarks>
    /// - SpacingOnPath: dùng _midS (middle distance) và offsets (spacing).
    /// - OneObjectPerPoint: dùng công thức nghịch của UpdateVisual() theo snapMode.
    /// - Reset edge-trigger để ReachMiddle có thể fire lại sau snap.
    /// </remarks>
    public bool CenterOnObjectToMiddle(string objectName)
    {
        Debug.Log($"[ViewObjectOnPath] Received TargetObject event for {objectName}", this);

        int iTarget = FindIndexByName(objectName);
        if (iTarget < 0)
        {
            Debug.LogWarning($"[ViewObjectOnPath] Target not found: '{objectName}'", this);
            return false;
        }

        float delta;

        if (_layoutMode == ObjectLayoutMode.SpacingOnPath)
        {
            float offset = GetOffsetAt(iTarget);
            float targetBaseS = _midS - offset;
            delta = targetBaseS - _baseS;
        }
        else
        {
            // Inverse mapping from UpdateVisual:
            // ToFirst : idx = shift - i
            // ToLast  : idx = shift + (last - i)
            // ToMiddle: idx = shift + (i - midObj)
            int last = _objects.Count - 1;
            float targetShift;

            switch (_snapMode)
            {
                case SnapGroupMode.SnapGroupToFirst:
                    targetShift = _midIndex + iTarget;
                    break;

                case SnapGroupMode.SnapGroupToLast:
                    targetShift = _midIndex - (last - iTarget);
                    break;

                default:
                    targetShift = _midIndex - (iTarget - _midObjIndexCache);
                    break;
            }

            delta = targetShift - _shiftFloat;
        }

        if (Mathf.Abs(delta) <= 0.0001f)
            return false;

        Apply(new PathMoveData { Delta = delta, NotifyMiddle = true });

        Debug.Log($"[ViewObjectOnPath] Target Centered: {_objects[iTarget].name}", this);

        // reset edge trigger để tick có thể fire lại đúng object mới
        _wasReachedMiddle = false;

        RequestDriftCheckAfterSnap();
        return true;
    }

    /// <summary>
    /// Detect object currently considered at Middle (OneObjectPerPoint).
    /// </summary>
    /// <remarks>
    /// Công thức phải khớp UpdateVisual():
    /// - ToFirst : idx = shift - i
    /// - ToLast  : idx = shift + (last - i)
    /// - ToMiddle: idx = shift + (i - midObj)
    /// </remarks>
    private void DetectMiddle()
    {
        if (_layoutMode != ObjectLayoutMode.OneObjectPerPoint)
            return;

        if (_objects.Count == 0)
            return;

        int logicalIndex = Mathf.RoundToInt(_shiftFloat);
        int lastObj = _objects.Count - 1;

        int objIndex;
        switch (_snapMode)
        {
            case SnapGroupMode.SnapGroupToFirst:
                objIndex = logicalIndex - _midIndex;
                break;

            case SnapGroupMode.SnapGroupToLast:
                objIndex = lastObj + logicalIndex - _midIndex;
                break;

            default:
                objIndex = _midObjIndexCache + _midIndex - logicalIndex;
                break;
        }

        objIndex = Mathf.Clamp(objIndex, 0, lastObj);

        Transform candidate = _objects[objIndex];

        if (candidate != null && candidate != _currentMiddle)
        {
            _currentMiddle = candidate;
            OnMiddleObjectChanged?.Invoke(candidate);
        }
    }

    /// <summary>
    /// Nếu middle object lệch khỏi tọa độ middle point thì animate về.
    /// </summary>
    private void FixMiddleDrift(float deltaTime)
    {
        if (_layoutMode != ObjectLayoutMode.OneObjectPerPoint)
            return;

        if (_currentMiddle == null)
            return;

        if (_pointBaker == null || _pointBaker.listPoint == null)
            return;

        if (!_pointBaker.listPoint.TryGetMiddleCoordinate(_pathCache, out Vector3 midPos))
            return;

        float dist = Vector3.Distance(_currentMiddle.position, midPos);

        if (!_isMiddleFixing)
        {
            if (dist < _middleFixStartEps)
                return;

            int middleObjIndex = _objects.IndexOf(_currentMiddle);
            int last = _objects.Count - 1;

            float targetShift;

            switch (_snapMode)
            {
                case SnapGroupMode.SnapGroupToFirst:
                    targetShift = _midIndex + middleObjIndex;
                    break;

                case SnapGroupMode.SnapGroupToLast:
                    targetShift = _midIndex - (last - middleObjIndex);
                    break;

                default:
                    targetShift = _midIndex - (middleObjIndex - _midObjIndexCache);
                    break;
            }

            float backup = _shiftFloat;
            _shiftFloat = targetShift;
            ClampShiftFloat();
            _middleFixTargetShift = _shiftFloat;
            _shiftFloat = backup;

            _middleFixVelocity = 0f;
            _isMiddleFixing = true;
        }

        if (_isMiddleFixing)
        {
            _shiftFloat = Mathf.SmoothDamp(
                _shiftFloat,
                _middleFixTargetShift,
                ref _middleFixVelocity,
                _middleFixSmoothTime,
                Mathf.Infinity,
                deltaTime);

            ClampShiftFloat();
            UpdateVisual();

            if (Mathf.Abs(_shiftFloat - _middleFixTargetShift) <= _middleFixStopEps)
            {
                _shiftFloat = Mathf.Round(_middleFixTargetShift);
                _middleFixVelocity = 0f;
                _isMiddleFixing = false;

                ClampShiftFloat();
                UpdateVisual();
            }
        }
    }

    #region Reach Middle Event
    [SerializeField] private bool _logWhenReachMiddle = true;

    /// <summary>
    /// Edge trigger: true khi đã fire "Reach Middle", reset khi rời Middle.
    /// </summary>
    [SerializeField] private bool _wasReachedMiddle = false;

    /// <summary>
    /// Lấy tọa độ world của Middle point.
    /// </summary>
    private Vector3 GetMiddleWorldPosition()
    {
        if (_pointBaker != null && _pointBaker.listPoint != null)
        {
            if (_pointBaker.listPoint.TryGetMiddleCoordinate(_pathCache, out Vector3 midPos))
                return midPos;
        }

        if (_layoutMode == ObjectLayoutMode.SpacingOnPath && _pointBaker != null && _pointBaker.listPoint != null)
        {
            if (_pointBaker.listPoint.EvaluateByDistance(_pathCache, _midS, out Vector3 pos))
                return pos;
        }

        if (_pathCache.positions != null && _pathCache.positions.Count > 0)
        {
            int idx = Mathf.Clamp(_midIndex, 0, _pathCache.positions.Count - 1);
            return _pathCache.positions[idx];
        }

        return transform.position;
    }

    private bool TryFindNearestObjectToMiddle(out Transform obj, out int objIndex)
    {
        obj = null;
        objIndex = -1;

        if (_objects == null || _objects.Count == 0)
            return false;

        Vector3 midPos = GetMiddleWorldPosition();
        float best = float.MaxValue;

        for (int i = 0; i < _objects.Count; i++)
        {
            Transform t = _objects[i];
            if (t == null) continue;

            if (!t.gameObject.activeInHierarchy) continue;

            float d = (t.position - midPos).sqrMagnitude;
            if (d < best)
            {
                best = d;
                obj = t;
                objIndex = i;
            }
        }

        return obj != null;
    }

    private string GetCleanObjectName(string rawName)
    {
        const string suffix = "_Instance";
        if (!string.IsNullOrEmpty(rawName) && rawName.EndsWith(suffix))
            return rawName.Substring(0, rawName.Length - suffix.Length);
        return rawName;
    }

    /// <summary>
    /// Fire event khi object "thực sự" chạm Middle (nearest-by-distance), edge-trigger.
    /// </summary>
    private void TickMiddleDetectAndNotify()
    {
        if (!_logWhenReachMiddle) return;

        if (!TryFindNearestObjectToMiddle(out Transform candidate, out int _))
            return;

        Vector3 midPos = GetMiddleWorldPosition();
        float dist = Vector3.Distance(candidate.position, midPos);
        bool reached = dist <= _reachMiddlePosEps;

        if (candidate != _currentMiddle)
            _currentMiddle = candidate;

        if (reached && !_wasReachedMiddle)
        {
            _wasReachedMiddle = true;

            Debug.Log($"[ViewObjectOnPath] Reach Middle: {_currentMiddle.name}");

            string name = GetCleanObjectName(_currentMiddle.name);

            CoreEvents.TargetObject.Raise(new TargetObjectEvent(
                TargetObjectEvent.TypeTarget.UI_To_Data,
                name));
        }
        else if (!reached && _wasReachedMiddle)
        {
            _wasReachedMiddle = false;
        }
    }

    /// <summary>
    /// Smooth snap tới target pos (baseS/shiftFloat).
    /// </summary>
    /// <remarks>
    /// - Không dùng Apply để tránh clamp/notify nhiều lần; UpdateVisual + DetectMiddle vẫn đảm bảo.
    /// </remarks>
    private void TickAutoSnap(float dt)
    {
        float cur = GetCurrentPosition();

        float next = Mathf.SmoothDamp(
            cur,
            _autoSnapTargetPos,
            ref _autoSnapVel,
            _autoSnapSmoothTime,
            Mathf.Infinity,
            dt);

        float delta = next - cur;

        // Apply theo layout mode để clamp & update visual
        Apply(new PathMoveData
        {
            Delta = delta,
            NotifyMiddle = true
        });

        if (Mathf.Abs(GetCurrentPosition() - _autoSnapTargetPos) <= _autoSnapStopEps)
        {
            // Snap cuối về exact để hết drift nhỏ
            float final = _autoSnapTargetPos;
            float fixDelta = final - GetCurrentPosition();

            Apply(new PathMoveData
            {
                Delta = fixDelta,
                NotifyMiddle = true
            });

            _autoSnapVel = 0f;
            _isAutoSnapping = false;
        }
    }
    #endregion
}