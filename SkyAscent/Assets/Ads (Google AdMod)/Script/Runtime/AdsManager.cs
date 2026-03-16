using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

// namespace Script.Manager
// {
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-350)]
    [HelpURL("https://developers.google.com/admob/unity/quick-start")]
    public class AdsManager : CoreEventBase
    {
        private const string DefaultSettingsResourcePath = "AdMobRewardedSettings";
        private const int MaxMainThreadActionsPerFrame = 8;

        private static readonly AdFormatType[] KnownFormats =
        {
            AdFormatType.Banner,
            AdFormatType.Interstitial,
            AdFormatType.Rewarded,
            AdFormatType.RewardedInterstitial,
            AdFormatType.AppOpen,
            AdFormatType.NativeOverlay,
        };

        private static AdsManager instanceValue;

        [FormerlySerializedAs("_settings")]
        [SerializeField] private AdMobSettingsSO settings;
        [FormerlySerializedAs("_settingsResourcePath")]
        [SerializeField] private string settingsResourcePath = DefaultSettingsResourcePath;

        private readonly Queue<Action> _mainThreadActions = new Queue<Action>();
        private readonly Dictionary<AdFormatType, Coroutine> _retryCoroutines = new Dictionary<AdFormatType, Coroutine>();

        private IAdService _service;
        private Coroutine _preloadRoutine;
        private bool _editorRewardSimulationRunning;
        private RewardedAdPlacement _rewardedPlacementInFlight = RewardedAdPlacement.None;

        public static AdsManager instance => instanceValue;
        public IAdService service => _service;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureBootstrapInstance()
        {
            if (FindFirstObjectByType<AdsManager>() != null)
                return;

            var loadedSettings = Resources.Load<AdMobSettingsSO>(DefaultSettingsResourcePath);
            if (loadedSettings == null || !loadedSettings.AutoCreateManagerAtRuntime)
                return;

            var go = new GameObject("[Manager] AdsManager");
            go.AddComponent<AdsManager>();
        }

        protected override void Awake()
        {
            base.Awake();

            if (instanceValue != null && instanceValue != this)
            {
                Destroy(gameObject);
                return;
            }

            instanceValue = this;

            LoadSettingsIfNeeded();
            CreateServiceIfNeeded();

            if (settings != null && settings.DontDestroyManagerOnLoad)
                DontDestroyOnLoad(gameObject);

            SceneManager.activeSceneChanged += OnActiveSceneChanged;
        }

        private void Start()
        {
            if (settings == null || !settings.AdsEnabled)
                return;

            if (settings.AutoInitializeOnStart)
            {
                InitializeAds();
                ScheduleConfiguredPreloads(AdPreloadTrigger.Initialize);
            }
        }

        private void Update()
        {
            DrainMainThreadActions();
        }

        private void OnDestroy()
        {
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;

            StopConfiguredPreloads();
            StopAllRetryCoroutines();

            if (_service != null)
            {
                _service.ResultReceived -= OnServiceResult;
                _service.DestroyAll();
            }

            if (instanceValue == this)
                instanceValue = null;
        }

        public override void SubscribeEvents()
        {
            CoreEvents.RewardedAdRequest.Subscribe(OnRewardedAdRequest, Binder);
            CoreEvents.OnMenu.Subscribe(OnMenuEvent, Binder);
            CoreEvents.LoadChapter.Subscribe(OnLoadDataEvent, Binder);
        }

        public void InitializeAds()
        {
            CreateServiceIfNeeded();
            _service?.Initialize();
        }

        public void PreloadAd(AdFormatType format)
        {
            CreateServiceIfNeeded();
            if (_service == null || settings == null)
                return;

            CancelRetry(format);
            _service.Preload(format);
        }

        public void ShowAd(AdFormatType format)
        {
            CreateServiceIfNeeded();
            if (_service == null)
                return;

            _service.Show(format);
        }

        public void HideAd(AdFormatType format)
        {
            CreateServiceIfNeeded();
            _service?.Hide(format);
        }

        public void DestroyAd(AdFormatType format)
        {
            CreateServiceIfNeeded();
            CancelRetry(format);
            _service?.Destroy(format);
        }

        public bool IsReady(AdFormatType format)
        {
            CreateServiceIfNeeded();
            return _service != null && _service.IsReady(format);
        }

        private void LoadSettingsIfNeeded()
        {
            if (settings != null)
            {
                settings.EnsureDefaults();
                return;
            }

            string resourcePath = string.IsNullOrWhiteSpace(settingsResourcePath)
                ? DefaultSettingsResourcePath
                : settingsResourcePath.Trim();

            settings = Resources.Load<AdMobSettingsSO>(resourcePath);
            settings?.EnsureDefaults();
        }

        private void CreateServiceIfNeeded()
        {
            if (_service != null || settings == null)
                return;

            switch (settings.Provider)
            {
                case AdProviderType.AdMob:
                    _service = new AdMobService(EnqueueMainThread);
                    break;
                default:
                    _service = new NullAdService(EnqueueMainThread);
                    break;
            }

            _service.Configure(settings);
            _service.ResultReceived += OnServiceResult;

            Injector.GlobalServices.Set(_service);
            Injector.GlobalServices.Set(this);
        }

        private void OnRewardedAdRequest(RewardedAdRequestEvent e)
        {
            if (e == null || e.Placement == RewardedAdPlacement.None)
                return;

            if (_rewardedPlacementInFlight != RewardedAdPlacement.None)
            {
                CoreEvents.RewardedAdResult.Raise(new RewardedAdResultEvent(
                    e.Placement,
                    RewardedAdResultType.Busy,
                    "Another rewarded ad request is already in progress."));
                return;
            }

            CreateServiceIfNeeded();
            if (_service == null)
            {
                CoreEvents.RewardedAdResult.Raise(new RewardedAdResultEvent(
                    e.Placement,
                    RewardedAdResultType.Disabled,
                    "Ad service is unavailable."));
                return;
            }

            _rewardedPlacementInFlight = e.Placement;
            ShowAd(AdFormatType.Rewarded);
        }

        private void OnMenuEvent(OnMenuEvent e)
        {
            if (settings == null || _service == null || e == null)
                return;

            if (e.IsOpenMenu)
            {
                ScheduleConfiguredPreloads(AdPreloadTrigger.MenuOpened);

                if (settings.IsFormatEnabled(AdFormatType.Banner) && settings.ShouldShowBannerWhenMenuOpens)
                    ShowAd(AdFormatType.Banner);

                if (settings.IsFormatEnabled(AdFormatType.NativeOverlay) && settings.ShouldShowNativeOverlayWhenMenuOpens)
                    ShowAd(AdFormatType.NativeOverlay);

                return;
            }

            if (settings.ShouldHideBannerWhenMenuCloses)
                HideAd(AdFormatType.Banner);

            if (settings.ShouldHideNativeOverlayWhenMenuCloses)
                HideAd(AdFormatType.NativeOverlay);
        }

        private void OnLoadDataEvent(LoadDataEvent e)
        {
            if (settings == null || _service == null || e == null)
                return;

            ScheduleConfiguredPreloads(AdPreloadTrigger.Loading);
        }

        private void OnActiveSceneChanged(Scene current, Scene next)
        {
            if (settings == null || _service == null)
                return;

            foreach (AdFormatType format in KnownFormats)
            {
                if (settings.IsFormatEnabled(format) && !settings.ShouldPersistAcrossScenes(format))
                    DestroyAd(format);
            }
        }

        private void OnServiceResult(AdServiceResult result)
        {
            if (result == null)
                return;

            if (settings != null && settings.VerboseLogging)
            {
                Debug.Log($"[AdsManager] {result.Format} -> {result.ResultType}. {result.Message}");
            }

            switch (result.ResultType)
            {
                case AdServiceResultType.Loaded:
                case AdServiceResultType.Shown:
                case AdServiceResultType.Hidden:
                case AdServiceResultType.Destroyed:
                case AdServiceResultType.Closed:
                    CancelRetry(result.Format);
                    break;
                case AdServiceResultType.FailedToLoad:
                case AdServiceResultType.FailedToShow:
                    ScheduleRetry(result.Format);
                    break;
            }

            if (result.Format == AdFormatType.Rewarded)
            {
                HandleRewardedResult(result);
            }
        }

        private void HandleRewardedResult(AdServiceResult result)
        {
            RewardedAdPlacement placement = ResolveRewardedPlacement();

            switch (result.ResultType)
            {
                case AdServiceResultType.RewardEarned:
                    CoreEvents.RewardedAdResult.Raise(new RewardedAdResultEvent(
                        placement,
                        RewardedAdResultType.RewardEarned,
                        result.Message));
                    ClearRewardedPlacement();
                    break;
                case AdServiceResultType.Skipped:
                    CoreEvents.RewardedAdResult.Raise(new RewardedAdResultEvent(
                        placement,
                        RewardedAdResultType.Skipped,
                        result.Message));
                    ClearRewardedPlacement();
                    break;
                case AdServiceResultType.FailedToLoad:
                    CoreEvents.RewardedAdResult.Raise(new RewardedAdResultEvent(
                        placement,
                        RewardedAdResultType.FailedToLoad,
                        result.Message));
                    ClearRewardedPlacement();
                    break;
                case AdServiceResultType.FailedToShow:
                    CoreEvents.RewardedAdResult.Raise(new RewardedAdResultEvent(
                        placement,
                        RewardedAdResultType.FailedToShow,
                        result.Message));
                    ClearRewardedPlacement();
                    break;
                case AdServiceResultType.Disabled:
                    CoreEvents.RewardedAdResult.Raise(new RewardedAdResultEvent(
                        placement,
                        RewardedAdResultType.Disabled,
                        result.Message));
                    ClearRewardedPlacement();
                    break;
                case AdServiceResultType.NotReady:
                    CoreEvents.RewardedAdResult.Raise(new RewardedAdResultEvent(
                        placement,
                        RewardedAdResultType.NotReady,
                        result.Message));
                    ClearRewardedPlacement();
                    break;
                case AdServiceResultType.Busy:
                    CoreEvents.RewardedAdResult.Raise(new RewardedAdResultEvent(
                        placement,
                        RewardedAdResultType.Busy,
                        result.Message));
                    ClearRewardedPlacement();
                    break;
                case AdServiceResultType.SdkUnavailable:
                    if (Application.isEditor && settings != null && settings.SimulateRewardInEditorWithoutSdk)
                    {
                        StartEditorRewardSimulation(placement);
                        return;
                    }

                    CoreEvents.RewardedAdResult.Raise(new RewardedAdResultEvent(
                        placement,
                        RewardedAdResultType.SdkUnavailable,
                        result.Message));
                    ClearRewardedPlacement();
                    break;
            }
        }

        private void StartEditorRewardSimulation(RewardedAdPlacement placement)
        {
            if (_editorRewardSimulationRunning)
            {
                CoreEvents.RewardedAdResult.Raise(new RewardedAdResultEvent(
                    placement,
                    RewardedAdResultType.Busy,
                    "Editor rewarded simulation is already running."));
                ClearRewardedPlacement();
                return;
            }

            StartCoroutine(EditorRewardSimulationRoutine(placement));
        }

        private IEnumerator EditorRewardSimulationRoutine(RewardedAdPlacement placement)
        {
            _editorRewardSimulationRunning = true;
            yield return new WaitForSecondsRealtime(settings.EditorSimulatedRewardDelay);
            _editorRewardSimulationRunning = false;

            CoreEvents.RewardedAdResult.Raise(new RewardedAdResultEvent(
                placement,
                RewardedAdResultType.RewardEarned,
                "Editor rewarded simulation completed."));
            ClearRewardedPlacement();
        }

        private RewardedAdPlacement ResolveRewardedPlacement()
        {
            return _rewardedPlacementInFlight == RewardedAdPlacement.None
                ? RewardedAdPlacement.Revive
                : _rewardedPlacementInFlight;
        }

        private void ClearRewardedPlacement()
        {
            _rewardedPlacementInFlight = RewardedAdPlacement.None;
        }

        private void ScheduleConfiguredPreloads(AdPreloadTrigger trigger)
        {
            if (settings == null || _service == null)
                return;

            StopConfiguredPreloads();
            _preloadRoutine = StartCoroutine(PreloadFormatsRoutine(trigger));
        }

        private IEnumerator PreloadFormatsRoutine(AdPreloadTrigger trigger)
        {
            foreach (AdFormatType format in KnownFormats)
            {
                if (!settings.IsFormatEnabled(format) || !settings.HasConfiguredAdUnitId(format))
                    continue;

                bool shouldPreload = trigger switch
                {
                    AdPreloadTrigger.Initialize => settings.ShouldPreloadOnInitialize(format),
                    AdPreloadTrigger.MenuOpened => settings.ShouldPreloadWhenMenuOpens(format),
                    AdPreloadTrigger.Loading => settings.ShouldPreloadDuringLoading(format),
                    _ => false,
                };

                if (!shouldPreload)
                    continue;

                PreloadAd(format);
                yield return null;
            }

            _preloadRoutine = null;
        }

        private void StopConfiguredPreloads()
        {
            if (_preloadRoutine == null)
                return;

            StopCoroutine(_preloadRoutine);
            _preloadRoutine = null;
        }

        private void ScheduleRetry(AdFormatType format)
        {
            if (settings == null ||
                !settings.AutoRetryLoadAfterFailure ||
                !settings.IsFormatEnabled(format) ||
                !settings.HasConfiguredAdUnitId(format) ||
                _retryCoroutines.ContainsKey(format))
            {
                return;
            }

            _retryCoroutines[format] = StartCoroutine(RetryRoutine(format));
        }

        private IEnumerator RetryRoutine(AdFormatType format)
        {
            yield return new WaitForSecondsRealtime(settings.RetryLoadDelaySeconds);
            _retryCoroutines.Remove(format);

            if (_service == null || _service.IsReady(format))
                yield break;

            PreloadAd(format);
        }

        private void CancelRetry(AdFormatType format)
        {
            if (!_retryCoroutines.TryGetValue(format, out var routine) || routine == null)
                return;

            StopCoroutine(routine);
            _retryCoroutines.Remove(format);
        }

        private void StopAllRetryCoroutines()
        {
            foreach (var pair in _retryCoroutines)
            {
                if (pair.Value != null)
                    StopCoroutine(pair.Value);
            }

            _retryCoroutines.Clear();
        }

        private void DrainMainThreadActions()
        {
            int processedCount = 0;

            while (processedCount < MaxMainThreadActionsPerFrame)
            {
                Action action;

                lock (_mainThreadActions)
                {
                    if (_mainThreadActions.Count == 0)
                        break;

                    action = _mainThreadActions.Dequeue();
                }

                action?.Invoke();
                processedCount++;
            }
        }

        private void EnqueueMainThread(Action action)
        {
            if (action == null)
                return;

            lock (_mainThreadActions)
            {
                _mainThreadActions.Enqueue(action);
            }
        }

        private enum AdPreloadTrigger
        {
            Initialize,
            MenuOpened,
            Loading,
        }
    }
// }
