


/// <summary>
/// State mặc định của Secondary FSM: không save/load.
/// </summary>
[GameState(typeof(NoneEvent), StateLayer.Secondary)]
public class NoneStateSecondary : StateBase
{
    public NoneStateSecondary(StateMachine m) : base(m) { } 
}

[GameState(typeof(OnSaveGameEvent), StateLayer.Secondary)]
public class OnSaveGameState : StateBase
{
    public OnSaveGameState (StateMachine m) : base(m) {}
}

[GameState(typeof(OnLoadGameEvent), StateLayer.Secondary)]
public class OnLoadGameState : StateBase
{
    public OnLoadGameState (StateMachine m) : base(m) {}
}

[GameState(typeof(UpgradePanelEvent), StateLayer.Secondary)]
public class UpgradeState: StateBase
{
    public UpgradeState (StateMachine m) : base(m) {}
}

[GameState(typeof(SettingPanelEvent), StateLayer.Secondary)]
public class SettingState: StateBase
{
    public SettingState (StateMachine m) : base(m) {}
}

// ///////////////////////////////////////////////////////////////////////////////////////////////

[GameState(typeof(OnMenuEvent))]
public class OnMenuState : StateBase
{
    public OnMenuState(StateMachine m) : base(m) { }
    public override void OnEnter()
    {
        //GameEvents.OnInMenu.Raise();
    }
}

[GameState(typeof(OnQuitAppEvent))]
public class OnQuitAppState : StateBase
{
    public OnQuitAppState(StateMachine m) : base(m) { }
    public override void OnEnter()
    {
        //GameEvents.OnQuitApp.Raise();
    }
}

[GameState(typeof(OnNewSessionEvent))]
public class OnNewSessionState : StateBase
{
    public OnNewSessionState(StateMachine m) : base(m) { }
    public override void OnEnter()
    {
        //GameEvents.OnNewSession.Raise();
    }
}

[GameState(typeof(OnSessionEvent))]
public class OnSessionState : StateBase
{
    public OnSessionState(StateMachine m) : base(m) { }
    public override void OnEnter()
    {
    }
}

//[GameState(typeof(OnPrepareEndEvent))]
//public class OnPrepareEndState : StateBase
//{
//    public OnPrepareEndState(StateMachine m) : base(m) { }
//    public override void OnEnter()
//    {
//        //GameEvents.OnPrepareEnd.Raise();
//    }
//}

[GameState(typeof(OnEndSessionEvent))]
public class OnEndSessionState : StateBase
{
    public OnEndSessionState(StateMachine m) : base(m) { }
    public override void OnEnter()
    {
        //GameEvents.OnEndSession.Raise();
    }
}

[GameState(typeof(OnContinueSessionEvent))]
public class OnContinueSessionState : StateBase
{
    public OnContinueSessionState(StateMachine m) : base(m) { }
    public override void OnEnter()
    {
        //GameEvents.OnContinueSession.Raise();
    }
}

[GameState(typeof(OnPauseSessionEvent))]
public class OnPauseSessionState : StateBase
{
    public OnPauseSessionState(StateMachine m) : base(m) { }
    public override void OnEnter()
    {
        //GameEvents.OnPauseSession.Raise();
    }
}

[GameState(typeof(OnResumeSessionEvent))]
public class OnResumeSessionState : StateBase
{
    public OnResumeSessionState(StateMachine m) : base(m) { }
    public override void OnEnter()
    {
        //GameEvents.OnResumeSession.Raise();
    }
}

[GameState(typeof(OnQuitSessionEvent))]
public class OnQuitSessionState : StateBase
{
    public OnQuitSessionState(StateMachine m) : base(m) { }
    public override void OnEnter()
    {
        //GameEvents.OnQuitSession.Raise();
    }
}
