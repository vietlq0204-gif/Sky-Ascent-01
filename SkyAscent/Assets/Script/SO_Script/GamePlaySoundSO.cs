using UnityEngine;

[CreateAssetMenu(menuName = "Sound/GamePlay Sound SO")]
public class GamePlaySoundSO : SoundCollectionSO
{
    [Header("Collision")]
    [SerializeField] private SoundModel coinCollision = new SoundModel();
    [SerializeField] private SoundModel stoneCollision = new SoundModel();

    [Header("Session")]
    [SerializeField] private SoundModel newSession = new SoundModel();
    [SerializeField] private SoundModel onSession = new SoundModel();
    [SerializeField] private SoundModel prepareEndSession = new SoundModel();

    public SoundModel CoinCollision => coinCollision;
    public SoundModel StoneCollision => stoneCollision;
    public SoundModel NewSession => newSession;
    public SoundModel OnSession => onSession;
    public SoundModel PrepareEndSession => prepareEndSession;

    protected override SoundDefinition[] CreateDefinitions()
    {
        return new[]
        {
            CreateDefinition(SoundCue.CoinCollision, SoundChannel.GamePlay, coinCollision),
            CreateDefinition(SoundCue.StoneCollision, SoundChannel.GamePlay, stoneCollision),
            CreateDefinition(SoundCue.NewSession, SoundChannel.GamePlay, newSession),
            CreateDefinition(SoundCue.OnSession, SoundChannel.GamePlay, onSession),
            CreateDefinition(SoundCue.PrepareEndSession, SoundChannel.GamePlay, prepareEndSession),
        };
    }
}
