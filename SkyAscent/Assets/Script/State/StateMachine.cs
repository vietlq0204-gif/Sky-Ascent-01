using System;
using System.Collections.Generic;

public interface IState
{
    void OnEnter();
    void OnExit();
    void OnUpdate();
}

public class StateMachine
{
    private readonly Dictionary<Type, IState> _states = new();
    private IState _currentState;

    public Type CurrentStateType => _currentState?.GetType();   // kiểu state hiện tại
    public string CurrentStateName => _currentState?.GetType().Name ?? "None";

    public void RegisterState<T>(T state) where T : IState
    {
        _states[typeof(T)] = state;
    }

    public void RegisterState(IState state)
    {
        _states[state.GetType()] = state;
    }

    // overload
    public void ChangeState(Type stateType)
    {
        if (_currentState != null && _currentState.GetType() == stateType) return;
        _currentState?.OnExit();                // dọn dẹp
        if (_states.TryGetValue(stateType, out var newState))
        {
            _currentState = newState;
            _currentState.OnEnter();
        }
    }

    public void Update() => _currentState?.OnUpdate();
}

public abstract class StateBase : IState
{
    protected readonly StateMachine _machine;

    protected StateBase(StateMachine machine)
    {
        _machine = machine;
    }

    public virtual void OnEnter() { }
    public virtual void OnExit() { }
    public virtual void OnUpdate() { }
}

[Flags]
public enum StateLayer
{
    Primary = 1,
    Secondary = 2
}

/// <summary>
/// Attribute đánh dấu State và Event liên kết
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class GameStateAttribute : Attribute
{
    /// <summary>Event dùng để trigger state.</summary>
    public Type TriggerEventType { get; }

    /// <summary>State thuộc layer nào (Primary/Secondary).</summary>
    public StateLayer Layer { get; }

    /// <summary>
    /// Khởi tạo attribute mapping Event -> State + layer.
    /// </summary>
    /// <param name="triggerEventType">Kiểu event trigger.</param>
    /// <param name="layer">Layer mục tiêu.</param>
    public GameStateAttribute(Type triggerEventType, StateLayer layer = StateLayer.Primary)
    {
        TriggerEventType = triggerEventType;
        Layer = layer;
    }
}
