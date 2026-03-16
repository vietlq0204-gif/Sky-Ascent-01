using System;

public interface IAdService
{
    event Action<AdServiceResult> ResultReceived;

    bool IsInitialized { get; }

    void Configure(IAdSettings settings);
    void Initialize();
    void Preload(AdFormatType format);
    void Show(AdFormatType format);
    void Hide(AdFormatType format);
    void Destroy(AdFormatType format);
    void DestroyAll();
    bool IsReady(AdFormatType format);
}
