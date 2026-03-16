using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// EventBinder là một MonoBehaviour giúp quản lý việc đăng ký và hủy đăng ký sự kiện một cách tự động.
/// </summary>
public class EventBinder : MonoBehaviour
{
    private readonly List<Action> _unsubscribers = new();

    public void AddUnsubscriber(Action a)
    {
        if (a == null) return;
        _unsubscribers.Add(a);
    }

    private void OnDisable() => UnsubscribeAll();

    private void OnDestroy() => UnsubscribeAll();

    private void UnsubscribeAll()
    {
        foreach (var unsub in _unsubscribers) unsub?.Invoke();
        _unsubscribers.Clear();
    }
}
