using System.Collections.Concurrent;
using System.Reflection;
using FilterOperator = Flox.Core.Data.FilterOperator;

namespace Flox.Shared.Components.Searching;

public class SearchFieldDescriptor
{
    public SearchControlType ControlType { get; init; }

    public string Field
    {
        get
        {
            return Property.Name;
        }
    }

    public string Label { get; init; } = default!;

    public FilterOperator Operator { get; init; }

    public string[]? Options { get; init; }

    public int Order { get; init; }

    public PropertyInfo Property { get; init; } = default!;

    public Type UnderlyingType
    {
        get
        {
            return Nullable.GetUnderlyingType(ValueType) ?? ValueType;
        }
    }

    public Type ValueType
    {
        get
        {
            return Property.PropertyType;
        }
    }
}

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

    public static List<SearchFieldDescriptor> GetFields<TEntity>()
    {
        return GetFields(typeof(TEntity));
    }

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
