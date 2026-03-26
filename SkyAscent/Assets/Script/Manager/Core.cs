using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;

public interface ICoreContext
{
    StateMachine StateMachine { get; }
    EventBinder Binder { get; }
}

public enum DelayType
{
    None,
    Loading,
    PrepareNewSession,
    PrepareEndSession
}

[RequireComponent(typeof(StateManager))]
[DefaultExecutionOrder(-500)]
public class Core : CoreEventBase, ICoreContext, IInject<GameBootstrap>
{
    [SerializeField] private GameBootstrap _gameBootstrap;

    private StateMachine _stateMachine;
    private StateMachine _secondaryStateMachine;

    public StateMachine StateMachine => _stateMachine;
    public StateMachine SecondaryStateMachine => _secondaryStateMachine;

    [SerializeField] private int targetFps = 60;

    [SerializeField] private string lastEventName;
    [SerializeField] private string primaryStateName;
    [SerializeField] private string secondaryStateName;
    public GameObject Player;

    [Tooltip("Thời gian")]
    [SerializeField] float T;   // thời gian (s)
    bool timeIsRunning;

    #region Unity lifecycle

    protected override void Awake()
    {
        base.Awake();
        //DontDestroyOnLoad(gameObject);
        Application.targetFrameRate = targetFps;
        
        _stateMachine = new StateMachine();
        _secondaryStateMachine = new StateMachine();

        _gameBootstrap.LoadNow();
        
        
    }

    private void OnValidate()
    {
        targetFps = Mathf.Max(-1, targetFps);
    }

    private void Start()
    {
        InitData();
    }

    private void Update()
    {
        UpdateVairableCore();

        if (timeIsRunning) T += Time.deltaTime; // cộng dồn mỗi frame


        //Application.targetFrameRate
    }

    #endregion

    #region Inject

    //public void Inject(ISaveSystem saveSystem) { _saveSystem = saveSystem; }
    public void Inject(GameBootstrap context) { _gameBootstrap = context; }


    #endregion

    #region Event
    public override void SubscribeEvents()
    {
        CoreEvents.OnUIPress.Subscribe(e => OnUIPress(e.UIPress), Binder);

        CoreEvents.OnQuitApp.Subscribe(e => OnQuitApp(), Binder);

        CoreEvents.OnEndSession.Subscribe(e => EndSession(e.IsComplete), Binder);
        CoreEvents.OnResumeSession.Subscribe(e => OnResumeSession(e), Binder);
        CoreEvents.RewardedAdResult.Subscribe(e => OnRewardedAdResult(e), Binder);

        //CoreEvents.PickupItem.Subscribe(e => testPickupItemEvent(e), Binder);

        CoreEvents.PlayerFife.Subscribe(e => CheckPlayerLife(e), Binder);
    }
    #endregion
    private void CheckPlayerLife(PlayerFifeEvent e)
    {
        var state = e.state;

        if (state == PlayerFifeState.dead)
        {
            // pause game
            Debug.LogWarning("Pause game");
        }
    }

    #region Player


    #endregion

    //ItemContext testPickupItemEvent(PickupItemEvent e)
    //{

    //    Debug.LogWarning($"[Core] \nEvent ( {e.GetType().Name} ) Raise" +
    //        $"\nCollision Phase: {e.collisionPhase}" +
    //        $"\nItem Type: {e.ItemContext.Type}" +
    //        $"\nQuantity: {e.ItemContext.Quantity}" +
    //        $"\nDescription: {e.ItemContext.Description}" +
    //        $"\nCan Pick Up: {e.ItemContext.CanPickUp}\n");
    //    return e.ItemContext;
    //}

    #region Profession 

    private void OnQuitApp()
    {
        Application.Quit();

#if UNITY_EDITOR
        if (Application.isEditor)
        {
            UnityEditor.EditorApplication.isPlaying = false; // dừng play mode trong editor
            return;
        }
#endif
    }

    private void EndSession(bool isComplete)
    {
        if (!isComplete) return;

        _gameBootstrap.SaveNow();

        Debug.Log("Save after endSession");
    }

    #endregion

    #region Logic

    private void OnResumeSession(OnResumeSessionEvent e)
    {
        if (e == null) return;

        if (e.ResumeType == ResumeSessionType.Revive)
        {
            CoreEvents.PlayerFife.Raise(new PlayerFifeEvent(PlayerFifeState.Revive));
        }

        StateMachine.ChangeState(typeof(OnSessionState));
    }

    private void OnRewardedAdResult(RewardedAdResultEvent e)
    {
        if (e == null) return;
        if (e.Placement != RewardedAdPlacement.Revive) return;

        if (e.ResultType == RewardedAdResultType.RewardEarned)
        {
            CoreEvents.OnContinueSession.Raise(new OnContinueSessionEvent(
                ContinueSessionType.Revive,
                PopupType.Popup_CountNumbers));
            return;
        }

        Debug.LogWarning($"[Core] Rewarded revive result: {e.ResultType}. {e.Message}");
    }

    private void InitData()
    {
        Player = Player != null ? Player : GameObject.FindGameObjectWithTag("Player");
    }

    private void UpdateVairableCore()
    {
        if (CoreEvents.LastEventName != lastEventName && !string.IsNullOrEmpty(CoreEvents.LastEventName))
        {
            //_history.Add(new EventRecord { Name = CoreEvents.LastEventName, Time = Time.time });
            lastEventName = CoreEvents.LastEventName;
        }
        primaryStateName = _stateMachine.CurrentStateName;
        secondaryStateName = _secondaryStateMachine.CurrentStateName;
    }

    /// <summary>
    /// Chọn loại Action dựa trên UIPress nhận được từ sự kiện OnUIPressEvent
    /// </summary>
    /// <param name="uIPress"></param>
    private void OnUIPress(UIPress uIPress)
    {
        switch (uIPress)
        {
            case UIPress.Tab_QuitApp:
                Debug.Log($"[Core] \nEvent ( {uIPress} ) Raise");

                CoreEvents.OnQuitApp.Raise();
                break;

            case UIPress.Tab_Home:
                if (SecondaryStateMachine.CurrentStateType == typeof(SettingState))
                {
                    CoreEvents.SettinngPanel.Raise(new SettingPanelEvent(false));
                    SecondaryStateMachine.ChangeState(typeof(NoneStateSecondary));

                    if (StateMachine.CurrentStateType == typeof(OnPauseSessionState))
                    {
                        CoreEvents.OnPauseSession.Raise(new OnPauseSessionEvent(PauseSessionType.Manual));
                    }
                    else
                    {
                        CoreEvents.OnMenu.Raise(new OnMenuEvent(true));
                    }

                    break;
                }

                //Debug.Log($"[Core] \nEvent ( {uIPress} ) Raise");
                CoreEvents.OnMenu.Raise(new OnMenuEvent(true));
                SecondaryStateMachine.ChangeState(typeof(NoneStateSecondary)); // resset secondary state khi về home
                break;

            case UIPress.Tab_Setting:
                //Debug.Log($"[Core] \nEvent ( {uIPress} ) Raise");
                CoreEvents.SettinngPanel.Raise(new SettingPanelEvent(true));
                break;

            case UIPress.Tab_Pause:
                CoreEvents.OnPauseSession.Raise(new OnPauseSessionEvent(PauseSessionType.Manual));
                break;

            case UIPress.Tab_Upgrade:
                //Debug.Log($"[Core] \nEvent ( {uIPress} ) Raise");
                CoreEvents.UpdgadePanel.Raise(new UpgradePanelEvent(true));
                break;

            case UIPress.Tab_Add_Coin:
            case UIPress.Tab_Add_Health:
            case UIPress.Tab_CoinAddHealth:
            case UIPress.Tab_ClosePopup:
                break;

            case UIPress.Tab_Play:
                //Debug.Log($"[Core] \nEvent ( {uIPress} ) Raise");
                //CoreEvents.OnDelay.Raise(new OnDelayEvent(DelayType.PrepareNewSession, 10f)); // 10f là để chuẩn bị đến khi tàu đạt OnSession
                CoreEvents.OnNewSession.Raise();
                break;

            case UIPress.Tab_Popup_Home:
                //Debug.Log($"[Core] \nEvent ( {uIPress} ) Raise");
                CoreEvents.OnQuitSession.Raise(new OnQuitSessionEvent(QuitSessionType.PopupHome));
                CoreEvents.OnMenu.Raise(new OnMenuEvent(true));
                SecondaryStateMachine.ChangeState(typeof(NoneStateSecondary));
                break;

            case UIPress.Tab_Popup_ContinueSession:
                CoreEvents.OnResumeSession.Raise(new OnResumeSessionEvent(ResumeSessionType.Pause));
                break;

            case UIPress.Tab_Popup_Revive:
                CoreEvents.RewardedAdRequest.Raise(new RewardedAdRequestEvent(RewardedAdPlacement.Revive));
                break;

            case UIPress.Tab_AdRewardCoin:
                CoreEvents.RewardedAdRequest.Raise(new RewardedAdRequestEvent(RewardedAdPlacement.AddCoin));
                break;

            case UIPress.Tab_AdAddHealth:
                CoreEvents.RewardedAdRequest.Raise(new RewardedAdRequestEvent(RewardedAdPlacement.AddHealth));
                break;
            default:
                Debug.LogWarning($"[Core]" +
                    $"\nNút ( {uIPress} ) Chưa được đăng kí cho Action || Event nào." +
                    $" Vui lòng kiểm tra tại đây!\n");
                break;
        }
    }

    #endregion

    #region API

    #endregion

    #region Helper public

    public static Task WaitForCoroutine(MonoBehaviour owner, IEnumerator routine)
    {
        var tcs = new TaskCompletionSource<bool>();

        owner.StartCoroutine(Run());

        IEnumerator Run()
        {
            yield return owner.StartCoroutine(routine);
            tcs.SetResult(true);
        }

        return tcs.Task;
    }

    /// <summary>
    /// helper zoom gameObject
    /// </summary>
    /// <remarks>
    /// CẦN NÂNG CẤP ĐỂ ZOOM MƯỢT HƠN.
    /// </remarks>
    /// <param name="transform"></param>
    /// <param name="scale"></param>
    public void ZoomGameObject(Transform transform, Vector3 scale)
    {
        transform.localScale = scale;
    }

    #region timer helper
    public void StartTimer()
    {
        T = 0f;
        timeIsRunning = true;
    }

    public void StopTimer()
    {
        timeIsRunning = false;
    }

    public void ResetTimer()
    {
        T = 0f;
    }
    #endregion

    /// <summary>
    /// helper đếm ngược trước và trả về bool result
    /// </summary>
    /// <param name="time"></param>
    /// <returns></returns>
    public IEnumerator WaitTime(float time, System.Action<bool> onComplete)
    {
        yield return new WaitForSeconds(time);
        onComplete?.Invoke(true);

        //StartCoroutine(_core.WaitTime(1f, (result) =>
        //{
        //    x = result;
        //}));
    }

    //public bool isPauseGame = false;
    //private void PauseGame()
    //{
    //    if (isPauseGame)
    //    {
    //        Time.timeScale = 0f; // tạm dừng game
    //        return;
    //    }

    //    Time.timeScale = 1f;
    //}

    // API debug

    #endregion
}
