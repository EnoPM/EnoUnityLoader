using System;
using System.Linq;

namespace EnoModLoader.Configuration;

/// <summary>
/// Section and key of a setting. Used as a unique key for identification within a
/// <see cref="ConfigFile" />.
/// The same definition can be used in multiple config files, it will point to different settings then.
/// </summary>
/// <param name="Section">Group of the setting, case sensitive.</param>
/// <param name="Key">Name of the setting, case sensitive.</param>
public sealed record ConfigDefinition(string Section, string Key)
{
    private static readonly char[] InvalidConfigChars = ['=', '\n', '\t', '\\', '"', '\'', '[', ']'];

    /// <summary>
    /// Group of the setting. All settings within a config file are grouped by this.
    /// </summary>
    public string Section { get; } = ValidateAndReturn(Section, nameof(Section));

    /// <summary>
    /// Name of the setting.
    /// </summary>
    public string Key { get; } = ValidateAndReturn(Key, nameof(Key));

    private static string ValidateAndReturn(string val, string name)
    {
        ArgumentNullException.ThrowIfNull(val, name);
        if (val != val.Trim())
            throw new ArgumentException("Cannot use whitespace characters at start or end of section and key names",
                                        name);
        if (val.Any(c => InvalidConfigChars.Contains(c)))
            throw new
                ArgumentException(@"Cannot use any of the following characters in section and key names: = \n \t \ "" ' [ ]",
                                  name);
        return val;
    }

    /// <inheritdoc />
    public override string ToString() => $"{Section}.{Key}";
}
