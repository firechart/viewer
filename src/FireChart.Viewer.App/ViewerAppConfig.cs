using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using FireChart.Viewer.Appearance;
using FireChart.Viewer.Core.Fhir;
using FireChart.Viewer.Smart;
using Hl7.Fhir.Specification;
using Microsoft.AspNetCore.Components;

namespace FireChart.Viewer.App;

// viewer.json of the application, the keys of the plan (section 4.4) that the frame understands so far: one server, its SMART client, the language, the
// terminology server, the clinical summary form, the theme and the variables of the appearance. An unknown key is an error, not silence
internal sealed record ViewerAppConfig
{
    public required ServerConfig Server { get; init; }

    public SmartConfig? Smart { get; init; }

    public string? Locale { get; init; }

    public TerminologyConfig? Terminology { get; init; }

    public ClinicalSummaryConfig? ClinicalSummary { get; init; }

    public AppearanceConfig? Appearance { get; init; }
}

internal sealed record ServerConfig
{
    public required string Url { get; init; }

    public string? Name { get; init; }

    public string? FhirRelease { get; init; }

    public int? PageSize { get; init; }
}

internal sealed record SmartConfig
{
    public required string ClientId { get; init; }

    public string? Scopes { get; init; }

    public string? Flow { get; init; }

    public string? Authority { get; init; }

    public string? ExtraParameters { get; init; }
}

internal sealed record TerminologyConfig
{
    public string? Server { get; init; }
}

internal sealed record ClinicalSummaryConfig
{
    public string? Form { get; init; }
}

internal sealed record AppearanceConfig
{
    public string? Theme { get; init; }

    public Dictionary<string, string>? Variables { get; init; }

    public Dictionary<string, Dictionary<string, string>>? Rules { get; init; }
}

// The server of the configuration as the Viewer sees it; the key is the address, so two frames with two servers keep their sessions apart
internal sealed class ConfiguredServer(ServerConfig server, SmartConfig? smart, FhirRelease? release) : IViewerServer
{
    private const int DefaultPageSize = 50;

    public string Key => server.Url;

    public string Name => server.Name ?? new Uri(server.Url).Host;

    public string BaseUrl => server.Url;

    public AuthenticationType AuthenticationType => smart is null ? AuthenticationType.None : AuthenticationType.OAuth2;

    public FhirRelease? FhirRelease => release;

    public int PageSize => server.PageSize ?? DefaultPageSize;

    public string? Authority => smart?.Authority;

    public string? OAuth2ClientId => smart?.ClientId;

    public string? OAuth2Scope => smart?.Scopes;

    public string? OAuth2ExtraParameters => smart?.ExtraParameters;
}

// What the frame shows: the Viewer with its options, or the errors of the configuration
// Culture: none for "auto", the language of the browser
internal sealed record ViewerAppSetup(IViewerServer? Server, SmartFlow SmartFlow, CultureInfo? Culture, string? TerminologyServer, string? ClinicalSummaryForm,
                                      ViewerAppearance? Appearance, IReadOnlyList<ViewerConfigError> Errors)
{
    public static ViewerAppSetup Failed(params IReadOnlyList<ViewerConfigError> errors)
    {
        return new ViewerAppSetup(null, SmartFlow.Redirect, null, null, null, null, errors);
    }
}

internal sealed class ViewerAppConfigLoader(IHttpClientFactory httpClientFactory, NavigationManager navigationManager)
{
    public const string FileName = "viewer.json";

    private static readonly JsonSerializerOptions s_json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private ViewerAppSetup? _setup;

    public async Task<ViewerAppSetup> LoadAsync(CancellationToken cancellationToken = default)
    {
        return _setup ??= await ReadAsync(cancellationToken);
    }

    // The languages of the texts of the Viewer
    public static readonly IReadOnlyList<string> Locales = ["en", "de", "es", "ru", "zh-Hans"];

    private async Task<ViewerAppSetup> ReadAsync(CancellationToken cancellationToken)
    {
        string text;
        try
        {
            using var http = httpClientFactory.CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Get, navigationManager.ToAbsoluteUri(FileName));
            request.Headers.CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue { NoCache = true };
            using var response = await http.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return Failed("config_not_found", FileName, $"{FileName} could not be read: HTTP {(int)response.StatusCode}.");
            }

            text = await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            return Failed("config_not_found", FileName, ex.Message);
        }

        return Parse(text);
    }

    // The same checks for viewer.json, a configuration the host of the frame sends and the editor of the sandbox
    public static ViewerAppSetup Parse(string text)
    {
        ViewerAppConfig? config;
        try
        {
            var keyErrors = ConfigKeys.Check(text);
            if (keyErrors.Count > 0)
            {
                return ViewerAppSetup.Failed(keyErrors);
            }

            config = JsonSerializer.Deserialize<ViewerAppConfig>(text, s_json);
        }
        catch (JsonException ex)
        {
            // The messages of System.Text.Json are trimmed in the browser (resource keys only): the path says where
            return Failed("invalid_value", (ex.Path ?? "$").TrimStart('$', '.'), "The value is not of the expected type, or the JSON is not well formed.");
        }

        return config is null ? Failed("invalid_config", "$", "The configuration is empty.") : Validate(config);
    }

    private static ViewerAppSetup Validate(ViewerAppConfig config)
    {
        var errors = new List<ViewerConfigError>();
        if (!Uri.TryCreate(config.Server.Url, UriKind.Absolute, out var url) || url.Scheme is not ("https" or "http"))
        {
            errors.Add(new ViewerConfigError("invalid_option", "server.url", $"'{config.Server.Url}' is not an absolute http(s) address."));
        }

        FhirRelease? release = null;
        if (config.Server.FhirRelease is { } literal)
        {
            if (Enum.TryParse<FhirRelease>(literal, true, out var parsed))
            {
                release = parsed;
            }
            else
            {
                errors.Add(new ViewerConfigError("invalid_option", "server.fhirRelease", $"'{literal}' is not a release of FHIR (STU3, R4, R4B, R5, R6)."));
            }
        }

        var flow = SmartFlow.Redirect;
        if (config.Smart?.Flow is { } flowName && !Enum.TryParse(flowName, true, out flow))
        {
            errors.Add(new ViewerConfigError("invalid_option", "smart.flow", $"'{flowName}' is not a flow: redirect or popup."));
        }

        CultureInfo? culture = null;
        if (config.Locale is { } locale && !locale.Equals("auto", StringComparison.OrdinalIgnoreCase))
        {
            culture = CultureOf(locale);
            if (culture is null)
            {
                errors.Add(new ViewerConfigError("invalid_option", "locale", $"'{locale}' is not a language of the Viewer: auto, {string.Join(", ", Locales)}."));
            }
        }

        ViewerAppearance? appearance = null;
        if (config.Appearance is { } look)
        {
            var theme = ViewerTheme.Auto;
            if (look.Theme is { } themeName && !Enum.TryParse(themeName, true, out theme))
            {
                errors.Add(new ViewerConfigError("invalid_option", "appearance.theme", $"'{themeName}' is not a theme: light, dark or auto."));
            }

            appearance = new ViewerAppearance
            {
                Theme = theme,
                Variables = new Dictionary<string, string>(look.Variables ?? [], StringComparer.Ordinal),
                Rules = new Dictionary<string, Dictionary<string, string>>(look.Rules ?? [], StringComparer.Ordinal)
            };
        }

        var server = errors.Count == 0 ? new ConfiguredServer(config.Server, config.Smart, release) : null;
        return new ViewerAppSetup(server, flow, culture, config.Terminology?.Server, config.ClinicalSummary?.Form,
                                  appearance ?? new ViewerAppearance { Theme = ViewerTheme.Auto }, errors);
    }

    // A tag of a language the Viewer has texts for (ru, ru-RU, zh-Hans); the region stays for the formats of dates and numbers
    private static CultureInfo? CultureOf(string locale)
    {
        try
        {
            var culture = CultureInfo.GetCultureInfo(locale);
            var language = culture.TwoLetterISOLanguageName;
            return culture.Name.Length > 0 && Locales.Any(l => l.StartsWith(language, StringComparison.OrdinalIgnoreCase)) ? culture : null;
        }
        catch (CultureNotFoundException)
        {
            return null;
        }
    }

    private static ViewerAppSetup Failed(string code, string param, string message)
    {
        return ViewerAppSetup.Failed(new ViewerConfigError(code, param, message));
    }
}

// The keys of viewer.json against the records of the configuration: an unknown key is named with the closest known one, a required one that is missing
// is named too. Done before the binding, whose messages say nothing in the browser
internal static class ConfigKeys
{
    public static List<ViewerConfigError> Check(string json)
    {
        var errors = new List<ViewerConfigError>();
        using var document = JsonDocument.Parse(json, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        Walk(document.RootElement, typeof(ViewerAppConfig), string.Empty, errors);
        return errors;
    }

    private static void Walk(JsonElement element, Type type, string path, List<ViewerConfigError> errors)
    {
        if (element.ValueKind != JsonValueKind.Object || !IsConfigRecord(type))
        {
            return;
        }

        var properties = type.GetProperties().ToDictionary(p => JsonNamingPolicy.CamelCase.ConvertName(p.Name), StringComparer.Ordinal);
        foreach (var member in element.EnumerateObject())
        {
            var param = path.Length == 0 ? member.Name : $"{path}.{member.Name}";
            if (properties.TryGetValue(member.Name, out var property))
            {
                Walk(member.Value, property.PropertyType, param, errors);
                continue;
            }

            var closest = properties.Keys.OrderBy(k => Distance(k, member.Name)).First();
            var hint = Distance(closest, member.Name) <= 2 ? $" Did you mean '{closest}'?" : $" Known options: {string.Join(", ", properties.Keys)}.";
            errors.Add(new ViewerConfigError("unknown_option", param, $"'{member.Name}' is not an option.{hint}"));
        }

        foreach (var (name, property) in properties)
        {
            if (property.GetCustomAttributes(typeof(System.Runtime.CompilerServices.RequiredMemberAttribute), false).Length > 0 && !element.TryGetProperty(name, out _))
            {
                errors.Add(new ViewerConfigError("missing_option", path.Length == 0 ? name : $"{path}.{name}", $"'{name}' is required."));
            }
        }
    }

    private static bool IsConfigRecord(Type type)
    {
        return type.Namespace == typeof(ViewerAppConfig).Namespace && type.Name.EndsWith("Config", StringComparison.Ordinal);
    }

    private static int Distance(string a, string b)
    {
        var row = Enumerable.Range(0, b.Length + 1).ToArray();
        for (var i = 1; i <= a.Length; i++)
        {
            var previous = row[0];
            row[0] = i;
            for (var j = 1; j <= b.Length; j++)
            {
                var current = row[j];
                row[j] = Math.Min(Math.Min(row[j] + 1, row[j - 1] + 1), previous + (char.ToLowerInvariant(a[i - 1]) == char.ToLowerInvariant(b[j - 1]) ? 0 : 1));
                previous = current;
            }
        }

        return row[b.Length];
    }
}
