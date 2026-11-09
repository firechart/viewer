using Flox.Core.Data;

namespace Flox.Core.Models;

// A setting is a key and its JSON: the typed class ([FloxSettings]) is the schema, the row is the envelope. One entity for the settings of a user and of
// the system; the rows differ in owner and rights only. A setting of a user is the user's own (Owner, Owner); a setting of the system is written by an
// administrator (ReadAccess = Public, WriteAccess = Role Administrator), read by everyone, anonymous callers included. The keys of the two kinds do not
// meet: a key is the id of a typed settings class, and a class is of one scope
public class Setting : GuidEntity
{
    // Only field ever filtered on (AppSettingsProvider) - [Index] makes the local Fisher store keep
    // it as an indexed duplicated column (see AddFisherEntity) instead of scanning JSON.
    [Index]
    public string Key { get; set; } = null!;

    public string? Value { get; set; }
}
