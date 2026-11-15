using System.Text.RegularExpressions;

namespace FireChart.Viewer.Tests.Viewer;

// Hebrew (and any right-to-left language) turns the page: the styles say start and end, never left and right, so they turn with it. A line that must
// stay physical says so with the comment rtl-ok
public partial class RightToLeftStyleTests
{
    [GeneratedRegex(@"\b(margin|padding|border)-(left|right)\b|(?<![\w-])(left|right)\s*:|text-align\s*:\s*(left|right)|float\s*:\s*(left|right)|border-(top|bottom)-(left|right)-radius")]
    private static partial Regex PhysicalProperty();

    [GeneratedRegex("""class="([^"]*)"|Class="([^"]*)"|class=@\("([^"]*)""")]
    private static partial Regex ClassAttribute();

    [GeneratedRegex(@"(^|:)(-?m[lr]|p[lr]|left|right|rounded-[lr]|rounded-[tb][lr]|border-[lr]|text-left|text-right|float-left|float-right|scroll-[mp][lr])(-|$)")]
    private static partial Regex PhysicalUtility();

    [Test]
    public void TheStylesOfThePublicPart_SayStartAndEnd_NotLeftAndRight()
    {
        Assert.That(Physical(PublicRoot(), ["library/styles", "src"]), Is.Empty);
    }

    // The lines of the styles and the classes of the Razor files under the folders that name a physical side
    public static List<string> Physical(string root, IEnumerable<string> folders)
    {
        var found = new List<string>();
        foreach (var file in folders.SelectMany(folder => Directory.EnumerateFiles(Path.Combine(root, folder), "*.*", System.IO.SearchOption.AllDirectories)))
        {
            var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
            if (relative.Contains("/bin/", StringComparison.Ordinal) || relative.Contains("/obj/", StringComparison.Ordinal) || relative.Contains("/lib/", StringComparison.Ordinal)
                || relative.EndsWith(".Tailwind.css", StringComparison.Ordinal) || relative.Contains("daisy", StringComparison.Ordinal))
            {
                continue;
            }

            var css = relative.EndsWith(".css", StringComparison.Ordinal);
            var razor = relative.EndsWith(".razor", StringComparison.Ordinal) || relative.EndsWith(".cshtml", StringComparison.Ordinal);
            if (!css && !razor)
            {
                continue;
            }

            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                if (line.Contains("rtl-ok", StringComparison.Ordinal))
                {
                    continue;
                }

                var physical = css
                    ? PhysicalProperty().IsMatch(line)
                    : ClassAttribute().Matches(line).SelectMany(match => match.Groups.Values.Skip(1).Where(group => group.Success).SelectMany(group => group.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries)))
                                      .Any(token => PhysicalUtility().IsMatch(token));
                if (physical)
                {
                    found.Add($"{relative}:{i + 1}: {line.Trim()}");
                }
            }
        }

        return found;
    }

    private static string PublicRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "FireChart.Viewer.slnx")))
        {
            directory = directory.Parent;
        }

        return directory!.FullName;
    }
}
