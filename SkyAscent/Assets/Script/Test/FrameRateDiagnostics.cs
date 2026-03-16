using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Profiling;
using Unity.Profiling;

/// <summary>
/// Hiển thị các thông số chẩn đoán frame rate trên Android ra TMP Text.
/// </summary>
/// <remarks>
/// Dùng để kiểm tra app có đang bị khóa FPS, bị ảnh hưởng bởi frame pacing,
/// refresh rate, thiết lập render, độ phân giải, hoặc bottleneck CPU/GPU hay không.
/// </remarks>
public class FrameRateDiagnostics : MonoBehaviour
{
    [Header("Target Settings")]
    [SerializeField] private int targetFps = 60;
    [SerializeField] private int renderFrameInterval = 1;

    [Header("Resolution Settings")]
    [SerializeField] private bool applyCustomScreenResolution = false;
    [SerializeField] private int screenWidth = 1280;
    [SerializeField] private int screenHeight = 720;
    [SerializeField] private bool fullscreen = true;

    [Header("Render Scale Settings")]
    [SerializeField] private bool applyRenderScale = false;
    [SerializeField][Range(0.25f, 1.5f)] private float renderScale = 1.0f;

    [Header("UI")]
    [SerializeField] private TMP_Text outputText;
    [SerializeField] private float refreshInterval = 0.5f;

    private readonly StringBuilder _builder = new StringBuilder(1024);

    private float _timeCounter;
    private int _frameCounter;
    private float _presentedFps;
    private float _presentedFrameMs;

    private float _cpuFrameMs;
    private float _gpuFrameMs;
    private float _cpuEstimatedFps;
    private float _gpuEstimatedFps;
    private float _estimatedBottleneckFps;

    private ProfilerRecorder _cpuFrameTimeRecorder;
    private ProfilerRecorder _gpuFrameTimeRecorder;
    private bool _hasCpuRecorder;
    private bool _hasGpuRecorder;

    /// <summary>
    /// Thiết lập các giá trị chẩn đoán khi ứng dụng khởi động.
    /// </summary>
    /// <remarks>
    /// Dùng để loại trừ khả năng app đang bị khóa FPS từ phía code hoặc do cấu hình render.
    /// </remarks>
    private void Awake()
    {
        ApplyRuntimeSettings();
        StartProfilers();
    }

    /// <summary>
    /// Giải phóng recorder khi object bị disable.
    /// </summary>
    private void OnDisable()
    {
        DisposeProfilers();
    }

    /// <summary>
    /// Kiểm tra dữ liệu Inspector hợp lệ.
    /// </summary>
    private void OnValidate()
    {
        refreshInterval = Mathf.Max(0.1f, refreshInterval);

        targetFps = Mathf.Max(-1, targetFps);
        renderFrameInterval = Mathf.Max(1, renderFrameInterval);

        screenWidth = Mathf.Max(64, screenWidth);
        screenHeight = Mathf.Max(64, screenHeight);

        renderScale = Mathf.Clamp(renderScale, 0.25f, 1.5f);
    }

    /// <summary>
    /// Cập nhật FPS thực tế và nội dung hiển thị theo chu kỳ.
    /// </summary>
    private void Update()
    {
        _timeCounter += Time.unscaledDeltaTime;
        _frameCounter++;

        if (_timeCounter < refreshInterval)
        {
            return;
        }

        _presentedFps = _frameCounter / _timeCounter;
        _presentedFrameMs = 1000f / Mathf.Max(_presentedFps, 0.0001f);

        _timeCounter = 0f;
        _frameCounter = 0;

        UpdateProfilerValues();
        UpdateOutputText();
    }

    /// <summary>
    /// Áp dụng các thiết lập runtime liên quan đến FPS và độ phân giải.
    /// </summary>
    /// <remarks>
    /// - vSyncCount thường không có tác dụng trên mobile như trên PC, nhưng vẫn tắt để loại trừ.
    /// - Screen.SetResolution trên Android có thể không luôn phản ánh như desktop.
    /// - Render scale thường hữu ích hơn khi debug hiệu năng.
    /// </remarks>
    /// <returns>Không trả về giá trị.</returns>
    private void ApplyRuntimeSettings()
    {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = targetFps;
        OnDemandRendering.renderFrameInterval = renderFrameInterval;

        if (applyCustomScreenResolution)
        {
            Screen.SetResolution(screenWidth, screenHeight, fullscreen);
        }

        if (applyRenderScale)
        {
            ScalableBufferManager.ResizeBuffers(renderScale, renderScale);
        }
        else
        {
            ScalableBufferManager.ResizeBuffers(1.0f, 1.0f);
        }
    }

    /// <summary>
    /// Khởi tạo profiler recorder cho CPU và GPU frame time.
    /// </summary>
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
            _cpuFrameTimeRecorder = ProfilerRecorder.StartNew(
                ProfilerCategory.Internal,
                "CPU Main Thread Frame Time");

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
    /// <remarks>
    /// Một số thiết bị hoặc backend có thể không hỗ trợ.
    /// </remarks>
    /// <returns>Không trả về giá trị.</returns>
    private void TryStartGpuRecorder()
    {
        try
        {
            _gpuFrameTimeRecorder = ProfilerRecorder.StartNew(
                ProfilerCategory.Render,
                "GPU Frame Time");

            _hasGpuRecorder = _gpuFrameTimeRecorder.Valid;
        }
        catch
        {
            _hasGpuRecorder = false;
        }
    }

    /// <summary>
    /// Cập nhật giá trị CPU/GPU frame time và FPS ước tính.
    /// </summary>
    /// <remarks>
    /// Presented FPS là FPS đang thực sự được hiển thị/present.
    /// Estimated FPS là FPS ước tính theo khả năng CPU/GPU nếu không bị khóa bởi refresh rate hoặc pacing.
    /// </remarks>
    /// <returns>Không trả về giá trị.</returns>
    private void UpdateProfilerValues()
    {
        if (_hasCpuRecorder && _cpuFrameTimeRecorder.Valid)
        {
            _cpuFrameMs = NanosecondsToMilliseconds(_cpuFrameTimeRecorder.LastValue);
            _cpuEstimatedFps = ConvertMsToFps(_cpuFrameMs);
        }
        else
        {
            _cpuFrameMs = 0f;
            _cpuEstimatedFps = 0f;
        }

        if (_hasGpuRecorder && _gpuFrameTimeRecorder.Valid)
        {
            _gpuFrameMs = NanosecondsToMilliseconds(_gpuFrameTimeRecorder.LastValue);
            _gpuEstimatedFps = ConvertMsToFps(_gpuFrameMs);
        }
        else
        {
            _gpuFrameMs = 0f;
            _gpuEstimatedFps = 0f;
        }

        float bottleneckMs = 0f;

        if (_cpuFrameMs > 0f && _gpuFrameMs > 0f)
        {
            bottleneckMs = Mathf.Max(_cpuFrameMs, _gpuFrameMs);
        }
        else if (_cpuFrameMs > 0f)
        {
            bottleneckMs = _cpuFrameMs;
        }
        else if (_gpuFrameMs > 0f)
        {
            bottleneckMs = _gpuFrameMs;
        }

        _estimatedBottleneckFps = ConvertMsToFps(bottleneckMs);
    }

    /// <summary>
    /// Ghi toàn bộ thông tin chẩn đoán ra TMP Text.
    /// </summary>
    /// <remarks>
    /// Nếu chưa gán outputText thì hàm sẽ thoát an toàn.
    /// </remarks>
    /// <returns>Không trả về giá trị.</returns>
    private void UpdateOutputText()
    {
        if (outputText == null)
        {
            return;
        }

        Resolution resolution = Screen.currentResolution;

        _builder.Clear();
        _builder.AppendLine("   === Frame Rate Diagnostics ===");
        _builder.AppendLine();

        _builder.AppendLine("[Presented / Measured]");
        _builder.AppendLine($"Presented FPS: {_presentedFps:0.0}");
        _builder.AppendLine($"Presented Frame: {_presentedFrameMs:0.00} ms");
        _builder.AppendLine();

        _builder.AppendLine("[Estimated Uncapped]");
        _builder.AppendLine($"CPU Frame: {FormatMsOrNa(_cpuFrameMs)}");
        _builder.AppendLine($"GPU Frame: {FormatMsOrNa(_gpuFrameMs)}");
        //_builder.AppendLine($"CPU Estimated FPS: {FormatFpsOrNa(_cpuEstimatedFps)}");
        //_builder.AppendLine($"GPU Estimated FPS: {FormatFpsOrNa(_gpuEstimatedFps)}");
        _builder.AppendLine($"Estimated Bottleneck FPS: {FormatFpsOrNa(_estimatedBottleneckFps)}");
        _builder.AppendLine();

        _builder.AppendLine("[Limits / Sync]");
        //_builder.AppendLine($"Application.targetFrameRate: {Application.targetFrameRate}");
        _builder.AppendLine($"QualitySettings.vSyncCount: {QualitySettings.vSyncCount}");
        _builder.AppendLine($"OnDemandRendering.renderFrameInterval: {OnDemandRendering.renderFrameInterval}");
        _builder.AppendLine($"Screen.refreshRate: {resolution.refreshRateRatio.value:0.##} Hz");
        _builder.AppendLine();

        _builder.AppendLine("[Resolution]");
        _builder.AppendLine($"Screen.currentResolution: {resolution.width} x {resolution.height}");
        //_builder.AppendLine($"Screen Size: {Screen.width} x {Screen.height}");
        //_builder.AppendLine($"Orientation: {Screen.orientation}");
        //_builder.AppendLine($"Custom Screen Resolution: {(applyCustomScreenResolution ? "ON" : "OFF")}");
        //_builder.AppendLine($"Requested Screen Resolution: {screenWidth} x {screenHeight}");
        //_builder.AppendLine($"Custom Render Scale: {(applyRenderScale ? renderScale.ToString("0.00") : "OFF")}");
        _builder.AppendLine();

        _builder.AppendLine("[Graphics]");
        _builder.AppendLine($"Graphics API: {SystemInfo.graphicsDeviceType}");
        _builder.AppendLine($"Graphics Device: {SystemInfo.graphicsDeviceName}");
        _builder.AppendLine($"Graphics Vendor: {SystemInfo.graphicsDeviceVendor}");
        _builder.AppendLine($"Graphics Memory: {SystemInfo.graphicsMemorySize} MB");
        _builder.AppendLine();

        //_builder.AppendLine("[Device]");
        //_builder.AppendLine($"Device Model: {SystemInfo.deviceModel}");
        //_builder.AppendLine($"Device Name: {SystemInfo.deviceName}");
        //_builder.AppendLine($"Operating System: {SystemInfo.operatingSystem}");
        //_builder.AppendLine($"Processor: {SystemInfo.processorType}");
        //_builder.AppendLine($"CPU Cores: {SystemInfo.processorCount}");
        _builder.AppendLine($"System Memory: {SystemInfo.systemMemorySize} MB");

        outputText.text = _builder.ToString();
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
    /// Chuyển nano giây sang mili giây.
    /// </summary>
    /// <param name="nanoseconds">Giá trị nano giây.</param>
    /// <returns>Giá trị mili giây.</returns>
    private float NanosecondsToMilliseconds(long nanoseconds)
    {
        return nanoseconds / 1_000_000f;
    }

    /// <summary>
    /// Quy đổi frame time mili giây sang FPS.
    /// </summary>
    /// <param name="frameMs">Frame time tính theo mili giây.</param>
    /// <returns>FPS ước tính. Trả về 0 nếu dữ liệu không hợp lệ.</returns>
    private float ConvertMsToFps(float frameMs)
    {
        if (frameMs <= 0.0001f)
        {
            return 0f;
        }

        return 1000f / frameMs;
    }

    /// <summary>
    /// Định dạng FPS hoặc N/A.
    /// </summary>
    /// <param name="fps">Giá trị FPS.</param>
    /// <returns>Chuỗi đã định dạng.</returns>
    private string FormatFpsOrNa(float fps)
    {
        return fps > 0f ? fps.ToString("0.0") : "N/A";
    }

    /// <summary>
    /// Định dạng mili giây hoặc N/A.
    /// </summary>
    /// <param name="ms">Giá trị mili giây.</param>
    /// <returns>Chuỗi đã định dạng.</returns>
    private string FormatMsOrNa(float ms)
    {
        return ms > 0f ? $"{ms:0.00} ms" : "N/A";
    }

    /// <summary>
    /// Áp dụng lại setting runtime bằng tay.
    /// </summary>
    /// <remarks>
    /// Có thể gọi từ button debug hoặc script khác khi muốn đổi setting lúc runtime.
    /// </remarks>
    /// <returns>Không trả về giá trị.</returns>
    public void ReapplySettings()
    {
        ApplyRuntimeSettings();
    }
}
