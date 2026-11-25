using System.Globalization;
using FireChart.Viewer.Core;
using FireChart.Viewer.Core.Fhir;
using FireChart.Viewer.Core.Reports;
using FireChart.Viewer.Library;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Task = System.Threading.Tasks.Task;

namespace FireChart.Viewer.Reports;

// The components of a page that is not a report (a list): the data of the server, the components of the modes read when needed, the culture; its
// content (RecordTile) draws records by them. What the searches of the page brought with their records (Related) is known, so a tile reads nothing.
// The content is drawn hidden until every block of it has what it waits for (the components of the modes, the records it reads) and the progress has
// been quiet for a moment: the page appears once, whole, not a tile at a time
internal sealed class ReportHost : ComponentBase, IDisposable
{
    private const int QuietMilliseconds = 80;

    private readonly CancellationTokenSource _cancel = new();
    private readonly CancellationToken _token;
    private ReportScope? _scope;
    private IViewerServer? _server;
    private int _known;
    private bool _revealed;
    private bool _told;
    private bool _checking;
    private int _version;

    [Inject]
    private IFhirServerReader Reader { get; set; } = null!;

    [Inject]
    private IReleaseModels Models { get; set; } = null!;

    [Inject]
    private IServerResourceTypeProvider Types { get; set; } = null!;

    [Inject]
    private IViewerAssets LibraryAssets { get; set; } = null!;

    [Inject]
    private ViewerTerminology Terminology { get; set; } = null!;

    [CascadingParameter]
    public ViewerSession Session { get; set; } = null!;

    [Parameter]
    public IReadOnlyList<RelatedResource> Related { get; set; } = [];

    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    // The library of the page, when it has one (what its search read is not read again); none: one of the host
    [Parameter]
    public ReportLibrary? Library { get; set; }

    // The page still waits for its data (the search of a list): the content stays hidden
    [Parameter]
    public bool Waiting { get; set; }

    // Told when the content is shown (true) and hidden again for a new load (false)
    [Parameter]
    public EventCallback<bool> OnReady { get; set; }

    // The token is taken once: the source is disposed with the host, and a load awaited since may still ask for it
    public ReportHost()
    {
        _token = _cancel.Token;
    }

    protected override void OnInitialized()
    {
        Terminology.Changed += OnWords;
    }

    private void OnProgress()
    {
        Interlocked.Increment(ref _version);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (Waiting || _revealed || _checking || _scope is null)
        {
            return;
        }

        // The blocks drawn have started what they wait for; the content shows once nothing is loading and nothing new started for a moment
        _checking = true;
        try
        {
            var progress = _scope.Runtime.Data.Progress;
            int version;
            do
            {
                version = Volatile.Read(ref _version);
                await Task.Delay(QuietMilliseconds, _token);
            }
            while (!Waiting && (progress.IsLoading || version != Volatile.Read(ref _version)));
        }
        catch (OperationCanceledException)
        {
            return;
        }
        finally
        {
            _checking = false;
        }

        if (!Waiting && !_revealed)
        {
            _revealed = true;
            StateHasChanged();
            await OnReady.InvokeAsync(true);
        }
    }

    // Words of codes have come: the tiles draw again
    private void OnWords(object? sender, EventArgs e)
    {
        _scope?.Runtime.Invalidate();
        _ = InvokeAsync(StateHasChanged);
    }

    protected override async Task OnParametersSetAsync()
    {
        if (!ReferenceEquals(_server, Session.Server))
        {
            _server = Session.Server;
            _known = 0;
            var token = _token;
            var library = Library ?? new ReportLibrary(LibraryAssets, VendorLibrary.UrlOf);
            ReportRuntime runtime;
            try
            {
                var model = await Models.GetAsync(await Types.ReleaseOfAsync(Session.Server), token);
                var data = new ReportData(Reader, Session.Server, model, token);
                var format = new ReportFormat(CultureInfo.CurrentCulture, TimeProvider.System.LocalTimeZone, Terminology.For(Session.TerminologyServer),
                                              decimals: Session.Fhir.Decimals, strings: await library.StringsAsync(CultureInfo.CurrentUICulture, token));
                runtime = new ReportRuntime(new Dictionary<string, ComponentDefinition>(), data, format, name => library.LoadAsync(name, token));
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                return;
            }

            // Left while it loaded: nothing to show any more
            if (token.IsCancellationRequested)
            {
                return;
            }

            if (_scope is not null)
            {
                _scope.Runtime.Data.Progress.Changed -= OnProgress;
            }

            _scope = ReportScope.Root(runtime, null);
            _scope.Runtime.Data.Progress.Changed += OnProgress;
        }

        // A new host (the page started again) and a new load hide the content until it is whole again
        if (!_told || (Waiting && _revealed))
        {
            _told = true;
            _revealed = false;
            await OnReady.InvokeAsync(false);
        }

        // The related resources of the pages loaded since (More appends)
        if (_scope is not null && Related.Count > _known)
        {
            _scope.Runtime.Data.KnowRelated(Related.Skip(_known));
            _known = Related.Count;
        }
    }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        if (_scope is null)
        {
            return;
        }

        // Hidden, not left out: the blocks are drawn (a list draws the rows in view, which needs the layout) and load meanwhile
        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "data-report-host", _revealed ? "ready" : "loading");
        builder.AddAttribute(2, "style", _revealed ? null : "visibility:hidden");
        builder.AddAttribute(3, "aria-hidden", _revealed ? null : "true");
        builder.OpenComponent<CascadingValue<ReportScope>>(4);
        builder.AddComponentParameter(5, nameof(CascadingValue<ReportScope>.Value), _scope);
        builder.AddComponentParameter(6, nameof(CascadingValue<ReportScope>.IsFixed), true);
        builder.AddComponentParameter(7, nameof(CascadingValue<ReportScope>.ChildContent), ChildContent);
        builder.CloseComponent();
        builder.CloseElement();
    }

    public void Dispose()
    {
        if (_scope is not null)
        {
            _scope.Runtime.Data.Progress.Changed -= OnProgress;
        }

        Terminology.Changed -= OnWords;
        _cancel.Cancel();
        _cancel.Dispose();
    }
}

// A record drawn by the tile of its type (the component {Type}.tile of the library, else the built-in tile), in the ReportHost of the page; a row leads
// to Href
internal sealed class RecordTile : ComponentBase
{
    private static readonly ReportNode s_tile = ReportXml.Parse("""<Component name="Row"><Show value="{{%row}}" mode="tile"/></Component>""").Body[0];

    [CascadingParameter]
    public ReportScope? Scope { get; set; }

    [Parameter, EditorRequired]
    public ResourceSummary Item { get; set; } = null!;

    [Parameter]
    public string? Href { get; set; }

    // The scope of the row, made once for its record: a block given a new scope loads again, and the page draws again often (its toolbar, the count)
    private ReportScope? _rowScope;
    private ReportScope? _rowOf;
    private ResourceSummary? _rowItem;
    // One delegate per address: a new one at every render would make the tile prepare again while it still waits
    private Func<Hl7.Fhir.Model.PocoNode, string?>? _hrefOf;
    private string? _hrefFor;

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        if (Scope is null || Item.Json is null)
        {
            builder.OpenElement(0, "div");
            builder.AddAttribute(1, "class", "fx-summary-row");
            builder.AddContent(2, $"{Item.ResourceType}/{Item.Id}");
            builder.CloseElement();
            return;
        }

        if (_hrefOf is null || _hrefFor != Href)
        {
            var href = Href;
            _hrefFor = href;
            _hrefOf = _ => href;
        }

        builder.OpenComponent<ShowBlock>(3);
        builder.AddComponentParameter(4, nameof(ReportBlock.Node), s_tile);
        if (!ReferenceEquals(_rowOf, Scope) || !ReferenceEquals(_rowItem, Item))
        {
            _rowOf = Scope;
            _rowItem = Item;
            _rowScope = Scope.With("row", [Scope.Runtime.Data.NodeOf(Item)]);
        }

        builder.AddComponentParameter(5, nameof(ReportBlock.Scope), _rowScope);
        builder.AddComponentParameter(6, nameof(ShowBlock.HrefOf), _hrefOf);
        builder.CloseComponent();
    }
}

// A record drawn whole, as the page of a record of the Viewer shows it: the component {Type}.page of the library, else its banner and its view (the
// components of the types, else the built-in ones). The data of the scope serve what it refers to (the Bundle Visualizer: the Bundle, no server)
internal sealed class RecordCard : ComponentBase
{
    private static readonly IReadOnlyList<ReportNode> s_page = ReportXml.Parse("""<Component name="Card"><Show value="{{%row}}" mode="page"/></Component>""").Body;

    private static readonly IReadOnlyList<ReportNode> s_bannerAndView = ReportXml.Parse("""
        <Component name="Card">
          <Show value="{{%row}}" mode="banner"/>
          <Show value="{{%row}}" mode="view"/>
        </Component>
        """).Body;

    [CascadingParameter]
    public ReportScope? Scope { get; set; }

    [Parameter, EditorRequired]
    public ResourceSummary Item { get; set; } = null!;

    private ReportScope? _rowScope;
    private ReportScope? _rowOf;
    private ResourceSummary? _rowItem;
    private IReadOnlyList<ReportNode>? _body;

    protected override async Task OnParametersSetAsync()
    {
        if (Scope is null || Item.Json is null || (ReferenceEquals(_rowOf, Scope) && ReferenceEquals(_rowItem, Item)))
        {
            return;
        }

        _rowOf = Scope;
        _rowItem = Item;
        _rowScope = Scope.With("row", [Scope.Runtime.Data.NodeOf(Item)]);
        _body = await Scope.Runtime.ModeAsync(Item.ResourceType, "page") is null ? s_bannerAndView : s_page;
    }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        if (_rowScope is null || _body is null)
        {
            return;
        }

        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "class", "fx-root flex flex-col gap-2");
        foreach (var node in _body)
        {
            builder.OpenComponent<ShowBlock>(2);
            builder.AddComponentParameter(3, nameof(ReportBlock.Node), node);
            builder.AddComponentParameter(4, nameof(ReportBlock.Scope), _rowScope);
            builder.CloseComponent();
        }

        builder.CloseElement();
    }
}
