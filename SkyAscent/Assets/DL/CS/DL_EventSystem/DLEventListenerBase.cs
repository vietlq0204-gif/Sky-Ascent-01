

public interface IDLEventListener<T> { void OnEventRaised(T data); }

public abstract class DLEventListenerBase<T> : IDLEventListener<T>
{
    public abstract void OnEventRaised(T data);
}