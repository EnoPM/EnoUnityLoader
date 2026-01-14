using System;

namespace EnoModLoader.Configuration;

/// <summary>
/// Arguments for events concerning a change of a setting.
/// </summary>
public sealed class SettingChangedEventArgs : EventArgs
{
    /// <inheritdoc />
    public SettingChangedEventArgs(ConfigEntryBase changedSetting)
    {
        ChangedSetting = changedSetting;
    }

    /// <summary>
    /// Setting that was changed
    /// </summary>
    public ConfigEntryBase ChangedSetting { get; }
}
