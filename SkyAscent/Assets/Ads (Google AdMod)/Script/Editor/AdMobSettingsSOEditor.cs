#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

[CustomEditor(typeof(AdMobSettingsSO))]
public sealed class AdMobSettingsSOEditor : Editor
{
    private const string DefaultAssetPath = "Assets/Resources/AdMobRewardedSettings.asset";
    private const string QuickStartUrl = "https://developers.google.com/admob/unity/quick-start";
    private const string TestAdsUrl = "https://developers.google.com/admob/unity/test-ads";
    private const string GoogleMobileAdsSettingsMenu = "Assets/Google Mobile Ads/Settings...";
    private const string GoogleMobileAdsDefine = "GOOGLE_MOBILE_ADS";
    private const string GoogleMobileAdsSettingsTypeName = "GoogleMobileAds.Editor.GoogleMobileAdsSettings";

    private SerializedProperty _provider;
    private SerializedProperty _adsEnabled;
    private SerializedProperty _autoCreateManagerAtRuntime;
    private SerializedProperty _autoInitializeOnStart;
    private SerializedProperty _dontDestroyManagerOnLoad;
    private SerializedProperty _autoRetryLoadAfterFailure;
    private SerializedProperty _retryLoadDelaySeconds;
    private SerializedProperty _verboseLogging;
    private SerializedProperty _androidAppId;
    private SerializedProperty _iosAppId;
    private SerializedProperty _useTestDeviceIds;
    private SerializedProperty _androidTestDeviceIds;
    private SerializedProperty _iosTestDeviceIds;
    private SerializedProperty _banner;
    private SerializedProperty _interstitial;
    private SerializedProperty _rewarded;
    private SerializedProperty _rewardedInterstitial;
    private SerializedProperty _appOpen;
    private SerializedProperty _nativeOverlay;

    private void OnEnable()
    {
        _provider = serializedObject.FindProperty("_provider");
        _adsEnabled = serializedObject.FindProperty("_adsEnabled");
        _autoCreateManagerAtRuntime = serializedObject.FindProperty("_autoCreateManagerAtRuntime");
        _autoInitializeOnStart = serializedObject.FindProperty("_autoInitializeOnStart");
        _dontDestroyManagerOnLoad = serializedObject.FindProperty("_dontDestroyManagerOnLoad");
        _autoRetryLoadAfterFailure = serializedObject.FindProperty("_autoRetryLoadAfterFailure");
        _retryLoadDelaySeconds = serializedObject.FindProperty("_retryLoadDelaySeconds");
        _verboseLogging = serializedObject.FindProperty("_verboseLogging");
        _androidAppId = serializedObject.FindProperty("_androidAppId");
        _iosAppId = serializedObject.FindProperty("_iosAppId");
        _useTestDeviceIds = serializedObject.FindProperty("_useTestDeviceIds");
        _androidTestDeviceIds = serializedObject.FindProperty("_androidTestDeviceIds");
        _iosTestDeviceIds = serializedObject.FindProperty("_iosTestDeviceIds");
        _banner = serializedObject.FindProperty("_banner");
        _interstitial = serializedObject.FindProperty("_interstitial");
        _rewarded = serializedObject.FindProperty("_rewarded");
        _rewardedInterstitial = serializedObject.FindProperty("_rewardedInterstitial");
        _appOpen = serializedObject.FindProperty("_appOpen");
        _nativeOverlay = serializedObject.FindProperty("_nativeOverlay");
    }

    public override void OnInspectorGUI()
    {
        var settings = (AdMobSettingsSO)target;
        settings.EnsureDefaults();

        serializedObject.Update();

        DrawHeader(settings);
        DrawProviderAndGeneral();
        DrawAppIdSection(settings);
        DrawTestingSection();
        DrawFormatsSection(settings);
        DrawActions(settings);

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawHeader(AdMobSettingsSO settings)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        bool pluginInstalled = IsGoogleMobileAdsAssemblyLoaded();
        bool defineInstalled = HasGoogleMobileAdsDefine();
        int enabledFormats = CountEnabledFormats(settings);
        int configuredFormats = CountConfiguredFormats(settings);

        Rect titleRect = EditorGUILayout.GetControlRect(false, 36f);
        EditorGUI.DrawRect(titleRect, new Color(0.12f, 0.18f, 0.24f));

        Rect titleLabelRect = new Rect(titleRect.x + 10f, titleRect.y + 6f, titleRect.width - 20f, 16f);
        Rect subtitleRect = new Rect(titleRect.x + 10f, titleRect.y + 19f, titleRect.width - 20f, 14f);
        EditorGUI.LabelField(titleLabelRect, "Ad Module Dashboard", EditorStyles.boldLabel);
        EditorGUI.LabelField(
            subtitleRect,
            $"Provider: {settings.Provider}   |   Enabled formats: {enabledFormats}/{GetSupportedFormatCount()}   |   Configured units: {configuredFormats}/{GetSupportedFormatCount()}",
            EditorStyles.miniLabel);

        EditorGUILayout.HelpBox(
            pluginInstalled
                ? "Google Mobile Ads SDK is installed in this project."
                : "Google Mobile Ads SDK is not detected in this project.",
            pluginInstalled ? MessageType.Info : MessageType.Warning);

        if (pluginInstalled && !defineInstalled)
        {
            EditorGUILayout.HelpBox(
                "SDK is present but GOOGLE_MOBILE_ADS is not enabled for the current build target yet. Let Unity recompile once.",
                MessageType.Warning);
        }

        if (!settings.LooksLikeValidAndroidAppId() && !string.IsNullOrWhiteSpace(settings.AndroidAppId))
        {
            EditorGUILayout.HelpBox(
                "Android App ID looks invalid. App ID should contain '~' and should not contain '/'.",
                MessageType.Warning);
        }

        if (!settings.LooksLikeValidIosAppId() && !string.IsNullOrWhiteSpace(settings.IosAppId))
        {
            EditorGUILayout.HelpBox(
                "iOS App ID looks invalid. App ID should contain '~' and should not contain '/'.",
                MessageType.Warning);
        }

        object googleSettings = LoadGoogleMobileAdsSettingsInstance();
        if (googleSettings != null)
        {
            string googleAndroidAppId = GetGoogleMobileAdsSettingString(googleSettings, "GoogleMobileAdsAndroidAppId");
            string googleIosAppId = GetGoogleMobileAdsSettingString(googleSettings, "GoogleMobileAdsIOSAppId");

            if (!string.Equals(googleAndroidAppId, settings.AndroidAppId, StringComparison.Ordinal) ||
                !string.Equals(googleIosAppId, settings.IosAppId, StringComparison.Ordinal))
            {
                EditorGUILayout.HelpBox(
                    "App IDs in this SO and GoogleMobileAdsSettings are different. Use the sync buttons below so build settings and your SO stay in sync.",
                    MessageType.Warning);
            }
        }

        EditorGUILayout.HelpBox(
            "No Ad Unit ID is hardcoded in code anymore. All App IDs and Ad Unit IDs must be entered manually in this asset.",
            MessageType.Info);
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(6f);
    }

    private void DrawProviderAndGeneral()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Provider & General", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(_provider);
        EditorGUILayout.PropertyField(_adsEnabled);
        EditorGUILayout.PropertyField(_autoCreateManagerAtRuntime);
        EditorGUILayout.PropertyField(_autoInitializeOnStart);
        EditorGUILayout.PropertyField(_dontDestroyManagerOnLoad);
        EditorGUILayout.PropertyField(_autoRetryLoadAfterFailure);
        EditorGUILayout.PropertyField(_retryLoadDelaySeconds);
        EditorGUILayout.PropertyField(_verboseLogging);
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(6f);
    }

    private void DrawAppIdSection(AdMobSettingsSO settings)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("App IDs", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(_androidAppId, new GUIContent("Android App ID (ca-app-pub-xxx~yyy)"));
        EditorGUILayout.PropertyField(_iosAppId, new GUIContent("iOS App ID"));

        EditorGUILayout.HelpBox(
            "These are App IDs, not Ad Unit IDs. App ID should look like ca-app-pub-xxxx~yyyy.",
            MessageType.None);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Pull From GoogleMobileAdsSettings"))
        {
            PullAppIdsFromGoogleSettings();
        }

        if (GUILayout.Button("Push To GoogleMobileAdsSettings"))
        {
            serializedObject.ApplyModifiedProperties();
            PushAppIdsToGoogleSettings(settings);
        }
        EditorGUILayout.EndHorizontal();

        if (GUILayout.Button("Open Google Mobile Ads Settings"))
        {
            if (!EditorApplication.ExecuteMenuItem(GoogleMobileAdsSettingsMenu))
            {
                EditorUtility.DisplayDialog(
                    "Google Mobile Ads Settings",
                    "Google Mobile Ads Settings menu was not found.",
                    "OK");
            }
        }

        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(6f);
    }

    private void DrawFormatsSection(AdMobSettingsSO settings)
    {
        EditorGUILayout.LabelField("Formats", EditorStyles.boldLabel);
        DrawAdFormatSection("Banner", _banner, settings, AdFormatType.Banner, includeBannerOptions: true);
        DrawAdFormatSection("Interstitial", _interstitial, settings, AdFormatType.Interstitial);
        DrawAdFormatSection("Rewarded", _rewarded, settings, AdFormatType.Rewarded, includeRewardedOptions: true);
        DrawAdFormatSection("Rewarded Interstitial", _rewardedInterstitial, settings, AdFormatType.RewardedInterstitial);
        DrawAdFormatSection("App Open", _appOpen, settings, AdFormatType.AppOpen);
        DrawAdFormatSection("Native Overlay", _nativeOverlay, settings, AdFormatType.NativeOverlay, includeNativeOptions: true);
    }

    private void DrawTestingSection()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Testing", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(_useTestDeviceIds, new GUIContent("Use Test Device IDs"));

        if (_useTestDeviceIds.boolValue)
        {
            EditorGUILayout.PropertyField(_androidTestDeviceIds, new GUIContent("Android Test Device IDs"), true);
            EditorGUILayout.PropertyField(_iosTestDeviceIds, new GUIContent("iOS Test Device IDs"), true);
            EditorGUILayout.HelpBox(
                "These IDs are applied through MobileAds.SetRequestConfiguration before the SDK initializes. A recognized device should receive test ads even when you use production ad unit IDs.",
                MessageType.None);
        }
        else
        {
            EditorGUILayout.HelpBox(
                "Test device IDs are disabled. Production ad requests will behave normally for the current device.",
                MessageType.Warning);
        }

        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(6f);
    }

    private void DrawAdFormatSection(
        string title,
        SerializedProperty property,
        AdMobSettingsSO settings,
        AdFormatType format,
        bool includeBannerOptions = false,
        bool includeRewardedOptions = false,
        bool includeNativeOptions = false)
    {
        SerializedProperty enabled = property.FindPropertyRelative("_enabled");
        SerializedProperty preloadOnInitialize = property.FindPropertyRelative("_preloadOnInitialize");
        SerializedProperty preloadWhenMenuOpens = property.FindPropertyRelative("_preloadWhenMenuOpens");
        SerializedProperty preloadDuringLoading = property.FindPropertyRelative("_preloadDuringLoading");
        SerializedProperty persistAcrossScenes = property.FindPropertyRelative("_persistAcrossScenes");
        SerializedProperty androidAdUnitId = property.FindPropertyRelative("_androidAdUnitId");
        SerializedProperty iosAdUnitId = property.FindPropertyRelative("_iosAdUnitId");
        bool hasAnyId = !string.IsNullOrWhiteSpace(androidAdUnitId.stringValue) || !string.IsNullOrWhiteSpace(iosAdUnitId.stringValue);
        bool isHealthy = !enabled.boolValue || hasAnyId;

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        DrawFormatCardHeader(title, enabled.boolValue, isHealthy);
        EditorGUILayout.PropertyField(enabled, new GUIContent("Enabled"));
        EditorGUILayout.PropertyField(androidAdUnitId, new GUIContent("Android Ad Unit ID (ca-app-pub-xxx/yyy)"));
        EditorGUILayout.PropertyField(iosAdUnitId, new GUIContent("iOS Ad Unit ID"));
        EditorGUILayout.PropertyField(preloadOnInitialize);
        EditorGUILayout.PropertyField(preloadWhenMenuOpens);
        EditorGUILayout.PropertyField(preloadDuringLoading);
        EditorGUILayout.PropertyField(persistAcrossScenes);

        if (enabled.boolValue && !hasAnyId)
        {
            EditorGUILayout.HelpBox(
                $"'{title}' is enabled but both platform Ad Unit IDs are empty.",
                MessageType.Warning);
        }

        if (enabled.boolValue && string.IsNullOrWhiteSpace(settings.GetFormatSettings(format)?.GetAdUnitIdForCurrentPlatform()))
        {
            EditorGUILayout.HelpBox(
                $"Current build target does not have a usable Ad Unit ID for '{title}' yet.",
                MessageType.Info);
        }

        if (includeBannerOptions)
        {
            EditorGUILayout.PropertyField(property.FindPropertyRelative("_bannerSize"));
            EditorGUILayout.PropertyField(property.FindPropertyRelative("_bannerPosition"));
            EditorGUILayout.PropertyField(property.FindPropertyRelative("_showWhenMenuOpens"));
            EditorGUILayout.PropertyField(property.FindPropertyRelative("_hideWhenMenuCloses"));
        }

        if (includeRewardedOptions)
        {
            EditorGUILayout.PropertyField(property.FindPropertyRelative("_simulateRewardInEditorWithoutSdk"));
            EditorGUILayout.PropertyField(property.FindPropertyRelative("_editorSimulatedRewardDelay"));
        }

        if (includeNativeOptions)
        {
            EditorGUILayout.PropertyField(property.FindPropertyRelative("_templatePosition"), new GUIContent("Template Position"));
            EditorGUILayout.PropertyField(property.FindPropertyRelative("_showWhenMenuOpens"));
            EditorGUILayout.PropertyField(property.FindPropertyRelative("_hideWhenMenuCloses"));
            EditorGUILayout.HelpBox(
                "Native Overlay uses AdMob native template rendering managed by the SDK. Position and menu visibility are configurable here.",
                MessageType.None);
        }

        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(4f);
    }

    private void DrawActions(AdMobSettingsSO settings)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);

        if (GUILayout.Button("Select Or Create Default Settings Asset"))
        {
            SelectOrCreateDefaultSettingsAsset();
        }

        if (GUILayout.Button("Create AdsManager In Current Scene"))
        {
            CreateAdsManagerInScene();
        }

        if (GUILayout.Button("Open AdMob Quick Start"))
        {
            Application.OpenURL(QuickStartUrl);
        }

        if (GUILayout.Button("Open Google Test Ads Docs"))
        {
            Application.OpenURL(TestAdsUrl);
        }

        EditorGUILayout.HelpBox(
            "For revive flow, the game currently uses the Rewarded section. Banner and Native Overlay can also be shown/hidden from menu events or tested manually from AdsManager inspector in Play Mode.",
            MessageType.None);
        EditorGUILayout.EndVertical();
    }

    private static void DrawFormatCardHeader(string title, bool enabled, bool healthy)
    {
        Rect headerRect = EditorGUILayout.GetControlRect(false, 22f);
        EditorGUI.DrawRect(
            headerRect,
            enabled
                ? (healthy ? new Color(0.17f, 0.27f, 0.19f) : new Color(0.32f, 0.21f, 0.12f))
                : new Color(0.16f, 0.16f, 0.16f));

        Rect titleRect = new Rect(headerRect.x + 8f, headerRect.y + 3f, headerRect.width - 120f, 18f);
        Rect statusRect = new Rect(headerRect.xMax - 106f, headerRect.y + 3f, 98f, 18f);

        EditorGUI.LabelField(titleRect, title, EditorStyles.boldLabel);
        EditorGUI.LabelField(
            statusRect,
            enabled
                ? (healthy ? "Enabled / Ready" : "Enabled / Missing")
                : "Disabled",
            EditorStyles.miniLabel);
    }

    private static int CountEnabledFormats(AdMobSettingsSO settings)
    {
        return Enum.GetValues(typeof(AdFormatType))
            .Cast<AdFormatType>()
            .Count(format => format != AdFormatType.None && settings.IsFormatEnabled(format));
    }

    private static int CountConfiguredFormats(AdMobSettingsSO settings)
    {
        return Enum.GetValues(typeof(AdFormatType))
            .Cast<AdFormatType>()
            .Count(format => format != AdFormatType.None && settings.HasConfiguredAdUnitId(format));
    }

    private static int GetSupportedFormatCount()
    {
        return Enum.GetValues(typeof(AdFormatType))
            .Cast<AdFormatType>()
            .Count(format => format != AdFormatType.None);
    }

    private void PullAppIdsFromGoogleSettings()
    {
        object googleSettings = LoadGoogleMobileAdsSettingsInstance();
        if (googleSettings == null)
            return;

        _androidAppId.stringValue = GetGoogleMobileAdsSettingString(googleSettings, "GoogleMobileAdsAndroidAppId");
        _iosAppId.stringValue = GetGoogleMobileAdsSettingString(googleSettings, "GoogleMobileAdsIOSAppId");
        serializedObject.ApplyModifiedProperties();
    }

    private static void PushAppIdsToGoogleSettings(AdMobSettingsSO settings)
    {
        object googleSettings = LoadGoogleMobileAdsSettingsInstance();
        if (googleSettings == null)
            return;

        SetGoogleMobileAdsSettingString(googleSettings, "GoogleMobileAdsAndroidAppId", settings.AndroidAppId);
        SetGoogleMobileAdsSettingString(googleSettings, "GoogleMobileAdsIOSAppId", settings.IosAppId);

        if (googleSettings is UnityEngine.Object unityObject)
        {
            EditorUtility.SetDirty(unityObject);
        }

        AssetDatabase.SaveAssets();
    }

    private static bool IsGoogleMobileAdsAssemblyLoaded()
    {
        return FindType(GoogleMobileAdsSettingsTypeName) != null;
    }

    private static bool HasGoogleMobileAdsDefine()
    {
        NamedBuildTarget target = NamedBuildTarget.FromBuildTargetGroup(EditorUserBuildSettings.selectedBuildTargetGroup);
        string symbols = PlayerSettings.GetScriptingDefineSymbols(target);

        return symbols
            .Split(';')
            .Any(symbol => string.Equals(symbol.Trim(), GoogleMobileAdsDefine, StringComparison.Ordinal));
    }

    private static void SelectOrCreateDefaultSettingsAsset()
    {
        var asset = AssetDatabase.LoadAssetAtPath<AdMobSettingsSO>(DefaultAssetPath);
        if (asset == null)
        {
            string folderPath = Path.GetDirectoryName(DefaultAssetPath)?.Replace("\\", "/");
            if (!string.IsNullOrEmpty(folderPath) && !AssetDatabase.IsValidFolder(folderPath))
                CreateFoldersRecursively(folderPath);

            asset = ScriptableObject.CreateInstance<AdMobSettingsSO>();
            asset.EnsureDefaults();
            AssetDatabase.CreateAsset(asset, DefaultAssetPath);
            AssetDatabase.SaveAssets();
        }

        Selection.activeObject = asset;
        EditorGUIUtility.PingObject(asset);
    }

    private static void CreateAdsManagerInScene()
    {
        var existing = UnityEngine.Object.FindFirstObjectByType<AdsManager>();
        if (existing != null)
        {
            Selection.activeObject = existing.gameObject;
            EditorGUIUtility.PingObject(existing.gameObject);
            return;
        }

        var go = new GameObject("[Manager] AdsManager");
        Undo.RegisterCreatedObjectUndo(go, "Create AdsManager");
        go.AddComponent<AdsManager>();
        Selection.activeObject = go;
        EditorGUIUtility.PingObject(go);
    }

    private static void CreateFoldersRecursively(string folderPath)
    {
        string[] parts = folderPath.Split('/');
        string current = parts[0];

        for (int i = 1; i < parts.Length; i++)
        {
            string next = $"{current}/{parts[i]}";
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);

            current = next;
        }
    }

    private static object LoadGoogleMobileAdsSettingsInstance()
    {
        Type type = FindType(GoogleMobileAdsSettingsTypeName);
        if (type == null)
            return null;

        MethodInfo loadMethod = type.GetMethod("LoadInstance", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        return loadMethod?.Invoke(null, null);
    }

    private static Type FindType(string fullName)
    {
        return AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType(fullName, false))
            .FirstOrDefault(type => type != null);
    }

    private static string GetGoogleMobileAdsSettingString(object instance, string propertyName)
    {
        if (instance == null)
            return string.Empty;

        PropertyInfo property = instance.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        return property?.GetValue(instance) as string ?? string.Empty;
    }

    private static void SetGoogleMobileAdsSettingString(object instance, string propertyName, string value)
    {
        if (instance == null)
            return;

        PropertyInfo property = instance.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        property?.SetValue(instance, value ?? string.Empty);
    }
}

[CustomEditor(typeof(AdsManager))]
public sealed class AdsManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var manager = (AdsManager)target;
        EditorGUILayout.Space(8f);

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox(
                "Enter Play Mode to use manual preload/show/hide/destroy test buttons for each ad format.",
                MessageType.Info);
            return;
        }

        if (GUILayout.Button("Initialize Ads"))
        {
            manager.InitializeAds();
        }

        DrawFormatButtons(manager, "Banner", AdFormatType.Banner, includeHide: true);
        DrawFormatButtons(manager, "Interstitial", AdFormatType.Interstitial);
        DrawFormatButtons(manager, "Rewarded", AdFormatType.Rewarded);
        DrawFormatButtons(manager, "Rewarded Interstitial", AdFormatType.RewardedInterstitial);
        DrawFormatButtons(manager, "App Open", AdFormatType.AppOpen);
        DrawFormatButtons(manager, "Native Overlay", AdFormatType.NativeOverlay, includeHide: true);
    }

    private static void DrawFormatButtons(AdsManager manager, string label, AdFormatType format, bool includeHide = false)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField($"{label}  |  Ready: {manager.IsReady(format)}", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Preload"))
            manager.PreloadAd(format);

        if (GUILayout.Button("Show"))
            manager.ShowAd(format);

        if (includeHide && GUILayout.Button("Hide"))
            manager.HideAd(format);

        if (GUILayout.Button("Destroy"))
            manager.DestroyAd(format);
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();
    }
}
#endif
