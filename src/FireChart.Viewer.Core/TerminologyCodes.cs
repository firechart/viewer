using System.Text.Json;

namespace FireChart.Viewer.Core;

/// <summary>
/// Collects the codes of records that a person would otherwise read as a bare code: what a page asks a terminology server about.
/// </summary>
/// <remarks>
/// A code is collected when a Coding has a <c>system</c> and a <c>code</c> and no <c>display</c> of its own. A Quantity (it has a value or a unit) and a
/// Reference are not codings; units are shown as written.
/// </remarks>
/// <example>
/// <code>
/// var codes = TerminologyCodes.Collect(bundleEntries.Select(entry =&gt; entry.Json));
/// foreach (var code in codes) { /* CodeSystem/$lookup on the terminology server */ }
/// </code>
/// </example>
public static class TerminologyCodes
{
    /// <summary>
    /// The most codes one call collects: a page does not flood the terminology server.
    /// </summary>
    public const int Limit = 200;

    /// <summary>
    /// Collects the codes without a display from resources.
    /// </summary>
    /// <param name="resources">The resources as the JSON text the server sent; a text that is not JSON is skipped.</param>
    /// <returns>At most <see cref="Limit"/> distinct codes.</returns>
    public static IReadOnlySet<CodeKey> Collect(IEnumerable<string> resources)
    {
        var found = new HashSet<CodeKey>();
        foreach (var json in resources)
        {
            try
            {
                using var document = JsonDocument.Parse(json);
                Walk(document.RootElement, found);
            }
            catch (JsonException)
            {
                // a resource that cannot be read has no codes to look up
            }

            if (found.Count >= Limit)
            {
                break;
            }
        }

        return found;
    }

    private static void Walk(JsonElement element, HashSet<CodeKey> found)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                if (IsBareCoding(element, out var key))
                {
                    found.Add(key);
                }

                foreach (var property in element.EnumerateObject())
                {
                    if (found.Count < Limit)
                    {
                        Walk(property.Value, found);
                    }
                }

                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    if (found.Count < Limit)
                    {
                        Walk(item, found);
                    }
                }

                break;
        }
    }

    private static bool IsBareCoding(JsonElement element, out CodeKey key)
    {
        key = default;
        if (!element.TryGetProperty("system", out var system) || system.ValueKind != JsonValueKind.String
            || !element.TryGetProperty("code", out var code) || code.ValueKind != JsonValueKind.String
            || element.TryGetProperty("display", out _) || element.TryGetProperty("value", out _) || element.TryGetProperty("unit", out _) || element.TryGetProperty("reference", out _))
        {
            return false;
        }

        var s = system.GetString();
        var c = code.GetString();
        if (string.IsNullOrWhiteSpace(s) || string.IsNullOrWhiteSpace(c) || !Uri.TryCreate(s, UriKind.Absolute, out _))
        {
            return false;
        }

        key = new CodeKey(s, c);
        return true;
    }
}
