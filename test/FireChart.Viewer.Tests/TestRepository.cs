namespace FireChart.Viewer.Tests;

// The checkout the tests run in: the whole repository (FireChart.slnx) or the public one (FireChart.Viewer.slnx), whose export rewrites the
// paths of this project to its own layout
internal static class TestRepository
{
    private static readonly string[] s_solutions = ["FireChart.slnx", "FireChart.Viewer.slnx"];

    public static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !s_solutions.Any(solution => File.Exists(System.IO.Path.Combine(directory.FullName, solution))))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException($"{string.Join(" or ", s_solutions)} is not found above {AppContext.BaseDirectory}");
    }

    // A folder or a file of the checkout by its path from the root, with forward slashes
    public static string Path(string relative)
    {
        return System.IO.Path.Combine(Root(), relative);
    }
}
