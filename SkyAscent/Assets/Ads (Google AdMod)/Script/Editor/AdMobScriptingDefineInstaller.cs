#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;

[InitializeOnLoad]
public static class AdMobScriptingDefineInstaller
{
    private const string GoogleMobileAdsSymbol = "GOOGLE_MOBILE_ADS";

    static AdMobScriptingDefineInstaller()
    {
        EditorApplication.delayCall += TryInstallGoogleMobileAdsDefine;
    }

    private static void TryInstallGoogleMobileAdsDefine()
    {
        if (!IsGoogleMobileAdsAssemblyLoaded())
            return;

        EnsureDefine(NamedBuildTarget.Standalone);
        EnsureDefine(NamedBuildTarget.Android);
        EnsureDefine(NamedBuildTarget.iOS);
    }

    private static bool IsGoogleMobileAdsAssemblyLoaded()
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (assembly == null)
                continue;

            if (assembly.GetType("GoogleMobileAds.Api.MobileAds") != null)
                return true;
        }

        return false;
    }

    private static void EnsureDefine(NamedBuildTarget target)
    {
        string symbols = PlayerSettings.GetScriptingDefineSymbols(target);
        var symbolList = new List<string>(symbols.Split(';'));

        for (int i = symbolList.Count - 1; i >= 0; i--)
        {
            symbolList[i] = symbolList[i].Trim();
            if (string.IsNullOrEmpty(symbolList[i]))
                symbolList.RemoveAt(i);
        }

        if (symbolList.Contains(GoogleMobileAdsSymbol))
            return;

        symbolList.Add(GoogleMobileAdsSymbol);
        PlayerSettings.SetScriptingDefineSymbols(target, string.Join(";", symbolList));
        // Debug.Log($"[AdMobScriptingDefineInstaller] Added {GoogleMobileAdsSymbol} for {target.TargetName}.");
    }
}
#endif
