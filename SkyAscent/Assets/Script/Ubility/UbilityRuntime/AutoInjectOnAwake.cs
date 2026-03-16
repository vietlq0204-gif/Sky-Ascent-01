using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Gắn script này lên bất kỳ GameObject nào để auto-inject
/// </summary>
/// <remarks>
/// Từ Injector.GlobalServices
/// </remarks>
public class AutoInjectOnAwake : MonoBehaviour
{

    private void Awake()
    {
        AutoInject();
    }

    /// <summary>
    /// Auto inject tất cả component trên GameObject này
    /// </summary>
    public void AutoInject()
    {
        Injector.InjectAllFromServices(gameObject);
    }

}

#if UNITY_EDITOR

[CustomEditor(typeof(AutoInjectOnAwake))]
public class AutoInjectOnAwakeEditor : Editor
{
    AutoInjectOnAwake _target;

    private void Reset()
    {
        if(Application.isPlaying) return;
        _target = (AutoInjectOnAwake)target;
        _target.AutoInject();
    }
}

#endif
