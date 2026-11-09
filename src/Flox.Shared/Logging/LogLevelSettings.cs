namespace Flox.Shared.Logging;

// The levels of a logger as data: the default and the level of each namespace prefix (null: the namespace follows the default).
// Level names are those of Serilog: Verbose, Debug, Information, Warning, Error, Fatal. The same record travels from the server to
// the clients (the event of the push channel and the Serilog section of /appsettings.client.json)
public sealed record LogLevelSettings(string Default, IReadOnlyDictionary<string, string?> Overrides);
