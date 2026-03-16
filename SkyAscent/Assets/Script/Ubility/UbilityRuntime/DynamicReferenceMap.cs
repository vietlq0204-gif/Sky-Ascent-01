using System;
using System.Collections.Generic;
using UnityEngine;

// DynamicReferenceMap: mini DI Container
public class DynamicReference
{
    private readonly Dictionary<Type, object> _map = new Dictionary<Type, object>();

    // // Đăng ký instance theo type T
    public void Set<T>(T instance)
    {
        _map[typeof(T)] = instance;
    }

    // // Đăng ký instance theo Type runtime (dùng khi không biết T compile-time)
    public void Set(Type type, object instance)
    {
        if (!type.IsInstanceOfType(instance))
        {
            Debug.LogError($"DynamicReferenceMap: instance không phù hợp type {type.Name}");
            return;
        }
        _map[type] = instance;
    }

    // // Lấy instance theo generic
    public T Get<T>()
    {
        if (_map.TryGetValue(typeof(T), out var obj))
        {
            return (T)obj;
        }

        throw new Exception($"DynamicReferenceMap: Chưa đăng ký type {typeof(T).Name}");
    }

    // // Lấy instance theo Type runtime
    public object Get(Type type)
    {
        if (_map.TryGetValue(type, out var obj))
        {
            return obj;
        }

        throw new Exception($"DynamicReferenceMap: Chưa đăng ký type {type.Name}");
    }

    // // TryGet an toàn
    public bool TryGet<T>(out T value)
    {
        if (_map.TryGetValue(typeof(T), out var obj))
        {
            value = (T)obj;
            return true;
        }

        value = default;
        return false;
    }

    public bool TryGet(Type type, out object value)
    {
        return _map.TryGetValue(type, out value);
    }
}
