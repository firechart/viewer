using System.Text.Json.Serialization;
using Flox.Core.Services;

namespace Flox.Core.Models;

/// <summary>What the server tells the WASM client before the client builds its own container (<c>GET /appsettings.client.json</c>). The client merges it into its configuration, so the names of the properties are the names of the configuration keys. Public information only: no secret, and the sections of a feature that is off are absent.</summary>
/// <param name="Flox">The platform keys.</param>
/// <param name="Serilog">The log levels.</param>
public sealed record ClientConfiguration
(
    ClientFloxConfiguration Flox,
    ClientSerilogConfiguration Serilog
);

/// <summary>The platform keys: <c>Flox:Edition</c>, <c>Flox:Oidc</c> and <c>Flox:OpenTelemetry</c>.</summary>
/// <param name="Edition">The edition of the product.</param>
/// <param name="Oidc">The sign-in settings; absent when sign-in is off.</param>
/// <param name="OpenTelemetry">The telemetry settings; absent when telemetry is off.</param>
public sealed record ClientFloxConfiguration
(
    [property: JsonConverter(typeof(JsonStringEnumConverter))]
    FloxEdition Edition,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    ClientOidcConfiguration? Oidc = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    ClientTelemetryConfiguration? OpenTelemetry = null
);

/// <summary>The sign-in settings of a public OpenID Connect client.</summary>
/// <param name="Enabled">Whether sign-in is on.</param>
/// <param name="Authority">The address of the identity provider.</param>
/// <param name="ClientId">The id of the public client.</param>
/// <param name="Scopes">The scopes to request.</param>
/// <param name="AdditionalParameters">Extra parameters of the authorization request.</param>
/// <param name="NameClaim">The claim that holds the display name.</param>
/// <param name="RoleClaims">The claims that hold the roles.</param>
public sealed record ClientOidcConfiguration
(
    bool Enabled,
    string? Authority,
    string? ClientId,
    IReadOnlyList<string> Scopes,
    IReadOnlyDictionary<string, string>? AdditionalParameters,
    string NameClaim,
    IReadOnlyList<string> RoleClaims
);

/// <summary>Whether the telemetry proxy of this server is on; never where the collector is.</summary>
/// <param name="Enabled">Whether the client may send telemetry to the server.</param>
public sealed record ClientTelemetryConfiguration
(
    bool Enabled
);

/// <summary>The log levels in the form of the Serilog section: the client reads them like its own configuration, so it logs at the level of the server from its first line.</summary>
/// <param name="MinimumLevel">The minimum levels.</param>
public sealed record ClientSerilogConfiguration
(
    ClientMinimumLevelConfiguration MinimumLevel
);

/// <summary>The minimum log levels in the form of the Serilog section.</summary>
/// <param name="Default">The default minimum level.</param>
/// <param name="Override">The minimum level per source prefix.</param>
public sealed record ClientMinimumLevelConfiguration
(
    string Default,
    IReadOnlyDictionary<string, string> Override
);
