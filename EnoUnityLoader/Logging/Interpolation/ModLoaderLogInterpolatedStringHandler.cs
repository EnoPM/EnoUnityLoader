using System;
using System.Runtime.CompilerServices;
using System.Text;

namespace EnoUnityLoader.Logging.Interpolation;

/// <summary>
/// Interpolated string handler for ModLoader <see cref="Logger" />. This allows to conditionally skip logging certain
/// messages and speed up logging in certain places.
/// </summary>
/// <remarks>
/// The class isn't meant to be constructed manually.
/// Instead, use <see cref="ManualLogSource.Log(LogLevel,ModLoaderLogInterpolatedStringHandler)" /> with
/// string interpolation.
/// </remarks>
[InterpolatedStringHandler]
public class ModLoaderLogInterpolatedStringHandler
{
    private const int GuessedLengthPerHole = 11;
    private readonly StringBuilder? _sb;

    /// <summary>
    /// Constructs a log handler.
    /// </summary>
    /// <param name="literalLength">Length of the literal string.</param>
    /// <param name="formattedCount">Number for formatted items.</param>
    /// <param name="logLevel">Log level the message belongs to.</param>
    /// <param name="isEnabled">Whether this string should be logged.</param>
    public ModLoaderLogInterpolatedStringHandler(int literalLength,
                                               int formattedCount,
                                               LogLevel logLevel,
                                               out bool isEnabled)
    {
        Enabled = (logLevel & Logger.ListenedLogLevels) != LogLevel.None;
        isEnabled = Enabled;
        _sb = Enabled ? new StringBuilder(literalLength + formattedCount * GuessedLengthPerHole) : null;
    }

    /// <summary>
    /// Whether the interpolation is enabled and string will be logged.
    /// </summary>
    public bool Enabled { get; }

    /// <summary>
    /// Appends a literal string to the interpolation.
    /// </summary>
    /// <param name="s">String to append.</param>
    public void AppendLiteral(string s)
    {
        if (!Enabled)
            return;
        _sb!.Append(s);
    }

    /// <summary>
    /// Appends a value to the interpolation.
    /// </summary>
    /// <param name="t">Value to append.</param>
    /// <typeparam name="T">Type of the value to append.</typeparam>
    public void AppendFormatted<T>(T t)
    {
        if (!Enabled)
            return;

        _sb!.Append(t);
    }

    /// <summary>
    /// Append a formattable item.
    /// </summary>
    /// <param name="t">Item to append.</param>
    /// <param name="format">Format to append with.</param>
    /// <typeparam name="T">Item type.</typeparam>
    public void AppendFormatted<T>(T t, string format) where T : IFormattable
    {
        if (!Enabled)
            return;

        _sb!.Append(t?.ToString(format, null));
    }

    /// <summary>
    /// Append an IntPtr.
    /// </summary>
    /// <param name="t">Item to append.</param>
    /// <param name="format">Format to append with.</param>
    public void AppendFormatted(IntPtr t, string format)
    {
        if (!Enabled)
            return;

        _sb!.Append(t.ToString(format));
    }

    /// <inheritdoc />
    public override string ToString() => _sb?.ToString() ?? string.Empty;
}

/// <inheritdoc />
[InterpolatedStringHandler]
public class ModLoaderFatalLogInterpolatedStringHandler : ModLoaderLogInterpolatedStringHandler
{
    /// <inheritdoc />
    public ModLoaderFatalLogInterpolatedStringHandler(int literalLength,
                                                    int formattedCount,
                                                    out bool isEnabled) : base(literalLength, formattedCount,
                                                                               LogLevel.Fatal, out isEnabled) { }
}

/// <inheritdoc />
[InterpolatedStringHandler]
public class ModLoaderErrorLogInterpolatedStringHandler : ModLoaderLogInterpolatedStringHandler
{
    /// <inheritdoc />
    public ModLoaderErrorLogInterpolatedStringHandler(int literalLength,
                                                    int formattedCount,
                                                    out bool isEnabled) : base(literalLength, formattedCount,
                                                                               LogLevel.Error, out isEnabled) { }
}

/// <inheritdoc />
[InterpolatedStringHandler]
public class ModLoaderWarningLogInterpolatedStringHandler : ModLoaderLogInterpolatedStringHandler
{
    /// <inheritdoc />
    public ModLoaderWarningLogInterpolatedStringHandler(int literalLength,
                                                      int formattedCount,
                                                      out bool isEnabled) : base(literalLength, formattedCount,
        LogLevel.Warning, out isEnabled) { }
}

/// <inheritdoc />
[InterpolatedStringHandler]
public class ModLoaderMessageLogInterpolatedStringHandler : ModLoaderLogInterpolatedStringHandler
{
    /// <inheritdoc />
    public ModLoaderMessageLogInterpolatedStringHandler(int literalLength,
                                                      int formattedCount,
                                                      out bool isEnabled) : base(literalLength, formattedCount,
        LogLevel.Message, out isEnabled) { }
}

/// <inheritdoc />
[InterpolatedStringHandler]
public class ModLoaderInfoLogInterpolatedStringHandler : ModLoaderLogInterpolatedStringHandler
{
    /// <inheritdoc />
    public ModLoaderInfoLogInterpolatedStringHandler(int literalLength,
                                                   int formattedCount,
                                                   out bool isEnabled) : base(literalLength, formattedCount,
                                                                              LogLevel.Info, out isEnabled) { }
}

/// <inheritdoc />
[InterpolatedStringHandler]
public class ModLoaderDebugLogInterpolatedStringHandler : ModLoaderLogInterpolatedStringHandler
{
    /// <inheritdoc />
    public ModLoaderDebugLogInterpolatedStringHandler(int literalLength,
                                                    int formattedCount,
                                                    out bool isEnabled) : base(literalLength, formattedCount,
                                                                               LogLevel.Debug, out isEnabled) { }
}
