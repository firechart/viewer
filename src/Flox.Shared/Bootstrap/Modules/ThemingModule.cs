namespace Flox.Shared.Bootstrap.Modules;

/// <summary>
/// Registers the theme service.
/// </summary>
public class ThemingModule : BootstrapModule
{
    /// <inheritdoc />
    public override void ConfigureServices()
    {
        Services.AddScoped<ThemeService>();
    }
}
