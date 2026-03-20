using System;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public class EventHub<T> where T : class, new()
{
    private event Action<T> _onEvent;
    private readonly object _lock = new();

    public void Subscribe(Action<T> listener) { lock (_lock) _onEvent += listener; }

    public void Unsubscribe(Action<T> listener) { lock (_lock) _onEvent -= listener; }

    public void Subscribe(Action<T> l, EventBinder b)
    {
        lock (_lock) _onEvent += l;
        b.AddUnsubscriber(() => { lock (_lock) _onEvent -= l; });
    }

    public void Raise(T args)
    {
        CoreEvents.LastEventName = typeof(T).Name;
        Action<T> snapshot;
        lock (_lock) snapshot = _onEvent;
        snapshot?.Invoke(args);
    }

    // cần fix, có thể bỏ đi sau này vì nó bắt buộc new T() -> GC
    public void Raise()
    {
        CoreEvents.LastEventName = typeof(T).Name;
        Action<T> snapshot;
        lock (_lock) snapshot = _onEvent;
        snapshot?.Invoke(new T());
    }
}

#region Secondary Event

public class OnQuitAppEvent { }

public class NoneEvent
{
    public enum NoneType
    {
        all,
        primary,
        secondary,
    }

    public NoneType Type { get; private set; }

    public NoneEvent()
    {
        Type = NoneType.all;
    }

    public NoneEvent(NoneType type)
    {
        this.Type = type;
    }
}

public class OnSaveGameEvent
{
    public string fileSave;
}

public class OnLoadGameEvent { }

public class OnDelayEvent
{
    public DelayType DelayType { get; private set; }
    public float DelayTime { get; private set; }
    public bool IsEnd { get; private set; }

    public OnDelayEvent() { }

    public OnDelayEvent(DelayType type, float delayTime, bool isEnd = false)
    {
        DelayType = type;
        DelayTime = delayTime;
        IsEnd = isEnd;
    }
}

#endregion

#region Core Events

public class LoadDataEvent
{
    public enum TypeData
    {
        Chapter,
        Save,
        Load,
    }

    public TypeData typeData;
    public bool Completed { get; set; }

    public LoadDataEvent() { }

    public LoadDataEvent(TypeData typeData, bool completed)
    {
        this.typeData = typeData;
        this.Completed = completed;
    }

    //public static LoadDataEvent LoadFail() => new LoadDataEvent(false);
    //public static LoadDataEvent LoadCompleted() => new LoadDataEvent(true);
}

public class OnMenuEvent
{
    public bool IsOpenMenu { get; private set; } = false;

    /// <summary>
    /// true: open menu, false: close menu
    /// </summary>
    public OnMenuEvent()
    {
        IsOpenMenu = true;
    }

    /// <summary>
    /// true: open menu, false: close menu
    /// </summary>
    /// <param name="isOpenMenu"></param>
    public OnMenuEvent(bool isOpenMenu)
    {
        this.IsOpenMenu = isOpenMenu;
    }
}

#endregion

#region Request / Response

public enum CoreEventType
{
    Send, // không yêu cầu trả lại data
    Request, // yêu cầu trả lại data
    Response // phản ứng sau khi nhận request
}
public enum SnapShotType
{
    None,
    ProgressSnapshot,
    ChapterSnapshot,
    SessionSnapshot,

    ScoreSnapshot,
}

public class ReturnDataEvent
{
    #region base

    /// <summary> Request or Response. </summary>
    public CoreEventType eventType { get; private set; }

    /// <summary>True nếu trả thành công.</summary>
    public bool complete { get; private set; } = true;

    /// <summary>Thông tin lỗi (nếu !complete).</summary>
    public string error { get; private set; } = string.Empty;

    public bool isDitry { get; private set; } = false;

    public ReturnDataEvent() { }

    #endregion

    #region Snapshot

    public SnapShotType snapshotType { get; private set; } = SnapShotType.None;

    // progress
    public ProgressSnapshot progressSnapshot { get; private set; }
    public ChapterSnapshot chapterSnapshot { get; private set; }
    public SessionSnapshot sessionSnapshot { get; private set; }

    public ReturnDataEvent(CoreEventType type, SnapShotType snapshotType, ProgressSnapshot progressSnapshot, bool isDitry = false)
    {
        this.eventType = type;
        this.snapshotType = snapshotType;
        this.progressSnapshot = progressSnapshot;
        this.isDitry = isDitry;
    }

    public ReturnDataEvent(CoreEventType type, SnapShotType snapshotType, ChapterSnapshot chapterSnapshot, bool isDitry = false)
    {
        this.eventType = type;
        this.snapshotType = snapshotType;
        this.chapterSnapshot = chapterSnapshot;
        this.isDitry = isDitry;
    }

    public ReturnDataEvent(CoreEventType type, SnapShotType snapshotType, SessionSnapshot sessionSnapshot, bool isDitry = false)
    {
        this.eventType = type;
        this.snapshotType = snapshotType;
        this.sessionSnapshot = sessionSnapshot;
        this.isDitry = isDitry;
    }

    // Item

    public ScoreSnapshot scoreSnapshot { get; private set; }

    public ReturnDataEvent (CoreEventType type, SnapShotType snapshotType, ScoreSnapshot scoreSnapshot, bool isDitry = false)
    {
        this.eventType = type;
        this.snapshotType = snapshotType;
        this.scoreSnapshot = scoreSnapshot;
        this.isDitry = isDitry;
    }

    #endregion

    #region Id

    public string returnId { get; private set; } = string.Empty;

    public ReturnDataEvent(CoreEventType eventType, string returnId = null)
    {
        this.eventType = eventType;
        this.returnId = returnId;
    }

    #endregion
}

public class SnapshotActionEvent
{
    public CoreEventType eventType { get; private set; }
    public SnapShotType snapshotType { get; private set; } = SnapShotType.None;

    public ScoreSnapshot scoreSnapshot { get; private set; }

    public SnapshotActionEvent() { }

    public SnapshotActionEvent(CoreEventType type, SnapShotType snapshotType, ScoreSnapshot scoreSnapshot)
    {
        this.eventType = type;
        this.snapshotType = snapshotType;
        this.scoreSnapshot = scoreSnapshot;
        
    }
}

#endregion

#region Game Session Events

public class OnNewSessionEvent { }
public class OnSessionEvent
{
    public bool Started { get; private set; } = false;

    // false: chuẩn bị bắt đầu, true: đã bắt đầu
    public OnSessionEvent() { }

    // false: chuẩn bị bắt đầu, true: đã bắt đầu
    public OnSessionEvent(bool started)
    {
        this.Started = started;
    }
}
public class OnPrepareEndEvent
{
    //bool Started { get; set; } = false;
    //public OnPrepareEndEvent() { }
    //public OnPrepareEndEvent(bool started)
    //{
    //    this.Started = started;
    //}
}
public class OnEndSessionEvent
{
    public bool IsComplete { get; private set; } = false;
    public SessionSO SessionSO { get; private set; } = null;
    public PopupType PopupType { get; set; } = PopupType.None;

    /// <summary>
    /// isComplete: true là kết thúc hoàn toàn, false/none là kết thúc tạm thời
    /// </summary>
    /// <param name="isComplete"></param>
    public OnEndSessionEvent() { }

    /// <summary>
    /// isComplete: true là kết thúc hoàn toàn, false/none là kết thúc tạm thời
    /// </summary>
    /// <param name="isComplete"></param>
    public OnEndSessionEvent(bool isComplete, SessionSO sessionSO, PopupType popupType = PopupType.None)
    {
        this.IsComplete = isComplete;
        this.SessionSO = sessionSO;
        this.PopupType = popupType;
    }
}

public enum ContinueSessionType
{
    None,
    Revive,
}

public enum PauseSessionType
{
    None,
    Manual,
}

public enum ResumeSessionType
{
    None,
    Pause,
    Revive,
}

public enum QuitSessionType
{
    None,
    PopupHome,
}

public class OnContinueSessionEvent
{
    public ContinueSessionType ContinueType { get; private set; } = ContinueSessionType.None;
    public PopupType PopupType { get; private set; } = PopupType.None;

    public OnContinueSessionEvent() { }

    public OnContinueSessionEvent(ContinueSessionType continueType, PopupType popupType = PopupType.None)
    {
        this.ContinueType = continueType;
        this.PopupType = popupType;
    }
}

public class OnPauseSessionEvent
{
    public PauseSessionType PauseType { get; private set; } = PauseSessionType.None;
    public PopupType PopupType { get; private set; } = PopupType.Popup_Pause;

    public OnPauseSessionEvent() { }

    public OnPauseSessionEvent(PauseSessionType pauseType, PopupType popupType = PopupType.Popup_Pause)
    {
        this.PauseType = pauseType;
        this.PopupType = popupType;
    }
}

public class OnResumeSessionEvent
{
    public ResumeSessionType ResumeType { get; private set; } = ResumeSessionType.None;

    public OnResumeSessionEvent() { }

    public OnResumeSessionEvent(ResumeSessionType resumeType)
    {
        this.ResumeType = resumeType;
    }
}

public class OnQuitSessionEvent
{
    public QuitSessionType QuitType { get; private set; } = QuitSessionType.None;

    public OnQuitSessionEvent() { }

    public OnQuitSessionEvent(QuitSessionType quitType)
    {
        this.QuitType = quitType;
    }
}

#endregion

#region Ads Events

public enum RewardedAdPlacement
{
    None,
    Revive,
    AddCoin,
    AddHealth,
}

public enum RewardedAdResultType
{
    None,
    RewardEarned,
    Skipped,
    FailedToLoad,
    FailedToShow,
    Disabled,
    NotReady,
    SdkUnavailable,
    Busy,
}

public sealed class RewardedAdRequestEvent
{
    public RewardedAdPlacement Placement { get; private set; } = RewardedAdPlacement.None;

    public RewardedAdRequestEvent() { }

    public RewardedAdRequestEvent(RewardedAdPlacement placement)
    {
        this.Placement = placement;
    }
}

public sealed class RewardedAdResultEvent
{
    public RewardedAdPlacement Placement { get; private set; } = RewardedAdPlacement.None;
    public RewardedAdResultType ResultType { get; private set; } = RewardedAdResultType.None;
    public string Message { get; private set; } = string.Empty;

    public RewardedAdResultEvent() { }

    public RewardedAdResultEvent(
        RewardedAdPlacement placement,
        RewardedAdResultType resultType,
        string message = "")
    {
        this.Placement = placement;
        this.ResultType = resultType;
        this.Message = message ?? string.Empty;
    }
}

#endregion

#region Function Event
public class TargetObjectEvent
{
    public enum TypeTarget
    {
        None,
        To_Save,
        Load_To,
        Data_To_UI,
        UI_To_Data,
    }

    public TypeTarget TypeOfTarget { get; private set; } = TypeTarget.None;
    public string name { get; private set; } = "";
    public GameObject TargetObject { get; private set; } = null;
    public object TargetO { get; private set; }
    public List<object> ListT { get; private set; }

    public SessionSO SessionSO { get; private set; }
    public CosmicObjectSO CosmicObjectSO { get; private set; }

    /// <summary>
    /// TargetObjectEvent(typeTarget, targetObject, GameObject)
    /// </summary>
    public TargetObjectEvent() { }

    public TargetObjectEvent(TypeTarget typeTarget, CosmicObjectSO cosmicObjectSO)
    {
        this.TypeOfTarget = typeTarget;
        this.CosmicObjectSO = cosmicObjectSO;
    }

    /// <summary>
    ///TargetObjectEvent(typeTarget, name)
    /// </summary>
    /// <param name="typeTarget"></param>
    /// <param name="targetObject"></param>
    public TargetObjectEvent(TypeTarget typeTarget, string name)
    {
        this.TypeOfTarget = typeTarget;
        this.name = name;
    }

    /// <summary>
    ///TargetObjectEvent(typeTarget, object)
    /// </summary>
    /// <param name="typeTarget"></param>
    /// <param name="targetObject"></param>
    public TargetObjectEvent(TypeTarget typeTarget, object targetO)
    {
        this.TypeOfTarget = typeTarget;
        this.TargetO = targetO;
    }

    ///// <summary>
    /////TargetObjectEvent(typeTarget, listObject)
    ///// </summary>
    ///// <param name="typeTarget"></param>
    ///// <param name="listT"></param>
    //public TargetObjectEvent(TypeTarget typeTarget, List<object> listT)
    //{
    //    this.TypeOfTarget = typeTarget;
    //    this.ListT = listT;
    //}

    /// <summary>
    /// TargetObjectEvent(typeTarget, GameObject)
    /// </summary>
    /// <param name="typeTarget">  loại target </param>
    /// <param name="targetObject"> đối tượng target </param>
    public TargetObjectEvent(TypeTarget typeTarget, GameObject targetObject)
    {
        this.TypeOfTarget = typeTarget;
        this.TargetObject = targetObject;
    }

}

public class OnMoveAlongPathEvent
{
    public bool IsEnd { get; private set; }

    public OnMoveAlongPathEvent() { }

    public OnMoveAlongPathEvent(bool isEnd)
    {
        this.IsEnd = isEnd;
    }

    public static OnMoveAlongPathEvent End() => new OnMoveAlongPathEvent(true);
}

public class OnMoveToPointEvent
{
    public object TargetPoint { get; private set; }
    public bool IsEnd { get; private set; }

    public OnMoveToPointEvent() { }
    public OnMoveToPointEvent(object targetPoint, bool isEnd = false)
    {
        this.TargetPoint = targetPoint;
        this.IsEnd = isEnd;
    }

    //public static OnMoveToPointEvent End() => new OnMoveToPointEvent(null, true);
}

#endregion

#region Input Events

public class SwipeEvent
{
    public SwipeDirection direction;
    public bool isStart;

    /// <summary>
    /// SwipeEvent(direction, isEnd)
    /// </summary>
    public SwipeEvent() { }

    /// <summary>
    /// SwipeEvent(direction, isEnd)
    /// </summary>
    /// <remarks></remarks>
    /// <param name="direction"> hường vuốt</param>
    /// <param name="isEnd"> đã kết thúc hành vuốt</param>
    public SwipeEvent(SwipeDirection direction, bool isStart)
    {
        this.direction = direction;
        this.isStart = isStart;
    }
}

public class DragInputEvent
{
    public DragPhase phase;

    // screen-space (always valid)
    public Vector2 startScreen;
    public Vector2 currentScreen;
    public Vector2 deltaScreen;
    public Vector2 endScreen;
    public Vector2 totalDeltaScreen;

    /// <summary>
    /// Hướng tức thời (từ delta của frame hiện tại).
    /// </summary>
    public DragDirection direction;

    /// <summary>
    /// Hướng state (Center/E/W/N/S) được cập nhật kiểu Swipe.
    /// </summary>
    public DragDirection stateDirection;

    // optional world-space
    public bool hasWorld;
    public Vector3 startWorld;
    public Vector3 currentWorld;
    public Vector3 deltaWorld;
    public Vector3 endWorld;

    /// <summary>True nếu event là Start.</summary>
    public bool isStart => phase == DragPhase.Start;

    /// <summary>True nếu event là Move.</summary>
    public bool isMove => phase == DragPhase.Move;

    /// <summary>True nếu event là End.</summary>
    public bool isEnd => phase == DragPhase.End;

    /// <summary>Reset for reuse (pool).</summary>
    public void Reset()
    {
        phase = DragPhase.Start;
        startScreen = currentScreen = deltaScreen = endScreen = totalDeltaScreen = Vector2.zero;
        direction = DragDirection.None;

        hasWorld = false;
        startWorld = currentWorld = deltaWorld = endWorld = Vector3.zero;
    }
}

#endregion

#region Map Events
public class OnProgressMoveToPointEvent
{
    public object DestinationPoint { get; private set; }
    public float Progress { get; private set; }
    //public bool Complete { get; private set; }

    public OnProgressMoveToPointEvent() { }

    public OnProgressMoveToPointEvent(object destinationPoint, float progress/*, bool complete*/)
    {
        this.DestinationPoint = destinationPoint;
        this.Progress = progress;
        //this.Complete = complete;
    }
}

public class OnMapDataEvent
{
    public MapSO MapDataSO;
    public CosmicObjectSO CosmicObjectSO;

    /// <summary>
    /// Tham số có thể là MapDataSO || CosmicObjectSO || null
    /// </summary>
    public OnMapDataEvent() { }

    /// <summary>
    /// tham số là CosmicObjectSO
    /// </summary>
    /// <param name="cosmicObjectSO"></param>
    public OnMapDataEvent(CosmicObjectSO cosmicObjectSO)
    {
        this.CosmicObjectSO = cosmicObjectSO;
    }

    /// <summary>
    /// tham số là MapDataSO
    /// </summary>
    /// <param name="mapDataSO"></param>
    public OnMapDataEvent(MapSO mapDataSO)
    {
        this.MapDataSO = mapDataSO;
    }

}

public class OnAutoCheckDistanceMapEvent
{
    public string TargetName { get; private set; }
    public DistanceType DistanceType { get; private set; }

    public OnAutoCheckDistanceMapEvent() { }

    public OnAutoCheckDistanceMapEvent(string targetName, DistanceType distanceType)
    {
        this.TargetName = targetName;
        this.DistanceType = distanceType;
    }

}

public class OnColliderWithObjBySpecialZoneEvent
{
    public SpecialZoneContext specialZoneContext;
    public bool isEnter;
    public OnColliderWithObjBySpecialZoneEvent() { }

    public OnColliderWithObjBySpecialZoneEvent(SpecialZoneContext context, bool isEnter = true)
    {
        this.specialZoneContext = context;
        this.isEnter = isEnter;
    }
}
#endregion

#region Effect Events
public class OnEffectEvent
{
    public object ParentEffectObject;
    public bool WaitEndDuration;
    public bool Play;

    public OnEffectEvent() { }

    /// <summary>
    /// Tự động chơi và không đợi kết thúc 
    /// </summary>
    /// <param name="parentEffectObject"></param>
    /// <param name="waitEnd"></param>
    /// <param name="play"></param>
    public OnEffectEvent(object parentEffectObject, bool waitEndDuration = false, bool play = true)
    {
        this.ParentEffectObject = parentEffectObject;
        this.WaitEndDuration = waitEndDuration;
        this.Play = play;
    }
}
#endregion

#region UI Events

public class OnUIPressEvent
{
    public UIPress UIPress { get; private set; }
    public OnUIPressEvent() { }
    public OnUIPressEvent(UIPress uIPress)
    {
        this.UIPress = uIPress;
    }
}

public class UpgradePanelEvent
{
    public bool IsOpenUpgradePanel { get; private set; }

    public UpgradePanelEvent()
    {
        IsOpenUpgradePanel = true;
    }

    public UpgradePanelEvent(bool isOpenUpgradePanel)
    {
        this.IsOpenUpgradePanel = isOpenUpgradePanel;
    }
}

public class SettingPanelEvent
{
    public bool IsOpenSettingPanel { get; private set; }
    public SettingPanelEvent()
    {
        IsOpenSettingPanel = true;
    }
    public SettingPanelEvent(bool isOpenSettingPanel)
    {
        this.IsOpenSettingPanel = isOpenSettingPanel;
    }
}

public class SavePanelEvent { }

public class UserPanelEvent { }

public sealed class OnUIButtonClickEvent
{
    public string ButtonName { get; private set; } = string.Empty;

    public OnUIButtonClickEvent() { }

    public OnUIButtonClickEvent(string buttonName)
    {
        this.ButtonName = buttonName ?? string.Empty;
    }
}

public sealed class OnPopupStateEvent
{
    public PopupType PopupType { get; private set; } = PopupType.None;
    public bool IsOpen { get; private set; }

    public OnPopupStateEvent() { }

    public OnPopupStateEvent(PopupType popupType, bool isOpen)
    {
        this.PopupType = popupType;
        this.IsOpen = isOpen;
    }
}

public sealed class UpdateUIHealthEvent
{
    public int CurrentHealth { get; private set; }
    public int MaxHealth { get; private set; }

    public UpdateUIHealthEvent() { }

    public UpdateUIHealthEvent(int currentHealth, int maxHealth)
    {
        this.CurrentHealth = currentHealth;
        this.MaxHealth = maxHealth;
    }
}

public sealed class UpdateUIValueEvent
{
    public MoneyType MoneyType { get; private set; }
    public int Quantity { get; private set; }    

    public UpdateUIValueEvent() {}

    public UpdateUIValueEvent(MoneyType moneyType, int quantity)
    {
        this.MoneyType = moneyType;
        this.Quantity = quantity;
    }
}
#endregion

#region Interaction Events
//public abstract class ItemInteractionEvent
//{
//    public ItemContext ItemContext { get; private set; }
//    public ItemInteractionEvent() { }

//    public ItemInteractionEvent(ItemContext itemContext)
//    {
//        this.ItemContext = itemContext;
//    }
//}

public enum InteractItemType
{
    Pickup,
    Spend,
    Gain,
    Lose,
}

public class InteractMoneyEvent
{
    public MoneyContext moneyContext;

    public InteractItemType interactItemType;

    public InteractMoneyEvent() { }
    public InteractMoneyEvent(MoneyContext moneyContext, InteractItemType interactItemType)
    {
        this.moneyContext = moneyContext;
        this.interactItemType = interactItemType;
    }
}

public enum InteractType
{
    collision,

}
public class InteractObstacleEvent
{
    public InteractType interactType;
    public ObstacleContext obstacleContext;

    public InteractObstacleEvent() { }

    public InteractObstacleEvent(InteractType interactType, ObstacleContext obstacleContext)
    {
        this.interactType = interactType;
        this.obstacleContext = obstacleContext;
    }
}
#endregion

#region Player Events

public enum PlayerFifeState
{
    Life,
    dead,
    Revive,
    
}

public class PlayerFifeEvent
{
    public PlayerFifeState state {  get; private set; }

    public PlayerFifeEvent() { }

    public PlayerFifeEvent(PlayerFifeState state)
    {
        this.state = state;
    }
}

#endregion


/// <summary>
/// Centralized Event 
/// </summary>
public static class CoreEvents
{
    public static string LastEventName;

    #region Core Events

    public static readonly EventHub<LoadDataEvent> LoadChapter = new EventHub<LoadDataEvent>();

    public static readonly EventHub<OnMenuEvent> OnMenu = new EventHub<OnMenuEvent>();
    public static readonly EventHub<OnQuitAppEvent> OnQuitApp = new EventHub<OnQuitAppEvent>();


    public static readonly EventHub<NoneEvent> None = new EventHub<NoneEvent>();
    public static readonly EventHub<OnSaveGameEvent> OnSaveGame = new EventHub<OnSaveGameEvent>();
    public static readonly EventHub<OnLoadGameEvent> OnLoadGame = new EventHub<OnLoadGameEvent>();
    public static readonly EventHub<OnDelayEvent> OnDelay = new EventHub<OnDelayEvent>();
    #endregion

    #region Request

    public static readonly EventHub<ReturnDataEvent> ReturnData = new EventHub<ReturnDataEvent>();

    public static readonly EventHub<SnapshotActionEvent> SnapshotAction = new EventHub<SnapshotActionEvent>();
    #endregion

    #region Game Session Events
    public static readonly EventHub<OnNewSessionEvent> OnNewSession = new EventHub<OnNewSessionEvent>();
    public static readonly EventHub<OnSessionEvent> OnSession = new EventHub<OnSessionEvent>();
    public static readonly EventHub<OnContinueSessionEvent> OnContinueSession = new EventHub<OnContinueSessionEvent>();
    public static readonly EventHub<OnPauseSessionEvent> OnPauseSession = new EventHub<OnPauseSessionEvent>();
    public static readonly EventHub<OnResumeSessionEvent> OnResumeSession = new EventHub<OnResumeSessionEvent>();
    public static readonly EventHub<OnQuitSessionEvent> OnQuitSession = new EventHub<OnQuitSessionEvent>();

    //public static readonly EventHub<OnBeginStartEvent> OnBeginStart = new EventHub<OnBeginStartEvent>();
    public static readonly EventHub<OnPrepareEndEvent> OnPrepareEnd = new EventHub<OnPrepareEndEvent>();
    public static readonly EventHub<OnEndSessionEvent> OnEndSession = new EventHub<OnEndSessionEvent>();
    #endregion

    #region Ads Events

    public static readonly EventHub<RewardedAdRequestEvent> RewardedAdRequest = new EventHub<RewardedAdRequestEvent>();
    public static readonly EventHub<RewardedAdResultEvent> RewardedAdResult = new EventHub<RewardedAdResultEvent>();

    #endregion

    #region Input Events

    //public static readonly EventHub<SwipeEvent> Swipe = new EventHub<SwipeEvent>();
    //public static readonly EventHub<OnSwipeEvent> OnSwipe = new EventHub<OnSwipeEvent>();

    public static readonly EventHub<DragInputEvent> Drag = new EventHub<DragInputEvent>();
    public static readonly EventHub<OnUIPressEvent> OnUIPress = new EventHub<OnUIPressEvent>();
    #endregion

    #region Function Event

    public static readonly EventHub<TargetObjectEvent> TargetObject = new EventHub<TargetObjectEvent>();
    public static readonly EventHub<OnMoveAlongPathEvent> OnMoveAlongToPath = new EventHub<OnMoveAlongPathEvent>();
    public static readonly EventHub<OnMoveToPointEvent> OnMoveToPoint = new EventHub<OnMoveToPointEvent>();
    #endregion

    #region Map Events
    public static readonly EventHub<OnProgressMoveToPointEvent> OnProgressMoveToPoint = new EventHub<OnProgressMoveToPointEvent>();
    public static readonly EventHub<OnMapDataEvent> MapDataEvent = new EventHub<OnMapDataEvent>();
    public static readonly EventHub<OnAutoCheckDistanceMapEvent> OnAutoCheckDistanceMap = new EventHub<OnAutoCheckDistanceMapEvent>();
    public static readonly EventHub<OnColliderWithObjBySpecialZoneEvent> OnColliderWithObjBySpecialZone = new EventHub<OnColliderWithObjBySpecialZoneEvent>();

    #endregion

    #region Effect Events
    public static readonly EventHub<OnEffectEvent> OnEffect = new EventHub<OnEffectEvent>();

    #endregion

    #region UI Events

    public static readonly EventHub<UpgradePanelEvent> UpdgadePanel = new EventHub<UpgradePanelEvent>();
    public static readonly EventHub<SettingPanelEvent> SettinngPanel = new EventHub<SettingPanelEvent>();

    public static readonly EventHub<SavePanelEvent> OnSavePanel = new EventHub<SavePanelEvent>();
    public static readonly EventHub<UserPanelEvent> OnUserPanel = new EventHub<UserPanelEvent>();
    public static readonly EventHub<OnUIButtonClickEvent> OnUIButtonClick = new EventHub<OnUIButtonClickEvent>();
    public static readonly EventHub<OnPopupStateEvent> OnPopupState = new EventHub<OnPopupStateEvent>();

    public static readonly EventHub<UpdateUIHealthEvent> UpdateUIHealth = new EventHub<UpdateUIHealthEvent>();
    public static readonly EventHub<UpdateUIValueEvent> UpdateUIValue = new EventHub<UpdateUIValueEvent>();

    #endregion

    #region Interact Events
    //public static readonly EventHub<PickupItemEvent> PickupItem = new EventHub<PickupItemEvent>();
    public static readonly EventHub<InteractMoneyEvent> InteractMoney = new EventHub<InteractMoneyEvent>();

    public static readonly EventHub<InteractObstacleEvent> InteractObstacle = new EventHub<InteractObstacleEvent>();
    #endregion

    #region Player
    public static readonly EventHub<PlayerFifeEvent> PlayerFife = new EventHub<PlayerFifeEvent>();
    #endregion
}


// Bỏ Raise() không args + bỏ new() constraint (giảm GC)

//Bỏ UIEvent.object, chuyển sang typed event (giảm bug, clean)

//Unity main thread: bỏ lock trừ khi thật sự bắn event từ background thread
