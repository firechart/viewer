using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Flox.Core.Data;

namespace Flox.Core.Serialization;

/// <summary>
/// Prevents fields marked with [ServerOnlyAttribute] from being serialized
/// by Api and OData controllers, and from being taken from a request
/// </summary>
public sealed class ServerOnlyTypeInfoResolver : DefaultJsonTypeInfoResolver
{
    public override JsonTypeInfo GetTypeInfo(Type type, JsonSerializerOptions options)
    {
        var typeInfo = base.GetTypeInfo(type, options);

        // Removed, not only left unserialized: a read-only collection would still be filled from the request
        var serverOnly = typeInfo.Properties
                                 .Where(property => property.AttributeProvider?.GetCustomAttributes(typeof(ServerOnlyAttribute), true)
                                                            .Length > 0)
                                 .ToList();
        foreach (var property in serverOnly)
        {
            typeInfo.Properties.Remove(property);
        }

        return typeInfo;
    }
}
