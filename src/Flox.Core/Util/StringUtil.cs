using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Flox.Core.Util;

public static partial class StringUtil
{
    [GeneratedRegex(@"\{[^}]*\}")]
    private static partial Regex PlaceholderRegex();

    /// <summary>
    /// Accepts and formats strings of kind {Hello} = {World}
    /// Used mostly to format log messages
    /// </summary>
    public static string FormatLogMessage(this string template, params object?[] args)
    {
        if (string.IsNullOrEmpty(template))
        {
            return template ?? string.Empty;
        }

        if (args.Length == 0)
        {
            return template;
        }

        var index = 0;
        return PlaceholderRegex()
           .Replace(template,
                    _ => index < args.Length
                        ? args[index++]
                          ?.ToString() ?? ""
                        : "");
    }

    /// <summary>
    /// Accepts and formats strings of kind {Hello} = {World}
    /// Used mostly to format route URLs
    /// </summary>
    public static string FormatMessage(this string format, params object?[] args)
    {
        if (string.IsNullOrEmpty(format))
        {
            return format ?? string.Empty;
        }

        if (args.Length == 0)
        {
            return format;
        }

        var argIndex = 0;
        var normalizedFormat = PlaceholderRegex()
           .Replace(format, _ => $"{{{argIndex++}}}");

        try
        {
            return string.Format(CultureInfo.CurrentCulture, normalizedFormat, args);
        }
        catch (FormatException)
        {
            return format;
        }
    }

    public static string ToSnakeCase(this string value)
    {
        return JsonNamingPolicy.SnakeCaseLower.ConvertName(value);
    }

    public static string ToLowerCamelCase(this string identifier)
    {
        return JsonNamingPolicy.CamelCase.ConvertName(identifier);
    }
}
