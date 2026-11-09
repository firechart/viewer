using Flox.Core.Settings;

namespace Flox.Shared.Settings;

public sealed class LanguageOptions : ISettingOptionsSource
{
    public IReadOnlyList<SettingOption> GetOptions()
    {
        return
        [
            new SettingOption(Strings.Caption_ThemeSystem, string.Empty),
            new SettingOption("English", "en-US"),
            new SettingOption("Deutsch", "de-DE"),
            new SettingOption("Español", "es-ES"),
            new SettingOption("Français", "fr-FR"),
            new SettingOption("Русский", "ru-RU"),
            new SettingOption("中文", "zh-CN")
        ];
    }
}
