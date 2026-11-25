using FireChart.Viewer.Core.Fhir;

namespace FireChart.Viewer.App.Sandbox;

// What the tab Bundle Visualizer of the sandbox holds, kept by the page while another tab is shown: what is typed, and what is drawn
public sealed class BundleSandboxState
{
    public const string Paste = "paste";
    public const string Address = "address";

    public string Mode { get; set; } = Paste;

    public string Text { get; set; } = string.Empty;

    public string? PresetId { get; set; }

    public string Location { get; set; } = string.Empty;

    public string? ShownText { get; set; }

    public string? ShownLocation { get; set; }

    public IViewerServer? ShownServer { get; set; }

    public FireChart.Viewer.Smart.SmartFlow ShownFlow { get; set; }

    public string? Chosen { get; set; }
}
