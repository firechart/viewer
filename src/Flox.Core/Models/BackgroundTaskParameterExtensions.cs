using System.Text.Json;
using Flox.Core.Util;

namespace Flox.Core.Models;

public static class BackgroundTaskParameterExtensions
{
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
