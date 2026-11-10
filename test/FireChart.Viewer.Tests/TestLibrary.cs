using FireChart.Viewer.Library;

namespace FireChart.Viewer.Tests;

// The vendor library as the project has it: the files of its wwwroot, read once (a client loads them one by one, WebVendorTemplates)
internal static class TestLibrary
{
    private static readonly Lazy<VendorTemplates> s_vendor = new(() => VendorTemplates.FromFolder(WebRoot()));

    public static VendorTemplates Vendor => s_vendor.Value;

    public static FxStyleSheets Styles => FxStyle.ReadFolder(WebRoot());

    // The static files of the library read from the folder, as a client reads them from its host
    public static FolderAssets Assets => new(WebRoot());

    // The vendor summary form "default", a new instance at every call
    public static FireChart.Viewer.Core.Forms.ClinicalSummaryFormDefinition SummaryDefault =>
        FireChart.Viewer.Core.ViewerJson.Read<FireChart.Viewer.Core.Forms.ClinicalSummaryFormDefinition>(VendorSummaryForms.ReadFolder(WebRoot())["default"]);

    public static string WebRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "FireChart.Viewer.slnx")))
        {
            directory = directory.Parent;
        }

        return Path.Combine(directory!.FullName, "src", "FireChart.Viewer.Library", VendorLibrary.WebRoot);
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
