using FireChart.Viewer.Library;

namespace FireChart.Viewer.Tests;

// The vendor library as the project has it: the files of its wwwroot
internal static class TestLibrary
{
    public static FxStyleSheets Styles => FxStyle.ReadFolder(WebRoot());

    // The static files of the library read from the folder, as a client reads them from its host
    public static FolderAssets Assets => new(WebRoot());


    public static string WebRoot()
    {
        return TestRepository.Path("library");
    }
}

// The static files of the library as a host serves them (the query of the version is not a part of the path); what was read is listed
internal sealed class FolderAssets(string webRoot) : FireChart.Viewer.Core.IViewerAssets
{
    public List<string> Read { get; } = [];

    public async Task<string?> ReadTextAsync(string path, CancellationToken cancellationToken)
    {
        var file = path.Split('?')[0];
        Read.Add(file);
        var local = Path.Combine(webRoot, file[(VendorLibrary.ContentRoot.Length + 1)..]);
        return File.Exists(local) ? await File.ReadAllTextAsync(local, cancellationToken) : null;
    }
}
