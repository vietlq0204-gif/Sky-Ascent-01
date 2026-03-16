using System;
using System.Collections.Generic;
using UnityEngine;

public enum DragPhase
{
    Start = 0,
    Move = 1,
    End = 2
}

public enum DragDirection
{
    None = 0,
    East,
    South,
    Center,
    West,
    North
}

/// <summary>
/// Pool cho DragInputEvent (chặn GC khi Move)
/// </summary>
/// <remarks>
/// Lưu ý quan trọng: subscriber không được giữ reference lâu dài. Với drag input thường xử lý ngay trong frame là OK.
/// </remarks>
public static class DragInputEventPool
{
    private static readonly Stack<DragInputEvent> _pool = new Stack<DragInputEvent>(64);
    private static readonly object _lock = new();

    public static DragInputEvent Get()
    {
        lock (_lock)
        {
            if (_pool.Count > 0)
                return _pool.Pop();
        }
        return new DragInputEvent();
    }

    public static void Release(DragInputEvent e)
    {
        if (e == null) return;
        e.Reset();
        lock (_lock) _pool.Push(e);
    }
}

/// <summary>
/// Helper tính direction từ delta (screen)
/// </summary>
public static class DragDirectionUtility
{
    /// <summary>
    /// Get direction from delta.
    /// </summary>
    /// <remarks>
    /// delta nhỏ hơn threshold -> None.
    /// </remarks>
    public static DragDirection GetDirection(Vector2 delta, float threshold)
    {
        if (delta.sqrMagnitude < threshold * threshold)
            return DragDirection.None;

        if (Mathf.Abs(delta.x) >= Mathf.Abs(delta.y))
            return delta.x >= 0 ? DragDirection.East : DragDirection.West;

        return delta.y >= 0 ? DragDirection.North : DragDirection.South;
    }
}

/// <summary>
/// Drag input publisher: phát CoreEvents.Drag với DragInputEvent.
/// Hỗ trợ UI 2D (screen) và kéo 3D (optional world mapping).
/// </summary>
public class DragInput : MonoBehaviour
{
    public enum WorldMappingMode
    {
        None = 0,        // chỉ screen (UI 2D / hệ thống tự map)
        Plane = 1,       // map screen -> world qua Plane
        Raycast = 2      // map screen -> world qua Physics.Raycast
    }

    [Header("Input")]
    [SerializeField] private int _dragButton = 0;

    [Header("State Direction (Swipe-like)")]
    [SerializeField] private bool _enableStateDirection = true;

    [SerializeField] private DragDirection _stateDirection = DragDirection.Center;

    [SerializeField] private float _stateSwipeThresholdPx = 50f;

    [Header("Direction")]
    [SerializeField] private float _directionThresholdPx = 5f;

    [Header("World Mapping (Optional)")]
    [SerializeField] private WorldMappingMode _worldMode = WorldMappingMode.None;
    [SerializeField] private Camera _camera;
    [SerializeField] private LayerMask _raycastMask = ~0;
    [SerializeField] private float _raycastMaxDistance = 1000f;

    [Header("Plane Mode Settings")]
    [SerializeField] private Vector3 _planeNormal = Vector3.up;
    [SerializeField] private float _planeDistanceToOrigin = 0f; // plane: normal·X + d = 0 => d = -distance
    // NOTE: cách đơn giản: plane đi qua origin với offset theo normal

    private bool _dragging;
    private Vector2 _startScreen;
    private Vector2 _lastScreen;

    private bool _hasStartWorld;
    private Vector3 _startWorld;
    private Vector3 _lastWorld;

    private void Awake()
    {
        //if (_camera == null) _camera = Camera.main;
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(_dragButton))
            HandleStart(Input.mousePosition);

        if (_dragging && Input.GetMouseButton(_dragButton))
            HandleMove(Input.mousePosition);

        if (_dragging && Input.GetMouseButtonUp(_dragButton))
            HandleEnd(Input.mousePosition);

      
    }

    private void HandleStart(Vector2 screenPos)
    {
        _dragging = true;
        _startScreen = _lastScreen = screenPos;

        _hasStartWorld = TryMapWorld(screenPos, out _startWorld);
        _lastWorld = _startWorld;

        Publish(DragPhase.Start, screenPos, Vector2.zero, screenPos, screenPos, Vector2.zero,
                DragDirection.None, _stateDirection,
                _hasStartWorld, _startWorld, _startWorld, Vector3.zero, _startWorld);
    }

    private void HandleMove(Vector2 screenPos)
    {
        Vector2 deltaScreen = screenPos - _lastScreen;
        _lastScreen = screenPos;

        DragDirection instantDir = DragDirectionUtility.GetDirection(deltaScreen, _directionThresholdPx);

        bool hasWorldNow = false;
        Vector3 worldNow = default;
        Vector3 deltaWorld = Vector3.zero;

        if (_worldMode != WorldMappingMode.None && _hasStartWorld)
        {
            hasWorldNow = TryMapWorld(screenPos, out worldNow);
            if (hasWorldNow)
            {
                deltaWorld = worldNow - _lastWorld;
                _lastWorld = worldNow;
            }
        }

        Publish(DragPhase.Move,
                _startScreen,
                deltaScreen,
                screenPos,
                Vector2.zero,
                screenPos - _startScreen,
                instantDir, _stateDirection,
                hasWorldNow && _hasStartWorld,
                _startWorld,
                hasWorldNow ? worldNow : _lastWorld,
                deltaWorld,
                Vector3.zero);
    }

    private void HandleEnd(Vector2 screenPos)
    {
        _dragging = false;

        Vector2 totalDelta = screenPos - _startScreen;

        if (_enableStateDirection)
        {
            DragDirection intent = DetectSwipeIntent(totalDelta);
            ApplySwipeIntent(intent);
        }

        bool hasWorldEnd = false;
        Vector3 endWorld = default;

        if (_worldMode != WorldMappingMode.None && _hasStartWorld)
            hasWorldEnd = TryMapWorld(screenPos, out endWorld);

        Publish(DragPhase.End,
                _startScreen,
                Vector2.zero,
                screenPos,
                screenPos,
                totalDelta,
                DragDirection.None, _stateDirection,
                hasWorldEnd && _hasStartWorld,
                _startWorld,
                hasWorldEnd ? endWorld : _lastWorld,
                Vector3.zero,
                hasWorldEnd ? endWorld : _lastWorld);

        _hasStartWorld = false;

        DrawSwipeLine();
    }

    /// <summary>
    /// Publish event via CoreEvents.
    /// </summary>
    private void Publish(
     DragPhase phase,
     Vector2 startScreen,
     Vector2 deltaScreen,
     Vector2 currentScreen,
     Vector2 endScreen,
     Vector2 totalDeltaScreen,
     DragDirection direction,
     DragDirection stateDirection,
     bool hasWorld,
     Vector3 startWorld,
     Vector3 currentWorld,
     Vector3 deltaWorld,
     Vector3 endWorld)
    {
        var e = DragInputEventPool.Get();

        e.phase = phase;
        e.startScreen = startScreen;
        e.deltaScreen = deltaScreen;
        e.currentScreen = currentScreen;
        e.endScreen = endScreen;
        e.totalDeltaScreen = totalDeltaScreen;

        e.direction = direction;                 // instant
        e.stateDirection = stateDirection;       // state

        e.hasWorld = hasWorld;
        e.startWorld = startWorld;
        e.currentWorld = currentWorld;
        e.deltaWorld = deltaWorld;
        e.endWorld = endWorld;

        CoreEvents.Drag.Raise(e);
        DragInputEventPool.Release(e);
    }


    /// <summary>
    /// Map screen -> world depending on mode.
    /// </summary>
    private bool TryMapWorld(Vector2 screenPos, out Vector3 world)
    {
        world = default;

        if (_worldMode == WorldMappingMode.None)
            return false;

        if (_camera == null)
            return false;

        Ray ray = _camera.ScreenPointToRay(screenPos);

        if (_worldMode == WorldMappingMode.Raycast)
        {
            if (Physics.Raycast(ray, out RaycastHit hit, _raycastMaxDistance, _raycastMask, QueryTriggerInteraction.Ignore))
            {
                world = hit.point;
                return true;
            }
            return false;
        }

        // Plane mode
        if (_worldMode == WorldMappingMode.Plane)
        {
            // plane: normal·X + d = 0
            Vector3 n = _planeNormal.sqrMagnitude > 0.0001f ? _planeNormal.normalized : Vector3.up;
            float d = -_planeDistanceToOrigin;
            Plane plane = new Plane(n, d);

            if (plane.Raycast(ray, out float enter))
            {
                world = ray.GetPoint(enter);
                return true;
            }
            return false;
        }

        return false;
    }

    /// <summary>
    /// Phát hiện intent từ totalDelta (start->end).
    /// </summary>
    /// <returns>Intent direction</returns>
    private DragDirection DetectSwipeIntent(Vector2 totalDelta)
    {
        if (totalDelta.magnitude < _stateSwipeThresholdPx)
            return DragDirection.None;

        bool isHorizontal = Mathf.Abs(totalDelta.x) > Mathf.Abs(totalDelta.y);

        if (isHorizontal)
            return totalDelta.x > 0 ? DragDirection.East : DragDirection.West;

        return totalDelta.y > 0 ? DragDirection.North : DragDirection.South;
    }

    /// <summary>
    /// Áp dụng intent để cập nhật stateDirection (Center <-> rìa).
    /// </summary>
    /// <param name="intent">Hướng intent</param>
    private void ApplySwipeIntent(DragDirection intent)
    {
        if (intent == DragDirection.None) return;

        if (intent == DragDirection.Center)
        {
            _stateDirection = DragDirection.Center;
            return;
        }

        if (_stateDirection == DragDirection.Center)
        {
            _stateDirection = intent;
            return;
        }

        bool towardCenter =
            (_stateDirection == DragDirection.North && intent == DragDirection.South) ||
            (_stateDirection == DragDirection.South && intent == DragDirection.North) ||
            (_stateDirection == DragDirection.East && intent == DragDirection.West) ||
            (_stateDirection == DragDirection.West && intent == DragDirection.East);

        _stateDirection = towardCenter ? DragDirection.Center : intent;
    }


//#if UNITY_EDITOR

    /// <summary>
    /// vẽ đường vuốt để debug
    /// </summary>
    private void DrawSwipeLine()
    {
        // DEBUG hiển thị đường vuốt 
        Vector3 startPosWorld = Camera.main.ScreenToWorldPoint(new Vector3(_startScreen.x, _startScreen.y, 10f));
        Vector3 endPosWorld = Camera.main.ScreenToWorldPoint(new Vector3(_lastScreen.x, _lastScreen.y, 10f));
        Debug.DrawLine(startPosWorld, endPosWorld, Color.cyan, 1.5f);

    }

//#endif
}

