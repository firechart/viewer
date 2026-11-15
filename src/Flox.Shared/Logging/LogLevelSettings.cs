namespace Flox.Shared.Logging;

/// <summary>
/// The levels of a logger as data. Level names are those of Serilog: Verbose, Debug, Information, Warning, Error, Fatal. The same record travels from
/// the server to the clients (the event of the push channel and the Serilog section of <c>/appsettings.client.json</c>).
/// </summary>
/// <param name="Default">The level of every namespace that has none of its own.</param>
/// <param name="Overrides">The level of each namespace prefix; <c>null</c> means the namespace follows the default.</param>
public sealed record LogLevelSettings(string Default, IReadOnlyDictionary<string, string?> Overrides);
