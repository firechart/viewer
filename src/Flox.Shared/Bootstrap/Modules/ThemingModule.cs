namespace Flox.Shared.Bootstrap.Modules;

public class ThemingModule : BootstrapModule
{
    public override void ConfigureServices()
    {
        Services.AddScoped<ThemeService>();
    }
}
