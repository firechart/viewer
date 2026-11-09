using Flox.Core.Data;

namespace Flox.Core.Replication;

// A [Replicated] entity type as the wire knows it
public sealed record ReplicatedType(string Name, Type EntityType, Type KeyType, ConflictPolicy Conflicts, ReplicationDirection Direction, ReplicationScope Scope = ReplicationScope.Own)
{
    public static bool IsReplicated(Type entityType)
    {
        return Attribute.IsDefined(entityType, typeof(ReplicatedAttribute));
    }

    public static ReplicatedType Of(Type entityType)
    {
        var attribute = (ReplicatedAttribute?)Attribute.GetCustomAttribute(entityType, typeof(ReplicatedAttribute)) ?? throw new InvalidOperationException($"{entityType.Name} is not marked [Replicated].");
        var keyType = entityType.GetInterfaces()
                                .Where(row => row.IsGenericType && row.GetGenericTypeDefinition() == typeof(IEntity<>))
                                .Select(row => row.GetGenericArguments()[0])
                                .FirstOrDefault() ?? throw new InvalidOperationException($"{entityType.Name} is not an entity with a key.");
        return new ReplicatedType(attribute.Name ?? entityType.Name, entityType, keyType, attribute.Conflicts, attribute.Direction, attribute.Scope);
    }
}

public sealed class ReplicationRegistry
{
    private readonly Dictionary<string, ReplicatedType> _byName;

    public ReplicationRegistry(IEnumerable<ReplicatedType> types)
    {
        _byName = new Dictionary<string, ReplicatedType>(StringComparer.OrdinalIgnoreCase);
        foreach (var type in types)
        {
            if (!_byName.TryAdd(type.Name, type) && _byName[type.Name].EntityType != type.EntityType)
            {
                throw new InvalidOperationException($"The replication name '{type.Name}' is taken by {_byName[type.Name].EntityType.Name} and {type.EntityType.Name}.");
            }
        }
    }

    public IReadOnlyCollection<ReplicatedType> All => _byName.Values;

    public ReplicatedType? Find(string name)
    {
        return _byName.GetValueOrDefault(name);
    }
}
