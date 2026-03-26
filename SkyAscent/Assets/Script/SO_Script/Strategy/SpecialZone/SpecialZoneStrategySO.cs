using System.Collections.Generic;
using UnityEngine;

public enum CalculatorType
{
    Add,
    Minus,
}

[CreateAssetMenu(menuName = "Strategy/Zone/ New Special Zone Strategy SO")]
public class SpecialZoneStrategySO : BaseSO
{
    [Header("Key")]
    public DifficultyType diffType;
    public SpecialZoneType zoneType;

    [ZoneKeyword(SpecialZoneType.Zone_01, SpecialZoneType.Zone_02)]
    public CalculatorType calculatorType;
    [ZoneKeyword(SpecialZoneType.Zone_01)] 
    public float ispVolatilityValue;
    [ZoneKeyword(SpecialZoneType.Zone_02)] 
    public float fuelVolatilityvalue;

    //public virtual float CalculatorIsp(PlayerContext playerCtx, CalculatorType type)
    //{
    //    var isp = 0f;

    //    switch (type)
    //    {
    //        case CalculatorType.Add:
    //            isp = playerCtx.ispCurrent + ispVolatilityValue;
    //            break;
    //        case CalculatorType.Minus:
    //            isp = playerCtx.ispCurrent - ispVolatilityValue;
    //            break;
    //        default:
    //            break;
    //    }

    //    return isp;
    //}

    //public virtual float CalculatorFuel(PlayerContext playerCtx, CalculatorType type)
    //{
    //    var fuel = 0f;

    //    switch (type)
    //    {
    //        case CalculatorType.Add:
    //            fuel = playerCtx.totalFuel + fuelVolatilityvalue;
    //            break;
    //        case CalculatorType.Minus:
    //            fuel = playerCtx.totalFuel - fuelVolatilityvalue;
    //            break;
    //        default:
    //            break;
    //    }

    //    return fuel;
    //}
}

/// <summary>
/// Global registry for special zone strategy
/// </summary>
public static class SpecialZoneStrategyResolver
{
    private static readonly Dictionary<(SpecialZoneType, DifficultyType), SpecialZoneStrategySO> map
        = new();

    // Đăng ký toàn bộ strategy của 1 ZoneSO
    public static void Register(SpecialZoneSO zoneSO)
    {
        if (zoneSO == null || zoneSO.strategy == null) return;

        foreach (var s in zoneSO.strategy)
        {
            if (s == null) continue;

            var key = (s.zoneType, s.diffType);
#if UNITY_EDITOR
            //if (map.ContainsKey(key))
            //{
            //    Debug.LogWarning($"[SpecialZoneStrategyResolver] Override strategy: {key}");
            //}
#endif
            map[key] = s;
        }
    }

    // Lấy strategy theo ZoneType + Difficulty
    public static SpecialZoneStrategySO Get(SpecialZoneType zone, DifficultyType diff)
    {
        return map.TryGetValue((zone, diff), out var strategy) ? strategy : null;
    }
}
