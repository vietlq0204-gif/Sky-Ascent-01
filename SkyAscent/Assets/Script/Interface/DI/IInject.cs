/// <summary>
/// Interface generic cho mọi loại context
/// </summary>
/// <typeparam name="TContext"></typeparam>
public interface IInject<TContext>
{
    void Inject(TContext context);
}

/// <summary>
/// Marker interface để auto-inject trên Awake nếu cần (cho MonoBehaviour)
/// </summary>
public interface IAutoInjectOnAwake
{
    // không cần method, chỉ là marker
}
