using Flox.Core.Replication;

namespace Flox.Shared.Services;

// What the user reads about a change the server refused: the details the server gave, else the reason, in the language of the client
public static class ReplicationFailureText
{
    public static string Describe(ReplicationFailure failure)
    {
        if (failure.Errors is { Count: > 0 } errors)
        {
            return string.Join("; ", errors.Select(row => string.IsNullOrEmpty(row.Field) ? row.Message : $"{row.Field}: {row.Message}")
                                           .Distinct(StringComparer.Ordinal));
        }

        return failure.Reason switch
        {
            "invalid" => Strings.Sync_Reason_Invalid,
            "forbidden" => Strings.Sync_Reason_Forbidden,
            "read-only" => Strings.Sync_Reason_ReadOnly,
            "conflict" => Strings.Sync_Reason_Conflict,
            _ => Strings.Sync_Reason_Unknown
        };
    }
}
