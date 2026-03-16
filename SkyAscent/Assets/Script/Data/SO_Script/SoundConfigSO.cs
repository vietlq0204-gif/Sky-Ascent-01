using System;
using System.Collections.Generic;
using UnityEngine;

public enum SoundCue
{
    None,
    UIButton,
    Popup,
    CoinCollision,
    StoneCollision,
    NewSession,
    OnSession,
    PrepareEndSession,
    Background,
}

public enum SoundChannel
{
    Music,
    UI,
    GamePlay,
}

[Serializable]
public class SoundModel
{
    [SerializeField] private AudioClip clip;
    [SerializeField, Range(0f, 1f)] private float volume = 1f;
    [SerializeField] private bool loop;

    public AudioClip Clip => clip;
    public float Volume => volume;
    public bool Loop => loop;
    public bool HasClip => clip != null;
}

public struct SoundDefinition
{
    public SoundCue Cue { get; }
    public SoundChannel Channel { get; }
    public SoundModel Model { get; }
    public bool HasClip => Model != null && Model.HasClip;

    public SoundDefinition(SoundCue cue, SoundChannel channel, SoundModel model)
    {
        Cue = cue;
        Channel = channel;
        Model = model;
    }
}

public abstract class SoundCollectionSO : BaseSO
{
    [NonSerialized] private SoundDefinition[] _definitions;
    [NonSerialized] private Dictionary<SoundCue, int> _lookup;

    public bool TryGetSound(SoundCue cue, out SoundDefinition definition)
    {
        EnsureLookup();

        if (_lookup != null &&
            _lookup.TryGetValue(cue, out int index) &&
            index >= 0 &&
            index < _definitions.Length)
        {
            definition = _definitions[index];
            return definition.HasClip;
        }

        definition = default;
        return false;
    }

    protected static SoundDefinition CreateDefinition(SoundCue cue, SoundChannel channel, SoundModel model)
    {
        return new SoundDefinition(cue, channel, model);
    }

    protected abstract SoundDefinition[] CreateDefinitions();

    private void OnEnable()
    {
        _definitions = null;
        _lookup = null;
    }

    private void EnsureLookup()
    {
        if (_definitions != null && _lookup != null)
            return;

        _definitions = CreateDefinitions() ?? Array.Empty<SoundDefinition>();
        _lookup = new Dictionary<SoundCue, int>(_definitions.Length);

        for (int i = 0; i < _definitions.Length; i++)
        {
            SoundDefinition definition = _definitions[i];
            if (definition.Cue == SoundCue.None)
                continue;

            if (_lookup.ContainsKey(definition.Cue))
            {
                Debug.LogWarning($"[SoundCollectionSO] Duplicate cue {definition.Cue} in {name}. Keeping first mapping.");
                continue;
            }

            _lookup.Add(definition.Cue, i);
        }
    }
}

[CreateAssetMenu(menuName = "Sound/Sound Config SO")]
public class SoundConfigSO : BaseSO
{
    [Header("Collections")]
    [SerializeField] private MusicSO music;
    [SerializeField] private UISoundSO ui;
    [SerializeField] private GamePlaySoundSO gamePlay;

    public MusicSO Music => music;
    public UISoundSO UI => ui;
    public GamePlaySoundSO GamePlay => gamePlay;

    public bool TryGetSound(SoundCue cue, out SoundDefinition definition)
    {
        if (TryGetSound(music, cue, out definition))
            return true;

        if (TryGetSound(ui, cue, out definition))
            return true;

        if (TryGetSound(gamePlay, cue, out definition))
            return true;

        definition = default;
        return false;
    }

    public bool TryGetSound(SoundCue cue, out SoundModel sound, out SoundChannel channel)
    {
        if (TryGetSound(cue, out SoundDefinition definition))
        {
            sound = definition.Model;
            channel = definition.Channel;
            return true;
        }

        sound = null;
        channel = SoundChannel.UI;
        return false;
    }

    private static bool TryGetSound(SoundCollectionSO collection, SoundCue cue, out SoundDefinition definition)
    {
        if (collection != null && collection.TryGetSound(cue, out definition))
            return true;

        definition = default;
        return false;
    }
}
