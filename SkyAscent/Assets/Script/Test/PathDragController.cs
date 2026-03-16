using UnityEngine;

public enum DragBehaviorMode { Normal, Inertia, FixPoint }

/// <summary>
/// Drag Controller (POCO):
/// - Map pixel -> delta path
/// - Inertia
/// - FixPoint snap (theo step)
/// </summary>
public class PathDragController
{
    private readonly IPathView _view;
    private readonly ViewObjectOnPathInput _cfg;

    private float _velocity;
    private bool _dragging;
    private float _totalDragAxis;

    // FixPoint anim
    private bool _isFixPointAnimating;
    private float _targetPos;
    private float _smoothVel;

    // Cooldown
    private bool _isFixPointCooldown;
    private float _fixPointCooldownTimer;

    private bool _inertiaAutoSnapDone;

    public PathDragController(IPathView view, ViewObjectOnPathInput cfg)
    {
        _view = view;
        _cfg = cfg;
    }

    /// <summary>
    /// Reset + Initialize view khi chapter loaded.
    /// </summary>
    public void InitData()
    {
        if (_view == null) return;

        _velocity = 0f;
        _dragging = false;
        _totalDragAxis = 0f;

        _isFixPointAnimating = false;
        _targetPos = 0f;
        _smoothVel = 0f;

        _isFixPointCooldown = false;
        _fixPointCooldownTimer = 0f;

        _view.ResetMotion();
        _view.Initialize();
    }

    /// <summary>
    /// Tick controller và view mỗi frame.
    /// </summary>
    public void Tick(float dt)
    {
        if (_view == null || _cfg == null) return;

        // FixPoint cooldown tick (mode = Time)
        if (!_dragging &&
            _cfg.DragMode == DragBehaviorMode.FixPoint &&
            _cfg.FixPointCooldown &&
            _isFixPointCooldown &&
            _cfg.FixPointCooldownMode == FixPointCooldownMode.Time)
        {
            _fixPointCooldownTimer -= dt;
            if (_fixPointCooldownTimer <= 0f)
            {
                _fixPointCooldownTimer = 0f;
                _isFixPointCooldown = false;
            }
        }

        if (!_dragging)
        {
            switch (_cfg.DragMode)
            {
                case DragBehaviorMode.Inertia:
                    TickInertia(dt);
                    break;

                case DragBehaviorMode.FixPoint:
                    TickFixPoint(dt);
                    break;

                default:
                    break;
            }
        }

        // Luôn tick view: detect middle luôn chạy; drift-fix chỉ chạy khi view được request.
        _view.Tick(dt);
    }

    /// <summary>
    /// Handle DragInputEvent.
    /// </summary>
    public void OnDrag(DragInputEvent e)
    {
        if (e == null || _view == null || _cfg == null) return;

        float axis = GetAxis(e.deltaScreen);

        switch (e.phase)
        {
            case DragPhase.Start:
                OnDragStart();
                break;

            case DragPhase.Move:
                OnDragMove(axis);
                break;

            case DragPhase.End:
                OnDragEnd();
                break;
        }
    }

    /// <summary>
    /// Target group để object nằm đúng Middle.
    /// </summary>
    public void TargetGroupOnObjectToMiddle(string objectName)
    {
        if (_view == null) return;

        // Dừng motion để tránh drift theo velocity cũ
        _velocity = 0f;
        _dragging = false;

        _isFixPointAnimating = false;
        _smoothVel = 0f;

        _view.CenterOnObjectToMiddle(objectName);
    }

    #region Drag Internals
    private void OnDragStart()
    {
        if (_cfg.DragMode == DragBehaviorMode.FixPoint && _isFixPointCooldown)
            return;

        _dragging = true;
        _totalDragAxis = 0f;
        _velocity = 0f;

        _isFixPointAnimating = false;
        _smoothVel = 0f;

        _inertiaAutoSnapDone = false; // reset
    }

    private void OnDragMove(float axis)
    {
        if (!_dragging) return;

        _totalDragAxis += axis;

        // FixPoint: chỉ accumulate, không Apply khi move
        if (_cfg.DragMode == DragBehaviorMode.FixPoint)
            return;

        float dt = Mathf.Max(Time.deltaTime, 0.0001f);
        float delta = ConvertToPathDelta(axis);

        _velocity = delta / dt;

        _view.Apply(new PathMoveData
        {
            Delta = delta,
            NotifyMiddle = false
        });
    }

    /// <summary>
    /// Handle drag end.
    /// </summary>
    private void OnDragEnd()
    {
        _dragging = false;

        if (_cfg.DragMode == DragBehaviorMode.FixPoint)
        {
            _velocity = 0f;
            TryStartFixPointOrSnapBack();
            return;
        }

        if (_cfg.DragMode == DragBehaviorMode.Normal)
        {
            _velocity = 0f;

            // Auto snap ngay khi thả
            if (_cfg.AutoSnapToMiddle)
                _view.SnapNearestToMiddle(_cfg.AutoSnapSmoothTime, _cfg.AutoSnapStopEps);

            return;
        }

        // Inertia: giữ velocity, snap sẽ trigger khi dừng
    }

    #endregion

    #region Inertia / FixPoint

    private void TickInertia(float dt)
    {
        if (Mathf.Abs(_velocity) <= _cfg.MinVelocity)
        {
            if (_cfg.AutoSnapToMiddle && !_inertiaAutoSnapDone)
            {
                _inertiaAutoSnapDone = true;
                _view.SnapNearestToMiddle(_cfg.AutoSnapSmoothTime, _cfg.AutoSnapStopEps);
            }
            return;
        }

        float delta = _velocity * dt;
        _view.Apply(new PathMoveData { Delta = delta, NotifyMiddle = true });

        _velocity = Mathf.Lerp(_velocity, 0f, _cfg.InertiaDamping * dt);
    }

    private void TickFixPoint(float dt)
    {
        // yêu cầu: drift-fix chỉ chạy SAU snap xong.
        // Nếu view đang drift-fix (sau snap) thì pause anim controller.
        if (_isFixPointAnimating && _view.IsDriftFixing())
            return;

        if (!_isFixPointAnimating)
            return;

        float prev = _view.GetCurrentPosition();

        float current = Mathf.SmoothDamp(
            prev,
            _targetPos,
            ref _smoothVel,
            _cfg.FixPointSmoothTime,
            Mathf.Infinity,
            dt);

        float delta = current - prev;

        _view.Apply(new PathMoveData
        {
            Delta = delta,
            NotifyMiddle = true
        });

        if (Mathf.Abs(current - _targetPos) <= _cfg.FixPointStopEps)
        {
            float snapped = Mathf.Round(_targetPos);
            float snapDelta = snapped - _view.GetCurrentPosition();

            _view.Apply(new PathMoveData
            {
                Delta = snapDelta,
                NotifyMiddle = true
            });

            _smoothVel = 0f;
            _isFixPointAnimating = false;

            // SAU SNAP XONG: request view kiểm tra drift
            _view.RequestDriftCheckAfterSnap();

            if (_cfg.FixPointCooldown &&
                _cfg.FixPointCooldownMode == FixPointCooldownMode.UntilReachedTarget)
            {
                _isFixPointCooldown = false;
            }
        }
    }

    /// <summary>
    /// FixPoint: nếu drag đủ mạnh thì nhảy 1 step theo hướng.
    /// Nếu drag nhẹ thì snap về nearest step (để thả ra luôn thấy "hít" vào điểm).
    /// </summary>
    /// <remarks>
    /// - Unit lấy từ view.GetStepSize():
    ///   - SpacingOnPath => spacing (vd 40)
    ///   - OneObjectPerPoint => 1
    /// </remarks>
    private void TryStartFixPointOrSnapBack()
    {
        float unit = Mathf.Max(0.0001f, _view.GetStepSize());

        // Snap base về nearest unit
        float current = _view.GetCurrentPosition();
        float snapBase = Mathf.Round(current / unit) * unit;

        float abs = Mathf.Abs(_totalDragAxis);

        float target = snapBase;

        // Nếu đủ threshold => nhảy thêm 1 step theo hướng swipe
        if (abs >= _cfg.FixPointSwipeThreshold)
        {
            int step = _totalDragAxis > 0 ? 1 : -1;
            if (_cfg.InvertSwipe) step *= -1;

            target = snapBase + step * unit;
        }

        // Nếu target gần current quá thì thôi
        if (Mathf.Abs(target - current) <= _cfg.FixPointStopEps)
            return;

        _targetPos = target;
        _isFixPointAnimating = true;
        _smoothVel = 0f;

        // Cooldown
        if (_cfg.FixPointCooldown)
        {
            _isFixPointCooldown = true;

            if (_cfg.FixPointCooldownMode == FixPointCooldownMode.Time)
                _fixPointCooldownTimer = _cfg.FixPointCooldownTime;
            // UntilReachedTarget: sẽ clear khi anim xong (ở TickFixPoint)
        }
    }

    #endregion

    #region Mapping

    /// <summary>
    /// Convert pixel drag -> path delta.
    /// </summary>
    /// <remarks>
    /// Hiện giữ theo logic cũ: ratio * pathLength * speed.
    /// (Nếu muốn OneObjectPerPoint mapping theo pointCount thì cần expose thêm từ view.)
    /// </remarks>
    private float ConvertToPathDelta(float pixel)
    {
        if (_cfg.InvertSwipe)
            pixel = -pixel;

        float denom = Mathf.Max(1f, _cfg.PixelsForFullPath);
        float ratio = pixel / denom;

        return ratio * _view.GetPathLength() * _cfg.Speed;
    }

    private float GetAxis(Vector2 delta)
    {
        if (_cfg.UseOnlyHorizontal)
            return delta.x;

        return Mathf.Abs(delta.x) > Mathf.Abs(delta.y) ? delta.x : delta.y;
    }

    #endregion
}