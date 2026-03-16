using UnityEngine;

/// <summary>
/// Player movement by swipe around screen center (virtual joystick).
/// Mode Z180: Move on world X/Y plane, lock world Z = 0.
/// Tilt like airplane by limiting angle between player's forward (local +Z) and world +Z.
/// </summary>
public partial class PlayerSwipeMoveZ180 : CoreEventBase
{
    [Header("Movement (World XY)")]
    [SerializeField] private float _moveSpeed = 6f;

    [Header("Input Mapping")]
    [Tooltip(" Bán kính vùng cảm ứng trên màn hình ")]
    [Range(0.1f, 0.9f)]
    [SerializeField] private float _screenRadiusFactor = 0.35f;

    [Tooltip("Deadzone in pixels around center to avoid jitter")]
    [SerializeField] private float _deadZonePx = 10f;
    [Tooltip("Nếu bật, sẽ đảo chiều input Y để điều khiển pitch (góc nghiêng lên/xuống)")]
    [SerializeField] private bool _invertPitchY = false;

    [Header("Tilt (Airplane Feel)")]
    [Tooltip("góc nghiêng (độ) tối đa giữa forward và world +Z")]
    [Range(0f, 89f)]
    [SerializeField] private float _maxTiltDeg = 45f;

    [Tooltip("Tốc độ xoay khi đang kéo (theo target rotation)")]
    [SerializeField] private float _tiltFollowSpeed = 10f;

    [Tooltip(" làm mượt khi trở về trung lập sau khi thả tay")]
    [SerializeField] private float _returnToNeutralSpeed = 8f;

    [Header("Runtime Debug")]
    [SerializeField] private bool _drawDebug = true;

    private bool _dragging;
    //private int _pointerId = -1;

    private Vector2 _screenCenter;
    private float _radiusPx;

    private Vector2 _input01; // [-1..1] trong vùng tròn bán kính = radiusPx, (0,0) ở center/drag origin
    private Quaternion _neutralRotation;

    [Tooltip("Nếu bật, sẽ dùng vị trí chạm đầu tiên làm center của joystick (floating joystick). Nếu tắt, center luôn là center màn hình (fixed joystick).")]
    [SerializeField] private bool _useTouchStartAsCenter = true;
    private Vector2 _dragOrigin;

    [Tooltip ("Nếu bật, sẽ giới hạn vị trí của player trong bounds hình chữ nhật trên world XY, defined by center/extents.")]
    [SerializeField] private bool _useBounds = true;
    [Tooltip ("center của bounds trên world XY. Player sẽ bị giới hạn di chuyển trong vùng hình chữ nhật với center này và extents bên dưới.")]
    [SerializeField] private Vector2 _boundsCenterXY = Vector2.zero;
    [Tooltip ("extents của bounds trên world XY. Player sẽ bị giới hạn di chuyển trong vùng hình chữ nhật với center ở trên và extents này (từ center đến cạnh).")]
    [SerializeField] private Vector2 _boundsExtentsXY = new Vector2(10f, 5f);
    [SerializeField] private bool _isPlayerLife;

    protected override void Awake()
    {
        /// <summary>
        /// Cache neutral rotation (forward aligned with world +Z by default).
        /// </summary>
        base.Awake();
        _neutralRotation = transform.rotation;
    }

    protected override void OnEnable()
    {
        /// <summary>
        /// Recompute mapping when enabled (in case resolution changed).
        /// </summary>
        base.OnEnable();
        RecalculateScreenMapping();
    }

    private void Update()
    {
        if (!AllowInput)
        {
            ForceStopInput();
            return;
        }

        RecalculateScreenMappingIfNeeded();

        // Read pointer (mouse on PC, touch on mobile)
        ReadPointerState(out bool isDown, out bool isHeld, out bool isUp, out Vector2 screenPos);

        if (isDown) BeginDrag(screenPos);
        if (_dragging && isHeld) UpdateDrag(screenPos);
        if (_dragging && isUp) EndDrag();

        // Apply movement & tilt
        ApplyMovement(Time.deltaTime);
        ApplyTilt(Time.deltaTime);

        if (_drawDebug) DrawDebug();
    }

    /// <summary>
    /// Called externally if Ní muốn feed input từ hệ DragInput sẵn có.
    /// currentScreenPos: vị trí con trỏ hiện tại trên màn hình.
    /// isDragging: trạng thái kéo.
    /// </summary>
    /// <remarks>
    /// Nếu dùng hàm này, Ní có thể tắt logic đọc Input trong Update và tự gọi FeedDrag(...) từ event drag.
    /// </remarks>
    public void FeedDrag(Vector2 currentScreenPos, bool isDragging)
    {
        if (isDragging && !_dragging) BeginDrag(currentScreenPos);
        if (isDragging && _dragging) UpdateDrag(currentScreenPos);
        if (!isDragging && _dragging) EndDrag();
    }

    /// <summary>
    /// Chức năng : Bắt đầu kéo, xác định origin (center) của joystick nếu dùng chế độ floating joystick.
    /// </summary>
    /// <param name="screenPos"></param>
    private void BeginDrag(Vector2 screenPos)
    {

        _dragging = true;

        if (_useTouchStartAsCenter)
            _dragOrigin = screenPos;

        _input01 = Vector2.zero;
        UpdateDrag(screenPos);
    }

    /// <summary>
    /// Chức năng : Cập nhật input dựa trên vị trí con trỏ hiện tại so với origin (floating joystick) hoặc screen center (fixed joystick).
    /// </summary>
    /// <param name="screenPos"></param>
    private void UpdateDrag(Vector2 screenPos)
    {
        // tính toán vector từ center (origin) đến vị trí con trỏ hiện tại
        Vector2 center = _useTouchStartAsCenter ? _dragOrigin : _screenCenter;
        Vector2 fromCenter = screenPos - center;

        if (fromCenter.sqrMagnitude <= _deadZonePx * _deadZonePx)
        {
            _input01 = Vector2.zero;
            return;
        }

        Vector2 v = fromCenter / Mathf.Max(1f, _radiusPx);
        if (v.sqrMagnitude > 1f) v.Normalize();

        _input01 = v;
    }

    /// <summary>
    /// chức năng : Kết thúc kéo, reset input về zero (trở về trung lập).
    /// </summary>
    private void EndDrag()
    {
        /// <summary>
        /// End drag, keep input at zero (return to neutral).
        /// </summary>
        _dragging = false;
        //_pointerId = -1;
        _input01 = Vector2.zero;
    }

    private void ForceStopInput()
    {
        _dragging = false;
        _input01 = Vector2.zero;
    }

    /// <summary>
    /// chức năng : Di chuyển object trên mặt phẳng world XY dựa trên input01, với tốc độ _moveSpeed. Z luôn bằng 0.
    /// </summary>
    /// <param name="dt"></param>
    private void ApplyMovement(float dt)
    {
        /// <summary>
        /// Move on world XY, lock Z = 0, clamp within center/extents bounds if enabled.
        /// </summary>
        if (_input01 == Vector2.zero) return;

        Vector3 delta = new Vector3(_input01.x, _input01.y, 0f) * (_moveSpeed * dt);
        Vector3 p = transform.position + delta;
        p.z = 0f;

        if (_useBounds)
        {
            float minX = _boundsCenterXY.x - _boundsExtentsXY.x;
            float maxX = _boundsCenterXY.x + _boundsExtentsXY.x;
            float minY = _boundsCenterXY.y - _boundsExtentsXY.y;
            float maxY = _boundsCenterXY.y + _boundsExtentsXY.y;

            p.x = Mathf.Clamp(p.x, minX, maxX);
            p.y = Mathf.Clamp(p.y, minY, maxY);
        }

        transform.position = p;
    }

    /// <summary>
    /// chức năng : Xoay object để tạo cảm giác nghiêng khi di chuyển (như máy bay). 
    /// Roll quanh local Z dựa trên input01.x, pitch quanh local X dựa trên input01.y (có thể đảo chiều). 
    /// Góc nghiêng tối đa được giới hạn bởi _maxTiltDeg. 
    /// Khi không kéo nữa, xoay trở về trung lập (_neutralRotation) với tốc độ _returnToNeutralSpeed.
    /// </summary>
    /// <param name="dt"></param>
    private void ApplyTilt(float dt)
    {
        /// <summary>
        /// Tilt like airplane:
        /// - Swipe X controls roll around local Z (x > 0 => z < 0).
        /// - Swipe Y controls pitch around local X (invertable).
        /// </summary>
        float speed = _dragging ? _tiltFollowSpeed : _returnToNeutralSpeed;

        float x = _input01.x;
        float y = _input01.y;

        // Roll: x > 0 => z < 0
        float rollDeg = -x * _maxTiltDeg;

        // Pitch: toggle invert
        float pitchSign = _invertPitchY ? -1f : 1f;
        float pitchDeg = pitchSign * y * _maxTiltDeg;

        Quaternion targetRot = _neutralRotation * Quaternion.Euler(pitchDeg, 0f, rollDeg);

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            _dragging ? targetRot : _neutralRotation,
            1f - Mathf.Exp(-speed * dt)
        );
    }

    /// <summary>
    /// chức năng : Tính toán lại center và radius khi resolution thay đổi hoặc khi bật joystick nổi (floating joystick).
    /// </summary>
    private void RecalculateScreenMapping()
    {
        /// <summary>
        /// Cache screen center & radius in pixels.
        /// </summary>
        _screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        _radiusPx = Mathf.Min(Screen.width, Screen.height) * _screenRadiusFactor;
    }

    /// <summary>
    /// kiểm tra nếu resolution thay đổi ở runtime, recompute center/radius.
    /// </summary>
    private void RecalculateScreenMappingIfNeeded()
    {
        /// <summary>
        /// If resolution changes at runtime, recompute center/radius.
        /// </summary>
        if (_screenCenter.x != Screen.width * 0.5f || _screenCenter.y != Screen.height * 0.5f)
            RecalculateScreenMapping();
    }

    /// <summary>
    /// Đọc trạng thái con trỏ (chuột hoặc chạm) một cách thống nhất, trả về: - isDown: vừa bắt đầu nhấn, - isHeld: đang giữ, - isUp: vừa thả, - screenPos: vị trí con trỏ
    /// </summary>
    /// <param name="isDown"></param>
    /// <param name="isHeld"></param>
    /// <param name="isUp"></param>
    /// <param name="screenPos"></param>
    private void ReadPointerState(out bool isDown, out bool isHeld, out bool isUp, out Vector2 screenPos)
    {
        /// <summary>
        /// Unified mouse/touch reading.
        /// </summary>
        isDown = isHeld = isUp = false;
        screenPos = default;

        // Touch preferred on mobile
        if (Input.touchCount > 0)
        {
            Touch t = Input.GetTouch(0);
            screenPos = t.position;

            isDown = t.phase == TouchPhase.Began;
            isHeld = t.phase == TouchPhase.Moved || t.phase == TouchPhase.Stationary;
            isUp = t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled;
            return;
        }

        // Mouse fallback
        screenPos = Input.mousePosition;
        isDown = Input.GetMouseButtonDown(0);
        isHeld = Input.GetMouseButton(0);
        isUp = Input.GetMouseButtonUp(0);
    }

    private void DrawDebug()
    {
        /// <summary>
        /// Debug draw joystick origin and forward direction.
        /// </summary>
        Vector3 p = transform.position;
        Debug.DrawLine(p, p + new Vector3(_input01.x, _input01.y, 0f), Color.cyan);
        Debug.DrawLine(p, p + transform.forward * 1.5f, Color.green);
    }




}

// Event
public partial class PlayerSwipeMoveZ180
{
    public override void SubscribeEvents()
    {
        // Session event
        CoreEvents.OnMenu.Subscribe(e => OnMenu(e), Binder);
        CoreEvents.OnNewSession.Subscribe(_ => PrepareNewSession(), Binder);
        CoreEvents.OnSession.Subscribe(_ => OnSession(), Binder);
        CoreEvents.OnPrepareEnd.Subscribe(_ => PrepareEndSession(), Binder);
        CoreEvents.OnEndSession.Subscribe(_ => EndSession(), Binder);
        CoreEvents.OnPauseSession.Subscribe(_ => PauseSession(), Binder);
        CoreEvents.OnResumeSession.Subscribe(_ => ResumeSession(), Binder);
        CoreEvents.OnQuitSession.Subscribe(_ => QuitSession(), Binder);
        CoreEvents.PlayerFife.Subscribe(e => OnPlayerFife(e), Binder);
    }
}

// Profession
public partial class PlayerSwipeMoveZ180
{
    bool _isSessionInputEnabled;
    bool AllowInput => _isSessionInputEnabled && _isPlayerLife;

    private void OnMenu(OnMenuEvent e)
    {
        if (e == null || !e.IsOpenMenu) return;

        _isSessionInputEnabled = false;
        _isPlayerLife = false;
        ForceStopInput();
        StopReturnRoutine();
        ResetPlayerToOrigin();
    }

    private void PrepareNewSession()
    {
        _isSessionInputEnabled = false;
        _isPlayerLife = false;
        ForceStopInput();
    }

    private void OnSession()
    {
        _isSessionInputEnabled = true;
    }

    private void PauseSession()
    {
        _isSessionInputEnabled = false;
        ForceStopInput();
    }

    private void ResumeSession()
    {
        _isSessionInputEnabled = true;
    }

    private void PrepareEndSession()
    {
        _isSessionInputEnabled = false;
        ForceStopInput();

        // dừng coroutine đang chạy nếu có để tránh xung đột
        StopReturnRoutine();
        _returnRoutine = StartCoroutine(MoveToPositionSmooth(
            target: Vector3.zero,
            smoothTime: 0.25f,
            maxSpeed: 10f,
            snapDistance: 0.001f,
            onComplete: () =>
            {
                _returnRoutine = null;
                // Nếu cần: mở input lại hoặc trigger bước tiếp theo
                // _isSessionInputEnabled = true;
            }
        ));
    }
    private void EndSession()
    {
        _isSessionInputEnabled = false;
        ForceStopInput();
    }

    private void QuitSession()
    {
        _isSessionInputEnabled = false;
        _isPlayerLife = false;
        ForceStopInput();
    }

    private void OnPlayerFife(PlayerFifeEvent e)
    {
        if (e == null) return;

        switch (e.state)
        {
            case PlayerFifeState.Life:
                _isPlayerLife = true;
                break;
            case PlayerFifeState.dead:
                _isPlayerLife = false;
                ForceStopInput();
                break;
        }
    }

    private Coroutine _returnRoutine;

    private void StopReturnRoutine()
    {
        if (_returnRoutine == null) return;

        StopCoroutine(_returnRoutine);
        _returnRoutine = null;
    }

    private void ResetPlayerToOrigin()
    {
        transform.position = Vector3.zero;

        if (TryGetComponent<Rigidbody>(out var rb))
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        if (TryGetComponent<Rigidbody2D>(out var rb2d))
        {
            rb2d.linearVelocity = Vector2.zero;
            rb2d.angularVelocity = 0f;
        }
    }

    /// <summary>
    /// Smoothly moves transform.position to target using SmoothDamp (no teleport).
    /// </summary>
    /// <remarks>
    /// - Uses SmoothDamp for natural easing.
    /// - Snaps to target when close enough to avoid float jitter.
    /// - Stops automatically when reached.
    /// </remarks>
    /// <param name="target">Target world position.</param>
    /// <param name="smoothTime">Smoothing time in seconds (smaller = faster).</param>
    /// <param name="maxSpeed">Max move speed (units/sec).</param>
    /// <param name="snapDistance">Snap to target if within this distance.</param>
    /// <param name="onComplete">Callback when finished.</param>
    /// <returns>IEnumerator for coroutine.</returns>
    private System.Collections.IEnumerator MoveToPositionSmooth(
        Vector3 target,
        float smoothTime,
        float maxSpeed,
        float snapDistance,
        System.Action onComplete = null
    )
    {
        Vector3 velocity = Vector3.zero;

        while (true)
        {
            Vector3 current = transform.position;

            if ((current - target).sqrMagnitude <= snapDistance * snapDistance)
            {
                transform.position = target;
                onComplete?.Invoke();
                yield break;
            }

            // SmoothDamp is frame-rate independent when using deltaTime
            transform.position = Vector3.SmoothDamp(
                current,
                target,
                ref velocity,
                smoothTime,
                maxSpeed,
                Time.deltaTime
            );

            yield return null;
        }
    }
}
