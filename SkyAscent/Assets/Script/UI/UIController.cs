using Unity.VisualScripting;
using UnityEngine;

public partial class UIController : CoreEventBase
{
    [SerializeField] private UIManager UIManager;

    protected override void Awake()
    {
        base.Awake();
    }

}

// Event
public partial class UIController
{
    public override void SubscribeEvents()
    {
        CoreEvents.UpdateUIHealth.Subscribe(e => UpdateUIHealth(e), Binder);
        CoreEvents.UpdateUIValue.Subscribe(e => UpdateUIScoreValue(e), Binder);
        CoreEvents.SnapshotAction.Subscribe(e => OnSnapshotAction(e), Binder);
    }
}

// proffesion
public partial class UIController
{
    private void OnSnapshotAction(SnapshotActionEvent e)
    {
        //Debug.LogError($"[UIManager] aaaaaaaaaaa");

        if (e.eventType == CoreEventType.Send)
        {
            if (e.snapshotType == SnapShotType.ScoreSnapshot)
            {
                UpdateUIScoreValueWithSnapshot(e.scoreSnapshot);
            }
        }
    }

    void UpdateUIScoreValueWithSnapshot(ScoreSnapshot cs)
    {
        Debug.Log($"[UIController] Score Value on Snapshot is: {cs.CoinQuantity}");

       bool result = UIManager.SetValueInt(CoinName, cs.CoinQuantity);
    }
}

// Score Update
public partial class UIController
{
    private const string CoinName = "Value_Coin";
    private const string HealthValueName = "Value_Health";
    private const string HealthBarName = "Value_Heath";

    private void UpdateUIHealth(UpdateUIHealthEvent e)
    {
        if (UIManager.IsUnityNull()) return;
        if (e == null) return;

        float percent = e.MaxHealth > 0
            ? Mathf.Clamp01((float)e.CurrentHealth / e.MaxHealth) * 100f
            : 0f;

        UIManager.SetValueInt(HealthValueName, e.MaxHealth);
        SetHealthProgress(percent);
    }

    private void UpdateUIScoreValue(UpdateUIValueEvent e)
    {
        if (UIManager.IsUnityNull()) return;

        switch (e.MoneyType)
        {
            case MoneyType.Coin:
                UIManager.SetValueInt(CoinName, e.Quantity);
                break;

            default:
                break;
        }
    }

    public bool SetProgressValue(string elementName, float value)
    {
        if (UIManager.IsUnityNull()) return false;
        return UIManager.SetProgressValue(elementName, value);
    }

    public bool SetHealthProgress(float value)
    {
        return SetProgressValue(HealthBarName, value);
    }
}
