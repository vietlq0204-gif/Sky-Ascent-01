using UnityEngine;
using UnityEngine.Profiling;
using Unity.Profiling;

/// <summary>
/// Hiển thị overlay hiệu năng đơn giản bằng IMGUI.
/// </summary>
/// <remarks>
/// Dùng cho debug trong Editor hoặc Development Build.
/// Có hỗ trợ:
/// - FPS
/// - CPU frame time
/// - GPU frame time (nếu platform hỗ trợ)
/// - RAM usage
/// </remarks>
public class SimplePerformanceOverlay : MonoBehaviour
{
    [SerializeField] private bool showAtStart = true;
    [SerializeField] private int targetFps = 60;
    [SerializeField] private float refreshInterval = 0.5f;
    [SerializeField] private Vector2 offset = new Vector2(20f, 20f);
    [SerializeField] private Vector2 boxSize = new Vector2(320f, 120f);
    [SerializeField] private int fontSize = 18;
    [SerializeField] private Color textColor = Color.white;

    private float _timeCounter;
    private int _frameCounter;
    private float _currentFps;
    private float _currentFrameMs;
    private float _cpuFrameMs;
    private float _gpuFrameMs;
    private long _monoUsedBytes; // Bộ nhớ Mono đã sử dụng (chủ yếu là heap của C#)
    private long _totalAllocatedBytes; // Tổng bộ nhớ đã cấp phát (bao gồm cả đã giải phóng nhưng chưa thu hồi)

    private long
        _totalReservedBytes; // Tổng bộ nhớ đã cấp phát và chưa giải phóng (bao gồm cả đã giải phóng nhưng chưa thu hồi)

    private bool _isVisible;
    private GUIStyle _labelStyle;

    private ProfilerRecorder _cpuFrameTimeRecorder;
    private ProfilerRecorder _gpuFrameTimeRecorder;
    private bool _hasCpuRecorder;
    private bool _hasGpuRecorder;

    /// <summary>
    /// Thiết lập trạng thái ban đầu và khởi tạo recorder.
    /// </summary>
    private void Awake()
    {
        Application.targetFrameRate = targetFps;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        gameObject.SetActive(true);
        _isVisible = showAtStart;
        StartProfilers();
#else
        gameObject.SetActive(false);
#endif
    }

    /// <summary>
    /// Đảm bảo dữ liệu inspector hợp lệ.
    /// </summary>
    private void OnValidate()
    {
        targetFps = Mathf.Max(-1, targetFps);
        refreshInterval = Mathf.Max(0.1f, refreshInterval);
        fontSize = Mathf.Max(8, fontSize);
        boxSize.x = Mathf.Max(150f, boxSize.x);
        boxSize.y = Mathf.Max(80f, boxSize.y);

        InvalidateStyle();
    }

    /// <summary>
    /// Giải phóng profiler recorder khi object bị hủy.
    /// </summary>
    private void OnDisable()
    {
        DisposeProfilers();
    }

    /// <summary>
    /// Tính toán dữ liệu hiệu năng theo chu kỳ lấy mẫu.
    /// </summary>
    private void Update()
    {
        if (!_isVisible)
        {
            return;
        }

        _timeCounter += Time.unscaledDeltaTime;
        _frameCounter++;

        if (_timeCounter < refreshInterval)
        {
            return;
        }

        _currentFps = _frameCounter / _timeCounter;
        _currentFrameMs = 1000f / Mathf.Max(_currentFps, 0.0001f);

        UpdateProfilerValues();

        _timeCounter = 0f;
        _frameCounter = 0;
    }

    /// <summary>
    /// Vẽ thông tin hiệu năng lên màn hình.
    /// </summary>
    /// <remarks>
    /// Chỉ nên dùng cho debug.
    /// </remarks>
    private void OnGUI()
    {
        if (!_isVisible)
        {
            return;
        }

        EnsureStyle();

        string gpuText = _hasGpuRecorder ? $"{_gpuFrameMs:0.00} ms" : "N/A";

        string content =
                $"FPS: {_currentFps:0.0}\n" +
                // $"Frame: {_currentFrameMs:0.00} ms\n" +
                $"CPU: {_cpuFrameMs:0.00} ms\n" +
                $"GPU: {gpuText}\n"
            // +
            // $"RAM Alloc: {FormatBytesToMb(_totalAllocatedBytes)}\n" +
            // $"RAM Reserve: {FormatBytesToMb(_totalReservedBytes)}\n" +
            // $"Mono Used: {FormatBytesToMb(_monoUsedBytes)}"
            ;

        GUI.Label(
            new Rect(offset.x, offset.y, boxSize.x, boxSize.y),
            content,
            _labelStyle);
    }

    /// <summary>
    /// Bật hoặc tắt overlay.
    /// </summary>
    /// <param name="isVisible">True để hiện, false để ẩn.</param>
    public void SetVisible(bool isVisible)
    {
        _isVisible = isVisible;
    }

    /// <summary>
    /// Khởi tạo profiler recorder cho CPU và GPU frame time.
    /// </summary>
    /// <remarks>
    /// GPU recorder có thể không khả dụng trên một số nền tảng.
    /// </remarks>
    /// <returns>Không trả về giá trị.</returns>
    private void StartProfilers()
    {
        DisposeProfilers();

        TryStartCpuRecorder();
        TryStartGpuRecorder();
    }

    /// <summary>
    /// Thử khởi tạo CPU frame time recorder.
    /// </summary>
    /// <returns>Không trả về giá trị.</returns>
    private void TryStartCpuRecorder()
    {
        try
        {
            _cpuFrameTimeRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "CPU Main Thread Frame Time");
            _hasCpuRecorder = _cpuFrameTimeRecorder.Valid;
        }
        catch
        {
            _hasCpuRecorder = false;
        }
    }

    /// <summary>
    /// Thử khởi tạo GPU frame time recorder.
    /// </summary>
    /// <returns>Không trả về giá trị.</returns>
    private void TryStartGpuRecorder()
    {
        try
        {
            _gpuFrameTimeRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "GPU Frame Time");
            _hasGpuRecorder = _gpuFrameTimeRecorder.Valid;
        }
        catch
        {
            _hasGpuRecorder = false;
        }
    }

    /// <summary>
    /// Cập nhật các chỉ số profiler hiện tại.
    /// </summary>
    /// <returns>Không trả về giá trị.</returns>
    private void UpdateProfilerValues()
    {
        if (_hasCpuRecorder && _cpuFrameTimeRecorder.Valid)
        {
            _cpuFrameMs = NanosecondsToMilliseconds(_cpuFrameTimeRecorder.LastValue);
        }
        else
        {
            _cpuFrameMs = _currentFrameMs;
        }

        if (_hasGpuRecorder && _gpuFrameTimeRecorder.Valid)
        {
            _gpuFrameMs = NanosecondsToMilliseconds(_gpuFrameTimeRecorder.LastValue);
        }

        _monoUsedBytes = Profiler.GetMonoUsedSizeLong();
        _totalAllocatedBytes = Profiler.GetTotalAllocatedMemoryLong();
        _totalReservedBytes = Profiler.GetTotalReservedMemoryLong();
    }

    /// <summary>
    /// Giải phóng các recorder hiện tại.
    /// </summary>
    /// <returns>Không trả về giá trị.</returns>
    private void DisposeProfilers()
    {
        if (_cpuFrameTimeRecorder.Valid)
        {
            _cpuFrameTimeRecorder.Dispose();
        }

        if (_gpuFrameTimeRecorder.Valid)
        {
            _gpuFrameTimeRecorder.Dispose();
        }

        _hasCpuRecorder = false;
        _hasGpuRecorder = false;
    }

    /// <summary>
    /// Đánh dấu style cần tạo lại.
    /// </summary>
    /// <returns>Không trả về giá trị.</returns>
    private void InvalidateStyle()
    {
        _labelStyle = null;
    }

    /// <summary>
    /// Đảm bảo style đã được tạo.
    /// </summary>
    /// <returns>Không trả về giá trị.</returns>
    private void EnsureStyle()
    {
        if (_labelStyle != null)
        {
            return;
        }

        CreateStyle();
    }

    /// <summary>
    /// Tạo style hiển thị cho label.
    /// </summary>
    /// <returns>Không trả về giá trị.</returns>
    private void CreateStyle()
    {
        _labelStyle = new GUIStyle
        {
            fontSize = fontSize,
            richText = false,
            alignment = TextAnchor.UpperLeft,
            wordWrap = false
        };

        _labelStyle.normal.textColor = textColor;
    }

    /// <summary>
    /// Chuyển byte sang MB để hiển thị dễ đọc hơn.
    /// </summary>
    /// <param name="bytes">Số byte cần chuyển đổi.</param>
    /// <returns>Chuỗi định dạng MB.</returns>
    private string FormatBytesToMb(long bytes)
    {
        float mb = bytes / (1024f * 1024f);
        return $"{mb:0.00} MB";
    }

    /// <summary>
    /// Chuyển nano giây sang mili giây.
    /// </summary>
    /// <param name="nanoseconds">Giá trị nano giây.</param>
    /// <returns>Giá trị mili giây.</returns>
    private float NanosecondsToMilliseconds(long nanoseconds)
    {
        return nanoseconds / 1_000_000f;
    }
}