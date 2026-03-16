using System.Threading.Tasks;
using UnityEngine;

[System.Serializable]
public struct SoundVolumeState
{
    public float MasterVolume;
    public float MusicVolume;
    public float UIVolume;
    public float GamePlayVolume;

    public SoundVolumeState(
        float masterVolume,
        float musicVolume,
        float uiVolume,
        float gamePlayVolume)
    {
        MasterVolume = masterVolume;
        MusicVolume = musicVolume;
        UIVolume = uiVolume;
        GamePlayVolume = gamePlayVolume;
    }
}

[DefaultExecutionOrder(-450)]
public class SoundManager : CoreEventBase
{
    private const string DefaultConfigResourcePath = "Data/SoundConfig_Default";
    private const string MusicSourceName = "Audio_Music";
    private const string UiSourceName = "Audio_UI";
    private const string GamePlaySourceName = "Audio_GamePlay";
    private const string PlayerPrefsKeyPrefix = "SkyAscent.Sound.";
    private const string MasterVolumeKey = PlayerPrefsKeyPrefix + "MasterVolume";
    private const string MusicVolumeKey = PlayerPrefsKeyPrefix + "MusicVolume";
    private const string UIVolumeKey = PlayerPrefsKeyPrefix + "UIVolume";
    private const string GamePlayVolumeKey = PlayerPrefsKeyPrefix + "GamePlayVolume";
    private const string LegacyPopupVolumeKey = PlayerPrefsKeyPrefix + "PopupVolume";
    private const string LegacyCollisionVolumeKey = PlayerPrefsKeyPrefix + "CollisionVolume";
    private const string LegacySessionVolumeKey = PlayerPrefsKeyPrefix + "SessionVolume";

    public static SoundManager Instance { get; private set; }

    [SerializeField] private SoundConfigSO _soundConfig;

    [Header("Volume")]
    [SerializeField, Range(0f, 1f)] private float _masterVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float _musicVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float _uiVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float _gamePlayVolume = 1f;

    private bool _isPrimaryInstance;
    private AudioSource _musicSource;
    private AudioSource _uiSource;
    private AudioSource _gamePlaySource;

    public SoundConfigSO SoundConfig => _soundConfig;
    public float MasterVolume => _masterVolume;
    public float MusicVolume => _musicVolume;
    public float UIVolume => _uiVolume;
    public float GamePlayVolume => _gamePlayVolume;

    protected override void Awake()
    {
        base.Awake();

        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        _isPrimaryInstance = true;

        LoadDefaultConfigIfNeeded();
        LoadSavedVolumeSettings();
        EnsureAudioSources();
        ApplyChannelVolumes();
    }

    private void Start()
    {
        PlayBackgroundLoop();
    }

    private void OnDestroy()
    {
        if (!_isPrimaryInstance || Instance != this)
            return;

        StoreVolumeSettings();
        FlushStoredVolumeSettings();

        Instance = null;
    }

    private void OnApplicationPause(bool pause)
    {
        if (!_isPrimaryInstance || Instance != this)
            return;

        if (!pause)
            return;

        FlushStoredVolumeSettings();
    }

    private void OnApplicationQuit()
    {
        if (!_isPrimaryInstance || Instance != this)
            return;

        FlushStoredVolumeSettings();
    }

    public override void SubscribeEvents()
    {
        if (Instance != null && Instance != this)
            return;

        CoreEvents.OnUIButtonClick.Subscribe(_ => Play(SoundCue.UIButton), Binder);
        CoreEvents.OnPopupState.Subscribe(OnPopupStateChanged, Binder);
        CoreEvents.InteractMoney.Subscribe(OnInteractMoney, Binder);
        CoreEvents.InteractObstacle.Subscribe(OnInteractObstacle, Binder);
        
        CoreEvents.OnNewSession.Subscribe(_ => NewSession(), Binder);
        CoreEvents.OnSession.Subscribe(OnSessionStarted, Binder);
        CoreEvents.OnPrepareEnd.Subscribe(_ => PrepareEndSession(), Binder);
    }

    private async Task NewSession()
    {
        await Task.Delay(3000);
        Play(SoundCue.NewSession);
    }

    private async Task PrepareEndSession()
    {
        await Task.Delay(1000);
        Play(SoundCue.PrepareEndSession);
    }

    public void SetSoundConfig(SoundConfigSO soundConfig)
    {
        _soundConfig = soundConfig;
        EnsureAudioSources();
        ApplyChannelVolumes();

        if (_soundConfig == null)
        {
            StopBackground();
            return;
        }

        PlayBackgroundLoop();
    }

    public void Play(SoundCue cue)
    {
        if (!TryGetSound(cue, out SoundDefinition definition))
            return;

        if (cue == SoundCue.Background)
        {
            PlayBackgroundLoop();
            return;
        }

        AudioSource source = GetSource(definition.Channel);
        if (source == null)
            return;

        source.PlayOneShot(definition.Model.Clip, definition.Model.Volume);
    }

    public void PlayBackgroundLoop()
    {
        if (!TryGetSound(SoundCue.Background, out SoundDefinition definition))
            return;

        AudioSource source = GetSource(definition.Channel);
        if (source == null)
            return;

        bool clipChanged = source.clip != definition.Model.Clip;
        source.clip = definition.Model.Clip;
        source.loop = definition.Model.Loop;
        source.volume = GetEffectiveVolume(definition.Channel, definition.Model.Volume);

        if (clipChanged)
            source.Stop();

        if (!source.isPlaying)
            source.Play();
    }

    public void StopBackground()
    {
        if (_musicSource == null)
            return;

        _musicSource.Stop();
        _musicSource.clip = null;
    }

    public void SetMasterVolume(float volume)
    {
        _masterVolume = Mathf.Clamp01(volume);
        ApplyChannelVolumes();
        StoreVolumeSettings();
    }

    public void SetMusicVolume(float volume)
    {
        _musicVolume = Mathf.Clamp01(volume);
        ApplyChannelVolumes();
        StoreVolumeSettings();
    }

    public void SetUIVolume(float volume)
    {
        _uiVolume = Mathf.Clamp01(volume);
        ApplyChannelVolumes();
        StoreVolumeSettings();
    }

    public void SetGamePlayVolume(float volume)
    {
        _gamePlayVolume = Mathf.Clamp01(volume);
        ApplyChannelVolumes();
        StoreVolumeSettings();
    }

    public void SetChannelVolume(SoundChannel channel, float volume)
    {
        volume = Mathf.Clamp01(volume);

        switch (channel)
        {
            case SoundChannel.Music:
                _musicVolume = volume;
                break;

            case SoundChannel.UI:
                _uiVolume = volume;
                break;

            case SoundChannel.GamePlay:
                _gamePlayVolume = volume;
                break;
        }

        ApplyChannelVolumes();
        StoreVolumeSettings();
    }

    public float GetChannelVolume(SoundChannel channel)
    {
        switch (channel)
        {
            case SoundChannel.Music:
                return _musicVolume;

            case SoundChannel.UI:
                return _uiVolume;

            case SoundChannel.GamePlay:
                return _gamePlayVolume;

            default:
                return 1f;
        }
    }

    public SoundVolumeState GetVolumeState()
    {
        return new SoundVolumeState(
            _masterVolume,
            _musicVolume,
            _uiVolume,
            _gamePlayVolume);
    }

    public void SetVolumeState(SoundVolumeState volumeState)
    {
        _masterVolume = Mathf.Clamp01(volumeState.MasterVolume);
        _musicVolume = Mathf.Clamp01(volumeState.MusicVolume);
        _uiVolume = Mathf.Clamp01(volumeState.UIVolume);
        _gamePlayVolume = Mathf.Clamp01(volumeState.GamePlayVolume);

        ApplyChannelVolumes();
        StoreVolumeSettings();
    }

    public int GetMasterVolumePercent()
    {
        return VolumeToPercent(_masterVolume);
    }

    public void SetMasterVolumePercent(int percent)
    {
        SetMasterVolume(PercentToVolume(percent));
    }

    public int GetChannelVolumePercent(SoundChannel channel)
    {
        return VolumeToPercent(GetChannelVolume(channel));
    }

    public void SetChannelVolumePercent(SoundChannel channel, int percent)
    {
        SetChannelVolume(channel, PercentToVolume(percent));
    }

    private void OnPopupStateChanged(OnPopupStateEvent e)
    {
        if (e == null || !e.IsOpen || e.PopupType == PopupType.None)
            return;

        Play(SoundCue.Popup);
    }

    private void OnInteractMoney(InteractMoneyEvent e)
    {
        if (e == null || e.interactItemType != InteractItemType.Pickup)
            return;

        Play(SoundCue.CoinCollision);
    }

    private void OnInteractObstacle(InteractObstacleEvent e)
    {
        if (e == null || e.interactType != InteractType.collision)
            return;

        Play(SoundCue.StoneCollision);
    }

    private void OnSessionStarted(OnSessionEvent e)
    {
        if (e == null || !e.Started)
            return;

        Play(SoundCue.OnSession);
    }

    private void LoadDefaultConfigIfNeeded()
    {
        if (_soundConfig != null)
            return;

        _soundConfig = Resources.Load<SoundConfigSO>(DefaultConfigResourcePath);
        if (_soundConfig == null)
            Debug.LogWarning($"[SoundManager] Missing SoundConfigSO at Resources/{DefaultConfigResourcePath}.");
    }

    private void LoadSavedVolumeSettings()
    {
        _masterVolume = LoadVolumeOrDefault(MasterVolumeKey, _masterVolume);
        _musicVolume = LoadVolumeOrDefault(MusicVolumeKey, _musicVolume);

        if (PlayerPrefs.HasKey(GamePlayVolumeKey))
        {
            _uiVolume = LoadVolumeOrDefault(UIVolumeKey, _uiVolume);
            _gamePlayVolume = LoadVolumeOrDefault(GamePlayVolumeKey, _gamePlayVolume);
            return;
        }

        _uiVolume = LoadAverageVolumeOrDefault(_uiVolume, UIVolumeKey, LegacyPopupVolumeKey);
        _gamePlayVolume = LoadAverageVolumeOrDefault(_gamePlayVolume, LegacyCollisionVolumeKey, LegacySessionVolumeKey);
    }

    private void StoreVolumeSettings()
    {
        PlayerPrefs.SetFloat(MasterVolumeKey, _masterVolume);
        PlayerPrefs.SetFloat(MusicVolumeKey, _musicVolume);
        PlayerPrefs.SetFloat(UIVolumeKey, _uiVolume);
        PlayerPrefs.SetFloat(GamePlayVolumeKey, _gamePlayVolume);
    }

    private static void FlushStoredVolumeSettings()
    {
        PlayerPrefs.Save();
    }

    private void EnsureAudioSources()
    {
        _musicSource = GetOrCreateSource(MusicSourceName);
        _uiSource = GetOrCreateSource(UiSourceName);
        _gamePlaySource = GetOrCreateSource(GamePlaySourceName);
    }

    private AudioSource GetOrCreateSource(string sourceName)
    {
        Transform child = transform.Find(sourceName);
        AudioSource source = child != null ? child.GetComponent<AudioSource>() : null;

        if (source == null)
        {
            GameObject sourceObject = new GameObject(sourceName);
            sourceObject.transform.SetParent(transform, false);
            source = sourceObject.AddComponent<AudioSource>();
        }

        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.dopplerLevel = 0f;
        source.volume = 1f;
        source.loop = false;

        return source;
    }

    private void ApplyChannelVolumes()
    {
        if (_uiSource != null)
            _uiSource.volume = GetEffectiveVolume(SoundChannel.UI);

        if (_gamePlaySource != null)
            _gamePlaySource.volume = GetEffectiveVolume(SoundChannel.GamePlay);

        if (_musicSource == null)
            return;

        if (TryGetSound(SoundCue.Background, out SoundDefinition definition))
        {
            _musicSource.volume = GetEffectiveVolume(definition.Channel, definition.Model.Volume);
            return;
        }

        _musicSource.volume = GetEffectiveVolume(SoundChannel.Music);
    }

    private bool TryGetSound(SoundCue cue, out SoundDefinition definition)
    {
        definition = default;

        if (_soundConfig == null)
            return false;

        return _soundConfig.TryGetSound(cue, out definition);
    }

    private AudioSource GetSource(SoundChannel channel)
    {
        switch (channel)
        {
            case SoundChannel.Music:
                return _musicSource;

            case SoundChannel.UI:
                return _uiSource;

            case SoundChannel.GamePlay:
                return _gamePlaySource;

            default:
                return null;
        }
    }

    private float GetEffectiveVolume(SoundChannel channel, float clipVolume = 1f)
    {
        return Mathf.Clamp01(_masterVolume * GetChannelVolume(channel) * Mathf.Clamp01(clipVolume));
    }

    private static float LoadVolumeOrDefault(string key, float fallback)
    {
        if (!PlayerPrefs.HasKey(key))
            return Mathf.Clamp01(fallback);

        return Mathf.Clamp01(PlayerPrefs.GetFloat(key, fallback));
    }

    private static float LoadAverageVolumeOrDefault(float fallback, params string[] keys)
    {
        float total = 0f;
        int count = 0;

        for (int i = 0; i < keys.Length; i++)
        {
            string key = keys[i];
            if (!PlayerPrefs.HasKey(key))
                continue;

            total += Mathf.Clamp01(PlayerPrefs.GetFloat(key, fallback));
            count++;
        }

        if (count == 0)
            return Mathf.Clamp01(fallback);

        return Mathf.Clamp01(total / count);
    }

    private static int VolumeToPercent(float volume)
    {
        return Mathf.RoundToInt(Mathf.Clamp01(volume) * 100f);
    }

    private static float PercentToVolume(int percent)
    {
        return Mathf.Clamp01(percent / 100f);
    }
}
