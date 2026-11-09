using System.Text.RegularExpressions;

namespace FireChart.Viewer.Tests.Repository;

// The public part is published on its own (a mirror of public/): a relative path in its files that leaves public/ works in the whole
// repository and breaks in the mirror. Its root is the folder of FireChart.Viewer.slnx, in both
public partial class PublicBoundaryTests
{
    private static readonly string[] s_extensions = [".csproj", ".props", ".targets", ".slnx", ".css", ".js", ".mjs", ".html", ".sh", ".cmd", ".json", ".yaml", ".yml"];

    // The files of the build without an extension
    private static readonly string[] s_names = ["Dockerfile", ".dockerignore"];

    // The files that run a build: their paths count from public/, which is the root of the mirror and the context of the image
    private static readonly string[] s_buildExtensions = [".sh", ".cmd", ".yaml", ".yml"];

    private static readonly string[] s_skipped = ["bin", "obj", "node_modules", ".tailwind", "wwwroot/lib", "wwwroot/css"];

    [GeneratedRegex(@"(?:\.\.[/\\])+[\w.@%{}()$-][^\s""'`<>;,|*?]*")]
    private static partial Regex RelativePath();

    // public/... or ci/... as a path: written from the root of the whole repository
    [GeneratedRegex(@"(?<![\w./-])(?:public|ci)/[\w.-]+")]
    private static partial Regex RepositoryRootPath();

    [Test]
    public void NoRelativePath_LeavesThePublicPart()
    {
        var root = Root();
        var escapes = new List<string>();
        foreach (var file in Files(root))
        {
            var text = File.ReadAllText(file);
            foreach (Match match in RelativePath().Matches(text))
            {
                var target = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(file)!, match.Value.Replace('\\', '/')));
                if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                {
                    escapes.Add($"{Path.GetRelativePath(root, file)}: {match.Value}");
                }
            }
        }

        Assert.That(escapes, Is.Empty);
    }

    [Test]
    public void BuildFiles_CountPathsFromThePublicPart()
    {
        var root = Root();
        var found = new List<string>();
        foreach (var file in Files(root).Where(IsBuildFile))
        {
            foreach (Match match in RepositoryRootPath().Matches(File.ReadAllText(file)))
            {
                found.Add($"{Path.GetRelativePath(root, file)}: {match.Value}");
            }
        }

        Assert.That(found, Is.Empty);
    }

    // Every file the public repository shows: the closed part is not named there and its hosts are not linked
    private static readonly string[] s_textExtensions = [.. s_extensions, ".cs", ".razor", ".md", ".liquid", ".ts", ".xml", ".resx", ".txt"];

    // The public hosts of firechart.org
    private static readonly string[] s_publicHosts = ["firechart.org", "www.firechart.org", "app.firechart.org", "viewer.firechart.org"];

    // It names the projects that must stay out on purpose
    private const string IsolationTests = "ViewerIsolationTests.cs";

    [GeneratedRegex(@"\b(?:Flox|FireChart)(?:\.[A-Z][A-Za-z0-9]*)+")]
    private static partial Regex ProjectLikeName();

    [GeneratedRegex(@"^\s*@?namespace\s+([\w.]+)", RegexOptions.Multiline)]
    private static partial Regex NamespaceDeclaration();

    [GeneratedRegex(@"https?://([a-z0-9.-]*firechart\.org)\b", RegexOptions.IgnoreCase)]
    private static partial Regex FireChartHost();

    [Test]
    public void ThePublicPart_NamesOnlyItsOwnProjects_AndLinksOnlyPublicHosts()
    {
        var root = Root();
        var files = Files(root, s_textExtensions).ToList();
        var known = Directory.EnumerateFiles(root, "*.csproj", System.IO.SearchOption.AllDirectories)
                             .Select(file => Path.GetFileNameWithoutExtension(file))
                             .Concat(files.Where(file => file.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) || file.EndsWith(".razor", StringComparison.OrdinalIgnoreCase))
                                          .SelectMany(file => NamespaceDeclaration().Matches(File.ReadAllText(file)).Select(match => match.Groups[1].Value)))
                             .ToHashSet(StringComparer.Ordinal);
        var found = new List<string>();
        foreach (var file in files)
        {
            var text = File.ReadAllText(file);
            var relative = Path.GetRelativePath(root, file);
            if (Path.GetFileName(file) != IsolationTests)
            {
                found.AddRange(ProjectLikeName().Matches(text)
                                                .Select(match => match.Value)
                                                .Where(name => !known.Any(own => name == own || name.StartsWith(own + ".", StringComparison.Ordinal)
                                                                                  || own.StartsWith(name + ".", StringComparison.Ordinal)))
                                                .Select(name => $"{relative}: {name}"));
            }

            found.AddRange(FireChartHost().Matches(text)
                                          .Select(match => match.Groups[1].Value)
                                          .Where(host => !s_publicHosts.Contains(host, StringComparer.OrdinalIgnoreCase))
                                          .Select(host => $"{relative}: {host}"));
        }

        Assert.That(found.Distinct(), Is.Empty);
    }

    private static bool IsBuildFile(string file)
    {
        return s_names.Contains(Path.GetFileName(file), StringComparer.Ordinal)
               || s_buildExtensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> Files(string root)
    {
        return Files(root, s_extensions);
    }

    private static IEnumerable<string> Files(string root, string[] extensions)
    {
        return Directory.EnumerateFiles(root, "*", System.IO.SearchOption.AllDirectories)
                        .Where(file => extensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase)
                                       || s_names.Contains(Path.GetFileName(file), StringComparer.Ordinal))
                        .Where(file =>
                        {
                            var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
                            return !s_skipped.Any(part => relative.Contains($"/{part}/", StringComparison.OrdinalIgnoreCase)
                                                          || relative.StartsWith($"{part}/", StringComparison.OrdinalIgnoreCase));
                        });
    }

    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "FireChart.Viewer.slnx")))
        {
            directory = directory.Parent;
        }

        return (directory ?? throw new InvalidOperationException("FireChart.Viewer.slnx is not found")).FullName + Path.DirectorySeparatorChar;
    }
}
