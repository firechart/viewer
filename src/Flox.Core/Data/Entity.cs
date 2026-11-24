using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;
using System.Text.Json.Serialization;

namespace Flox.Core.Data;

/// <summary>
/// A stored document with a revision. Who may do what with it depends on its kind, one of three: <see cref="IAclEntity"/> (an access list of
/// its own), <see cref="IAclDetail{TMaster}"/> (the access list of its master) or <see cref="ITableAccess"/> (the permissions of the table alone).
/// </summary>
public interface IEntity : INotifyPropertyChanged
{
    /// <summary>The revision of the document in the store (optimistic concurrency).</summary>
    long Revision { get; set; }

    /// <summary>When the document was created.</summary>
    DateTimeOffset CreatedAt { get; set; }
    /// <summary>When the document was last changed, or null if it never was.</summary>
    DateTimeOffset? UpdatedAt { get; set; }
}

/// <summary>A document with an owner and an access list of its own, kept apart from the document (<see cref="AclEntry"/>).</summary>
public interface IAclEntity : IEntity
{
    /// <summary>The id of the user who created the document: the subject <see cref="AclSubject.Owner"/> of its access list.</summary>
    string OwnerId { get; set; }
}

/// <summary>
/// A document without an access list of its own: what may be done with it is what the access list of its master gives (reading it needs
/// Read of the master, changing it Update of the master). One level: a detail of a detail names the same master.
/// </summary>
public interface IAclDetail : IEntity
{
    /// <summary>The key of the master; the property it reads is marked <see cref="AclMasterAttribute"/>.</summary>
    Guid MasterId { get; }
}

/// <summary>A detail of the master type <typeparamref name="TMaster"/>.</summary>
public interface IAclDetail<TMaster> : IAclDetail where TMaster : IAclEntity, IGuidEntity;

/// <summary>A document without access lists of rows: the permissions of the table alone decide (a table of a service of the server).</summary>
public interface ITableAccess : IEntity;

/// <summary>Marks the property of a detail that holds the key of its master (a Guid with a column of its own, <c>[Index]</c>).</summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public sealed class AclMasterAttribute : Attribute;

/// <summary>A document with a key.</summary>
public interface IEntity<TKey> : IEntity where TKey : notnull
{
    /// <summary>The key of the document.</summary>
    TKey Uid { get; set; }
    /// <summary>Copies the immutable fields of the source into this document, so that an update cannot change them.</summary>
    void CopyImmutableFieldsFrom(IEntity<TKey> source);
}

/// <summary>A document keyed by <see cref="Guid"/>.</summary>
public interface IGuidEntity : IEntity<Guid>;

/// <summary>The base class of documents of every kind: key, creation and update times, revision and change notification.</summary>
public abstract class Entity<TKey> : IEntity<TKey> where TKey : notnull
{
    /// <inheritdoc />
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    [Immutable]
    public TKey Uid { get; set; } = default!;

    /// <inheritdoc />
    [Immutable]
    [ServerOnly]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <inheritdoc />
    [ServerOnly]
    public DateTimeOffset? UpdatedAt { get; set; }

    /// <summary>
    /// The revision of the document in the store, 1 for a new one (0 only for a copy that was never stored or read). An update carries the
    /// revision that was read, and one of a document that has moved on (or of a copy that never was read) is refused with a concurrent update
    /// exception.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    [Immutable]
    public long Revision { get; set; }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <inheritdoc />
    public void CopyImmutableFieldsFrom(IEntity<TKey> source)
    {
        foreach (var property in GetType()
                                .GetProperties()
                                .Where(p => p.GetCustomAttribute<ImmutableAttribute>() != null && p.CanWrite))
        {
            property.SetValue(this, property.GetValue(source));
        }
    }

    /// <summary>Raises <see cref="PropertyChanged"/> for the property.</summary>
    protected void NotifyPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>A document with an owner and an access list of its own.</summary>
public abstract class AclEntity<TKey> : Entity<TKey>, IAclEntity where TKey : notnull
{
    /// <inheritdoc />
    [ServerOnly]
    [Immutable]
    public string OwnerId { get; set; } = Guid.Empty.ToString();
}

/// <summary>
/// A detail of a master: no owner and no access list of its own. A detail declares the property of the key of its master, marked
/// <see cref="AclMasterAttribute"/> and <c>[Index]</c>, and maps <see cref="MasterId"/> to it, so the key is stored once.
/// </summary>
public abstract class AclDetailEntity<TKey, TMaster> : Entity<TKey>, IAclDetail<TMaster> where TKey : notnull where TMaster : IAclEntity, IGuidEntity
{
    /// <inheritdoc />
    [JsonIgnore]
    public abstract Guid MasterId { get; }
}

/// <summary>A document of a table without access lists of rows.</summary>
public abstract class TableEntity<TKey> : Entity<TKey>, ITableAccess where TKey : notnull;

/// <summary>A document with a new time-ordered <see cref="Guid"/> as its key, an owner and an access list of its own.</summary>
public class GuidEntity : AclEntity<Guid>, IGuidEntity
{
    /// <summary>Creates the document with a new key.</summary>
    public GuidEntity()
    {
        Uid = Guid.CreateVersion7();
    }
}

/// <summary>A detail of <typeparamref name="TMaster"/> with a new time-ordered <see cref="Guid"/> as its key.</summary>
public abstract class GuidDetailEntity<TMaster> : AclDetailEntity<Guid, TMaster>, IGuidEntity where TMaster : IAclEntity, IGuidEntity
{
    /// <summary>Creates the document with a new key.</summary>
    protected GuidDetailEntity()
    {
        Uid = Guid.CreateVersion7();
    }
}

/// <summary>A document of a table without access lists of rows, with a new time-ordered <see cref="Guid"/> as its key.</summary>
public class GuidTableEntity : TableEntity<Guid>, IGuidEntity
{
    /// <summary>Creates the document with a new key.</summary>
    public GuidTableEntity()
    {
        Uid = Guid.CreateVersion7();
    }
}
