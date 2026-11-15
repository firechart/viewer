namespace Flox.Core.Settings;

/// <summary>A value the user can choose for a setting.</summary>
/// <param name="Text">The text shown for the value.</param>
/// <param name="Value">The value.</param>
public sealed record SettingOption
(
    string Text,
    object? Value
);

/// <summary>Lists the values of a setting that is not an enum.</summary>
public interface ISettingOptionsSource
{
    /// <summary>Lists the values.</summary>
    /// <returns>The options.</returns>
    IReadOnlyList<SettingOption> GetOptions();
}
