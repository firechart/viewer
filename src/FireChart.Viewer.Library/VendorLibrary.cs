using System.Reflection;
using Hl7.Fhir.Model;

namespace FireChart.Viewer.Library;

/// <summary>
/// The layout of the vendor library: the static files of this project (<c>wwwroot</c>), served by every host as
/// <c>_content/FireChart.Viewer.Library/...</c>, and read by a client one by one when they are needed.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><description><c>templates/{key}.liquid</c> for the generic templates (<c>_complex</c>, <c>_primitive</c>), <c>templates/datatypes</c> for the
/// datatypes and the primitive types of FHIR, <c>templates/resources</c> for the resources (and their elements, <c>Patient.contact</c>),
/// <c>templates/partials</c> for the rest (<c>tile-patient</c>, <c>badge</c>): a key says where its file is, there is no index.</description></item>
/// <item><description><c>locales/{language}/fhir-templates.json</c>: the translations of the static texts of the templates.</description></item>
/// <item><description><c>search-forms/{release}/{type}.json</c>: the search forms (<see cref="VendorSearchForms"/>).</description></item>
/// </list>
/// </remarks>
/// <example>
/// <code>
/// var path = VendorLibrary.TemplatePathOf("Patient@tile");   // templates/resources/Patient@tile.liquid
/// var url = VendorLibrary.UrlOf(path);                       // _content/FireChart.Viewer.Library/templates/resources/Patient@tile.liquid?v=1.2.0
/// </code>
/// </example>
public static class VendorLibrary
{
    /// <summary>
    /// The folder of the static files of the project.
    /// </summary>
    public const string WebRoot = "wwwroot";

    /// <summary>
    /// Where a host serves the static files of this library, relative to its base.
    /// </summary>
    public const string ContentRoot = "_content/FireChart.Viewer.Library";

    /// <summary>
    /// The folder of the templates.
    /// </summary>
    public const string TemplatesFolder = "templates";

    /// <summary>
    /// The folder of the translations.
    /// </summary>
    public const string LocalesFolder = "locales";

    /// <summary>
    /// The extension of a template.
    /// </summary>
    public const string TemplateExtension = ".liquid";

    /// <summary>
    /// The file of the translations of the templates in the folder of a language.
    /// </summary>
    public const string LocaleFile = "fhir-templates.json";

    /// <summary>
    /// The version of the library, the query of <see cref="UrlOf"/>: a new release reads new files, not the ones the browser cached.
    /// </summary>
    public static string Version { get; } = typeof(VendorLibrary).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0";

    /// <summary>
    /// The address of a file of the library for a client: below <see cref="ContentRoot"/>, with the version as the query.
    /// </summary>
    /// <param name="path">The path of the file in the library (<c>templates/partials/badge.liquid</c>).</param>
    /// <returns>The address relative to the base of the host.</returns>
    /// <example>
    /// <code>
    /// var url = VendorLibrary.UrlOf("locales/ru/fhir-templates.json");
    /// </code>
    /// </example>
    public static string UrlOf(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return $"{ContentRoot}/{path}?v={Uri.EscapeDataString(Version)}";
    }

    /// <summary>
    /// The path of the file of a template in the library.
    /// </summary>
    /// <param name="key">The key of the template (<c>Patient@tile</c>, <c>date@text</c>, <c>badge</c>, <c>_complex</c>).</param>
    /// <returns>The path (<c>templates/resources/Patient@tile.liquid</c>), or <see langword="null"/> for a key that cannot be a file name.</returns>
    /// <example>
    /// <code>
    /// VendorLibrary.TemplatePathOf("HumanName@text");   // templates/datatypes/HumanName@text.liquid
    /// VendorLibrary.TemplatePathOf("badge");            // templates/partials/badge.liquid
    /// </code>
    /// </example>
    public static string? TemplatePathOf(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (key.Length == 0 || key.Length > 128 || key.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '@' or '.' or '-' or '_')) || key.Contains("..", StringComparison.Ordinal))
        {
            return null;
        }

        var name = key.Split('@')[0].Split('.')[0];
        var folder = name.StartsWith('_') ? null
            : ModelInfo.IsPrimitive(name) || ModelInfo.IsDataType(name) ? "datatypes"
            : char.IsAsciiLetterUpper(name[0]) ? "resources"
            : "partials";
        return folder is null ? $"{TemplatesFolder}/{key}{TemplateExtension}" : $"{TemplatesFolder}/{folder}/{key}{TemplateExtension}";
    }

    /// <summary>
    /// The path of the file of the translations of a language.
    /// </summary>
    /// <param name="language">The language (<c>ru</c>) or the culture (<c>zh-Hans</c>), as the name of its folder.</param>
    /// <returns>The path (<c>locales/ru/fhir-templates.json</c>), or <see langword="null"/> for a name that cannot be a folder.</returns>
    /// <example>
    /// <code>
    /// var path = VendorLibrary.LocalePathOf("de");
    /// </code>
    /// </example>
    public static string? LocalePathOf(string language)
    {
        ArgumentNullException.ThrowIfNull(language);
        return language.Length is > 0 and <= 20 && language.All(c => char.IsAsciiLetterOrDigit(c) || c == '-') ? $"{LocalesFolder}/{language}/{LocaleFile}" : null;
    }
}
