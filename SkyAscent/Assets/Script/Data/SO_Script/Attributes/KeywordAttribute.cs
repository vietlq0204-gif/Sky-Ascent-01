using System;
using UnityEngine;

[AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
public sealed class KeywordAttribute : PropertyAttribute
{
    public readonly string[] Keywords;

    /// <summary>
    /// Override keyword(s) for editor filtering.
    /// </summary>
    /// <param name="keywords">One or more keywords like "fuel", "isp"</param>
    /// <param name="allowNameFallback">If true: if keywords don't match, allow fallback to name-token matching</param>
    public KeywordAttribute(params string[] keywords)
    {
        Keywords = keywords ?? Array.Empty<string>();
    }
}

[AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
public sealed class SpawnKeywordAttribute : PropertyAttribute
{
    public readonly SpawnType[] Value;

    public SpawnKeywordAttribute(params SpawnType[] value)
    {
        Value = value ?? Array.Empty<SpawnType>();
    }
}

[AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
public sealed class ZoneKeywordAttribute : PropertyAttribute
{
    public readonly SpecialZoneType[] ZoneTypes;

    /// <summary>
    /// Override keyword(s) for editor filtering based on SpecialZoneType.
    /// </summary>
    /// <param name="zoneTypes">One or more SpecialZoneType enum values</param>
    public ZoneKeywordAttribute(params SpecialZoneType[] zoneTypes)
    {
        ZoneTypes = zoneTypes ?? Array.Empty<SpecialZoneType>();
    }
}

[AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
public sealed class ItemKeywordAttribute : PropertyAttribute
{
    public readonly ItemType[] ItemTypes;

    public ItemKeywordAttribute(params ItemType[] itemTypes)
    {
        ItemTypes = itemTypes ?? Array.Empty<ItemType>();
    }
}