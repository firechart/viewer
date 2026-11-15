using Flox.Shared.Settings;
using Flox.Shared.Theming;

namespace Flox.Tests.Settings;

// The theme the user chose: Dark and Light whatever the system says, System as the system says (prefers-color-scheme)
[TestFixture]
public class ThemeServiceTests
{
    [TestCase(Theme.Dark, false, true)]
    [TestCase(Theme.Light, true, false)]
    [TestCase(Theme.System, true, true)]
    [TestCase(Theme.System, false, false)]
    public void TheChosenTheme_OrTheSystemForSystem(Theme theme, bool systemDark, bool dark)
    {
        var service = new ThemeService();

        service.ApplyThemeSettings(new ThemeSettings { Theme = theme }, systemDark);

        Assert.That(service.IsDarkMode, Is.EqualTo(dark));
    }
}
