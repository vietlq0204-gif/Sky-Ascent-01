using UnityEngine;

/// <summary>
/// Input/Behavior Config:
/// - Chỉ chứa thông số (SerializeField).
/// - Không chứa runtime state.
/// </summary>
public sealed class ViewObjectOnPathInput : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private float _pixelsForFullPath = 800f;
    [SerializeField] private bool _useOnlyHorizontal = true;
    [SerializeField] private bool _invertSwipe = false;
    [SerializeField] private float _speed = 1f;

    [Header("Behavior")]
    [SerializeField] private DragBehaviorMode _dragMode = DragBehaviorMode.Inertia;

    [Header("Inertia")]
    [SerializeField] private float _inertiaDamping = 5f;
    [SerializeField] private float _minVelocity = 0.01f;

    [Header("FixPoint")]
    [SerializeField] private float _fixPointSwipeThreshold = 80f;
    [SerializeField] private FixPointCooldownMode _fixPointCooldownMode = FixPointCooldownMode.Time;
    [SerializeField] private bool _fixPointCooldown = true;
    [SerializeField] private float _fixPointCooldownTime = 0.08f;
    [SerializeField] private float _fixPointSmoothTime = 0.08f;
    [SerializeField] private float _fixPointStopEps = 0.0005f;

    /// <summary>Pixels tương ứng với 1 vòng full-path.</summary>
    public float PixelsForFullPath => _pixelsForFullPath;
    /// <summary>Chỉ lấy delta.x khi drag.</summary>
    public bool UseOnlyHorizontal => _useOnlyHorizontal;
    /// <summary>Đảo chiều swipe.</summary>
    public bool InvertSwipe => _invertSwipe;
    /// <summary>Multiplier tốc độ drag.</summary>
    public float Speed => _speed;

    /// <summary>Mode drag behavior.</summary>
    public DragBehaviorMode DragMode => _dragMode;

    /// <summary>Damping inertia.</summary>
    public float InertiaDamping => _inertiaDamping;
    /// <summary>Ngưỡng vận tốc tối thiểu để inertia chạy.</summary>
    public float MinVelocity => _minVelocity;

    /// <summary>Ngưỡng swipe để nhảy fixpoint.</summary>
    public float FixPointSwipeThreshold => _fixPointSwipeThreshold;
    /// <summary>Cooldown mode.</summary>
    public FixPointCooldownMode FixPointCooldownMode => _fixPointCooldownMode;
    /// <summary>Bật cooldown fixpoint.</summary>
    public bool FixPointCooldown => _fixPointCooldown;
    /// <summary>Cooldown time.</summary>
    public float FixPointCooldownTime => _fixPointCooldownTime;
    /// <summary>Smooth time cho fixpoint anim.</summary>
    public float FixPointSmoothTime => _fixPointSmoothTime;
    /// <summary>Eps stop cho fixpoint anim.</summary>
    public float FixPointStopEps => _fixPointStopEps;

    [Header("Auto Snap (Normal/Inertia)")]
    [SerializeField] private bool _autoSnapToMiddle = true;
    [SerializeField] private float _autoSnapSmoothTime = 0.08f;
    [SerializeField] private float _autoSnapStopEps = 0.0005f;

    /// <summary>Bật auto snap về middle sau khi dừng (Normal/Inertia).</summary>
    public bool AutoSnapToMiddle => _autoSnapToMiddle;

    /// <summary>Smooth time khi auto snap.</summary>
    public float AutoSnapSmoothTime => _autoSnapSmoothTime;

    /// <summary>Eps để kết thúc auto snap.</summary>
    public float AutoSnapStopEps => _autoSnapStopEps;
}