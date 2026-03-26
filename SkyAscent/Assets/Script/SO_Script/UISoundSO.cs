using UnityEngine;

[CreateAssetMenu(menuName = "Sound/UI Sound SO")]
public class UISoundSO : SoundCollectionSO
{
    [Header("UI")]
    [SerializeField] private SoundModel uiButton = new SoundModel();
    [SerializeField] private SoundModel popup = new SoundModel();

    public SoundModel UIButton => uiButton;
    public SoundModel Popup => popup;

    protected override SoundDefinition[] CreateDefinitions()
    {
        return new[]
        {
            CreateDefinition(SoundCue.UIButton, SoundChannel.UI, uiButton),
            CreateDefinition(SoundCue.Popup, SoundChannel.UI, popup),
        };
    }
}
