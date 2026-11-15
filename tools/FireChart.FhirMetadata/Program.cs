using FireChart.Tools.FhirMetadata;

// dotnet run --project public/tools/FireChart.FhirMetadata: writes the metadata of the core FHIR releases into FireChart.Viewer.Core
// (src/FireChart.Viewer.Core/Fhir/Metadata/*.g.cs of the folder of FireChart.Viewer.slnx, found from the current folder up)
var root = new DirectoryInfo(Directory.GetCurrentDirectory());
while (root is not null && !File.Exists(Path.Combine(root.FullName, "FireChart.Viewer.slnx")) && !File.Exists(Path.Combine(root.FullName, "public", "FireChart.Viewer.slnx")))
{
    root = root.Parent;
}

if (root is null)
{
    await Console.Error.WriteLineAsync("Run it in the repository: FireChart.Viewer.slnx is not found from here up.");
    return 1;
}

var publicRoot = File.Exists(Path.Combine(root.FullName, "FireChart.Viewer.slnx")) ? root.FullName : Path.Combine(root.FullName, "public");
var target = Path.Combine(publicRoot, "src", "FireChart.Viewer.Core", "Fhir", "Metadata");
foreach (var release in FhirMetadataGenerator.Releases)
{
    var folder = await CorePackages.EnsureAsync(release.PackageName, release.PackageVersion);
    var file = Path.Combine(target, release.ClassName + ".g.cs");
    var text = FhirMetadataGenerator.Generate(folder, release);
    var changed = !File.Exists(file) || await File.ReadAllTextAsync(file) != text;
    if (changed)
    {
        await File.WriteAllTextAsync(file, text);
    }

    await Console.Out.WriteLineAsync($"{release.Release}: {release.PackageName}@{release.PackageVersion} -> {file} ({(changed ? "written" : "unchanged")})");
}

return 0;
