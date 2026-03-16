using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

//public abstract class BaseInject<TContext> : MonoBehaviour
//{
//    protected TContext Context { get; private set; }

//    public virtual void Inject(TContext context)
//    {
//        Context = context;
//    }
//}

/// <summary>
/// Universal Injector
/// </summary>
/// <remarks>
/// Dùng được Cho cả POCO / Mono/ SO
/// </remarks>
public static class Injector
{
    /// <summary>
    /// Global services: DI container mini dùng DynamicReferenceMap
    /// </summary>
    public static DynamicReference GlobalServices { get; } = new DynamicReference();

    /// <summary>
    /// Cache thông tin type -> các handler inject
    /// </summary>
    private class InjectHandler
    {
        public Type ContextType;      // kiểu context (vd: SessionSO)
        public MethodInfo Method;     // method Inject(TContext)
        public Type InterfaceType;    // interface IInject<TContext>
    }

    /// <summary>
    /// Cache cho Component / Scriptable / POCO
    /// </summary>
    /// <remarks>
    /// Key: type của đối tượng, Value: list handler
    /// </remarks>
    private static readonly Dictionary<Type, List<InjectHandler>> _injectCache =
        new Dictionary<Type, List<InjectHandler>>();

    /// <summary>
    /// Lấy danh sách handler cho 1 type bất kỳ (Mono, SO, POCO)
    /// </summary>
    /// <param name="targetType"></param>
    /// <returns></returns>
    private static List<InjectHandler> GetHandlersForType(Type targetType)
    {
        if (_injectCache.TryGetValue(targetType, out var handlers))
            return handlers;

        handlers = new List<InjectHandler>();

        // Tìm tất cả interface IInject<TContext> mà type này implement
        var interfaces = targetType.GetInterfaces();
        foreach (var iface in interfaces)
        {
            if (iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IInject<>))
            {
                var contextType = iface.GetGenericArguments()[0];            // TContext
                var method = iface.GetMethod("Inject");                      // Inject(TContext)

                if (method != null)
                {
                    handlers.Add(new InjectHandler
                    {
                        ContextType = contextType,
                        Method = method,
                        InterfaceType = iface
                    });
                }
            }
        }

        _injectCache[targetType] = handlers;
        return handlers;
    }

    #region API 1: Inject ALL contexts từ services

    /// <summary>
    /// Inject tất cả component trên GameObject từ services (nếu có context tương ứng)
    /// </summary>
    /// <param name="go"></param>
    /// <param name="services"></param>
    public static void InjectAllFromServices(GameObject go, DynamicReference services = null)
    {
        if (go == null) return;

        if (services == null)
            services = GlobalServices;

        var components = go.GetComponents<MonoBehaviour>();
        foreach (var comp in components)
        {
            if (comp == null) continue;
            InjectObjectFromServices(comp, services);
        }
    }

    /// <summary>
    /// Inject 1 object (Mono, SO, POCO) bằng các context lấy từ services
    /// </summary>
    /// <param name="target"></param>
    /// <param name="services"></param>
    public static void InjectObjectFromServices(object target, DynamicReference services = null)
    {
        if (target == null) return;

        if (services == null)
            services = GlobalServices;

        var targetType = target.GetType();
        var handlers = GetHandlersForType(targetType);

        if (handlers.Count == 0)
            return; // // object này không implement IInject<TContext>

        foreach (var handler in handlers)
        {
            if (services.TryGet(handler.ContextType, out var context))
            {
                // // gọi Inject(context) qua reflection
                handler.Method.Invoke(target, new object[] { context });
            }
            else
            {
                // // không có context tương ứng trong container => có thể log nhẹ nếu muốn
                // Debug.LogWarning($"UniversalInjector: Không tìm thấy context {handler.ContextType.Name} cho {targetType.Name}");
            }
        }
    }
    
    #endregion

    #region API 2: Inject với context truyền tay

    /// <summary>
    /// Inject 1 context cụ thể vào toàn bộ component trên GameObject
    /// </summary>
    /// <param name="go"></param>
    /// <param name="context"></param>
    public static void Inject(GameObject go, object context)
    {
        if (go == null || context == null) return;

        var components = go.GetComponents<MonoBehaviour>();
        foreach (var comp in components)
        {
            if (comp == null) continue;
            Debug.Log($"[Injector] Component finded is: {comp.name}");
            InjectObjectWithContext(comp, context);
        }
    }

    /// <summary>
    /// Inject 1 context vào 1 object (Mono, SO, POCO)
    /// </summary>
    /// <param name="target"></param>
    /// <param name="context"></param>
    public static void InjectObjectWithContext(object target, object context)
    {
        if (target == null || context == null) return;

        var targetType = target.GetType();
        var contextType = context.GetType();

        var handlers = GetHandlersForType(targetType);
        if (handlers.Count == 0)
            return;

        foreach (var handler in handlers)
        {
            // chỉ inject vào các handler có ContextType trùng với type của context
            if (handler.ContextType == contextType)
            {
                handler.Method.Invoke(target, new object[] { context });
            }
            else
            {
                Debug.LogWarning($"[Injector] ContextType finded is: {contextType.ToString()}");

            }
        }
    }

    /// <summary>
    /// Inject nhiều context cùng lúc
    /// </summary>
    /// <param name="go"></param>
    /// <param name="contexts"></param>
    public static void Injects(GameObject go, params object[] contexts)
    {
        if (go == null || contexts == null || contexts.Length == 0) return;

        var components = go.GetComponents<MonoBehaviour>();
        foreach (var comp in components)
        {
            if (comp == null) continue;
            InjectObjectWithContexts(comp, contexts);
        }
    }

    public static void InjectObjectWithContexts(object target, params object[] contexts)
    {
        if (target == null || contexts == null || contexts.Length == 0) return;

        var targetType = target.GetType();
        var handlers = GetHandlersForType(targetType);
        if (handlers.Count == 0)
            return;

        foreach (var handler in handlers)
        {
            foreach (var ctx in contexts)
            {
                if (ctx == null) continue;
                if (handler.ContextType == ctx.GetType())
                {
                    handler.Method.Invoke(target, new object[] { ctx });
                }
            }
        }
    }

    #endregion
}
