using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;
using System.Text.Json.Serialization;

namespace Flox.Core.Data;

/// <summary>A stored document with a revision, an owner and the rights of the row; see <see cref="Access"/>.</summary>
public interface IEntity : INotifyPropertyChanged
{
    /// <summary>The revision of the document in the store (optimistic concurrency).</summary>
    long Revision { get; set; }

    /// <summary>When the document was created.</summary>
    DateTimeOffset CreatedAt { get; set; }
    /// <summary>When the document was last changed, or null if it never was.</summary>
    DateTimeOffset? UpdatedAt { get; set; }
    /// <summary>The id of the user who owns the document.</summary>
    string OwnerId { get; set; }
    /// <summary>Who may read the document besides its owner.</summary>
    Access ReadAccess { get; set; }
    /// <summary>Who may change the document besides its owner.</summary>
    Access WriteAccess { get; set; }
    /// <summary>The role that may read the document when <see cref="ReadAccess"/> is <see cref="Access.Role"/>.</summary>
    string? ReadRole { get; set; }
    /// <summary>The role that may change the document when <see cref="WriteAccess"/> is <see cref="Access.Role"/>.</summary>
    string? WriteRole { get; set; }
}

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

/// <summary>A document keyed by a string.</summary>
public interface IStringEntity : IEntity<string>;

/// <summary>The base class of documents: key, creation and update times, owner, rights, revision and change notification.</summary>
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

    /// <inheritdoc />
    [ServerOnly]
    [Immutable]
    public string OwnerId { get; set; } = Guid.Empty.ToString();

    /// <inheritdoc />
    [ServerOnly]
    [Immutable]
    public Access ReadAccess { get; set; } = Access.Owner;

    /// <inheritdoc />
    [ServerOnly]
    [Immutable]
    public Access WriteAccess { get; set; } = Access.Owner;

    /// <inheritdoc />
    [ServerOnly]
    [Immutable]
    public string? ReadRole { get; set; }

    /// <inheritdoc />
    [ServerOnly]
    [Immutable]
    public string? WriteRole { get; set; }

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

/// <summary>A document with a new time-ordered <see cref="Guid"/> as its key.</summary>
public class GuidEntity : Entity<Guid>, IGuidEntity
{
    /// <summary>Creates the document with a new key.</summary>
    public GuidEntity()
    {
        Uid = Guid.CreateVersion7();
    }
}

/// <summary>A document keyed by a string.</summary>
public class StringEntity : Entity<string>, IStringEntity;
