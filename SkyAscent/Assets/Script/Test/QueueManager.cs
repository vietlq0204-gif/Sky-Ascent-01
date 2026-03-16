using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Quản lý object ngoài path (disable + đổi parent)
/// Tách biệt khỏi View.
/// </summary>
public sealed class QueueManager : MonoBehaviour
{
    [SerializeField] private Transform _queueRoot;

    private readonly Dictionary<Transform, Transform> _originalParent
        = new Dictionary<Transform, Transform>(64);

    public void Initialize(List<Transform> objects)
    {
        EnsureQueueRoot();
        CacheParents(objects);
    }

    private void EnsureQueueRoot()
    {
        if (_queueRoot != null) return;

        GameObject go = new GameObject("QueueRoot");
        go.transform.SetParent(transform, false);
        _queueRoot = go.transform;
    }

    private void CacheParents(List<Transform> objects)
    {
        _originalParent.Clear();

        foreach (var obj in objects)
        {
            if (obj != null)
                _originalParent[obj] = obj.parent;
        }
    }

    public void SendToQueue(Transform obj)
    {
        if (obj == null || _queueRoot == null) return;

        obj.SetParent(_queueRoot, false);
        obj.localPosition = Vector3.zero;
        obj.localRotation = Quaternion.identity;
        obj.gameObject.SetActive(false);
    }

    public void RestoreFromQueue(Transform obj)
    {
        if (obj == null) return;

        if (_originalParent.TryGetValue(obj, out Transform parent))
        {
            obj.SetParent(parent, true);
        }

        obj.gameObject.SetActive(true);
    }
}
