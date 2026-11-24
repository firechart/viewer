using System.Globalization;
using Flox.Licensing;
using Microsoft.Extensions.Configuration;

namespace Flox.Shared.Bootstrap;

// The summary of the license from the configuration of a client: the server writes it as Flox:License into appsettings.client.json, which the
// client merges into its configuration (flat keys); without the section (no server, the server not reached) the free edition
internal static class LicenseConfiguration
{
    public const string SectionName = "Flox:License";

    public static LicenseSummary Read(IConfiguration section)
    {
        if (!Enum.TryParse<LicenseEdition>(section[nameof(LicenseSummary.Edition)], ignoreCase: true, out var edition))
        {
            return LicenseSummary.Free;
        }

        return new LicenseSummary(
            edition,
            Enum.TryParse<LicenseMode>(section[nameof(LicenseSummary.Mode)], ignoreCase: true, out var mode) ? mode : LicenseMode.None,
            [.. section.GetSection(nameof(LicenseSummary.Modules)).GetChildren().Select(module => module.Value).OfType<string>()],
            Enum.TryParse<LicenseState>(section[nameof(LicenseSummary.State)], out var state) ? state : LicenseState.None,
            DateOnly.TryParse(section[nameof(LicenseSummary.ValidUntil)], CultureInfo.InvariantCulture, out var until) ? until : null,
            int.TryParse(section[nameof(LicenseSummary.Seats)], CultureInfo.InvariantCulture, out var seats) ? seats : null,
            section[nameof(LicenseSummary.Product)]);
    }
}
