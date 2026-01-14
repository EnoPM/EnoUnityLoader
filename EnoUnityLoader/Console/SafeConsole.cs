// --------------------------------------------------
// UnityInjector - SafeConsole.cs
// Copyright (c) Usagirei 2015 - 2015
// --------------------------------------------------

using System;
using System.Reflection;

namespace EnoModLoader.Console;

/// <summary>
/// Console class with safe handlers for Unity 4.x, which does not have a proper Console implementation
/// </summary>
internal static class SafeConsole
{
    private static GetColorDelegate? _getBackgroundColor;
    private static SetColorDelegate? _setBackgroundColor;

    private static GetColorDelegate? _getForegroundColor;
    private static SetColorDelegate? _setForegroundColor;

    private static GetStringDelegate? _getTitle;
    private static SetStringDelegate? _setTitle;

    static SafeConsole()
    {
        var tConsole = typeof(System.Console);
        InitColors(tConsole);
    }

    public static bool BackgroundColorExists { get; private set; }

    public static ConsoleColor BackgroundColor
    {
        get => _getBackgroundColor?.Invoke() ?? ConsoleColor.Black;
        set => _setBackgroundColor?.Invoke(value);
    }

    public static bool ForegroundColorExists { get; private set; }

    public static ConsoleColor ForegroundColor
    {
        get => _getForegroundColor?.Invoke() ?? ConsoleColor.Gray;
        set => _setForegroundColor?.Invoke(value);
    }

    public static bool TitleExists { get; private set; }

    public static string Title
    {
        get => _getTitle?.Invoke() ?? string.Empty;
        set => _setTitle?.Invoke(value);
    }

    private static void InitColors(Type tConsole)
    {
        const BindingFlags bindingFlags = BindingFlags.Public | BindingFlags.Static;

        var gfc = tConsole.GetMethod("get_ForegroundColor", bindingFlags);
        var sfc = tConsole.GetMethod("set_ForegroundColor", bindingFlags);

        var gbc = tConsole.GetMethod("get_BackgroundColor", bindingFlags);
        var sbc = tConsole.GetMethod("set_BackgroundColor", bindingFlags);

        var gtt = tConsole.GetMethod("get_Title", bindingFlags);
        var stt = tConsole.GetMethod("set_Title", bindingFlags);

        _setForegroundColor = sfc != null
                                  ? (SetColorDelegate)Delegate.CreateDelegate(typeof(SetColorDelegate), sfc)
                                  : null;

        _setBackgroundColor = sbc != null
                                  ? (SetColorDelegate)Delegate.CreateDelegate(typeof(SetColorDelegate), sbc)
                                  : null;

        _getForegroundColor = gfc != null
                                  ? (GetColorDelegate)Delegate.CreateDelegate(typeof(GetColorDelegate), gfc)
                                  : null;

        _getBackgroundColor = gbc != null
                                  ? (GetColorDelegate)Delegate.CreateDelegate(typeof(GetColorDelegate), gbc)
                                  : null;

        _getTitle = gtt != null
                        ? (GetStringDelegate)Delegate.CreateDelegate(typeof(GetStringDelegate), gtt)
                        : null;

        _setTitle = stt != null
                        ? (SetStringDelegate)Delegate.CreateDelegate(typeof(SetStringDelegate), stt)
                        : null;

        BackgroundColorExists = _setBackgroundColor != null && _getBackgroundColor != null;
        ForegroundColorExists = _setForegroundColor != null && _getForegroundColor != null;
        TitleExists = _setTitle != null && _getTitle != null;
    }

    private delegate ConsoleColor GetColorDelegate();

    private delegate void SetColorDelegate(ConsoleColor value);

    private delegate string GetStringDelegate();

    private delegate void SetStringDelegate(string value);
}
