using Flox.Core.Data;

namespace Flox.Core.Models;

/// <summary>A setting is a key and its JSON: the typed class (<c>[FloxSettings]</c>) is the schema, the row is the envelope. One entity holds the settings of a user and of the system, and the rows differ in owner and rights only. A setting of a user is the user's own; a setting of the system is written by an administrator and read by everyone, anonymous callers included. The keys of the two kinds do not meet: a key is the id of a typed settings class, and a class is of one scope.</summary>
public class Setting : GuidEntity
{
    /// <summary>The id of the typed settings class; the only field ever filtered on, so it is a column of the table instead of a scan of the JSON.</summary>
    [Index]
    public string Key { get; set; } = null!;

    /// <summary>The settings as JSON.</summary>
    public string? Value { get; set; }
}
