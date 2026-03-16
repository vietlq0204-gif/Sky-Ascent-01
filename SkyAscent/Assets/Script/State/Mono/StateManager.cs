using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

[DefaultExecutionOrder(-400)]
/// <summary>
/// Tự động đăng ký State vào StateMachine và ánh xạ Event và State.
/// Khi Event được Raise, hệ thống tự động chuyển sang State tương ứng.
/// </summary>
public class StateManager : MonoBehaviour, IInject<Core>
{
    private Core _core;

    /// <summary>
    /// Cache ánh xạ giữa EventType và  StateType để tránh quét reflection nhiều lần.
    /// Key: typeof(Event) — Value: typeof(State)
    /// </summary>
    private static Dictionary<Type, Type> _eventToState;

    private void Start()
    {
        AutoRegisterStatesAndEvents();

        CoreEvents.OnMenu.Raise(new OnMenuEvent(true));
        CoreEvents.None.Raise(new NoneEvent());
    }

    private void Update()
    {
        _core.StateMachine.Update();
        _core.SecondaryStateMachine.Update();
    }

    public void Inject(Core context) { _core = context; }

    /// <summary>
    /// tự động đăng ký các trạng thái và sự kiện dựa trên attribute
    /// </summary>
    private void AutoRegisterStatesAndEvents()
    {
        // Nếu cache chưa khởi tạo thì tạo mới
        _eventToState ??= new Dictionary<Type, Type>();

        // Quét toàn bộ type trong assembly đang chạy
        var stateTypes = Assembly.GetExecutingAssembly()
            .GetTypes()
            .Where(t => typeof(StateBase).IsAssignableFrom(t) && !t.IsAbstract);

        foreach (var type in stateTypes)
        {
            var attr = type.GetCustomAttribute<GameStateAttribute>();

            // Primary
            if (attr.Layer.HasFlag(StateLayer.Primary))
            {
                var instance = (IState)Activator.CreateInstance(type, _core.StateMachine);
                _core.StateMachine.RegisterState(instance);
                SubscribeEventToMachine(attr.TriggerEventType, _core.StateMachine, instance.GetType());
            }
            //  Secondary
            if (attr.Layer.HasFlag(StateLayer.Secondary))
            {
                var instance = (IState)Activator.CreateInstance(type, _core.SecondaryStateMachine);
                _core.SecondaryStateMachine.RegisterState(instance);
                SubscribeEventToMachine(attr.TriggerEventType, _core.SecondaryStateMachine, instance.GetType());
            }
        }

        Debug.Log($"[StateManager] Đã đăng ký {_eventToState.Count} Event/State.");
    }

    /// <summary>
    /// Subscribe event T để ChangeState trên đúng machine.
    /// </summary>
    private void SubscribeEventToMachine(Type eventType, StateMachine targetMachine, Type targetStateType)
    {
        var field = typeof(CoreEvents).GetFields(BindingFlags.Public | BindingFlags.Static)
            .FirstOrDefault(f => f.FieldType.IsGenericType &&
                                 f.FieldType.GetGenericArguments()[0] == eventType);

        if (field == null) return;

        var hub = field.GetValue(null);

        var method = GetType().GetMethod(nameof(SubscribeTyped), BindingFlags.NonPublic | BindingFlags.Instance);
        var genericMethod = method.MakeGenericMethod(eventType);

        genericMethod.Invoke(this, new object[] { hub, targetMachine, targetStateType });
    }

    /// <summary>
    /// Helper generic: bind EventHub<T> -> targetMachine.ChangeState(targetStateType)
    /// </summary>
    private void SubscribeTyped<T>(EventHub<T> hub, StateMachine targetMachine, Type targetStateType)
        where T : class, new()
    {
        hub.Subscribe(_ => targetMachine.ChangeState(targetStateType), _core.Binder);
    }
}
