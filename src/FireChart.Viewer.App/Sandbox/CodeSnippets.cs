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
            code.AppendLine("@inject NavigationManager NavigationManager");
        }

        code.AppendLine();
        code.AppendLine("<FhirViewer Server=\"_server\"");
        code.Append("            RoutePrefix=\"viewer/\"");
        if (smart)
        {
            code.AppendLine().Append("            SmartRedirectUri=\"@NavigationManager.ToAbsoluteUri(\"smart/callback\").ToString()\"");
        }

        if (setup.SmartFlow == SmartFlow.Popup)
        {
            code.AppendLine().Append("            SmartFlow=\"SmartFlow.Popup\"");
        }

        if (setup.TerminologyServer is { } terminology)
        {
            code.AppendLine().Append(CultureInfo.InvariantCulture, $"            TerminologyServer=\"{terminology}\"");
        }

        if (setup.ClinicalSummaryForm is { } form)
        {
            code.AppendLine().Append(CultureInfo.InvariantCulture, $"            ClinicalSummaryForm=\"{form}\"");
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
