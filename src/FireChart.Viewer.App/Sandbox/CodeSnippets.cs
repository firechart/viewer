using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using FireChart.Viewer.Appearance;
using FireChart.Viewer.Smart;

namespace FireChart.Viewer.App.Sandbox;

internal sealed record CodeSnippet(string Title, string Language, string Code);

// "Copy to your page": the configuration of the sandbox as a frame on the own server and as the Blazor component of the package
internal static class CodeSnippets
{
    private static readonly JsonSerializerOptions s_indented = new() { WriteIndented = true };

    public static string Version { get; } =
        typeof(FhirViewer).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "*";

    public static IReadOnlyList<CodeSnippet> Build(JsonObject config)
    {
        var setup = ViewerAppConfigLoader.Parse(config.ToJsonString());
        var snippets = new List<CodeSnippet>
        {
            new("viewer.json", "json", config.ToJsonString(s_indented)),
            new("Docker", "bash", Docker()),
            new("iframe", "html", Frame())
        };
        if (setup.Server is not null)
        {
            snippets.Add(new CodeSnippet("Blazor (NuGet)", "razor", Blazor(config, setup)));
        }

        return snippets;
    }

    // The image serves the Viewer on a server of the customer; HTTPS comes from the reverse proxy in front of it
    private static string Docker()
    {
        return $"""
                # viewer.json: the configuration above; host.json: the origins that may drive the frame ("frameAncestors")
                docker run -d --name fhir-viewer -p 8080:8080 \
                  -v "$PWD/viewer.json:/usr/share/nginx/html/viewer.json:ro" \
                  -v "$PWD/host.json:/usr/share/nginx/html/host.json:ro" \
                  firechart-viewer:{Version}
                """;
    }

    private static string Frame()
    {
        return """
               <!-- The Viewer on a server of your own (the Docker image above behind your HTTPS proxy), at https://viewer.example.org.
                    A SMART server: register https://viewer.example.org/smart/callback (redirect)
                    and https://viewer.example.org/_content/FireChart.Viewer/smart-callback.html (popup) as redirect URIs -->
               <iframe src="https://viewer.example.org/embed"
                       title="FHIR Viewer"
                       style="width: 100%; height: 800px; border: 0"></iframe>
               """;
    }

    private static string Blazor(JsonObject config, ViewerAppSetup setup)
    {
        var server = setup.Server!;
        var smart = server.AuthenticationType == Core.Fhir.AuthenticationType.OAuth2;
        var code = new StringBuilder();
        code.AppendLine(CultureInfo.InvariantCulture, $"@* dotnet add package FireChart.Viewer --version {Version} *@");
        code.AppendLine("@page \"/viewer/{*Path}\"");
        if (smart)
        {
            code.AppendLine("@using FireChart.Viewer.Smart");
            code.AppendLine("@inject NavigationManager NavigationManager");
        }

        code.AppendLine();
        code.AppendLine("<FhirViewer Server=\"_server\"");
        code.Append("            RoutePrefix=\"viewer/\"");
        if (smart)
        {
            var flow = setup.SmartFlow == SmartFlow.Popup ? ", Flow = SmartFlow.Popup" : string.Empty;
            code.AppendLine().Append(CultureInfo.InvariantCulture, $"            Smart=\"@(new SmartOptions {{ RedirectUri = NavigationManager.ToAbsoluteUri(\"smart/callback\").ToString(){flow} }})\"");
        }

        // The FHIR settings that differ from the defaults
        var fhir = setup.Fhir;
        var settings = new List<string>();
        if (fhir.TerminologyServerUrl is { } terminology)
        {
            settings.Add($"TerminologyServerUrl = \"{terminology}\"");
        }

        if (fhir.PageSize != FhirOptions.DefaultPageSize)
        {
            settings.Add($"PageSize = {fhir.PageSize.ToString(CultureInfo.InvariantCulture)}");
        }

        if (fhir.LoadAll)
        {
            settings.Add("LoadAll = true");
        }

        if (fhir.Decimals != FhirOptions.DefaultDecimals)
        {
            settings.Add($"Decimals = {fhir.Decimals.ToString(CultureInfo.InvariantCulture)}");
        }

        if (settings.Count > 0)
        {
            code.AppendLine().Append(CultureInfo.InvariantCulture, $"            Fhir=@(new FhirOptions {{ {string.Join(", ", settings)} }})");
        }

        var appearance = setup.Appearance;
        var hasAppearance = config["appearance"] is JsonObject;
        if (hasAppearance)
        {
            code.AppendLine().Append("            Appearance=\"_appearance\"");
        }

        code.AppendLine(" />");
        code.AppendLine();
        code.AppendLine("@code {");
        code.AppendLine();
        code.AppendLine(CultureInfo.InvariantCulture, $"    private readonly IViewerServer _server = new ViewerServer(\"{server.BaseUrl}\", \"{server.Name}\");");
        if (hasAppearance && appearance is not null)
        {
            code.AppendLine();
            code.AppendLine("    private readonly ViewerAppearance _appearance = new()");
            code.AppendLine("    {");
            code.Append(CultureInfo.InvariantCulture, $"        Theme = ViewerTheme.{appearance.Theme}");
            if (appearance.Style is { } style)
            {
                code.AppendLine(",").Append(CultureInfo.InvariantCulture, $"        Style = \"{style}\"");
            }

            if (appearance.Variables.Count > 0)
            {
                code.AppendLine(",").AppendLine("        Variables =").AppendLine("        {");
                code.Append(string.Join(",\n", appearance.Variables.Select(v => $"            [\"{v.Key}\"] = \"{v.Value}\"")));
                code.AppendLine().Append("        }");
            }

            code.AppendLine().AppendLine("    };");
        }

        code.AppendLine();
        code.AppendLine("    private sealed record ViewerServer(string BaseUrl, string Name) : IViewerServer");
        code.AppendLine("    {");
        code.AppendLine("        public string Key => BaseUrl;");
        code.AppendLine(CultureInfo.InvariantCulture, $"        public AuthenticationType AuthenticationType => AuthenticationType.{server.AuthenticationType};");
        code.AppendLine(CultureInfo.InvariantCulture, $"        public FhirRelease? FhirRelease => {(server.FhirRelease is { } release ? "Hl7.Fhir.Specification.FhirRelease." + release : "null")};");
        code.AppendLine(CultureInfo.InvariantCulture, $"        public int PageSize => {server.PageSize};");
        if (smart)
        {
            code.AppendLine(CultureInfo.InvariantCulture, $"        public string? OAuth2ClientId => {Literal(server.OAuth2ClientId)};");
            code.AppendLine(CultureInfo.InvariantCulture, $"        public string? OAuth2Scope => {Literal(server.OAuth2Scope)};");
            if (server.OAuth2ExtraParameters is not null)
            {
                code.AppendLine(CultureInfo.InvariantCulture, $"        public string? OAuth2ExtraParameters => {Literal(server.OAuth2ExtraParameters)};");
            }

            if (server.Authority is not null)
            {
                code.AppendLine(CultureInfo.InvariantCulture, $"        public string? Authority => {Literal(server.Authority)};");
            }
        }

        code.AppendLine("    }");
        code.AppendLine();
        code.Append('}');
        if (setup.Culture is { } culture)
        {
            code.AppendLine().AppendLine().Append(CultureInfo.InvariantCulture, $"@* The language is the culture of the application: CultureInfo.DefaultThreadCurrentUICulture = new(\"{culture.Name}\") *@");
        }

        return code.ToString();
    }

    private static string Literal(string? value)
    {
        return value is null ? "null" : JsonSerializer.Serialize(value);
    }
}
