using System.Text.Json;
using System.Text.Json.Serialization;

namespace Flox.Core.Data;

/// <summary>The kind of a subject of an access list.</summary>
public enum AclSubjectKind
{
    /// <summary>The owner of the document (<see cref="IAclEntity.OwnerId"/>), signed in.</summary>
    Owner = 0,

    /// <summary>Anyone, anonymous callers included.</summary>
    Everyone = 1,

    /// <summary>Any signed-in user.</summary>
    Authenticated = 2,

    /// <summary>The members of a role.</summary>
    Role = 3,

    /// <summary>One user, by the id of the account (the <c>sub</c> of the token).</summary>
    User = 4
}

/// <summary>
/// Who an entry of an access list is for. Its text form is its key in the table of the access lists: <c>owner</c>, <c>everyone</c>,
/// <c>authenticated</c>, <c>role:NAME</c> (the name of the role in upper case, as Orchard compares roles) and <c>user:ID</c>.
/// </summary>
[JsonConverter(typeof(AclSubjectJsonConverter))]
public readonly record struct AclSubject
{
    private const string OwnerKey = "owner";
    private const string EveryoneKey = "everyone";
    private const string AuthenticatedKey = "authenticated";
    private const string RolePrefix = "role:";
    private const string UserPrefix = "user:";

    private AclSubject(AclSubjectKind kind, string? name)
    {
        Kind = kind;
        Name = name;
    }

    /// <summary>The kind of the subject.</summary>
    public AclSubjectKind Kind { get; }

    /// <summary>The role (upper case) or the user id; null for the other kinds.</summary>
    public string? Name { get; }

    /// <summary>The owner of the document.</summary>
    public static AclSubject Owner { get; } = new AclSubject(AclSubjectKind.Owner, null);

    /// <summary>Anyone, anonymous callers included.</summary>
    public static AclSubject Everyone { get; } = new AclSubject(AclSubjectKind.Everyone, null);

    /// <summary>Any signed-in user.</summary>
    public static AclSubject Authenticated { get; } = new AclSubject(AclSubjectKind.Authenticated, null);

    /// <summary>The key of the subject in the table of the access lists.</summary>
    public string Key
    {
        get
        {
            return Kind switch
            {
                AclSubjectKind.Owner => OwnerKey,
                AclSubjectKind.Everyone => EveryoneKey,
                AclSubjectKind.Authenticated => AuthenticatedKey,
                AclSubjectKind.Role => RolePrefix + Name,
                _ => UserPrefix + Name
            };
        }
    }

    /// <summary>The members of a role; the name is compared without case, as Orchard compares the names of roles.</summary>
    public static AclSubject Role(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new AclSubject(AclSubjectKind.Role, name.Trim().ToUpperInvariant());
    }

    /// <summary>One user, by the id of the account.</summary>
    public static AclSubject User(string userId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        return new AclSubject(AclSubjectKind.User, userId.Trim());
    }

    /// <summary>Reads the text form of a subject.</summary>
    /// <exception cref="FormatException">The text is not a subject.</exception>
    public static AclSubject Parse(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return key switch
        {
            OwnerKey => Owner,
            EveryoneKey => Everyone,
            AuthenticatedKey => Authenticated,
            _ when key.StartsWith(RolePrefix, StringComparison.Ordinal) && key.Length > RolePrefix.Length => Role(key[RolePrefix.Length..]),
            _ when key.StartsWith(UserPrefix, StringComparison.Ordinal) && key.Length > UserPrefix.Length => User(key[UserPrefix.Length..]),
            _ => throw new FormatException($"'{key}' is not a subject of an access list: owner, everyone, authenticated, role:NAME or user:ID.")
        };
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return Key;
    }
}

/// <summary>One entry of the access list of a document: the subject may do the operation (a name of <see cref="Authorization.EntityOperations"/> or of the entity).</summary>
/// <param name="Subject">Who the entry is for.</param>
/// <param name="Operation">The operation it allows.</param>
public sealed record AclEntry(AclSubject Subject, string Operation)
{
    /// <summary>The entries that give one subject several operations.</summary>
    public static IEnumerable<AclEntry> For(AclSubject subject, params IEnumerable<string> operations)
    {
        return operations.Select(operation => new AclEntry(subject, operation));
    }
}

/// <summary>Writes a subject as its key and reads it back.</summary>
public sealed class AclSubjectJsonConverter : JsonConverter<AclSubject>
{
    /// <inheritdoc />
    public override AclSubject Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        try
        {
            return AclSubject.Parse(reader.GetString() ?? string.Empty);
        }
        catch (FormatException ex)
        {
            throw new JsonException(ex.Message, ex);
        }
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, AclSubject value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteStringValue(value.Key);
    }
}
