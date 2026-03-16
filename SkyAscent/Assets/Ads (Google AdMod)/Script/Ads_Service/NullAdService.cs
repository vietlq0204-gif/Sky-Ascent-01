using System;

public sealed class NullAdService : IAdService
{
    private readonly Action<Action> _dispatchToMainThread;

    private IAdSettings _settings;
    private bool _isInitialized;

    public NullAdService(Action<Action> dispatchToMainThread)
    {
        _dispatchToMainThread = dispatchToMainThread;
    }

    public event Action<AdServiceResult> ResultReceived;

    public bool IsInitialized => _isInitialized;

    public void Configure(IAdSettings settings)
    {
        _settings = settings;
    }

    public void Initialize()
    {
        _isInitialized = true;
        PublishResult(
            AdFormatType.None,
            AdServiceResultType.Disabled,
            BuildProviderMessage());
    }

    public void Preload(AdFormatType format)
    {
    }

    public void Show(AdFormatType format)
    {
        PublishResult(
            format,
            AdServiceResultType.Disabled,
            BuildProviderMessage());
    }

    public void Hide(AdFormatType format)
    {
    }

    public void Destroy(AdFormatType format)
    {
    }

    public void DestroyAll()
    {
    }

    public bool IsReady(AdFormatType format)
    {
        return false;
    }

    private string BuildProviderMessage()
    {
        if (_settings == null)
            return "Ad settings are missing.";

        if (!_settings.AdsEnabled)
            return "Ads are disabled in settings.";

        return _settings.Provider == AdProviderType.None
            ? "Ad provider is set to None."
            : $"Provider {_settings.Provider} is not implemented in this build.";
    }

    private void PublishResult(
        AdFormatType format,
        AdServiceResultType resultType,
        string message)
    {
        void Raise()
        {
            ResultReceived?.Invoke(new AdServiceResult(format, resultType, message));
        }

        if (_dispatchToMainThread != null)
        {
            _dispatchToMainThread(Raise);
            return;
        }

        Raise();
    }
}
