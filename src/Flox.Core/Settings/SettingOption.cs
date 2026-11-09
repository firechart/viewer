namespace Flox.Core.Settings;

public sealed record SettingOption
(
    string Text,
    object? Value
);

public interface ISettingOptionsSource
{
    IReadOnlyList<SettingOption> GetOptions();
}
