using System.Formats.Tar;
using System.IO.Compression;

namespace FireChart.Tools.FhirMetadata;

// The core packages in a cache of the tool (~/.firechart/packages/{name}#{version}/package, or FIRECHART_PACKAGE_CACHE), downloaded from the FHIR package
// registry when missing. Not the cache of the Firely SDK: the registry republishes a version (hl7.fhir.r4.core 4.0.1 got bindings it lacked), and a
// cache filled years ago would give other metadata than a fresh one
public static class CorePackages
{
    // The registry of the rest of FireChart (Flox.Core Constants.DEFAULT_PACKAGE_SERVER): packages.fhir.org serves other builds of some core packages
    private const string Registry = "https://packages2.fhir.org/packages";

    // Another folder of the cache (a build machine, a test of the download)
    public const string CacheVariable = "FIRECHART_PACKAGE_CACHE";

    public static string Folder(string name, string version)
    {
        var cache = Environment.GetEnvironmentVariable(CacheVariable) is { Length: > 0 } configured
            ? configured
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".firechart", "packages");
        return Path.Combine(cache, $"{name}#{version}", "package");
    }

    public static async Task<string> EnsureAsync(string name, string version, CancellationToken cancellationToken = default)
    {
        var folder = Folder(name, version);
        if (Directory.Exists(folder))
        {
            return folder;
        }

        var root = Path.GetDirectoryName(folder)!;
        var temporary = root + ".download-" + Guid.NewGuid().ToString("N");
#pragma warning disable RS0030 // a command line tool run by hand: one request per missing package, no host to give a factory
        using var http = new HttpClient();
#pragma warning restore RS0030
        await using (var gzip = new GZipStream(await http.GetStreamAsync(new Uri($"{Registry}/{name}/{version}"), cancellationToken), CompressionMode.Decompress))
        {
            Directory.CreateDirectory(temporary);
            await TarFile.ExtractToDirectoryAsync(gzip, temporary, overwriteFiles: true, cancellationToken);
        }

        Directory.Move(temporary, root);
        return folder;
    }
}
