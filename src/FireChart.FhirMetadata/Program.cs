using FireChart.Tools.FhirMetadata;

// dotnet run --project src/FireChart.FhirMetadata: writes the metadata of the core FHIR releases into FireChart.Viewer.Core
// (src/FireChart.Viewer.Core/Fhir/Metadata/*.g.cs of the repository, whose root is found by its solution from the current folder up:
// FireChart.slnx, or FireChart.Viewer.slnx of the public repository, whose export rewrites the path)
string[] solutions = ["FireChart.slnx", "FireChart.Viewer.slnx"];
var root = new DirectoryInfo(Directory.GetCurrentDirectory());
while (root is not null && !solutions.Any(solution => File.Exists(Path.Combine(root.FullName, solution))))
{
    root = root.Parent;
}

if (root is null)
{
    await Console.Error.WriteLineAsync("Run it in the repository: FireChart.slnx or FireChart.Viewer.slnx is not found from here up.");
    return 1;
}

var target = Path.Combine(root.FullName, "src/FireChart.Viewer.Core/Fhir/Metadata");
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
