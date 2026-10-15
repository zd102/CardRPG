using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;

namespace CardRPG;

/// <summary>Display templates are separate from business identifiers and saved log classification.</summary>
public static class UiCopy
{
    private static Dictionary<string,string>? _catalog;
    public static bool Stress { get; set; }
    private static string Lookup(string key)
    {
        _catalog??=JsonSerializer.Deserialize<Dictionary<string,string>>(FileAccess.GetFileAsString("res://Data/ui-copy.zh.json"))!;
        return _catalog.TryGetValue(key,out var value)?value:throw new InvalidOperationException("Missing display copy: "+key);
    }
    public static string Text(string key)=>Lookup(key);
    public static string Format(string key,params object?[] arguments)=>string.Format(CultureInfo.CurrentCulture,Lookup(key),arguments);
    public static string Present(string value)
    {
        value=value.Replace(" kg","\u00a0kg").Replace(" ms","\u00a0ms");
        if(!Stress || value.Length<4)return value;
        // Approximately 30% more visible characters; numbers, costs and IDs are kept intact.
        return value+new string('宽',Math.Max(1,(int)Math.Ceiling(value.Length*.3)));
    }
}
