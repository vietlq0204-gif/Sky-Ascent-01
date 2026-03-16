using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Định nghĩa hàm Raise(object) chung.
/// </summary>
/// <remarks>
/// Dùng để serialize polymorphic (IntEvent, FloatEvent…).
/// </remarks>
public abstract class DLEventBase : ScriptableObject // (abstract)
{
    public abstract void Raise(object data);
}

/// <summary>
/// Triển khai event cụ thể cho từng type (int, float…).
/// Giữ danh sách IDLEventListener<T>.
/// </summary>
/// <remarks>
/// Gọi Raise(T) → broadcast dữ liệu tới các listener đã đăng ký.
/// </remarks> 
public class DLEvent<T> : DLEventBase //(generic)
{
    private readonly List<IDLEventListener<T>> listeners = new();

    public void Register(IDLEventListener<T> listener)
    {
        if (!listeners.Contains(listener))
            listeners.Add(listener);
    }

    public void Unregister(IDLEventListener<T> listener)
    {
        if (listeners.Contains(listener))
            listeners.Remove(listener);
    }

    public void Raise(T data)
    {
        // duyệt ngược để tránh lỗi xóa trong vòng lặp
        for (int i = listeners.Count - 1; i >= 0; i--)
            listeners[i].OnEventRaised(data);
    }

    public override void Raise(object value)
    {
        if (value is T cast)
            Raise(cast);
        else
            Debug.LogWarning($"[DLEvent<{typeof(T).Name}>] Type mismatch when Raise({value?.GetType().Name ?? "null"})");
    }

}

