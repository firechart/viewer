using System.Collections.Concurrent;
using System.Reflection;
using FilterOperator = Flox.Core.Data.FilterOperator;

namespace Flox.Shared.Components.Searching;

/// <summary>
/// Describes one searchable property of an entity: its label, the kind of input and the filter operator.
/// </summary>
public class SearchFieldDescriptor
{
    /// <summary>
    /// The kind of input control for the field.
    /// </summary>
    public SearchControlType ControlType { get; init; }

    /// <summary>
    /// The name of the field, that is of the property.
    /// </summary>
    public string Field
    {
        get
        {
            return Property.Name;
        }
    }

    /// <summary>
    /// The label shown for the field.
    /// </summary>
    public string Label { get; init; } = default!;

    /// <summary>
    /// The filter operator the field applies.
    /// </summary>
    public FilterOperator Operator { get; init; }

    /// <summary>
    /// The values the user can choose from, if the field has a fixed set.
    /// </summary>
    public string[]? Options { get; init; }

    /// <summary>
    /// The position of the field in the form.
    /// </summary>
    public int Order { get; init; }

    /// <summary>
    /// The searchable property of the entity.
    /// </summary>
    public PropertyInfo Property { get; init; } = default!;

    /// <summary>
    /// The type of the value, without <see cref="Nullable{T}"/>.
    /// </summary>
    public Type UnderlyingType
    {
        get
        {
            return Nullable.GetUnderlyingType(ValueType) ?? ValueType;
        }
    }

    /// <summary>
    /// The type of the value, as declared.
    /// </summary>
    public Type ValueType
    {
        get
        {
            return Property.PropertyType;
        }
    }
}

/// <summary>
/// Finds the searchable fields of an entity type and caches them.
/// </summary>
public static class SearchFieldDescriptorCache
{
    private static readonly ConcurrentDictionary<Type, List<SearchFieldDescriptor>> s_cache = new ConcurrentDictionary<Type, List<SearchFieldDescriptor>>();

    private static SearchFieldDescriptor Build(PropertyInfo property, SearchableAttribute attribute)
    {
        var controlType = attribute.ControlType == SearchControlType.Auto ? InferControlType(property.PropertyType, attribute) : attribute.ControlType;

        var op = attribute.Operator != FilterOperator.None ? attribute.Operator : DefaultOperator(controlType);

        return new SearchFieldDescriptor
        {
            Property = property,
            Label = attribute.Label ?? property.Name,
            ControlType = controlType,
            Operator = op,
            Options = attribute.Options,
            Order = attribute.Order
        };
    }

    private static FilterOperator DefaultOperator(SearchControlType controlType)
    {
        return controlType switch
        {
            // Like => case-insensitive, wrapped as "*value*" unless the user enters explicit '*'/'_' wildcards
            SearchControlType.Text => FilterOperator.Like,
            SearchControlType.Select => FilterOperator.EqualsIgnoreCase,
            SearchControlType.MultiSelect => FilterOperator.In,
            SearchControlType.Bool => FilterOperator.Eq,
            SearchControlType.DateRange => FilterOperator.Gte,
            SearchControlType.NumberRange => FilterOperator.Gte,
            _ => FilterOperator.Eq
        };
    }

    private static SearchControlType InferControlType(Type propertyType, SearchableAttribute attribute)
    {
        if (attribute.Options is { Length: > 0 })
        {
            return SearchControlType.Select;
        }

        var underlying = Nullable.GetUnderlyingType(propertyType) ?? propertyType;

        if (underlying == typeof(bool))
        {
            return SearchControlType.Bool;
        }

        if (underlying == typeof(DateTime) || underlying == typeof(DateTimeOffset) || underlying == typeof(DateOnly))
        {
            return SearchControlType.DateRange;
        }

        if (underlying.IsPrimitive || underlying == typeof(decimal))
        {
            return SearchControlType.NumberRange;
        }

        return SearchControlType.Text;
    }

    /// <summary>
    /// Returns the searchable fields of the entity type, ordered for the form.
    /// </summary>
    public static List<SearchFieldDescriptor> GetFields<TEntity>()
    {
        return GetFields(typeof(TEntity));
    }

    /// <summary>
    /// Returns the searchable fields of the type, ordered for the form.
    /// </summary>
    public static List<SearchFieldDescriptor> GetFields(Type entityType)
    {
        return s_cache.GetOrAdd(entityType,
                                static type => type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                                                   .Select(property => (property, attribute: property.GetCustomAttribute<SearchableAttribute>()))
                                                   .Where(x => x.attribute != null)
                                                   .Select(x => Build(x.property, x.attribute!))
                                                   .OrderBy(d => d.Order)
                                                   .ThenBy(d => d.Label)
                                                   .ToList());
    }
}
