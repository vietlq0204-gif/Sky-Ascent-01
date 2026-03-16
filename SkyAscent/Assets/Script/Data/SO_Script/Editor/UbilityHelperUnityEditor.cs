using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

public static class UbilityHelperUnityEditor
{
    #region attribute
    private const BindingFlags BF =
       BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    /// <summary>
    /// Vẽ các field có [Keyword(...)] và match với token từ activeKey (ví dụ: zoneType.ToString()).
    /// - Keyword-only: field không có [Keyword] sẽ không hiện.
    /// - Không fallback theo tên field.
    /// - Không cần map thủ công.
    /// </summary>
    /// <param name="serializedObject"></param>
    /// <param name="hostType">typeof(SpecialZoneStrategySO)</param>
    /// <param name="activeKey">data.zoneType.ToString() (vd: "AddFuel")</param>
    public static void DrawPropertiesByActiveKeyTokens(
        SerializedObject serializedObject,
        Type hostType,
        string activeKey)
    {
        if (serializedObject == null || hostType == null || string.IsNullOrEmpty(activeKey))
            return;

        var activeTokens = GetTokensCached(activeKey);

        var prop = serializedObject.GetIterator();
        prop.NextVisible(true); // skip script

        while (prop.NextVisible(false))
        {
            if (prop.name == "m_Script") continue;

            var attr = GetKeywordAttributeCached(hostType, prop.name);
            if (attr == null) continue; // keyword-only

            if (IsKeywordMatch(attr, activeTokens))
                EditorGUILayout.PropertyField(prop, true);
        }
    }

    /// <summary>
    /// Vẽ các field có TAttribute, và trong attribute có mảng TEnum[] chứa activeEnum.
    /// - Attribute có thể đặt tên mảng enum tùy ý (Values/ZoneTypes/Allowed...).
    /// - Helper sẽ tự tìm member có kiểu TEnum[] (field hoặc property).
    /// </summary>
    /// <typeparam name="TEnum">Enum type (vd: SpecialZoneType)</typeparam>
    /// <typeparam name="TAttribute">Attribute type (vd: ZoneKeywordAttribute)</typeparam>
    public static void DrawPropertiesByEnumAttribute<TEnum, TAttribute>(
        SerializedObject serializedObject,
        Type hostType,
        TEnum activeEnum)
        where TEnum : struct, Enum
        where TAttribute : Attribute
    {
        if (serializedObject == null || hostType == null)
            return;

        var getEnumArray = GetEnumArrayAccessor(typeof(TAttribute), typeof(TEnum));
        if (getEnumArray == null)
        {
            EditorGUILayout.HelpBox(
                $"[{typeof(TAttribute).Name}] must expose a field or property of type {typeof(TEnum).Name}[]",
                MessageType.Error
            );
            return;
        }

        var prop = serializedObject.GetIterator();
        prop.NextVisible(true); // skip script

        while (prop.NextVisible(false))
        {
            if (prop.name == "m_Script") continue;

            var field = GetFieldCached(hostType, prop.name);
            if (field == null) continue;

            var attr = field.GetCustomAttribute<TAttribute>(true);
            if (attr == null) continue;

            var arr = getEnumArray(attr);
            if (arr == null || arr.Length == 0) continue;

            if (ContainsEnum(arr, activeEnum))
                EditorGUILayout.PropertyField(prop, true);
        }
    }

    #region Keyword Attribute helper

    // Cache: "AddFuel" -> {"add","fuel"}
    private static readonly Dictionary<string, HashSet<string>> _activeKeyTokenCache = new();

    // Cache: (hostType, fieldName) -> KeywordAttribute
    private static readonly Dictionary<(Type host, string field), KeywordAttribute> _keywordAttrCacheByField
        = new();

    /// <summary>
    /// Match if any Keyword(...) exists in active tokens.
    /// </summary>
    private static bool IsKeywordMatch(KeywordAttribute attr, HashSet<string> activeTokens)
    {
        var kws = attr.Keywords;
        if (kws == null || kws.Length == 0) return false;

        for (int i = 0; i < kws.Length; i++)
        {
            var k = kws[i];
            if (string.IsNullOrWhiteSpace(k)) continue;

            if (activeTokens.Contains(k.Trim().ToLowerInvariant()))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Cache tokens from a key string (ex: "AddFuel" => "add","fuel")
    /// </summary>
    private static HashSet<string> GetTokensCached(string key)
    {
        key = key.Trim();
        if (_activeKeyTokenCache.TryGetValue(key, out var cached))
            return cached;

        var tokens = SplitTokens(key);
        _activeKeyTokenCache[key] = tokens;
        return tokens;
    }

    /// <summary>
    /// Cache [Keyword] attribute lookup per field.
    /// </summary>
    private static KeywordAttribute GetKeywordAttributeCached(Type hostType, string fieldName)
    {
        var cacheKey = (hostType, fieldName);
        if (_keywordAttrCacheByField.TryGetValue(cacheKey, out var cached))
            return cached;

        var f = hostType.GetField(fieldName, BF);
        var attr = f != null ? f.GetCustomAttribute<KeywordAttribute>(true) : null;

        _keywordAttrCacheByField[cacheKey] = attr; // cache null too
        return attr;
    }

    /// <summary>
    /// Split CamelCase / snake_case into lowercase tokens.
    /// </summary>
    private static HashSet<string> SplitTokens(string input)
    {
        var set = new HashSet<string>();
        if (string.IsNullOrEmpty(input)) return set;

        var parts = Regex.Split(input, @"(?<!^)(?=[A-Z])|_");
        for (int i = 0; i < parts.Length; i++)
        {
            var p = parts[i];
            if (!string.IsNullOrEmpty(p))
                set.Add(p.ToLowerInvariant());
        }
        return set;
    }

    #endregion

    #region Enum Attribute helper

    // Cache: (hostType, fieldName) => FieldInfo
    private static readonly Dictionary<(Type host, string field), FieldInfo> _hostFieldCache = new();

    // Cache: attributeType => accessor(Attribute -> Array of TEnum)
    private static readonly Dictionary<Type, Func<Attribute, Array>> _enumAccessorByAttributeType = new();

    /// <summary>
    /// Check if Array contains activeEnum.
    /// </summary>
    private static bool ContainsEnum<TEnum>(Array arr, TEnum activeEnum)
        where TEnum : struct, Enum
    {
        for (int i = 0; i < arr.Length; i++)
        {
            if (arr.GetValue(i) is TEnum e && EqualityComparer<TEnum>.Default.Equals(e, activeEnum))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Cached FieldInfo lookup.
    /// </summary>
    private static FieldInfo GetFieldCached(Type hostType, string fieldName)
    {
        var key = (hostType, fieldName);
        if (_hostFieldCache.TryGetValue(key, out var cached))
            return cached;

        var f = hostType.GetField(fieldName, BF);
        _hostFieldCache[key] = f; // cache null too
        return f;
    }

    /// <summary>
    /// Build/accessor that returns Array from attribute's first member of type TEnum[].
    /// Member can be a field or a property, name doesn't matter.
    /// </summary>
    private static Func<Attribute, Array> GetEnumArrayAccessor(Type attributeType, Type enumType)
    {
        if (_enumAccessorByAttributeType.TryGetValue(attributeType, out var cached))
            return cached;

        var targetArrayType = enumType.MakeArrayType();

        // 1) Search fields
        var fields = attributeType.GetFields(BF);
        for (int i = 0; i < fields.Length; i++)
        {
            var f = fields[i];
            if (f.FieldType == targetArrayType)
            {
                Func<Attribute, Array> acc = (a) => (Array)f.GetValue(a);
                _enumAccessorByAttributeType[attributeType] = acc;
                return acc;
            }
        }

        // 2) Search properties
        var props = attributeType.GetProperties(BF);
        for (int i = 0; i < props.Length; i++)
        {
            var p = props[i];
            if (!p.CanRead) continue;
            if (p.PropertyType == targetArrayType)
            {
                Func<Attribute, Array> acc = (a) => (Array)p.GetValue(a);
                _enumAccessorByAttributeType[attributeType] = acc;
                return acc;
            }
        }

        _enumAccessorByAttributeType[attributeType] = null;
        return null;
    }

    /// <summary>
    /// Clear all caches (useful if Domain Reload is disabled).
    /// </summary>
    public static void ClearAttributeFilterCaches()
    {
        _activeKeyTokenCache.Clear();
        _keywordAttrCacheByField.Clear();
        _hostFieldCache.Clear();
        _enumAccessorByAttributeType.Clear();
    }

    #endregion

    #endregion

    /// <summary>
    /// Đổi tên asset trên disk để khớp với newName
    /// </summary>
    /// <param name="asset"></param>
    /// <param name="newName"></param>
    public static void ApplyPresetNameIO(ScriptableObject asset, string newName)
    {
        if (asset == null || string.IsNullOrEmpty(newName))
            return;

        string path = AssetDatabase.GetAssetPath(asset);
        if (string.IsNullOrEmpty(path))
            return;

        string currentName = System.IO.Path.GetFileNameWithoutExtension(path);
        if (currentName == newName)
            return;

        AssetDatabase.RenameAsset(path, newName);
        AssetDatabase.SaveAssets();
    }

    /// <summary>
    /// Đảm bảo ScriptableObject có _name và description hợp lệ.
    /// </summary>
    /// <param name="asset"></param>
    /// <param name="defaultDescTemplate"></param>
    public static void EnsureBasicMeta(ScriptableObject asset, string defaultDescTemplate = "[{0}] Chưa mô tả gì")
    {
        if (asset == null) return;

        var t = asset.GetType();
        const BindingFlags BF = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        // _name
        var nameField = t.GetField("_name", BF);
        if (nameField != null && nameField.FieldType == typeof(string))
        {
            string val = (string)nameField.GetValue(asset);
            if (string.IsNullOrEmpty(val))
            {
                // đặt tên mặc định
                val = $"{t.Name}_AUTO";
                nameField.SetValue(asset, val);
                EditorUtility.SetDirty(asset);
                //AssetDatabase.SaveAssetIfDirty(asset);
            }

            // đảm bảo tên asset khớp _name
            ApplyPresetNameIO(asset, val);
        }

        // description
        var descField = t.GetField("description", BF);
        if (descField != null && descField.FieldType == typeof(string))
        {
            string desc = (string)descField.GetValue(asset);
            // nếu rỗng -> tự fill theo template
            if (string.IsNullOrEmpty(desc))
            {
                string displayName = nameField != null ? (string)nameField.GetValue(asset) : asset.name;
                string autoDesc = string.Format(defaultDescTemplate, displayName);
                descField.SetValue(asset, autoDesc);
                EditorUtility.SetDirty(asset);
                //AssetDatabase.SaveAssetIfDirty(asset);
            }
        }
    }

    /// <summary>
    /// Set field "_name" của ScriptableObject nếu khác newName và mark dirty.
    /// Optionally rename asset file name to match newName.
    /// </summary>
    /// <param name="asset"></param>
    /// <param name="newName"></param>
    /// <param name="renameAssetFile">Nếu true: rename file asset theo newName</param>
    /// <returns>True nếu có thay đổi</returns>
    public static bool ApplyPresetNameIfChanged(
        ScriptableObject asset,
        string newName,
        bool renameAssetFile = true)
    {
        if (asset == null || string.IsNullOrEmpty(newName))
            return false;

        const BindingFlags BF = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        var t = asset.GetType();
        var nameField = t.GetField("_name", BF);
        if (nameField == null || nameField.FieldType != typeof(string))
            return false;

        string current = (string)nameField.GetValue(asset);
        if (current == newName)
            return false;

        nameField.SetValue(asset, newName);
        EditorUtility.SetDirty(asset);

        if (renameAssetFile)
            ApplyPresetNameIO(asset, newName);

        return true;
    }

    public static SerializedProperty Find<TTarget, TValue>(this SerializedObject obj, Expression<Func<TTarget, TValue>> expr)
    {
        if (expr.Body is MemberExpression member)
        {
            return obj.FindProperty(member.Member.Name);
        }
        throw new ArgumentException("Biểu thức truyền vào phải là MemberExpression, ví dụ: x => x.fieldName");
    }

    /// <summary>
    /// Vẽ danh sách các ScriptableObject con
    /// </summary>
    /// <param name="list"></param>
    /// <param name="key"></param>
    /// <param name="title"></param>
    public static void DrawChildSOList(ScriptableObject[] list, string key, string title)
    {
        if (list == null || list.Length == 0) return;

        string foldoutKey = $"SOEditor_{key}";
        bool foldoutState = EditorPrefs.GetBool(foldoutKey, true);

        foldoutState = EditorGUILayout.Foldout(foldoutState, title, true);
        EditorPrefs.SetBool(foldoutKey, foldoutState);
        if (!foldoutState) return;

        EditorGUI.indentLevel++;
        foreach (var so in list)
        {
            if (so == null) continue;
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField($"[{so.name}]", EditorStyles.boldLabel);

            var child = new SerializedObject(so);
            child.Update();

            var prop = child.GetIterator();
            prop.NextVisible(true);
            while (prop.NextVisible(false))
                EditorGUILayout.PropertyField(prop, true);

            if (child.ApplyModifiedProperties())
            {
                EditorUtility.SetDirty(so);
                //AssetDatabase.SaveAssetIfDirty(so);
            }

            EditorGUILayout.EndVertical();
        }
        EditorGUI.indentLevel--;
    }

    /// <summary>
    /// Vẽ một ScriptableObject đơn
    /// </summary>
    /// <param name="so"></param>
    /// <param name="key"></param>
    /// <param name="title"></param>
    public static void DrawSO(ScriptableObject so, string key, string title)
    {
        if (so == null) return;

        string foldoutKey = $"SOEditor_{key}";
        bool foldoutState = EditorPrefs.GetBool(foldoutKey, true);

        foldoutState = EditorGUILayout.Foldout(foldoutState, title, true);
        EditorPrefs.SetBool(foldoutKey, foldoutState);
        if (!foldoutState) return;

        EditorGUI.indentLevel++;
        EditorGUILayout.BeginVertical("box");

        EditorGUILayout.LabelField($"[{so.name}]", EditorStyles.boldLabel);

        var serialized = new SerializedObject(so);
        serialized.Update();

        var prop = serialized.GetIterator();
        prop.NextVisible(true);

        while (prop.NextVisible(false))
            EditorGUILayout.PropertyField(prop, true);

        if (serialized.ApplyModifiedProperties())
        {
            EditorUtility.SetDirty(so);
            //AssetDatabase.SaveAssetIfDirty(so);
        }

        EditorGUILayout.EndVertical();
        EditorGUI.indentLevel--;
    }


    ///// <summary>
    ///// Tự động phát hiện và đồng bộ mọi ScriptableObject con của một ScriptableObject cha.
    ///// Hỗ trợ cả field đơn và collection (mảng, List).
    ///// </summary>
    //public static void SyncAllChildSOs(ScriptableObject parent)
    //{
    //    if (parent == null) return;

    //    const BindingFlags BF = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    //    var fields = parent.GetType().GetFields(BF);

    //    foreach (var f in fields)
    //    {
    //        if (f.IsNotSerialized) continue;
    //        var fieldType = f.FieldType;
    //        var value = f.GetValue(parent);

    //        // 1. Field đơn kiểu ScriptableObject
    //        if (typeof(ScriptableObject).IsAssignableFrom(fieldType))
    //        {
    //            var so = value as ScriptableObject;
    //            if (so != null)
    //                UpdateChild(so);
    //        }

    //        // 2. Mảng hoặc List<ScriptableObject>
    //        else if (typeof(IEnumerable).IsAssignableFrom(fieldType) && value != null)
    //        {
    //            foreach (var item in (IEnumerable)value)
    //            {
    //                if (item is ScriptableObject so)
    //                    UpdateChild(so);
    //            }
    //        }
    //    }

    //    // Đánh dấu cha dirty và yêu cầu Unity refresh view
    //    EditorUtility.SetDirty(parent);
    //    EditorApplication.QueuePlayerLoopUpdate();
    //    SceneView.RepaintAll();
    //}

    //// helper cho SyncAllChildSOs
    //private static void UpdateChild(ScriptableObject so)
    //{
    //    if (so == null) return;
    //    var child = new SerializedObject(so);
    //    child.Update();

    //    // Apply property changes và lưu
    //    if (child.ApplyModifiedProperties())
    //    {
    //        EditorUtility.SetDirty(so);
    //        AssetDatabase.SaveAssetIfDirty(so);
    //    }
    //}
}

public abstract class AutoEditor<T> : Editor where T : UnityEngine.Object
{
    protected T targetData;
    private readonly Dictionary<string, SerializedProperty> props = new();

    protected virtual void OnEnable()
    {
        targetData = (T)target;
        CacheSerializedProperties();
    }

    private void CacheSerializedProperties()
    {
        var fields = typeof(T).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        foreach (var f in fields)
        {
            if (f.IsNotSerialized) continue;
            var prop = serializedObject.FindProperty(f.Name);
            if (prop != null) props[f.Name] = prop;
        }
    }

    protected SerializedProperty Prop(string name)
    {
        return props.TryGetValue(name, out var p) ? p : null;
    }
}
