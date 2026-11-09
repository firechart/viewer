using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;
using System.Text.Json.Serialization;

namespace Flox.Core.Data;

public interface IEntity : INotifyPropertyChanged, IReplicaState
{
    DateTimeOffset CreatedAt { get; set; }
    DateTimeOffset? UpdatedAt { get; set; }
    string OwnerId { get; set; }
    Access ReadAccess { get; set; }
    Access WriteAccess { get; set; }
    string? ReadRole { get; set; }
    string? WriteRole { get; set; }
}

public interface IEntity<TKey> : IEntity where TKey : notnull
{
    TKey Uid { get; set; }
    void CopyImmutableFieldsFrom(IEntity<TKey> source);
}

public interface IGuidEntity : IEntity<Guid>;

public interface IStringEntity : IEntity<string>;

public abstract class Entity<TKey> : IEntity<TKey> where TKey : notnull
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    [Immutable]
    public TKey Uid { get; set; } = default!;

    [Immutable]
    [ServerOnly]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    [ServerOnly]
    public DateTimeOffset? UpdatedAt { get; set; }

    [ServerOnly]
    [Immutable]
    public string OwnerId { get; set; } = Guid.Empty.ToString();

    [ServerOnly]
    [Immutable]
    public Access ReadAccess { get; set; } = Access.Owner;

    [ServerOnly]
    [Immutable]
    public Access WriteAccess { get; set; } = Access.Owner;

    // The role of Access.Role (reading)
    [ServerOnly]
    [Immutable]
    public string? ReadRole { get; set; }

    // The role of Access.Role (writing)
    [ServerOnly]
    [Immutable]
    public string? WriteRole { get; set; }

    // The revision of the document in the store, 1 for a new one (0 only for a copy that was never stored or read). An update carries the revision
    // that was read, and one of a document that has moved on (or of a copy that never was read) is refused (ConcurrentUpdateException)
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    [Immutable]
    public long Revision { get; set; }

    [JsonIgnore]
    [ServerOnly]
    [Immutable]
    public DateTimeOffset ModifiedAt { get; set; }

    [JsonIgnore]
    [ServerOnly]
    [Immutable]
    public bool Deleted { get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;

    public void CopyImmutableFieldsFrom(IEntity<TKey> source)
    {
        foreach (var property in GetType()
                                .GetProperties()
                                .Where(p => p.GetCustomAttribute<ImmutableAttribute>() != null && p.CanWrite))
        {
            property.SetValue(this, property.GetValue(source));
        }
    }

    protected void NotifyPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public class GuidEntity : Entity<Guid>, IGuidEntity
{
    public GuidEntity()
    {
        Uid = Guid.CreateVersion7();
    }
}

public class StringEntity : Entity<string>, IStringEntity;
