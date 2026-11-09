using System.Text.Json.Serialization;
using Flox.Core.Services;

namespace Flox.Core.Models;

// What the server tells the WASM client before the client builds its own container (GET /appsettings.client.json). The client merges it
// into its configuration, so the names of the properties are the names of the configuration keys (the keys are not case sensitive).
// Public information only: no secret (PKCE public client), the sections of a feature that is off are absent
public sealed record ClientConfiguration
(
    ClientFloxConfiguration Flox,
    ClientSerilogConfiguration Serilog
);

// The platform keys: Flox:Edition, Flox:Oidc, Flox:OpenTelemetry
public sealed record ClientFloxConfiguration
(
    [property: JsonConverter(typeof(JsonStringEnumConverter))]
    FloxEdition Edition,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    ClientOidcConfiguration? Oidc = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    ClientTelemetryConfiguration? OpenTelemetry = null
);

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

// Only whether the OTLP proxy of this server is on, never where the collector is
public sealed record ClientTelemetryConfiguration
(
    bool Enabled
);

// The levels of the logs in the form of the Serilog section: the client reads them like its own configuration, so it logs at the level of the server from its first line
public sealed record ClientSerilogConfiguration
(
    ClientMinimumLevelConfiguration MinimumLevel
);

public sealed record ClientMinimumLevelConfiguration
(
    string Default,
    IReadOnlyDictionary<string, string> Override
);
