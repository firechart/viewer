namespace Flox.Core.Data;

/// <summary>
/// What the editor of an access list shows for one row: the operations of its entity, the entries of its list, who the subjects are and what
/// the permission matrix gives each of them, and the subjects of the caller. The server builds it, so that every editor shows the same rules.
/// </summary>
/// <param name="Entity">The name of the entity, as its permissions name it.</param>
/// <param name="Key">The key of the row.</param>
/// <param name="Operations">The operations a row of the entity can give.</param>
/// <param name="Entries">The entries of the list of the row.</param>
/// <param name="Subjects">The owner and every subject of the entries, with its name and what the matrix gives it.</param>
/// <param name="CallerSubjects">The keys of the subjects of the caller, to tell a grant to oneself.</param>
public sealed record AccessEditor
(
    string Entity,
    Guid Key,
    IReadOnlyList<AccessOperation> Operations,
    IReadOnlyList<AclEntry> Entries,
    IReadOnlyList<AccessSubjectInfo> Subjects,
    IReadOnlyList<string> CallerSubjects
);

/// <summary>An operation of the rows of an entity in an access list.</summary>
/// <param name="Name">The name of the operation (<c>Read</c>, <c>Update</c>, <c>Depseudonymize</c>).</param>
/// <param name="Sensitive">Whether it needs an explicit entry even for an administrator and is given to a user or a role by name only.</param>
public sealed record AccessOperation(string Name, bool Sensitive);

/// <summary>A subject of an access list as an editor shows it.</summary>
/// <param name="Subject">The subject.</param>
/// <param name="DisplayName">The name of the user or the role (of the owner, for the owner); the key for the other kinds.</param>
/// <param name="Granted">The operations of the entity the permission matrix gives the subject: an entry for any other one has no effect.</param>
public sealed record AccessSubjectInfo(AclSubject Subject, string DisplayName, IReadOnlyList<string> Granted);
