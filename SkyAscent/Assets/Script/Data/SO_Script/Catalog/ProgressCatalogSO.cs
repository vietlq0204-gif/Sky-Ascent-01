using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Catalog: giữ reference tới tất cả BaseSO liên quan và map Id -> SO.
/// </summary>
/// <remarks>
/// - Editor-time: RebuildEntries() duyệt object graph từ root để fill _entries.
/// - Runtime: BuildCacheIfNeeded() tạo dictionary.
/// </remarks>
[CreateAssetMenu(menuName = "Catalog/Progress Catalog SO")]
public sealed class ProgressCatalogSO : ScriptableObject
{
    #region Inspector

    [Header("Root")]
    [SerializeField] private ProgressSO _root;

    [Header("Editor - Auto build")]
    [SerializeField] private bool _autoRebuildOnValidate;

    [Header("Entries (Editor generated)")]
    [SerializeField] private List<BaseSO> _entries = new List<BaseSO>(256);

    [Header("Optional extras")]
    [SerializeField] private List<BaseSO> _extras = new List<BaseSO>(64);

    #endregion

    #region Runtime Cache

    private Dictionary<string, BaseSO> _byId;

    #endregion

    #region Public API

    /// <summary>
    /// Try resolve theo id.
    /// </summary>
    /// <remarks>
    /// - O(1) sau khi cache build.
    /// - Nếu id trùng, entry sau sẽ bị bỏ qua (và log error khi build).
    /// </remarks>
    public bool TryGet(string id, out BaseSO so)
    {
        so = null;
        if (string.IsNullOrEmpty(id)) return false;

        BuildCacheIfNeeded();
        return _byId.TryGetValue(id, out so);
    }

    /// <summary>
    /// Try resolve theo id và type.
    /// </summary>
    /// <returns>true nếu tìm thấy và cast đúng type</returns>
    public bool TryGet<T>(string id, out T so) where T : BaseSO
    {
        so = null;
        if (!TryGet(id, out var baseSo)) return false;

        so = baseSo as T;
        return so != null;
    }

    /// <summary>
    /// Lấy root đang gán.
    /// </summary>
    public ProgressSO Root => _root;

    #endregion

    #region Runtime - Cache

    /// <summary>
    /// Build dictionary runtime.
    /// </summary>
    /// <remarks>
    /// Không gọi AssetDatabase. Chỉ dùng references trong _entries/_extras.
    /// </remarks>
    private void BuildCacheIfNeeded()
    {
        if (_byId != null) return;

        _byId = new Dictionary<string, BaseSO>((_entries?.Count ?? 0) + (_extras?.Count ?? 0));

        AddListToCache(_entries);
        AddListToCache(_extras);
    }

    private void AddListToCache(List<BaseSO> list)
    {
        if (list == null) return;

        for (int i = 0; i < list.Count; i++)
        {
            var so = list[i];
            if (so == null) continue;

            var id = so.Id;
            if (string.IsNullOrEmpty(id)) continue;

            if (_byId.ContainsKey(id))
            {
                Debug.LogError($"[Catalog] Duplicate Id '{id}' between '{_byId[id].name}' and '{so.name}'.", this);
                continue;
            }

            _byId.Add(id, so);
        }
    }

    #endregion

#if UNITY_EDITOR

    #region Editor - Rebuild Entries

    private void OnValidate()
    {
        if (!_autoRebuildOnValidate) return;
        if (!EditorApplication.isPlayingOrWillChangePlaymode)
        {
            RebuildEntries();
        }
    }

    [ContextMenu("Rebuild Entries From Root")]
    private void RebuildEntries_Menu()
    {
        RebuildEntries();
    }

    /// <summary>
    /// Duyệt từ _root và gom mọi BaseSO references (kể cả nested list/array).
    /// </summary>
    /// <remarks>
    /// - Duyệt bằng reflection để không phụ thuộc cấu trúc cụ thể ChapterSO/SessionSO/MapSO.
    /// - Chỉ gom những SO đang được reference bởi graph (đúng nhu cầu runtime).
    /// </remarks>
    public void RebuildEntries()
    {
        _entries.Clear();

        if (_root == null)
        {
            EditorUtility.SetDirty(this);
            return;
        }

        var visited = new HashSet<int>(); // instanceID tránh loop
        var stack = new Stack<UnityEngine.Object>();
        stack.Push(_root);

        while (stack.Count > 0)
        {
            var obj = stack.Pop();
            if (obj == null) continue;

            int id = obj.GetInstanceID();
            if (!visited.Add(id)) continue;

            if (obj is BaseSO bso)
            {
                if (!_entries.Contains(bso))
                    _entries.Add(bso);
            }

            // chỉ traverse ScriptableObject
            if (obj is ScriptableObject so)
            {
                TraverseFieldsAndEnqueue(so, stack);
            }
        }

        EditorUtility.SetDirty(this);
        // reset cache để runtime build lại
        _byId = null;
    }

    private static void TraverseFieldsAndEnqueue(ScriptableObject so, Stack<UnityEngine.Object> stack)
    {
        var type = so.GetType();
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        var fields = type.GetFields(flags);
        for (int i = 0; i < fields.Length; i++)
        {
            var f = fields[i];
            if (f.IsNotSerialized) continue;

            object val = null;
            try { val = f.GetValue(so); }
            catch { /* ignore */ }

            if (val == null) continue;

            // UnityEngine.Object
            if (val is UnityEngine.Object uo)
            {
                if (uo is ScriptableObject) stack.Push(uo);
                continue;
            }

            // IEnumerable (array/list)
            if (val is IEnumerable enumerable && !(val is string))
            {
                foreach (var item in enumerable)
                {
                    if (item is UnityEngine.Object euo && euo is ScriptableObject)
                        stack.Push(euo);
                }
            }
        }
    }

    #endregion

#endif
}
