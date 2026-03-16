using System;
using System.Collections.Generic;
using UnityEngine;

#if GOOGLE_MOBILE_ADS
using GoogleMobileAds.Api;
#endif

public sealed class AdMobService : IAdService
{
    private readonly Action<Action> _dispatchToMainThread;
    private readonly HashSet<AdFormatType> _pendingPreloadFormats = new HashSet<AdFormatType>();
    private readonly HashSet<AdFormatType> _pendingShowFormats = new HashSet<AdFormatType>();

    private IAdSettings _settings;
    private bool _isInitializing;
    private bool _isShowingFullScreenAd;

#if GOOGLE_MOBILE_ADS
    private BannerView _bannerView;
    private InterstitialAd _interstitialAd;
    private RewardedAd _rewardedAd;
    private RewardedInterstitialAd _rewardedInterstitialAd;
    private AppOpenAd _appOpenAd;
    private NativeOverlayAd _nativeOverlayAd;

    private bool _isInitialized;
    private bool _isBannerLoading;
    private bool _isBannerLoaded;
    private bool _isInterstitialLoading;
    private bool _isRewardedLoading;
    private bool _isRewardedInterstitialLoading;
    private bool _isAppOpenLoading;
    private bool _isNativeOverlayLoading;
    private bool _isNativeOverlayLoaded;

    private bool _rewardedEarnedForCurrentShow;
    private bool _rewardedInterstitialEarnedForCurrentShow;

    private int _interstitialRequestVersion;
    private int _rewardedRequestVersion;
    private int _rewardedInterstitialRequestVersion;
    private int _appOpenRequestVersion;
    private int _nativeOverlayRequestVersion;
#endif

    public AdMobService(Action<Action> dispatchToMainThread)
    {
        _dispatchToMainThread = dispatchToMainThread;
    }

    public event Action<AdServiceResult> ResultReceived;

    public bool IsInitialized
    {
        get
        {
#if GOOGLE_MOBILE_ADS
            return _isInitialized;
#else
            return false;
#endif
        }
    }

    public void Configure(IAdSettings settings)
    {
        if (!ReferenceEquals(_settings, settings) && _settings != null)
            DestroyAll();

        _settings = settings;
    }

    public void Initialize()
    {
        if (_settings == null)
        {
            PublishResult(AdFormatType.None, AdServiceResultType.Disabled, "Ad settings are missing.");
            return;
        }

        if (!_settings.AdsEnabled)
        {
            PublishResult(AdFormatType.None, AdServiceResultType.Disabled, "Ads are disabled.");
            return;
        }

#if GOOGLE_MOBILE_ADS
        if (_isInitialized || _isInitializing)
            return;

        _isInitializing = true;

        // Keep SDK callbacks off the Unity thread, then marshal them back in small batches via AdsManager.
#pragma warning disable 618
        MobileAds.RaiseAdEventsOnUnityMainThread = false;
#pragma warning restore 618
        MobileAds.Initialize(_ =>
        {
            Dispatch(() =>
            {
                _isInitializing = false;
                _isInitialized = true;
                ProcessPendingLoads();
            });
        });
#else
        PublishResult(AdFormatType.None, AdServiceResultType.SdkUnavailable, "Google Mobile Ads SDK is unavailable.");
#endif
    }

    public void Preload(AdFormatType format)
    {
        if (!CanUseFormat(format))
            return;

#if GOOGLE_MOBILE_ADS
        if (!_isInitialized)
        {
            _pendingPreloadFormats.Add(format);
            Initialize();
            return;
        }

        switch (format)
        {
            case AdFormatType.Banner:
                LoadBanner();
                break;
            case AdFormatType.Interstitial:
                LoadInterstitial();
                break;
            case AdFormatType.Rewarded:
                LoadRewarded();
                break;
            case AdFormatType.RewardedInterstitial:
                LoadRewardedInterstitial();
                break;
            case AdFormatType.AppOpen:
                LoadAppOpen();
                break;
            case AdFormatType.NativeOverlay:
                LoadNativeOverlay();
                break;
        }
#else
        PublishResult(format, AdServiceResultType.SdkUnavailable, "Google Mobile Ads SDK is unavailable.");
#endif
    }

    public void Show(AdFormatType format)
    {
        if (!CanUseFormat(format))
            return;

#if GOOGLE_MOBILE_ADS
        if (!_isInitialized)
        {
            _pendingPreloadFormats.Add(format);
            _pendingShowFormats.Add(format);
            Initialize();
            return;
        }

        switch (format)
        {
            case AdFormatType.Banner:
                ShowBanner();
                break;
            case AdFormatType.Interstitial:
                ShowInterstitial();
                break;
            case AdFormatType.Rewarded:
                ShowRewarded();
                break;
            case AdFormatType.RewardedInterstitial:
                ShowRewardedInterstitial();
                break;
            case AdFormatType.AppOpen:
                ShowAppOpen();
                break;
            case AdFormatType.NativeOverlay:
                ShowNativeOverlay();
                break;
        }
#else
        PublishResult(format, AdServiceResultType.SdkUnavailable, "Google Mobile Ads SDK is unavailable.");
#endif
    }

    public void Hide(AdFormatType format)
    {
#if GOOGLE_MOBILE_ADS
        switch (format)
        {
            case AdFormatType.Banner:
                if (_bannerView != null)
                {
                    _bannerView.Hide();
                    PublishResult(format, AdServiceResultType.Hidden);
                }
                break;
            case AdFormatType.NativeOverlay:
                if (_nativeOverlayAd != null)
                {
                    _nativeOverlayAd.Hide();
                    PublishResult(format, AdServiceResultType.Hidden);
                }
                break;
        }
#endif
    }

    public void Destroy(AdFormatType format)
    {
        _pendingPreloadFormats.Remove(format);
        _pendingShowFormats.Remove(format);

#if GOOGLE_MOBILE_ADS
        switch (format)
        {
            case AdFormatType.Banner:
                DestroyBanner();
                break;
            case AdFormatType.Interstitial:
                DestroyInterstitial();
                break;
            case AdFormatType.Rewarded:
                DestroyRewarded();
                break;
            case AdFormatType.RewardedInterstitial:
                DestroyRewardedInterstitial();
                break;
            case AdFormatType.AppOpen:
                DestroyAppOpen();
                break;
            case AdFormatType.NativeOverlay:
                DestroyNativeOverlay();
                break;
        }
#endif
    }

    public void DestroyAll()
    {
        _pendingPreloadFormats.Clear();
        _pendingShowFormats.Clear();

        Destroy(AdFormatType.Banner);
        Destroy(AdFormatType.Interstitial);
        Destroy(AdFormatType.Rewarded);
        Destroy(AdFormatType.RewardedInterstitial);
        Destroy(AdFormatType.AppOpen);
        Destroy(AdFormatType.NativeOverlay);

        _isShowingFullScreenAd = false;
    }

    public bool IsReady(AdFormatType format)
    {
#if GOOGLE_MOBILE_ADS
        switch (format)
        {
            case AdFormatType.Banner:
                return _bannerView != null && _isBannerLoaded;
            case AdFormatType.Interstitial:
                return _interstitialAd != null && _interstitialAd.CanShowAd();
            case AdFormatType.Rewarded:
                return _rewardedAd != null && _rewardedAd.CanShowAd();
            case AdFormatType.RewardedInterstitial:
                return _rewardedInterstitialAd != null && _rewardedInterstitialAd.CanShowAd();
            case AdFormatType.AppOpen:
                return _appOpenAd != null && _appOpenAd.CanShowAd();
            case AdFormatType.NativeOverlay:
                return _nativeOverlayAd != null && _isNativeOverlayLoaded;
            default:
                return false;
        }
#else
        return false;
#endif
    }

    private bool CanUseFormat(AdFormatType format)
    {
        if (format == AdFormatType.None)
            return false;

        if (_settings == null)
        {
            PublishResult(format, AdServiceResultType.Disabled, "Ad settings are missing.");
            return false;
        }

        if (!_settings.AdsEnabled)
        {
            PublishResult(format, AdServiceResultType.Disabled, "Ads are disabled.");
            return false;
        }

        if (!_settings.IsFormatEnabled(format))
        {
            PublishResult(format, AdServiceResultType.Disabled, $"{format} is disabled in settings.");
            return false;
        }

        if (!_settings.HasConfiguredAdUnitId(format))
        {
            PublishResult(format, AdServiceResultType.Disabled, $"{format} ad unit ID is empty.");
            return false;
        }

        return true;
    }

    private void ProcessPendingLoads()
    {
        var preloadFormats = new List<AdFormatType>(_pendingPreloadFormats);
        var showFormats = new List<AdFormatType>(_pendingShowFormats);

        _pendingPreloadFormats.Clear();

        for (int i = 0; i < preloadFormats.Count; i++)
        {
            Preload(preloadFormats[i]);
        }

        for (int i = 0; i < showFormats.Count; i++)
        {
            Preload(showFormats[i]);
        }
    }

    private void Dispatch(Action action)
    {
        if (action == null)
            return;

        if (_dispatchToMainThread != null)
        {
            _dispatchToMainThread(action);
            return;
        }

        action.Invoke();
    }

    private void PublishResult(
        AdFormatType format,
        AdServiceResultType resultType,
        string message = "",
        bool rewardEarned = false)
    {
        Dispatch(() => ResultReceived?.Invoke(new AdServiceResult(format, resultType, message, rewardEarned)));
    }

#if GOOGLE_MOBILE_ADS
    private AdRequest BuildRequest()
    {
        return new AdRequest();
    }

    private NativeAdOptions BuildNativeAdOptions()
    {
        return new NativeAdOptions
        {
            AdChoicesPlacement = AdChoicesPlacement.TopRightCorner,
            MediaAspectRatio = MediaAspectRatio.Any,
            VideoOptions = new VideoOptions
            {
                StartMuted = true,
                ClickToExpandRequested = false,
                CustomControlsRequested = false,
            },
        };
    }

    private void LoadBanner()
    {
        if (_isBannerLoading || (_bannerView != null && _isBannerLoaded))
            return;

        DestroyBanner(false);

        _isBannerLoading = true;
        _isBannerLoaded = false;
        _bannerView = new BannerView(
            _settings.GetAdUnitId(AdFormatType.Banner),
            ResolveBannerSize(),
            ResolvePosition(_settings.BannerPosition));

        RegisterBannerCallbacks(_bannerView);
        _bannerView.LoadAd(BuildRequest());
    }

    private void RegisterBannerCallbacks(BannerView bannerView)
    {
        bannerView.OnBannerAdLoaded += () =>
        {
            Dispatch(() =>
            {
                if (bannerView != _bannerView)
                    return;

                _isBannerLoading = false;
                _isBannerLoaded = true;
                PublishResult(AdFormatType.Banner, AdServiceResultType.Loaded);

                if (_pendingShowFormats.Remove(AdFormatType.Banner))
                    ShowBanner();
            });
        };

        bannerView.OnBannerAdLoadFailed += error =>
        {
            Dispatch(() =>
            {
                if (bannerView != _bannerView)
                    return;

                DestroyBanner(false);
                PublishResult(
                    AdFormatType.Banner,
                    AdServiceResultType.FailedToLoad,
                    error != null ? error.ToString() : "Banner failed to load.");
            });
        };
    }

    private void ShowBanner()
    {
        if (_bannerView == null || !_isBannerLoaded)
        {
            _pendingShowFormats.Add(AdFormatType.Banner);
            LoadBanner();
            return;
        }

        _bannerView.Show();
        PublishResult(AdFormatType.Banner, AdServiceResultType.Shown);
    }

    private void LoadInterstitial()
    {
        if (_isInterstitialLoading || (_interstitialAd != null && _interstitialAd.CanShowAd()))
            return;

        DestroyInterstitial(false);

        _isInterstitialLoading = true;
        int requestVersion = ++_interstitialRequestVersion;

        InterstitialAd.Load(_settings.GetAdUnitId(AdFormatType.Interstitial), BuildRequest(), (ad, error) =>
        {
            Dispatch(() => HandleInterstitialLoaded(requestVersion, ad, error));
        });
    }

    private void HandleInterstitialLoaded(int requestVersion, InterstitialAd ad, LoadAdError error)
    {
        if (requestVersion != _interstitialRequestVersion)
        {
            ad?.Destroy();
            return;
        }

        _isInterstitialLoading = false;

        if (error != null || ad == null)
        {
            ad?.Destroy();
            PublishResult(
                AdFormatType.Interstitial,
                AdServiceResultType.FailedToLoad,
                error != null ? error.ToString() : "Interstitial failed to load.");
            return;
        }

        _interstitialAd = ad;
        RegisterInterstitialCallbacks(ad);
        PublishResult(AdFormatType.Interstitial, AdServiceResultType.Loaded);

        if (_pendingShowFormats.Remove(AdFormatType.Interstitial))
            ShowInterstitial();
    }

    private void RegisterInterstitialCallbacks(InterstitialAd ad)
    {
        ad.OnAdFullScreenContentOpened += () =>
        {
            Dispatch(() =>
            {
                if (ad != _interstitialAd)
                    return;

                _isShowingFullScreenAd = true;
                PublishResult(AdFormatType.Interstitial, AdServiceResultType.Shown);
            });
        };

        ad.OnAdFullScreenContentClosed += () =>
        {
            Dispatch(() =>
            {
                if (ad != _interstitialAd)
                {
                    ad.Destroy();
                    return;
                }

                _isShowingFullScreenAd = false;
                DestroyInterstitial(false);
                PublishResult(AdFormatType.Interstitial, AdServiceResultType.Closed);
            });
        };

        ad.OnAdFullScreenContentFailed += error =>
        {
            Dispatch(() =>
            {
                if (ad != _interstitialAd)
                {
                    ad.Destroy();
                    return;
                }

                _isShowingFullScreenAd = false;
                DestroyInterstitial(false);
                PublishResult(
                    AdFormatType.Interstitial,
                    AdServiceResultType.FailedToShow,
                    error != null ? error.ToString() : "Interstitial failed to show.");
            });
        };
    }

    private void ShowInterstitial()
    {
        if (_isShowingFullScreenAd)
        {
            PublishResult(AdFormatType.Interstitial, AdServiceResultType.Busy, "A fullscreen ad is already showing.");
            return;
        }

        if (_interstitialAd == null || !_interstitialAd.CanShowAd())
        {
            _pendingShowFormats.Add(AdFormatType.Interstitial);
            LoadInterstitial();
            return;
        }

        _interstitialAd.Show();
    }

    private void LoadRewarded()
    {
        if (_isRewardedLoading || (_rewardedAd != null && _rewardedAd.CanShowAd()))
            return;

        DestroyRewarded(false);

        _isRewardedLoading = true;
        int requestVersion = ++_rewardedRequestVersion;

        RewardedAd.Load(_settings.GetAdUnitId(AdFormatType.Rewarded), BuildRequest(), (ad, error) =>
        {
            Dispatch(() => HandleRewardedLoaded(requestVersion, ad, error));
        });
    }

    private void HandleRewardedLoaded(int requestVersion, RewardedAd ad, LoadAdError error)
    {
        if (requestVersion != _rewardedRequestVersion)
        {
            ad?.Destroy();
            return;
        }

        _isRewardedLoading = false;

        if (error != null || ad == null)
        {
            ad?.Destroy();
            PublishResult(
                AdFormatType.Rewarded,
                AdServiceResultType.FailedToLoad,
                error != null ? error.ToString() : "Rewarded ad failed to load.");
            return;
        }

        _rewardedAd = ad;
        RegisterRewardedCallbacks(ad);
        PublishResult(AdFormatType.Rewarded, AdServiceResultType.Loaded);

        if (_pendingShowFormats.Remove(AdFormatType.Rewarded))
            ShowRewarded();
    }

    private void RegisterRewardedCallbacks(RewardedAd ad)
    {
        ad.OnAdFullScreenContentOpened += () =>
        {
            Dispatch(() =>
            {
                if (ad != _rewardedAd)
                    return;

                _isShowingFullScreenAd = true;
                PublishResult(AdFormatType.Rewarded, AdServiceResultType.Shown);
            });
        };

        ad.OnAdFullScreenContentClosed += () =>
        {
            Dispatch(() =>
            {
                if (ad != _rewardedAd)
                {
                    ad.Destroy();
                    return;
                }

                _isShowingFullScreenAd = false;
                bool rewardEarned = _rewardedEarnedForCurrentShow;
                DestroyRewarded(false);

                PublishResult(
                    AdFormatType.Rewarded,
                    rewardEarned ? AdServiceResultType.RewardEarned : AdServiceResultType.Skipped,
                    rewardEarned ? "Rewarded ad completed." : "Rewarded ad closed before reward.",
                    rewardEarned);
            });
        };

        ad.OnAdFullScreenContentFailed += error =>
        {
            Dispatch(() =>
            {
                if (ad != _rewardedAd)
                {
                    ad.Destroy();
                    return;
                }

                _isShowingFullScreenAd = false;
                DestroyRewarded(false);
                PublishResult(
                    AdFormatType.Rewarded,
                    AdServiceResultType.FailedToShow,
                    error != null ? error.ToString() : "Rewarded ad failed to show.");
            });
        };
    }

    private void ShowRewarded()
    {
        if (_isShowingFullScreenAd)
        {
            PublishResult(AdFormatType.Rewarded, AdServiceResultType.Busy, "A fullscreen ad is already showing.");
            return;
        }

        if (_rewardedAd == null || !_rewardedAd.CanShowAd())
        {
            _pendingShowFormats.Add(AdFormatType.Rewarded);
            LoadRewarded();
            return;
        }

        _rewardedEarnedForCurrentShow = false;
        RewardedAd currentAd = _rewardedAd;
        currentAd.Show(_ =>
        {
            Dispatch(() =>
            {
                if (currentAd != _rewardedAd)
                    return;

                _rewardedEarnedForCurrentShow = true;
            });
        });
    }

    private void LoadRewardedInterstitial()
    {
        if (_isRewardedInterstitialLoading || (_rewardedInterstitialAd != null && _rewardedInterstitialAd.CanShowAd()))
            return;

        DestroyRewardedInterstitial(false);

        _isRewardedInterstitialLoading = true;
        int requestVersion = ++_rewardedInterstitialRequestVersion;

        RewardedInterstitialAd.Load(_settings.GetAdUnitId(AdFormatType.RewardedInterstitial), BuildRequest(), (ad, error) =>
        {
            Dispatch(() => HandleRewardedInterstitialLoaded(requestVersion, ad, error));
        });
    }

    private void HandleRewardedInterstitialLoaded(int requestVersion, RewardedInterstitialAd ad, LoadAdError error)
    {
        if (requestVersion != _rewardedInterstitialRequestVersion)
        {
            ad?.Destroy();
            return;
        }

        _isRewardedInterstitialLoading = false;

        if (error != null || ad == null)
        {
            ad?.Destroy();
            PublishResult(
                AdFormatType.RewardedInterstitial,
                AdServiceResultType.FailedToLoad,
                error != null ? error.ToString() : "Rewarded interstitial failed to load.");
            return;
        }

        _rewardedInterstitialAd = ad;
        RegisterRewardedInterstitialCallbacks(ad);
        PublishResult(AdFormatType.RewardedInterstitial, AdServiceResultType.Loaded);

        if (_pendingShowFormats.Remove(AdFormatType.RewardedInterstitial))
            ShowRewardedInterstitial();
    }

    private void RegisterRewardedInterstitialCallbacks(RewardedInterstitialAd ad)
    {
        ad.OnAdFullScreenContentOpened += () =>
        {
            Dispatch(() =>
            {
                if (ad != _rewardedInterstitialAd)
                    return;

                _isShowingFullScreenAd = true;
                PublishResult(AdFormatType.RewardedInterstitial, AdServiceResultType.Shown);
            });
        };

        ad.OnAdFullScreenContentClosed += () =>
        {
            Dispatch(() =>
            {
                if (ad != _rewardedInterstitialAd)
                {
                    ad.Destroy();
                    return;
                }

                _isShowingFullScreenAd = false;
                bool rewardEarned = _rewardedInterstitialEarnedForCurrentShow;
                DestroyRewardedInterstitial(false);

                PublishResult(
                    AdFormatType.RewardedInterstitial,
                    rewardEarned ? AdServiceResultType.RewardEarned : AdServiceResultType.Skipped,
                    rewardEarned ? "Rewarded interstitial completed." : "Rewarded interstitial closed before reward.",
                    rewardEarned);
            });
        };

        ad.OnAdFullScreenContentFailed += error =>
        {
            Dispatch(() =>
            {
                if (ad != _rewardedInterstitialAd)
                {
                    ad.Destroy();
                    return;
                }

                _isShowingFullScreenAd = false;
                DestroyRewardedInterstitial(false);
                PublishResult(
                    AdFormatType.RewardedInterstitial,
                    AdServiceResultType.FailedToShow,
                    error != null ? error.ToString() : "Rewarded interstitial failed to show.");
            });
        };
    }

    private void ShowRewardedInterstitial()
    {
        if (_isShowingFullScreenAd)
        {
            PublishResult(AdFormatType.RewardedInterstitial, AdServiceResultType.Busy, "A fullscreen ad is already showing.");
            return;
        }

        if (_rewardedInterstitialAd == null || !_rewardedInterstitialAd.CanShowAd())
        {
            _pendingShowFormats.Add(AdFormatType.RewardedInterstitial);
            LoadRewardedInterstitial();
            return;
        }

        _rewardedInterstitialEarnedForCurrentShow = false;
        RewardedInterstitialAd currentAd = _rewardedInterstitialAd;
        currentAd.Show(_ =>
        {
            Dispatch(() =>
            {
                if (currentAd != _rewardedInterstitialAd)
                    return;

                _rewardedInterstitialEarnedForCurrentShow = true;
            });
        });
    }

    private void LoadAppOpen()
    {
        if (_isAppOpenLoading || (_appOpenAd != null && _appOpenAd.CanShowAd()))
            return;

        DestroyAppOpen(false);

        _isAppOpenLoading = true;
        int requestVersion = ++_appOpenRequestVersion;

        AppOpenAd.Load(_settings.GetAdUnitId(AdFormatType.AppOpen), BuildRequest(), (ad, error) =>
        {
            Dispatch(() => HandleAppOpenLoaded(requestVersion, ad, error));
        });
    }

    private void HandleAppOpenLoaded(int requestVersion, AppOpenAd ad, LoadAdError error)
    {
        if (requestVersion != _appOpenRequestVersion)
        {
            ad?.Destroy();
            return;
        }

        _isAppOpenLoading = false;

        if (error != null || ad == null)
        {
            ad?.Destroy();
            PublishResult(
                AdFormatType.AppOpen,
                AdServiceResultType.FailedToLoad,
                error != null ? error.ToString() : "App open ad failed to load.");
            return;
        }

        _appOpenAd = ad;
        RegisterAppOpenCallbacks(ad);
        PublishResult(AdFormatType.AppOpen, AdServiceResultType.Loaded);

        if (_pendingShowFormats.Remove(AdFormatType.AppOpen))
            ShowAppOpen();
    }

    private void RegisterAppOpenCallbacks(AppOpenAd ad)
    {
        ad.OnAdFullScreenContentOpened += () =>
        {
            Dispatch(() =>
            {
                if (ad != _appOpenAd)
                    return;

                _isShowingFullScreenAd = true;
                PublishResult(AdFormatType.AppOpen, AdServiceResultType.Shown);
            });
        };

        ad.OnAdFullScreenContentClosed += () =>
        {
            Dispatch(() =>
            {
                if (ad != _appOpenAd)
                {
                    ad.Destroy();
                    return;
                }

                _isShowingFullScreenAd = false;
                DestroyAppOpen(false);
                PublishResult(AdFormatType.AppOpen, AdServiceResultType.Closed);
            });
        };

        ad.OnAdFullScreenContentFailed += error =>
        {
            Dispatch(() =>
            {
                if (ad != _appOpenAd)
                {
                    ad.Destroy();
                    return;
                }

                _isShowingFullScreenAd = false;
                DestroyAppOpen(false);
                PublishResult(
                    AdFormatType.AppOpen,
                    AdServiceResultType.FailedToShow,
                    error != null ? error.ToString() : "App open ad failed to show.");
            });
        };
    }

    private void ShowAppOpen()
    {
        if (_isShowingFullScreenAd)
        {
            PublishResult(AdFormatType.AppOpen, AdServiceResultType.Busy, "A fullscreen ad is already showing.");
            return;
        }

        if (_appOpenAd == null || !_appOpenAd.CanShowAd())
        {
            _pendingShowFormats.Add(AdFormatType.AppOpen);
            LoadAppOpen();
            return;
        }

        _appOpenAd.Show();
    }

    private void LoadNativeOverlay()
    {
        if (_isNativeOverlayLoading || (_nativeOverlayAd != null && _isNativeOverlayLoaded))
            return;

        DestroyNativeOverlay(false);

        _isNativeOverlayLoading = true;
        int requestVersion = ++_nativeOverlayRequestVersion;

        NativeOverlayAd.Load(_settings.GetAdUnitId(AdFormatType.NativeOverlay), BuildRequest(), BuildNativeAdOptions(), (ad, error) =>
        {
            Dispatch(() => HandleNativeOverlayLoaded(requestVersion, ad, error));
        });
    }

    private void HandleNativeOverlayLoaded(int requestVersion, NativeOverlayAd ad, LoadAdError error)
    {
        if (requestVersion != _nativeOverlayRequestVersion)
        {
            ad?.Destroy();
            return;
        }

        _isNativeOverlayLoading = false;

        if (error != null || ad == null)
        {
            ad?.Destroy();
            PublishResult(
                AdFormatType.NativeOverlay,
                AdServiceResultType.FailedToLoad,
                error != null ? error.ToString() : "Native overlay failed to load.");
            return;
        }

        _nativeOverlayAd = ad;
        _isNativeOverlayLoaded = true;
        _nativeOverlayAd.SetTemplatePosition(ResolvePosition(_settings.NativeOverlayPosition));
        RegisterNativeOverlayCallbacks(ad);
        PublishResult(AdFormatType.NativeOverlay, AdServiceResultType.Loaded);

        if (_pendingShowFormats.Remove(AdFormatType.NativeOverlay))
            ShowNativeOverlay();
    }

    private void RegisterNativeOverlayCallbacks(NativeOverlayAd ad)
    {
        ad.OnAdFullScreenContentOpened += () =>
        {
            Dispatch(() =>
            {
                if (ad != _nativeOverlayAd)
                    return;

                _isShowingFullScreenAd = true;
            });
        };

        ad.OnAdFullScreenContentClosed += () =>
        {
            Dispatch(() =>
            {
                if (ad != _nativeOverlayAd)
                {
                    ad.Destroy();
                    return;
                }

                _isShowingFullScreenAd = false;
                PublishResult(AdFormatType.NativeOverlay, AdServiceResultType.Closed);
            });
        };
    }

    private void ShowNativeOverlay()
    {
        if (_nativeOverlayAd == null || !_isNativeOverlayLoaded)
        {
            _pendingShowFormats.Add(AdFormatType.NativeOverlay);
            LoadNativeOverlay();
            return;
        }

        _nativeOverlayAd.SetTemplatePosition(ResolvePosition(_settings.NativeOverlayPosition));
        _nativeOverlayAd.Show();
        PublishResult(AdFormatType.NativeOverlay, AdServiceResultType.Shown);
    }

    private void DestroyBanner(bool publishResult = true)
    {
        if (_bannerView != null)
        {
            BannerView bannerView = _bannerView;
            _bannerView = null;
            bannerView.Destroy();

            if (publishResult)
                PublishResult(AdFormatType.Banner, AdServiceResultType.Destroyed);
        }

        _isBannerLoading = false;
        _isBannerLoaded = false;
    }

    private void DestroyInterstitial(bool publishResult = true)
    {
        _interstitialRequestVersion++;

        if (_interstitialAd != null)
        {
            InterstitialAd interstitialAd = _interstitialAd;
            _interstitialAd = null;
            interstitialAd.Destroy();

            if (publishResult)
                PublishResult(AdFormatType.Interstitial, AdServiceResultType.Destroyed);
        }

        _isInterstitialLoading = false;
    }

    private void DestroyRewarded(bool publishResult = true)
    {
        _rewardedRequestVersion++;

        if (_rewardedAd != null)
        {
            RewardedAd rewardedAd = _rewardedAd;
            _rewardedAd = null;
            rewardedAd.Destroy();

            if (publishResult)
                PublishResult(AdFormatType.Rewarded, AdServiceResultType.Destroyed);
        }

        _isRewardedLoading = false;
        _rewardedEarnedForCurrentShow = false;
    }

    private void DestroyRewardedInterstitial(bool publishResult = true)
    {
        _rewardedInterstitialRequestVersion++;

        if (_rewardedInterstitialAd != null)
        {
            RewardedInterstitialAd rewardedInterstitialAd = _rewardedInterstitialAd;
            _rewardedInterstitialAd = null;
            rewardedInterstitialAd.Destroy();

            if (publishResult)
                PublishResult(AdFormatType.RewardedInterstitial, AdServiceResultType.Destroyed);
        }

        _isRewardedInterstitialLoading = false;
        _rewardedInterstitialEarnedForCurrentShow = false;
    }

    private void DestroyAppOpen(bool publishResult = true)
    {
        _appOpenRequestVersion++;

        if (_appOpenAd != null)
        {
            AppOpenAd appOpenAd = _appOpenAd;
            _appOpenAd = null;
            appOpenAd.Destroy();

            if (publishResult)
                PublishResult(AdFormatType.AppOpen, AdServiceResultType.Destroyed);
        }

        _isAppOpenLoading = false;
    }

    private void DestroyNativeOverlay(bool publishResult = true)
    {
        _nativeOverlayRequestVersion++;

        if (_nativeOverlayAd != null)
        {
            NativeOverlayAd nativeOverlayAd = _nativeOverlayAd;
            _nativeOverlayAd = null;
            nativeOverlayAd.Destroy();

            if (publishResult)
                PublishResult(AdFormatType.NativeOverlay, AdServiceResultType.Destroyed);
        }

        _isNativeOverlayLoading = false;
        _isNativeOverlayLoaded = false;
    }

    private AdSize ResolveBannerSize()
    {
        switch (_settings.BannerSize)
        {
            case BannerAdSizeType.LargeBanner:
                return new AdSize(320, 100);
            case BannerAdSizeType.IABBanner:
                return AdSize.IABBanner;
            case BannerAdSizeType.Leaderboard:
                return AdSize.Leaderboard;
            case BannerAdSizeType.MediumRectangle:
                return AdSize.MediumRectangle;
            case BannerAdSizeType.AnchoredAdaptive:
                return AdSize.GetCurrentOrientationAnchoredAdaptiveBannerAdSizeWithWidth(AdSize.FullWidth);
            default:
                return AdSize.Banner;
        }
    }

    private AdPosition ResolvePosition(BannerScreenPositionType position)
    {
        switch (position)
        {
            case BannerScreenPositionType.Top:
                return AdPosition.Top;
            case BannerScreenPositionType.TopLeft:
                return AdPosition.TopLeft;
            case BannerScreenPositionType.TopRight:
                return AdPosition.TopRight;
            case BannerScreenPositionType.BottomLeft:
                return AdPosition.BottomLeft;
            case BannerScreenPositionType.BottomRight:
                return AdPosition.BottomRight;
            case BannerScreenPositionType.Center:
                return AdPosition.Center;
            default:
                return AdPosition.Bottom;
        }
    }
#endif
}
