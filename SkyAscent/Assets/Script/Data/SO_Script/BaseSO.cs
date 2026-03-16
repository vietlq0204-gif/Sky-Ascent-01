using System;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public interface IStableId
{
    /// <summary>
    /// Stable id, không đổi theo name.
    /// </summary>
    string Id { get; }
}

/// <summary>
/// Base cho mọi ScriptableObject có stable id.
/// </summary>
public abstract class BaseSO : ScriptableObject, IStableId
{
    [SerializeField] private string _id;

    /// <summary>
    /// Stable id dùng cho truy vấn/runtime.
    /// </summary>
    public string Id => _id;

    [Tooltip("")]
    public string _name;

    [TextArea, Tooltip("")]
    public string description;

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Nếu chưa có id thì auto generate để tránh bị skip khi build catalog cache.
        if (string.IsNullOrEmpty(_id))
        {
            // Option 1: GUID random
            //_id = Guid.NewGuid().ToString("N");

            //Option 2(tốt hơn nếu muốn id = GUID của asset):
             var path = AssetDatabase.GetAssetPath(this);
            if (!string.IsNullOrEmpty(path))
                _id = AssetDatabase.AssetPathToGUID(path);

            EditorUtility.SetDirty(this);
        }
    }
#endif
}
