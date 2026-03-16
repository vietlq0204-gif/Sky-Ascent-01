using UnityEngine;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;

// dùng để lưu playerData, truyền đi
[Serializable]
public class PlayerContext
{
    public int Health;

    public PlayerContext(int health)
    {
        this.Health = health;
    }
}

public partial class ShipController : CoreEventBase, /*IInject<ShipDataSO>,*/ IInject<PlayerContext>, IInject<ItemManager>, IInject<GameBootstrap>
{
    public const int DefaultMaxHealth = 3;
    public const int DefaultCoinHealthUpgradeCost = 20;
    public const int DefaultHealthUpgradeAmount = 1;

    //[SerializeField, Tooltip("Config của Ship (SO)")]
    //private ShipDataSO shipDataSO;
    [SerializeField] private int HealthData = DefaultMaxHealth;
    [SerializeField] private int healthUpgradeCoinCost = DefaultCoinHealthUpgradeCost;
    [SerializeField] private int healthUpgradeAmount = DefaultHealthUpgradeAmount;
    private bool Isdead = false;

    [SerializeField, Tooltip("Data hiện tại của Ship")]
    private PlayerContext playerContext;

    [SerializeField] private ItemManager itemManager;
    [SerializeField] private GameBootstrap gameBootstrap;

    [SerializeField, Tooltip("Các thành phần của Ship")]
    private List<GameObject> shipIngredient;

    #region Editor
#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!Application.isPlaying)
        {
            GetShipIngredient();
            //GetData();
        }

    }

#endif
    #endregion

    #region Unity lifecycle

    private void Start()
    {
        InitPlayerContext();

    }

    private void Update()
    {
        CheckPlayerDead(); // tạm thời gọi ở đâu để test, fix lại sau
    }

    #endregion

    #region Inject
    //public void Inject(ShipDataSO context) => shipDataSO = context;
    public void Inject(PlayerContext context) => playerContext = context;
    public void Inject(ItemManager context) => itemManager = context;
    public void Inject(GameBootstrap context) => gameBootstrap = context;

    #endregion

}

// Player logic
public partial class ShipController
{
    private int MaxHealth => Mathf.Max(HealthData, 1);

    private void InitPlayerContext()
    {
        ResolveDependencies();

        // tạm thời khởi tạo data cho playerContext ở đây
        //playerContext = new PlayerContext(shipDataSO.EngineDataSO.Isp, shipDataSO.FuelTankDataSO.Weigh);
        playerContext = new PlayerContext(HealthData);
        BroadcastHealthUI();
    }

    private enum CalculatorType
    {
        None,
        Add,
        Minus,
    }

    #region PlayerLifecycle
    private void ResetHealth()
    {
        playerContext ??= new PlayerContext(HealthData);
        playerContext.Health = HealthData; // mai mốt thay bằng data so 
        Isdead = false;
        BroadcastHealthUI();
        CoreEvents.PlayerFife.Raise(new PlayerFifeEvent(PlayerFifeState.Life));
    }

    private void ApplyDamage(CalculatorType calculatorType, int damageAmout)
    {
        var type = calculatorType;
        if (type == CalculatorType.None) return;

        UpdateHealth(type, damageAmout);
    }

    private void UpdateHealth(CalculatorType type, int healthAmout)
    {
        if (type == CalculatorType.Add)
            playerContext.Health += healthAmout;

        else if (type == CalculatorType.Minus)
            playerContext.Health -= healthAmout;

        BroadcastHealthUI();
    }

    private void BroadcastHealthUI()
    {
        int maxHealth = MaxHealth;
        int currentHealth = playerContext != null
            ? Mathf.Clamp(playerContext.Health, 0, maxHealth)
            : 0;

        CoreEvents.UpdateUIHealth.Raise(new UpdateUIHealthEvent(currentHealth, maxHealth));
    }

    private void CheckPlayerDead()
    {
        if (Isdead) return;
        if (playerContext.Health <= 0)
        {
            //Debug.LogError("Player dead");
            Isdead = true;

            // gọi event player dead
            CoreEvents.PlayerFife.Raise(new PlayerFifeEvent(PlayerFifeState.dead));

        }
    }

    #endregion

}

// Ubility
public partial class ShipController
{
    private void ResolveDependencies()
    {
        itemManager ??= FindFirstObjectByType<ItemManager>();
        gameBootstrap ??= FindFirstObjectByType<GameBootstrap>();
    }

    private int GetHealthUpgradeAmount()
    {
        return healthUpgradeAmount > 0 ? healthUpgradeAmount : DefaultHealthUpgradeAmount;
    }

    private int GetHealthUpgradeCoinCost()
    {
        return healthUpgradeCoinCost > 0 ? healthUpgradeCoinCost : DefaultCoinHealthUpgradeCost;
    }

    private void UpgradeMaxHealth(int amount)
    {
        if (amount <= 0) return;

        int newMaxHealth = MaxHealth + amount;
        int currentHealth = playerContext != null ? playerContext.Health : MaxHealth;

        HealthData = newMaxHealth;
        playerContext ??= new PlayerContext(newMaxHealth);
        playerContext.Health = Mathf.Clamp(currentHealth + amount, 0, newMaxHealth);
        Isdead = false;

        BroadcastHealthUI();
        SavePlayerStat();
        CoreEvents.OnUIPress.Raise(new OnUIPressEvent(UIPress.Tab_ClosePopup));
    }

    private void TryUpgradeMaxHealthWithCoin()
    {
        ResolveDependencies();

        if (itemManager == null)
        {
            Debug.LogWarning("[ShipController] ItemManager not found. Cannot spend coin for health upgrade.");
            return;
        }

        int cost = GetHealthUpgradeCoinCost();
        if (!itemManager.TrySpendCoins(cost))
        {
            Debug.LogWarning($"[ShipController] Not enough coin to upgrade health. Need {cost}, current {itemManager.GetCoinAmount()}.");
            return;
        }

        UpgradeMaxHealth(GetHealthUpgradeAmount());
    }

    private void SavePlayerStat()
    {
        ResolveDependencies();
        gameBootstrap?.SaveNow();
    }

    /// <summary>
    /// Lấy tất cả thành phần của Ship (chưa tối ưu)
    /// </summary>
    void GetShipIngredient()
    {
        shipIngredient.Clear();

        foreach (Transform child in transform)
        {
            shipIngredient.Add(child.gameObject);
        }
    }
}

// Event
public partial class ShipController
{
    public override void SubscribeEvents()
    {
        CoreEvents.OnMenu.Subscribe(e => OnMenu(e), Binder);
        CoreEvents.UpdgadePanel.Subscribe(e => OnUpgradePanel(e), Binder);
        CoreEvents.OnUIPress.Subscribe(e => OnUIPress(e.UIPress), Binder);

        // Session event
        CoreEvents.OnNewSession.Subscribe(_ => NewSession(), Binder);
        CoreEvents.OnSession.Subscribe(_ => OnSession(), Binder);
        CoreEvents.OnPrepareEnd.Subscribe(_ => PrepareEndSession(), Binder);
        CoreEvents.OnEndSession.Subscribe(_ => EndSession(), Binder);
        CoreEvents.PlayerFife.Subscribe(e => OnPlayerFife(e), Binder);
        CoreEvents.RewardedAdResult.Subscribe(e => OnRewardedAdResult(e), Binder);

        // SpecialZone event
        CoreEvents.OnColliderWithObjBySpecialZone.Subscribe
        (
            e =>
            {
                if (e.isEnter)
                    OnEnterColliderWithSpecialZone(e.specialZoneContext);
                else
                    OnExitCollierWithSpecialZone(e.specialZoneContext);
            },
            Binder
        );

        CoreEvents.InteractObstacle.Subscribe(e => OnInteractObstacle(e), Binder);

    }
}

// Profession 
public partial class ShipController
{
    private void OnUIPress(UIPress press)
    {
        if (press != UIPress.Tab_CoinAddHealth) return;

        TryUpgradeMaxHealthWithCoin();
    }

    private void OnRewardedAdResult(RewardedAdResultEvent e)
    {
        if (e == null) return;
        if (e.Placement != RewardedAdPlacement.AddHealth) return;
        if (e.ResultType != RewardedAdResultType.RewardEarned) return;

        UpgradeMaxHealth(GetHealthUpgradeAmount());
    }

    #region UI
    private async void OnMenu(OnMenuEvent e)
    {
        bool isOpenMenu = e.IsOpenMenu;

        await Task.Delay(2000); // lỏ cc

        if (isOpenMenu)
        {
            shipIngredient.ForEach(ingredient => ingredient.SetActive(false));
        }
        else
        {
            shipIngredient.ForEach(ingredient => ingredient.SetActive(true));
        }
    }

    private async void OnUpgradePanel(UpgradePanelEvent e)
    {
        bool isOpenUpgradePanel = e.IsOpenUpgradePanel;

        await Task.Delay(2000); // lỏ cc

        if (isOpenUpgradePanel)
        {
            shipIngredient.ForEach(ingredient => ingredient.SetActive(true));
        }
        else
        {
            shipIngredient.ForEach(ingredient => ingredient.SetActive(false));
        }
    }
    #endregion

    #region SpecialZone
    private void OnExitCollierWithSpecialZone(SpecialZoneContext ctx)
    {
        //ApplySpecialZoneStategy(ctx, false);

        //Debug.Log($"[ShipController] va chạm zone: {ctx.ZoneSO._name} ({ctx.ZoneType},{ctx.Strategy._name})");

    }

    private void OnEnterColliderWithSpecialZone(SpecialZoneContext ctx)
    {
        Debug.Log($"[ShipController] va chạm zone: {ctx.ZoneSO._name} ({ctx.ZoneType},{ctx.Strategy._name})");

        //ApplySpecialZoneStategy(ctx, true);
    }

    ///// <summary>
    ///// Áp dụng strategy cho PlayerContext
    ///// </summary>
    ///// <param name="ctx"></param>
    //private void ApplySpecialZoneStategy(SpecialZoneContext ctx, bool OnEnter)
    //{
    //    if (ctx.Strategy == null)
    //    {
    //        Debug.LogWarning("[ShipController] Strategy null trong context");
    //        return;
    //    }

    //    if (OnEnter)
    //    {
    //        playerContext.ispCurrent = ctx.Strategy.CalculatorIsp
    //            (playerContext, CalculatorType.Minus);

    //        playerContext.totalFuel = ctx.Strategy.CalculatorFuel
    //            (playerContext, CalculatorType.Minus);
    //    }
    //    else
    //    {
    //        playerContext.ispCurrent = ctx.Strategy.CalculatorIsp
    //            (playerContext, CalculatorType.Add);
    //    }

    //}

    #endregion

    #region Session

    private async Task NewSession()
    {
        // tắt menu
        //CoreEvents.OnMenu.Raise(new OnMenuEvent(false));
        shipIngredient.ForEach(ingredient => ingredient.SetActive(true));

        await EffectUtility.RaiseEffect(shipIngredient[1], 4f);

        ResetHealth();
    }

    /// <summary>
    /// Xử lí các logic của Ship khi OnSessionEvent được kích hoạt
    /// </summary>
    private async void OnSession()
    {

        await Task.Delay(0);

    }

    /// <summary>
    /// Chuẩn bị Data, Invironment cho Ship đến PrepareEndSession
    /// 
    /// </summary>
    private async void PrepareEndSession()
    {
        await Task.Delay(0);
    }

    /// <summary>
    /// Xử lí các logic của Ship khi EndSessionEvent được kích hoạt
    /// - chạy Effect và đợi nó kết thúc
    /// </summary>
    private async void EndSession()
    {
        await Task.Delay(0);
        await EffectUtility.RaiseEffect(shipIngredient[1], 1.5f, play: false);
    }

    private void OnPlayerFife(PlayerFifeEvent e)
    {
        if (e == null) return;
        if (e.state != PlayerFifeState.Revive) return;

        ResetHealth();
    }

    #endregion

    private void OnInteractObstacle(InteractObstacleEvent e)
    {
        var ObstacleCt = e.obstacleContext;
        var DamageCt = e.obstacleContext.damageContext;

        //Debug.LogError($"[Shipcontroler] Receive event: <{DamageCt.Cause}> " +
        //    $"<{ObstacleCt.interactType}> " +
        //    $"to <{DamageCt.Victim}> " +
        //    $"with <{DamageCt.Amout}> damage");

        var calculatorType = CalculatorType.Minus; // default is minus

        ApplyDamage(calculatorType, DamageCt.Amout);
    }
}

public partial class ShipController
{
    public PlayerStatSaveData CapturePlayerStat()
    {
        return new PlayerStatSaveData
        {
            MaxHealth = MaxHealth,
        };
    }

    public void ApplyPlayerStat(PlayerStatSaveData data)
    {
        if (data == null) return;

        HealthData = Mathf.Max(data.MaxHealth, DefaultMaxHealth);
        playerContext ??= new PlayerContext(MaxHealth);
        playerContext.Health = Mathf.Clamp(playerContext.Health, 0, MaxHealth);
    }

    public void AfterApplyPlayerStat()
    {
        BroadcastHealthUI();
    }
}

[Serializable]
public sealed class PlayerStatSaveData
{
    public int MaxHealth = ShipController.DefaultMaxHealth;
}

public sealed class PlayerStatSaveAdapter : Save.Abstractions.ISaveable, IInject<ShipController>
{
    private ShipController _shipController;

    public PlayerStatSaveAdapter(ShipController shipController)
    {
        _shipController = shipController;
    }

    public void Inject(ShipController context)
    {
        _shipController = context;
    }

    public string Key => "PlayerStat";

    public int Version => 1;

    public bool ShouldSave => _shipController != null;

    public void BeforeSave()
    {
    }

    public object Capture()
    {
        return _shipController?.CapturePlayerStat();
    }

    public void Restore(object data, int version)
    {
        if (data is PlayerStatSaveData dto)
        {
            _shipController?.ApplyPlayerStat(dto);
        }
    }

    public void AfterLoad()
    {
        _shipController?.AfterApplyPlayerStat();
    }
}




