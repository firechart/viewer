using System.Globalization;
using Flox.Shared.Components;
using FireChart.Viewer.Core.Reports;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Task = System.Threading.Tasks.Task;

namespace FireChart.Viewer.Reports;

// The panel of a report loaded at once: it follows the progress itself and draws only itself, at most every 100 ms. The report behind it draws
// again only when the loading ends: a report of hundreds of blocks drawn at every step of its progress would stall the browser
internal sealed class ReportLoadingPanel : ComponentBase, IDisposable
{
    private int _scheduled;

    [Parameter, EditorRequired]
    public ReportProgress Progress { get; set; } = null!;

    [Parameter]
    public EventCallback OnCancel { get; set; }

    protected override void OnInitialized()
    {
        Progress.Changed += OnProgress;
    }

    private void OnProgress()
    {
        if (Interlocked.Exchange(ref _scheduled, 1) == 1)
        {
            return;
        }

        _ = InvokeAsync(async () =>
        {
            await Task.Delay(100);
            Interlocked.Exchange(ref _scheduled, 0);
            StateHasChanged();
        });
    }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenComponent<LoadingPanel>(0);
        builder.AddComponentParameter(5, nameof(LoadingPanel.Title), Strings.Caption_LoadingRecord);
        builder.AddComponentParameter(1, nameof(LoadingPanel.Done), Progress.Finished);
        builder.AddComponentParameter(2, nameof(LoadingPanel.Total), Progress.Started);
        builder.AddComponentParameter(3, nameof(LoadingPanel.Messages),
                                      (IReadOnlyList<string>)[.. Progress.Messages.Select(loaded => string.Format(CultureInfo.CurrentCulture, Strings.Message_LoadedRecords, loaded.Type, loaded.Count))]);
        builder.AddComponentParameter(4, nameof(LoadingPanel.OnCancel), OnCancel);
        builder.CloseComponent();
    }

    public void Dispose()
    {
        Progress.Changed -= OnProgress;
    }
}
