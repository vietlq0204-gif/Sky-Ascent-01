public interface IAdSettings
{
    AdProviderType Provider { get; }
    bool AdsEnabled { get; }
    bool AutoCreateManagerAtRuntime { get; }
    bool AutoInitializeOnStart { get; }
    bool DontDestroyManagerOnLoad { get; }
    bool AutoRetryLoadAfterFailure { get; }
    float RetryLoadDelaySeconds { get; }
    bool VerboseLogging { get; }
    string AndroidAppId { get; }
    string IosAppId { get; }
    BannerAdSizeType BannerSize { get; }
    BannerScreenPositionType BannerPosition { get; }
    bool ShouldShowBannerWhenMenuOpens { get; }
    bool ShouldHideBannerWhenMenuCloses { get; }
    BannerScreenPositionType NativeOverlayPosition { get; }
    bool ShouldShowNativeOverlayWhenMenuOpens { get; }
    bool ShouldHideNativeOverlayWhenMenuCloses { get; }
    bool SimulateRewardInEditorWithoutSdk { get; }
    float EditorSimulatedRewardDelay { get; }

    bool IsFormatEnabled(AdFormatType format);
    bool HasConfiguredAdUnitId(AdFormatType format);
    string GetAdUnitId(AdFormatType format);
    bool ShouldPreloadOnInitialize(AdFormatType format);
    bool ShouldPreloadWhenMenuOpens(AdFormatType format);
    bool ShouldPreloadDuringLoading(AdFormatType format);
    bool ShouldPersistAcrossScenes(AdFormatType format);
    string[] GetTestDeviceIdsForCurrentPlatform();
}
