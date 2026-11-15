using System.Reflection;
using System.Text.Json;

namespace Flox.Core.Util;

/// <summary>
/// Helpers for reflection over types, properties and assembly resources.
/// </summary>
public static class ReflectionUtil
{
    private static bool IsEnumerableType(Type type)
    {
        if (type == typeof(string))
        {
            return false;
        }

        if (type.IsArray)
        {
            return true;
        }

        return (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>)) || type.GetInterfaces()
                                                                                                       .Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));
    }

    /// <summary>
    /// Deserializes a JSON resource of the assembly; returns null when there is no such resource.
    /// </summary>
    public static T? Deserialize<T>(Assembly assembly, string resourceName) where T : class
    {
        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
        {
            return null;
        }

        return JsonSerializer.Deserialize<T>(stream);
    }

    /// <summary>
    /// Returns the enums declared in the namespace.
    /// </summary>
    public static IEnumerable<Type> GetEnumsFromNamespace(Assembly asm, string namespaceName)
    {
        return asm.GetTypes()
                  .Where(type => type.Namespace == namespaceName && type.IsEnum);
    }

    /// <summary>
    /// Returns a copy of an embedded resource as a stream, or null when there is none.
    /// </summary>
    public static Stream? GetResourceAsStream(Assembly assembly, string resourceName)
    {
        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
        {
            return null;
        }

        var memoryStream = new MemoryStream();
        stream.CopyTo(memoryStream);
        memoryStream.Position = 0;
        return memoryStream;
    }

    /// <summary>
    /// Returns an embedded resource as text, or null when there is none.
    /// </summary>
    public static string? GetResourceAsString(Assembly assembly, string resourceName)
    {
        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
        {
            return null;
        }

        using var streamReader = new StreamReader(stream);
        return streamReader.ReadToEnd();
    }

    /// <summary>
    /// Returns the classes of a namespace of the assembly, or of every loaded assembly when none is given.
    /// </summary>
    public static IEnumerable<Type> GetTypesFromNamespace(Assembly? asm, string? namespaceName = null)
    {
        if (asm != null)
        {
            return asm.GetTypes()
                      .Where(type => type.IsClass && (namespaceName == null || type.Namespace == namespaceName));
        }
        else
        {
            return
            [
                .. AppDomain.CurrentDomain
                            .GetAssemblies()
                            .SelectMany(assembly =>
                             {
                                 try
                                 {
                                     return assembly.GetTypes();
                                 }
                                 catch (ReflectionTypeLoadException e)
                                 {
                                     return e.Types
                                             .Where(t => t is not null)
                                             .Select(row => row!);
                                 }
                                 catch
                                 {
                                     return [];
                                 }
                             })
                            .Where(type => type.Namespace != null && (namespaceName == null || type.Namespace.StartsWith(namespaceName, StringComparison.Ordinal)))
            ];
        }
    }

    /// <summary>
    /// Tells whether the type is a class that is not a primitive, a collection or a nullable value type.
    /// </summary>
    public static (bool result, bool isNullable) IsClass(Type type)
    {
        if (Nullable.GetUnderlyingType(type) is not null || !type.IsClass || IsPrimitive(type) || IsEnumerableType(type))
        {
            return (false, false);
        }

        return (true, false);
    }

    /// <summary>
    /// Tells whether the property holds such a class and whether it is nullable.
    /// </summary>
    public static (bool result, bool isNullable) IsClassProperty(PropertyInfo property)
    {
        var type = property.PropertyType;

        // Nullable<T> struct, non-class, primitive, or collection — not a class
        if (Nullable.GetUnderlyingType(type) is not null || !type.IsClass || IsPrimitive(type) || IsEnumerableType(type))
        {
            return (false, false);
        }

        var nullabilityInfo = new NullabilityInfoContext().Create(property);
        return (true, nullabilityInfo.ReadState == NullabilityState.Nullable);
    }

    /// <summary>
    /// Tells whether the property is a collection (a string is not) and gives its element type.
    /// </summary>
    public static bool IsCollection(PropertyInfo property, out Type? elementType)
    {
        elementType = null;
        var type = property.PropertyType;

        // string — exclude (it is enumerable but not collection)
        if (type == typeof(string))
        {
            return false;
        }

        if (type.IsArray)
        {
            elementType = type.GetElementType();
            return true;
        }

        var enumerableType = type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>)
            ? type
            : type.GetInterfaces()
                  .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));

        if (enumerableType is not null)
        {
            elementType = enumerableType.GetGenericArguments()[0];
            return true;
        }

        return false;
    }

    /// <summary>
    /// Tells whether the type is a JsonElement and whether it is nullable.
    /// </summary>
    public static (bool result, bool isNullable) IsJsonElement(Type type)
    {
        if (Nullable.GetUnderlyingType(type) == typeof(JsonElement))
        {
            return (true, true);
        }

        if (type == typeof(JsonElement))
        {
            return (true, false);
        }

        return (false, false);
    }

    /// <summary>
    /// Tells whether the property holds a JsonElement and whether it is nullable.
    /// </summary>
    public static (bool result, bool isNullable) IsJsonElementProperty(PropertyInfo propertyInfo)
    {
        return IsJsonElement(propertyInfo.PropertyType);
    }

    /// <summary>
    /// Tells whether the property accepts null.
    /// </summary>
    public static bool IsNullableProperty(PropertyInfo property)
    {
        var type = property.PropertyType;

        // Value type: int?, Guid?, DateTime? — is Nullable<T>
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
        {
            return true;
        }

        // Reference type: string?, MyClass? — requres NullabilityInfoContext (.NET 6+)
        var context = new NullabilityInfoContext();
        var info = context.Create(property);
        return info.WriteState == NullabilityState.Nullable || info.ReadState == NullabilityState.Nullable;
    }

    /// <summary>
    /// Tells whether the type is a simple value: a primitive, an enum, a string, a number, a date, a Guid or the like (or its nullable).
    /// </summary>
    public static bool IsPrimitive(Type type)
    {
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
        {
            type = Nullable.GetUnderlyingType(type)!;
        }

        return type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(decimal) || type == typeof(DateTime) || type == typeof(DateTimeOffset) || type == typeof(DateOnly) || type == typeof(TimeOnly) || type == typeof(TimeSpan) || type == typeof(Guid);
    }

    /// <summary>
    /// Tells whether the property is a collection of simple values and gives their type.
    /// </summary>
    public static bool IsPrimitiveCollection(PropertyInfo property, out Type? elementType)
    {
        elementType = null;

        if (!IsCollection(property, out var candidate) || candidate is null)
        {
            return false;
        }

        var underlying = Nullable.GetUnderlyingType(candidate) ?? candidate;
        if (!IsPrimitive(underlying))
        {
            return false;
        }

        elementType = candidate;
        return true;
    }

    /// <summary>
    /// Tells whether the property is a collection of classes and gives their type.
    /// </summary>
    public static bool IsClassCollection(PropertyInfo property, out Type? elementType)
    {
        elementType = null;

        if (!IsCollection(property, out var candidate) || candidate is null)
        {
            return false;
        }

        var underlying = Nullable.GetUnderlyingType(candidate) ?? candidate;
        (var rv, var b) = IsClass(underlying);
        if (!rv)
        {
            return false;
        }

        elementType = candidate;
        return true;
    }

    /// <summary>
    /// Tells whether the property holds a simple value and whether it is nullable.
    /// </summary>
    public static (bool result, bool isNullable) IsPrimitiveProperty(PropertyInfo property)
    {
        var type = property.PropertyType;
        var underlyingType = Nullable.GetUnderlyingType(type);

        if (underlyingType is not null)
        {
            return IsPrimitive(underlyingType) ? (true, true) : (false, false);
        }

        if (IsPrimitive(type))
        {
            // string is a reference type — check NullabilityInfoContext
            if (type == typeof(string))
            {
                var info = new NullabilityInfoContext().Create(property);
                return (true, info.ReadState == NullabilityState.Nullable);
            }

            return (true, false);
        }

        return (false, false);
    }

    /// <summary>
    /// Tells whether the type is an enum or a nullable enum, whether it is nullable and gives the enum type.
    /// </summary>
    public static (bool result, bool isNullable) IsEnumOrNullableEnum(Type type, out Type? enumType)
    {
        if (type.IsEnum)
        {
            enumType = type;
            return (true, false);
        }

        var underlying = Nullable.GetUnderlyingType(type);
        if (underlying?.IsEnum == true)
        {
            enumType = underlying;
            return (true, true);
        }

        enumType = null;
        return (false, false);
    }

    /// <summary>
    /// Tells whether the type derives from or implements the parent type; an open generic parent is matched by its definition.
    /// </summary>
    /// <remarks>
    /// InheritsFrom(typeof(List&lt;int&gt;), typeof(IEnumerable&lt;&gt;))  // true
    /// InheritsFrom(typeof(List&lt;int&gt;), typeof(IDisposable))    // false
    /// InheritsFrom(typeof(MyEntity), typeof(Entity))          // true
    /// InheritsFrom(typeof(int), typeof(object))               // false
    /// </remarks>
    public static bool InheritsFrom(Type type, Type parentType)
    {
        if (parentType.IsInterface)
        {
            return parentType.IsAssignableFrom(type);
        }

        if (parentType.IsGenericTypeDefinition)
        {
            var current = type;
            while (current != null && current != typeof(object))
            {
                if (current.IsGenericType && current.GetGenericTypeDefinition() == parentType)
                {
                    return true;
                }

                current = current.BaseType;
            }

            return false;
        }

        return parentType.IsAssignableFrom(type);
    }
}
