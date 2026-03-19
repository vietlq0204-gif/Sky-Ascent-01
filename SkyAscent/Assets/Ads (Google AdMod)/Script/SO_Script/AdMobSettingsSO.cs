using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class AdFormatSettings
{
    [SerializeField] private bool _enabled;
    [SerializeField] private bool _preloadOnInitialize = true;
    [SerializeField] private bool _preloadWhenMenuOpens = true;
    [SerializeField] private bool _preloadDuringLoading = true;
    [SerializeField] private bool _persistAcrossScenes;
    [SerializeField] private string _androidAdUnitId = string.Empty;
    [SerializeField] private string _iosAdUnitId = string.Empty;

    public bool Enabled => _enabled;
    public bool PreloadOnInitialize => _preloadOnInitialize;
    public bool PreloadWhenMenuOpens => _preloadWhenMenuOpens;
    public bool PreloadDuringLoading => _preloadDuringLoading;
    public bool PersistAcrossScenes => _persistAcrossScenes;
    public string AndroidAdUnitId => (_androidAdUnitId ?? string.Empty).Trim();
    public string IosAdUnitId => (_iosAdUnitId ?? string.Empty).Trim();
    public bool HasAndroidAdUnitId => !string.IsNullOrWhiteSpace(AndroidAdUnitId);
    public bool HasIosAdUnitId => !string.IsNullOrWhiteSpace(IosAdUnitId);

    public string GetAdUnitIdForCurrentPlatform()
    {
#if UNITY_IOS
        return IosAdUnitId;
#elif UNITY_ANDROID
        return AndroidAdUnitId;
#else
        if (!string.IsNullOrWhiteSpace(AndroidAdUnitId))
            return AndroidAdUnitId;

        return IosAdUnitId;
#endif
    }

    public void EnsureDefaults()
    {
        _androidAdUnitId ??= string.Empty;
        _iosAdUnitId ??= string.Empty;
    }
}

[Serializable]
public sealed class BannerAdFormatSettings : AdFormatSettings
{
    [SerializeField] private BannerAdSizeType _bannerSize = BannerAdSizeType.Banner;
    [SerializeField] private BannerScreenPositionType _bannerPosition = BannerScreenPositionType.Bottom;
    [SerializeField] private bool _showWhenMenuOpens = true;
    [SerializeField] private bool _hideWhenMenuCloses = true;

    public BannerAdSizeType BannerSize => _bannerSize;
    public BannerScreenPositionType BannerPosition => _bannerPosition;
    public bool ShowWhenMenuOpens => _showWhenMenuOpens;
    public bool HideWhenMenuCloses => _hideWhenMenuCloses;
}

[Serializable]
public sealed class RewardedAdFormatSettings : AdFormatSettings
{
    [SerializeField] private bool _simulateRewardInEditorWithoutSdk = true;
    [SerializeField, Min(0.1f)] private float _editorSimulatedRewardDelay = 1.5f;

    public bool SimulateRewardInEditorWithoutSdk => _simulateRewardInEditorWithoutSdk;
    public float EditorSimulatedRewardDelay => Mathf.Max(_editorSimulatedRewardDelay, 0.1f);
}

[Serializable]
public sealed class NativeOverlayAdFormatSettings : AdFormatSettings
{
    [SerializeField] private BannerScreenPositionType _templatePosition = BannerScreenPositionType.Bottom;
    [SerializeField] private bool _showWhenMenuOpens;
    [SerializeField] private bool _hideWhenMenuCloses = true;

    public BannerScreenPositionType TemplatePosition => _templatePosition;
    public bool ShowWhenMenuOpens => _showWhenMenuOpens;
    public bool HideWhenMenuCloses => _hideWhenMenuCloses;
}

[CreateAssetMenu(
    fileName = "AdMobSettings",
    menuName = "SkyAscent/Ads/Ad Module Settings")]
[HelpURL("https://developers.google.com/admob/unity/quick-start")]
public sealed class AdMobSettingsSO : ScriptableObject, IAdSettings
{
    [Header("Provider")]
    [SerializeField] private AdProviderType _provider = AdProviderType.AdMob;

    [Header("General")]
    [SerializeField] private bool _adsEnabled = true;
    [SerializeField] private bool _autoCreateManagerAtRuntime = true;
    [SerializeField] private bool _autoInitializeOnStart = true;
    [SerializeField] private bool _dontDestroyManagerOnLoad = true;
    [SerializeField] private bool _autoRetryLoadAfterFailure = true;
    [SerializeField, Min(1f)] private float _retryLoadDelaySeconds = 10f;
    [SerializeField] private bool _verboseLogging = true;

    [Header("App IDs")]
    [SerializeField] private string _androidAppId = string.Empty;
    [SerializeField] private string _iosAppId = string.Empty;

    [Header("Testing")]
    [SerializeField] private bool _useTestDeviceIds = true;
    [SerializeField] private string[] _androidTestDeviceIds = Array.Empty<string>();
    [SerializeField] private string[] _iosTestDeviceIds = Array.Empty<string>();

    [Header("Ad Formats")]
    [SerializeField] private BannerAdFormatSettings _banner = new BannerAdFormatSettings();
    [SerializeField] private AdFormatSettings _interstitial = new AdFormatSettings();
    [SerializeField] private RewardedAdFormatSettings _rewarded = new RewardedAdFormatSettings();
    [SerializeField] private AdFormatSettings _rewardedInterstitial = new AdFormatSettings();
    [SerializeField] private AdFormatSettings _appOpen = new AdFormatSettings();
    [SerializeField] private NativeOverlayAdFormatSettings _nativeOverlay = new NativeOverlayAdFormatSettings();

    public AdProviderType Provider => _provider;
    public bool AdsEnabled => _adsEnabled;
    public bool AutoCreateManagerAtRuntime => _autoCreateManagerAtRuntime;
    public bool AutoInitializeOnStart => _autoInitializeOnStart;
    public bool DontDestroyManagerOnLoad => _dontDestroyManagerOnLoad;
    public bool AutoRetryLoadAfterFailure => _autoRetryLoadAfterFailure;
    public float RetryLoadDelaySeconds => Mathf.Max(_retryLoadDelaySeconds, 1f);
    public bool VerboseLogging => _verboseLogging;
    public string AndroidAppId => (_androidAppId ?? string.Empty).Trim();
    public string IosAppId => (_iosAppId ?? string.Empty).Trim();
    public BannerAdSizeType BannerSize => Banner.BannerSize;
    public BannerScreenPositionType BannerPosition => Banner.BannerPosition;
    public bool ShouldShowBannerWhenMenuOpens => Banner.ShowWhenMenuOpens;
    public bool ShouldHideBannerWhenMenuCloses => Banner.HideWhenMenuCloses;
    public BannerScreenPositionType NativeOverlayPosition => NativeOverlay.TemplatePosition;
    public bool ShouldShowNativeOverlayWhenMenuOpens => NativeOverlay.ShowWhenMenuOpens;
    public bool ShouldHideNativeOverlayWhenMenuCloses => NativeOverlay.HideWhenMenuCloses;
    public bool SimulateRewardInEditorWithoutSdk => Rewarded.SimulateRewardInEditorWithoutSdk;
    public float EditorSimulatedRewardDelay => Rewarded.EditorSimulatedRewardDelay;

    public BannerAdFormatSettings Banner
    {
        get
        {
            _banner ??= new BannerAdFormatSettings();
            return _banner;
        }
    }

    public AdFormatSettings Interstitial
    {
        get
        {
            _interstitial ??= new AdFormatSettings();
            return _interstitial;
        }
    }

    public RewardedAdFormatSettings Rewarded
    {
        get
        {
            _rewarded ??= new RewardedAdFormatSettings();
            return _rewarded;
        }
    }

    public AdFormatSettings RewardedInterstitial
    {
        get
        {
            _rewardedInterstitial ??= new AdFormatSettings();
            return _rewardedInterstitial;
        }
    }

    public AdFormatSettings AppOpen
    {
        get
        {
            _appOpen ??= new AdFormatSettings();
            return _appOpen;
        }
    }

    public NativeOverlayAdFormatSettings NativeOverlay
    {
        get
        {
            _nativeOverlay ??= new NativeOverlayAdFormatSettings();
            return _nativeOverlay;
        }
    }

    private void OnEnable()
    {
        EnsureDefaults();
    }

    private void OnValidate()
    {
        EnsureDefaults();
    }

    public void EnsureDefaults()
    {
        _androidAppId ??= string.Empty;
        _iosAppId ??= string.Empty;
        EnsureTestDeviceDefaults();

        _banner ??= new BannerAdFormatSettings();
        _interstitial ??= new AdFormatSettings();
        _rewarded ??= new RewardedAdFormatSettings();
        _rewardedInterstitial ??= new AdFormatSettings();
        _appOpen ??= new AdFormatSettings();
        _nativeOverlay ??= new NativeOverlayAdFormatSettings();

        Banner.EnsureDefaults();
        Interstitial.EnsureDefaults();
        Rewarded.EnsureDefaults();
        RewardedInterstitial.EnsureDefaults();
        AppOpen.EnsureDefaults();
        NativeOverlay.EnsureDefaults();
    }

    public bool HasConfiguredAdUnitId(AdFormatType format)
    {
        return !string.IsNullOrWhiteSpace(GetAdUnitId(format));
    }

    public string GetAdUnitId(AdFormatType format)
    {
        return GetFormatSettings(format)?.GetAdUnitIdForCurrentPlatform() ?? string.Empty;
    }

    public bool IsFormatEnabled(AdFormatType format)
    {
        return GetFormatSettings(format)?.Enabled ?? false;
    }

    public bool ShouldPreloadOnInitialize(AdFormatType format)
    {
        return GetFormatSettings(format)?.PreloadOnInitialize ?? false;
    }

    public bool ShouldPreloadWhenMenuOpens(AdFormatType format)
    {
        return GetFormatSettings(format)?.PreloadWhenMenuOpens ?? false;
    }

    public bool ShouldPreloadDuringLoading(AdFormatType format)
    {
        return GetFormatSettings(format)?.PreloadDuringLoading ?? false;
    }

    public bool ShouldPersistAcrossScenes(AdFormatType format)
    {
        return GetFormatSettings(format)?.PersistAcrossScenes ?? false;
    }

    public bool HasAndroidAppId()
    {
        return !string.IsNullOrWhiteSpace(AndroidAppId);
    }

    public bool HasIosAppId()
    {
        return !string.IsNullOrWhiteSpace(IosAppId);
    }

    public string[] GetTestDeviceIdsForCurrentPlatform()
    {
        if (!_useTestDeviceIds)
            return Array.Empty<string>();

#if UNITY_IOS
        return SanitizeTestDeviceIds(_iosTestDeviceIds);
#elif UNITY_ANDROID
        return SanitizeTestDeviceIds(_androidTestDeviceIds);
#else
        string[] androidIds = SanitizeTestDeviceIds(_androidTestDeviceIds);
        return androidIds.Length > 0 ? androidIds : SanitizeTestDeviceIds(_iosTestDeviceIds);
#endif
    }

    public bool LooksLikeValidAppId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        string trimmed = value.Trim();
        return trimmed.Contains("~") && !trimmed.Contains("/");
    }

    public bool LooksLikeValidAndroidAppId()
    {
        return LooksLikeValidAppId(AndroidAppId);
    }

    public bool LooksLikeValidIosAppId()
    {
        return LooksLikeValidAppId(IosAppId);
    }

    public AdFormatSettings GetFormatSettings(AdFormatType format)
    {
        switch (format)
        {
            case AdFormatType.Banner:
                return Banner;
            case AdFormatType.Interstitial:
                return Interstitial;
            case AdFormatType.Rewarded:
                return Rewarded;
            case AdFormatType.RewardedInterstitial:
                return RewardedInterstitial;
            case AdFormatType.AppOpen:
                return AppOpen;
            case AdFormatType.NativeOverlay:
                return NativeOverlay;
            default:
                return null;
        }
    }

    private void Reset()
    {
        EnsureDefaults();
    }

    private void EnsureTestDeviceDefaults()
    {
        _androidTestDeviceIds ??= Array.Empty<string>();
        _iosTestDeviceIds ??= Array.Empty<string>();
    }

    private static string[] SanitizeTestDeviceIds(string[] testDeviceIds)
    {
        if (testDeviceIds == null || testDeviceIds.Length == 0)
            return Array.Empty<string>();

        var sanitized = new List<string>(testDeviceIds.Length);
        for (int i = 0; i < testDeviceIds.Length; i++)
        {
            string trimmed = (testDeviceIds[i] ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(trimmed) || sanitized.Contains(trimmed))
                continue;

            sanitized.Add(trimmed);
        }

        return sanitized.Count > 0 ? sanitized.ToArray() : Array.Empty<string>();
    }
}
