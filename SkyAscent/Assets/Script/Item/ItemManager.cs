using System;
using System.Threading.Tasks;
using UnityEngine;

[Serializable]
public sealed class ScoreItemData
{
    [SerializeField] public int CoinAmount;
    // xp, level, ...
}

#region Snapshot
public readonly struct ScoreSnapshot
{
    public readonly int CoinQuantity;
    // xp, level...

    public ScoreSnapshot(int coin)
    {
        CoinQuantity = coin;
        // xp, level...
    }
}
#endregion

/// <summary>
/// DTO: Data Transfer Object, dùng để lưu trữ dữ liệu của ItemManager khi save/load game.
/// </summary>
public sealed class ItemSaveData
{
    public ScoreItemData ScoreItem;

    [Serializable]
    public sealed class ScoreItemData
    {
        public int CoinAmount;
        // xp, level, ...
    }
}

public partial class ItemManager : CoreEventBase
{
    public const int DefaultRewardedAdCoinAmount = 10;

    [SerializeField] ScoreItemData scoreData;
    [SerializeField] private int rewardedAdCoinAmount = DefaultRewardedAdCoinAmount;

    private ScoreSnapshot scoreSnapshot;

    protected override void Awake()
    {
        base.Awake();
        EnsureScoreData();
        RebuildSnapshot();
    }
}

// Snapshot logic
public partial class ItemManager
{
    private void EnsureScoreData()
    {
        scoreData ??= new ScoreItemData();
    }

    private int GetRewardedAdCoinAmount()
    {
        return rewardedAdCoinAmount > 0 ? rewardedAdCoinAmount : DefaultRewardedAdCoinAmount;
    }

    private void AddCoins(int amount)
    {
        if (amount <= 0) return;

        EnsureScoreData();
        scoreData.CoinAmount += amount;
        RebuildSnapshot();

        CoreEvents.UpdateUIValue.Raise(new UpdateUIValueEvent(
            MoneyType.Coin,
            scoreData.CoinAmount));
    }

    public int GetCoinAmount()
    {
        EnsureScoreData();
        return scoreData.CoinAmount;
    }

    public bool TrySpendCoins(int amount)
    {
        if (amount <= 0) return true;

        EnsureScoreData();
        if (scoreData.CoinAmount < amount)
            return false;

        scoreData.CoinAmount -= amount;
        RebuildSnapshot();

        CoreEvents.UpdateUIValue.Raise(new UpdateUIValueEvent(
            MoneyType.Coin,
            scoreData.CoinAmount));

        return true;
    }

    private void RebuildSnapshot()
    {
        //Debug.LogError($"[ItemManager] Coin quantity of snapshot is: {scoreData.CoinAmount}");

        scoreSnapshot = BuildScoreSnapshot(scoreData);
    }

    private ScoreSnapshot BuildScoreSnapshot(ScoreItemData scoreData)
    {
        //if (scoreData == null)
        //    return scoreSnapshot.Empty;

        return new ScoreSnapshot(
            scoreData.CoinAmount
            // xp,
            // level, ...
            );
    }

}

// save
public partial class ItemManager
{
    /// <summary>
    /// CaptureState: Lấy dữ liệu hiện tại của ItemManager và trả về một DTO (ItemSaveData) để lưu trữ.
    /// </summary>
    /// <returns></returns>
    public ItemSaveData CaptureState()
    {
        EnsureScoreData();

        var Data = new ItemSaveData();
        //Debug.LogError($"CoinAmount is: {scoreData.CoinAmount}");

        Data.ScoreItem = new ItemSaveData.ScoreItemData
        {
            CoinAmount = scoreData.CoinAmount,
            // xp, level, ...
        };

        // Data.WeaponItem = new ItemSaveData.WeaponItemData { ... };

        return Data;
    }

    public void ApplySaveData(ItemSaveData data)
    {
        EnsureScoreData();

        if (data == null)
        {
            Debug.LogError($"[UIManaget] ItemSaveData null");
            return;
        }
        if (data.ScoreItem != null)
        {
            //Debug.LogError($"CoinAmount on save is: {data.ScoreItem.CoinAmount}");

            scoreData.CoinAmount = data.ScoreItem.CoinAmount;
            // xp, level, ...
        }
        // if (data.WeaponItem != null)
        // {
        //     // áp dụng dữ liệu vũ khí
        // }

        //AfterApplySaveData();
    }

    public void AfterApplySaveData()
    {
        RebuildSnapshot();
        Broadcast_Snapshot();
    }

}

// event
public partial class ItemManager
{
    public override void SubscribeEvents()
    {
        CoreEvents.InteractMoney.Subscribe(e => InteractMoney(e), Binder);
        CoreEvents.RewardedAdResult.Subscribe(e => OnRewardedAdResult(e), Binder);
    }

    async void Broadcast_Snapshot()
    {
        await Task.Delay(500); // đợi cho UI, ... khởi tạo xong

        try
        {
            ScoreSnapshot sss = scoreSnapshot;

            //Debug.Log($"[ItemManager] Coin quantity of snapshot is: {sss.CoinQuantity}");

            CoreEvents.SnapshotAction.Raise(new SnapshotActionEvent(
            CoreEventType.Send,
            SnapShotType.ScoreSnapshot,
            sss
            ));
        }
        catch (Exception e)
        {

            throw e;
        }

    }
}

// profession
public partial class ItemManager
{
    private void OnRewardedAdResult(RewardedAdResultEvent e)
    {
        if (e == null) return;
        if (e.Placement != RewardedAdPlacement.AddCoin) return;
        if (e.ResultType != RewardedAdResultType.RewardEarned) return;

        AddCoins(GetRewardedAdCoinAmount());
    }

    void InteractMoney(InteractMoneyEvent e)
    {
        if (e == null) return;
        if (e.moneyContext.MoneyData == null) return;
        if (!e.moneyContext.CanPickUp) return;

        switch (e.interactItemType)
        {
            case InteractItemType.Pickup:
                switch (e.moneyContext.MoneyData.MonenyType)
                {
                    case MoneyType.Coin:
                        AddCoins(e.moneyContext.MoneyData.quantity);
                        break;
                    default:
                        break;
                }
                break;
            default:
                break;
        }
    }

}
