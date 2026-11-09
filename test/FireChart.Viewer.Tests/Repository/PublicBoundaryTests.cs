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

    private static bool IsBuildFile(string file)
    {
        return s_names.Contains(Path.GetFileName(file), StringComparer.Ordinal)
               || s_buildExtensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> Files(string root)
    {
        return Directory.EnumerateFiles(root, "*", System.IO.SearchOption.AllDirectories)
                        .Where(file => s_extensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase)
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
