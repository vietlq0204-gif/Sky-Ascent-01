using UnityEngine;

[CreateAssetMenu(menuName = "Sound/Music SO")]
public class MusicSO : SoundCollectionSO
{
    [Header("Music")]
    [SerializeField] private SoundModel background = new SoundModel();

    public SoundModel Background => background;

    protected override SoundDefinition[] CreateDefinitions()
    {
        return new[]
        {
            CreateDefinition(SoundCue.Background, SoundChannel.Music, background),
        };
    }
}
