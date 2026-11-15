using System.Text.Json;
using Flox.Core.Util;

namespace Flox.Core.Models;

/// <summary>Helpers to put parameters into a list and read them back as typed values.</summary>
public static class BackgroundTaskParameterExtensions
{
    /// <summary>Sets a parameter, replacing one of the same name; the value is stored as JSON.</summary>
    /// <param name="parameters">The list of parameters.</param>
    /// <param name="name">The name of the parameter.</param>
    /// <param name="value">The value.</param>
    public static void Add(this List<BackgroundTaskParameter> parameters, string name, object? value)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        parameters.RemoveAll(row => row.Name == name);
        parameters.Add(new BackgroundTaskParameter
        {
            Name = name,
            Value = JsonSerializer.Serialize(value, JsonUtil.DefaultCamelCaseOptions)
        });
    }

    /// <summary>Sets a parameter, replacing one of the same name; the value is stored as JSON.</summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="parameters">The list of parameters.</param>
    /// <param name="name">The name of the parameter.</param>
    /// <param name="value">The value.</param>
    public static void Add<T>(this List<BackgroundTaskParameter> parameters, string name, T value)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        parameters.RemoveAll(row => row.Name == name);
        parameters.Add(new BackgroundTaskParameter
        {
            Name = name,
            Value = JsonSerializer.Serialize(value, JsonUtil.DefaultCamelCaseOptions)
        });
    }

    /// <summary>Reads a parameter as a value of the given type.</summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="parameters">The list of parameters.</param>
    /// <param name="name">The name of the parameter.</param>
    /// <returns>The value, or the default of the type when the parameter is missing, empty or not valid JSON for the type.</returns>
    public static T? Get<T>(this List<BackgroundTaskParameter> parameters, string name)
    {
        var parameter = parameters.FirstOrDefault(row => row.Name == name);
        if (parameter == null)
        {
            return default;
        }

        if (string.IsNullOrEmpty(parameter?.Value))
        {
            return default;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(parameter.Value, JsonUtil.DefaultCamelCaseOptions);
        }
        catch (JsonException)
        {
            return default;
        }
    }
}
