using System.Globalization;

namespace FireChart.Viewer.Tests;

// The templates write dates in the culture of the application: the tests of the assembly run in a culture with ISO patterns, so the machine does not matter
[SetUpFixture]
public sealed class IsoCulture
{
    private CultureInfo? _culture;
    private CultureInfo? _uiCulture;

    public static CultureInfo Create()
    {
        var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        culture.DateTimeFormat.ShortDatePattern = "yyyy-MM-dd";
        culture.DateTimeFormat.ShortTimePattern = "HH:mm";
        culture.DateTimeFormat.YearMonthPattern = "yyyy-MM";
        return culture;
    }

    [OneTimeSetUp]
    public void Apply()
    {
        _culture = CultureInfo.DefaultThreadCurrentCulture;
        _uiCulture = CultureInfo.DefaultThreadCurrentUICulture;
        var iso = Create();
        CultureInfo.DefaultThreadCurrentCulture = iso;
        CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.GetCultureInfo("en-US");
        CultureInfo.CurrentCulture = iso;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");
    }

    [OneTimeTearDown]
    public void Restore()
    {
        CultureInfo.DefaultThreadCurrentCulture = _culture;
        CultureInfo.DefaultThreadCurrentUICulture = _uiCulture;
    }
}
